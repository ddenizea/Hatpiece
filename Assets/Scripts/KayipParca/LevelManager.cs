using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KayipParca
{
    /// <summary>
    /// Hatpiece: oyunun tum kurallari ve akisi (tur bazli, izgara hareketi).
    /// Kurallar Tools/solver.py ile birebir aynidir; seviyeler o cozucuyle test edildi.
    ///  - WASD / Ok tuslari: bir kare ilerle (carpmak hamle harcamaz)
    ///  - E / Q: dondur, Bosluk: bir tur bekle (vinc modunda: tut / birak)
    ///  - Sekil istasyonu (S): girdigin yondeki kenar cikinti <-> girinti olur; her istasyon tek kullanimlik (sonra duz zemin)
    ///  - Yuvaya yalnizca sekil birebir uyuyorsa girilir
    ///  - Ayna klon (C) yatayda ters hareket eder; ikisi de yuvadaysa kazanilir
    ///  - Her turdan sonra dusmanlar yurur, lazerler yanip soner, ates toplari ucar
    ///  - Lazer, ates, dusman ve tehlike karesi parcayi parcalar -> seviye basina donulur
    ///  - Vinc kumandasi (K) kontrolu kancaya verir; ejderhanin gogsu acikken Yamali'yi yakala
    ///  - Z: geri al, R: seviyeyi yeniden baslat
    ///  - Hileler (cheats acikken): N sonraki, B onceki, G seviyeyi gecmis say, 1-9 o seviyeye git, Tab seviye listesi
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [Header("Baglantilar (Kurulum menusu doldurur)")]
        public ThemeManager themes;
        public Tilemap floorMap, wallMap, decorMap;
        public Transform objectsRoot;
        public SpriteRenderer bgFar, bgNear;
        public Text titleText, movesText, messageText, hintText;
        public AudioSource sfx;

        [Header("Prefablar (Kurulum menusu doldurur)")]
        [Tooltip("Oyuncu ve ayna klon bu prefabdan uretilir. Menu: Kayıp Parça > 4) Parça Prefabını Kur")]
        public PieceView piecePrefab;

        [Header("Sesler (sonradan ekleyin, bos kalabilir)")]
        public AudioClip sfxMove;
        public AudioClip sfxRotate, sfxStation, sfxBump, sfxDeath, sfxFit, sfxWin, sfxLaser, sfxFire, sfxGrab;

        [Header("Ayarlar")]
        [Tooltip("1 = baslik ekrani ve giris hikayesiyle basla. Baska bir sayi = dogrudan o seviyeden (test icin).")]
        [Range(1, 20)] public int startLevel = 1;
        [Tooltip("Kamera oyuncuya dogru ne kadar kaysin (parallax icin)")]
        public float cameraDrift = 0.12f;

        [Header("Hileler")]
        [Tooltip("Açıkken: N sonraki seviye, B önceki seviye, G seviyeyi geçmiş say, 1-9 o seviyeye git, Tab tıklanabilir seviye listesi. Oyunu dağıtmadan önce kapatın.")]
        public bool cheats = true;

        public static readonly string[] ThemeKeys = { "T0_Kutu", "T1_Hali", "T2_Mutfak", "T3_Oyuncak", "T4_Gece", "T5_Kedi", "T6_Masa", "T7_Resim" };
        // Siyah-beyaz doodle stili: yuva sekil uymuyorsa soluk, uyunca parlak beyaz + isik (bkz. Tools/art_doodle.py)
        static readonly Color SlotColor = new Color(.47f, .47f, .47f), MatchColor = Color.white, CloneColor = new Color(.34f, .34f, .34f);
        static readonly Color PathColor = Color.white, FireColor = new Color(.7f, .7f, .7f);
        static readonly Color SpentStationColor = new Color(1f, 1f, 1f, .22f), CloneTint = new Color(.72f, .72f, .72f);
        // tus kapaklari (KeyHints): [Tus] etiket, gruplar 4 boslukla ayrilir
        const string Controls = "[W][A][S][D] hareket    [Q][E] döndür    [Boşluk] bekle    [Z] geri al    [R] yeniden";
        const string CraneControls = "[W][A][S][D] kancayı sür    [Boşluk] tut / bırak    [Z] geri al    [R] yeniden";

        // HUD yerlesimi (1920x1080 tuval birimi). Kamera tahtayi bu bantlarin arasina sigdirir.
        const float HudTop = 24f, IconSize = 96f, IconLabel = 32f, IconGap = 26f, MovesWidth = 300f;
        const float TopReserve = HudTop + IconSize + IconLabel + 18f;   // ust bant: baslik, hamle, sekil ikonlari
        const float BottomReserve = 128f;                               // alt bant: ipucu + kontroller

        enum Death { None, Hazard, Enemy, Laser, Fire, Moves }

        /// Baska bir bolum sahnesinden (or. Bolum 2) donerken hangi siradan devam edilecegi. -1 = baslik ekrani.
        public static int pendingStart = -1;

        struct State
        {
            public Vector2Int p, c, hook; public string ps, cs; public int t, moves;
            public bool crane, carrying, calm; public char ending;
            public Vector2Int f; public bool following;   // kayip parca
            public int spent;                             // kullanilmis istasyonlar (LevelData.StationBit maskesi)
        }

        // kurtarilan kayip parcalar (bolum boyunca birikir, PlayerPrefs'e kaydedilir)
        const string RescuedKey = "hatpiece_rescued";
        static readonly HashSet<string> rescued = new HashSet<string>();
        readonly Dictionary<int, List<string>> lostLevels = new Dictionary<int, List<string>>();
        SpriteRenderer lostSr;

        // akis
        List<Chapters.Entry> entries;
        int index;                       // entries icindeki sira
        bool busy = true, inScreen, screenNext, screenSkip, firstLoad = true;

        // seviye
        LevelData data;
        State s;
        readonly Stack<State> history = new Stack<State>();
        PieceView player, clone;
        readonly List<EnemyView> enemyViews = new List<EnemyView>();
        readonly List<SpriteRenderer> nextFrames = new List<SpriteRenderer>();
        readonly List<List<SpriteRenderer>> laserCells = new List<List<SpriteRenderer>>();
        readonly List<SpriteRenderer> fireWarns = new List<SpriteRenderer>();
        SpriteRenderer[] stationViews = System.Array.Empty<SpriteRenderer>();   // data.stations sirasiyla
        readonly Dictionary<long, SpriteAnim> fireballs = new Dictionary<long, SpriteAnim>();
        readonly List<(SpriteRenderer sr, char key)> slots = new List<(SpriteRenderer, char)>();
        readonly Dictionary<SpriteRenderer, SpriteRenderer> slotGlow = new Dictionary<SpriteRenderer, SpriteRenderer>();
        SpriteRenderer dragon, chestCover, chestGlow, yamaliSr, heartSlot, trolley, cable, hook, countdown;
        readonly Dictionary<string, Tile> tileCache = new Dictionary<string, Tile>();
        Tilemap shadowMap;
        Vector3 levelCenter;             // tahtanin ortasi
        Vector3 camCenter;               // kameranin dinlenme noktasi (HUD bantlari icin kaydirilmis)
        string themeKey;
        Vector2Int lastScreen;
        Camera cam;
        Image hudNow, hudGoal, hudGoal2;
        RectTransform hudIcons;
        Text controlsText;               // tus kapaklari satiri (ipucunun altinda)
        RectTransform rescuedRow;        // kurtarilan parca halkalari (hamle hapinin altinda)
        int rescuedShownTotal = -1, rescuedShownCount = -1;
        GameObject screenRoot; Image screenPic; Text screenText, screenPrompt;

        // hileler
        const int CheatNext = 100, CheatPrev = 101, CheatWin = 102, CheatMenu = 103;
        readonly List<string> levelNames = new List<string>();
        bool cheatMenu;
        Vector2 cheatScroll;
        GUIStyle cheatLabel, cheatButton;

        // ------------------------------------------------------------------ yasam dongusu
        void Start()
        {
            if (!piecePrefab)
            {
                Debug.LogError("[KayipParca] LevelManager.piecePrefab boş. Menü: Kayıp Parça > 4) Parça Prefabını Kur", this);
                enabled = false;
                return;
            }
            cam = Camera.main;
            LayoutHud();
            entries = Chapters.Load();
            LoadRescued();
            foreach (var e in entries)
            {
                var ta = Resources.Load<TextAsset>("KayipParca/Levels/" + e.file);
                var ld = ta ? LevelData.Parse(ta.text) : null;
                levelNames.Add(ld != null && !string.IsNullOrEmpty(ld.name) ? ld.name : e.file.StartsWith("@") ? e.chapter + " (sahne)" : e.file);
                if (ld == null || !ld.hasLost) continue;
                if (!lostLevels.TryGetValue(e.chapterIndex, out var list)) lostLevels[e.chapterIndex] = list = new List<string>();
                list.Add(e.file);
            }
            if (pendingStart >= 0)
            {
                int i = Mathf.Clamp(pendingStart, 0, entries.Count - 1); pendingStart = -1;
                StartCoroutine(ChapterStart(i));
            }
            else if (startLevel <= 1) StartCoroutine(TitleAndIntro());
            else StartCoroutine(LoadLevel(Mathf.Clamp(startLevel - 1, 0, entries.Count - 1)));
        }

        void Update()
        {
            if (cheats && entries != null && HandleCheats()) return;
            int a = ReadAction();
            if (a < 0) return;
            if (inScreen) { if (a == 8) screenNext = true; if (a == 10) screenSkip = true; return; }
            if (busy) return;
            if (a <= 5 || a == 8) Step(a);
            else if (a == 6) Undo();
            else if (a == 7) { Play(sfxBump); ResetState(); }
        }

        void LateUpdate()
        {
            // sekli uyan yuva hafifce nabiz gibi parlar
            float pulse = .5f + .5f * Mathf.Sin(Time.time * 4f);
            foreach (var kv in slotGlow)
                if (kv.Value && kv.Value.enabled) { kv.Value.color = new Color(1f, 1f, 1f, .45f + .4f * pulse); kv.Value.enabled = kv.Key && kv.Key.enabled; }
            if (!cam || !player || inScreen) return;
            if (data != null && (Screen.width != lastScreen.x || Screen.height != lastScreen.y)) SetupCameraAndBackground(themeKey, snap: false);
            var target = camCenter + (player.transform.position - levelCenter) * cameraDrift;
            target.z = -10;
            cam.transform.position = Vector3.Lerp(cam.transform.position, target, 1f - Mathf.Exp(-3f * Time.deltaTime));
            if (themes) themes.FollowPlayer(s.crane && hook ? hook.transform.position : player.transform.position);
            UpdateCable();
        }

        // ------------------------------------------------------------------ hikaye ekranlari
        IEnumerator TitleAndIntro()
        {
            busy = true;
            rescued.Clear(); SaveRescued();   // yeni oyun
            PlayerPrefs.DeleteKey(YamaliGame.YamaKey);   // Bolum 2'nin yama ordusu da sifirlanir
            yield return ShowScreen("Story/title", entries[0].chapter, "[Enter] Başla");
            var ta = Resources.Load<TextAsset>("KayipParca/Story/intro");
            if (ta != null)
            {
                foreach (var raw in ta.text.Replace("\r", "").Split('\n'))
                {
                    int bar = raw.IndexOf('|');
                    if (bar < 0) continue;
                    yield return ShowScreen("Story/" + raw.Substring(0, bar).Trim(), raw.Substring(bar + 1).Trim(), "[Enter] devam    [Esc] geç");
                    if (screenSkip) break;
                }
            }
            HideScreen();
            yield return LoadLevel(0);
        }

        IEnumerator ChapterStart(int i)
        {
            busy = true;
            yield return ShowScreen("Story/title", entries[i].chapter, "[Enter] Başla");
            HideScreen();
            yield return LoadLevel(i);
        }

        IEnumerator ShowScreen(string image, string text, string prompt)
        {
            EnsureScreen();
            screenRoot.SetActive(true);
            screenRoot.transform.SetAsLastSibling();
            screenPic.sprite = SpriteBank.Get(image);
            screenPic.enabled = screenPic.sprite != null;
            screenText.text = text.Replace("|", "\n");
            screenPrompt.text = prompt;
            inScreen = true; screenNext = false; screenSkip = false;
            yield return null;
            while (!screenNext && !screenSkip) yield return null;
            inScreen = false;
        }

        void HideScreen() { if (screenRoot) screenRoot.SetActive(false); }

        void EnsureScreen()
        {
            if (screenRoot) return;
            var canvas = (messageText ? messageText.canvas : FindAnyObjectByType<Canvas>()).transform;
            screenRoot = UIKit.MakeStoryScreen(canvas, out screenPic, out screenText, out screenPrompt);
        }

        // ------------------------------------------------------------------ HUD yerlesimi
        /// Sahnede kayitli HUD metinlerini kenarlarina sabitler. Kurulum menusu de ayni yerlesimi kullanir,
        /// oyun basinda tekrar uygulandigi icin eski sahneler de duzelir.
        ///  - Sol ust: baslik (sag ustteki hamle + ikon bloguna kadar gerilir, sigmazsa kuculur)
        ///  - Sag ust: hamle sayaci, onun saginda Sen / Hedef ikonlari
        ///  - Orta: mesajlar (ekranin %84'u genisliginde, sarar)
        ///  - Alt: ipucu + kontroller (kenarlara gerilir, yukari dogru buyur)
        public static void LayoutHudTexts(Text title, Text moves, Text message, Text hint, int iconCount = 0)
        {
            var cs = title ? title.GetComponentInParent<CanvasScaler>() : null;
            UIKit.ConfigureScaler(cs);
            float right = UIKit.Margin + MovesBlockWidth(iconCount);
            if (title)
            {
                // iki satir: ustte kucuk gri bolum adi, altta seviye adi (zengin metin, LoadLevel yazar)
                UIKit.Style(title, UIKit.Hud, 46, TextAnchor.MiddleLeft);
                UIKit.TopBand(title.rectTransform, UIKit.Margin, right + 30, HudTop, IconSize);
            }
            if (moves)
            {
                UIKit.Style(moves, UIKit.Hud, 44, TextAnchor.MiddleCenter);
                moves.horizontalOverflow = HorizontalWrapMode.Overflow;
                PlaceMoves(moves, iconCount);
            }
            if (message)
            {
                UIKit.Style(message, UIKit.Body, 60, TextAnchor.MiddleCenter);
                UIKit.Stretch(message.rectTransform, .08f, .2f, .92f, .8f);
            }
            if (hint)
            {
                UIKit.Style(hint, UIKit.Body, 28, TextAnchor.LowerCenter);
                UIKit.BottomBand(hint.rectTransform, 80, 80, 68, BottomReserve - 74);
            }
        }

        static float MovesBlockWidth(int iconCount) => MovesWidth + iconCount * (IconSize + IconGap);

        static void PlaceMoves(Text moves, int iconCount) =>
            UIKit.Pin(moves.rectTransform, new Vector2(1, 1), new Vector2(-UIKit.Margin - iconCount * (IconSize + IconGap), -HudTop), new Vector2(moves.rectTransform.sizeDelta.x > 0 ? moves.rectTransform.sizeDelta.x : MovesWidth, IconSize));

        /// Sag ust blok: hamle hapi ve altindaki kurtarilan satiri, gorunen ikon sayisina gore sola kayar.
        static void PlaceRight(Text moves, RectTransform rescued, int iconCount)
        {
            if (moves) PlaceMoves(moves, iconCount);
            if (rescued) UIKit.Pin(rescued, new Vector2(1, 1), new Vector2(-UIKit.Margin - iconCount * (IconSize + IconGap), -HudTop - IconSize - 6), new Vector2(MovesWidth, 28));
        }

        /// Kurtarilan satirini doldurur: gri etiket + her kayip parca icin bir halka (kurtarilan: gozlu beyaz top).
        public static void FillRescuedRow(RectTransform row, int total, int count)
        {
            for (int i = row.childCount - 1; i >= 0; i--)
            {
                var go = row.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            }
            if (total <= 0) return;
            var label = UIKit.MakeText(row, "Etiket", UIKit.HudLight, 22, TextAnchor.MiddleRight);
            label.color = UIKit.Mute; label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.text = "Kurtarılan";
            label.rectTransform.sizeDelta = new Vector2(label.preferredWidth + 10, 28);
            for (int i = 0; i < total; i++) UIKit.MakeDot(row, i < count);
        }

        void UpdateRescuedRow(int total, int count)
        {
            if (!rescuedRow || (total == rescuedShownTotal && count == rescuedShownCount)) return;
            rescuedShownTotal = total; rescuedShownCount = count;
            FillRescuedRow(rescuedRow, total, count);
        }

        public static string Upper(string s) => s.ToUpper(new System.Globalization.CultureInfo("tr-TR"));

        /// Baslik metni: ustte kucuk gri bolum adi, altta seviye adi.
        public static string TitleText(string chapter, int number, string name) =>
            $"<size=24><color=#999999>{Upper(chapter)}</color></size>\n{number}. {name}";

        /// Calisma aninda uretilen HUD parcalari (sahnedeki dort metnin yanina).
        public class HudWidgets
        {
            public Text controls; public RectTransform rescued, icons; public Image now, goal, goal2;
        }

        /// HUD'u kurar: sahnedeki metinleri yerlestirir, hap ve balon zeminlerini, tus kapaklarini, kurtarilan
        /// satirini ve sekil ikonlarini uretir. Editor'de de calisir (onizleme icin), o yuzden statik.
        public static HudWidgets BuildHud(Text title, Text moves, Text message, Text hint)
        {
            LayoutHudTexts(title, moves, message, hint);
            // orta mesaj: beyaz konusma balonu, siyah yazi (blob stili)
            if (message && !message.transform.parent.Find(message.name + "_Zemin"))
                UIKit.SetCorner(UIKit.AddBackdrop(message, Color.white, UIKit.BackdropPad), 48);
            if (message) message.color = Color.black;
            // hamle sayaci: beyaz hap, siyah rakam (genisligi UpdateHud yaziya gore ayarlar)
            if (moves && !moves.transform.parent.Find(moves.name + "_Zemin"))
                UIKit.SetCorner(UIKit.AddBackdrop(moves, Color.white, new Vector2(26, 12)), IconSize / 2);
            if (moves) moves.color = Color.black;
            var canvasText = moves ? moves : title ? title : message;
            if (!canvasText) return null;
            var canvasT = canvasText.canvas.transform;
            var w = new HudWidgets();
            // tus kapaklari: ipucunun altinda, alt banda ortali
            w.controls = UIKit.MakeText(canvasT, "Kontroller", UIKit.BodyLight, 26, TextAnchor.MiddleCenter);
            UIKit.BottomBand(w.controls.rectTransform, 80, 80, 16, 46);
            UIKit.AddKeyHints(w.controls);
            // kurtarilan parcalar: hamle hapinin altinda halkalar (FillRescuedRow doldurur)
            w.rescued = UIKit.MakeRect(canvasT, "KurtarilanSatiri");
            var rrow = w.rescued.gameObject.AddComponent<HorizontalLayoutGroup>();
            rrow.childAlignment = TextAnchor.MiddleRight; rrow.spacing = 8;
            rrow.childControlWidth = rrow.childControlHeight = false;
            rrow.childForceExpandWidth = rrow.childForceExpandHeight = false;
            PlaceRight(moves, w.rescued, 1);
            // Sekil ikonlari: sag ustte, sagdan sola dolar; gizlenen ikonun yeri kalmaz
            w.icons = UIKit.MakeRect(canvasT, "SekilIkonlari");
            UIKit.Pin(w.icons, new Vector2(1, 1), new Vector2(-UIKit.Margin, -HudTop), new Vector2(3 * IconSize + 2 * IconGap, IconSize));
            var row = w.icons.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.UpperRight; row.spacing = IconGap;
            row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            w.now = MakeIcon(w.icons, "Sen");
            w.goal = MakeIcon(w.icons, "Hedef");
            w.goal2 = MakeIcon(w.icons, "Hedef 2");
            return w;
        }

        void LayoutHud()
        {
            if (hudIcons) { LayoutHudTexts(titleText, movesText, messageText, hintText); return; }
            var w = BuildHud(titleText, movesText, messageText, hintText);
            if (w == null) return;
            controlsText = w.controls; rescuedRow = w.rescued; hudIcons = w.icons;
            hudNow = w.now; hudGoal = w.goal; hudGoal2 = w.goal2;
        }

        // ------------------------------------------------------------------ seviye yukleme
        IEnumerator LoadLevel(int i)
        {
            busy = true;
            // "@SahneAdi" satiri: o bolum ayri bir sahnede (or. @Bolum2)
            if (entries[i].file.StartsWith("@")) { SceneManager.LoadScene(entries[i].file.Substring(1)); yield break; }
            if (!firstLoad && themes) yield return themes.FadeOut();
            firstLoad = false;
            Build(i);
            ShowMessage($"{LevelNumber()}. {data.name}" + (string.IsNullOrEmpty(data.story) ? "" : "\n<size=34>" + data.story + "</size>"));
            if (themes) yield return themes.FadeIn(data.theme);
            busy = false;
            yield return new WaitForSeconds(1.6f);
            if (!busy && !inScreen && index == i) ShowMessage("");
        }

        int LevelNumber()
        {
            int n = 0;
            for (int k = 0; k <= index; k++) if (entries[k].chapterIndex == entries[index].chapterIndex) n++;
            return n;
        }

        void Build(int i)
        {
            index = i;
            var ta = Resources.Load<TextAsset>("KayipParca/Levels/" + entries[i].file);
            data = LevelData.Parse(ta.text);
            string key = ThemeKeys[Mathf.Clamp(data.theme, 0, ThemeKeys.Length - 1)];
            EnsureShadowMap();

            floorMap.ClearAllTiles(); wallMap.ClearAllTiles(); decorMap.ClearAllTiles(); shadowMap.ClearAllTiles();
            for (int k = objectsRoot.childCount - 1; k >= 0; k--) Destroy(objectsRoot.GetChild(k).gameObject);
            enemyViews.Clear(); nextFrames.Clear(); laserCells.Clear(); fireWarns.Clear(); fireballs.Clear(); slots.Clear(); slotGlow.Clear();
            stationViews = new SpriteRenderer[data.stations.Count];
            dragon = chestCover = chestGlow = yamaliSr = heartSlot = trolley = cable = hook = countdown = lostSr = null;

            for (int y = 0; y < data.height; y++)
                for (int x = 0; x < data.width; x++)
                {
                    var p = new Vector2Int(x, y);
                    char c = data.grid[x, y];
                    var cell = new Vector3Int(x, -y, 0);
                    if (c == '#') { wallMap.SetTile(cell, GetTile(key, ((x * 7 + y * 3) % 3 == 0 ? "wallm_" : "wall_") + WallBits(p))); continue; }
                    int v = (x * 7 + y * 13) % 9;
                    floorMap.SetTile(cell, GetTile(key, c == ',' ? "floor_alt" : v < 7 ? "floor" : "floor_" + (v - 6)));
                    // ic koseleri yuvarlat: iki yaninda duvar olan kosesine beyaz dolgu (sira: sol-ust, sag-ust, sol-alt, sag-alt)
                    bool wu = IsWall(p + Vector2Int.down), wd = IsWall(p + Vector2Int.up), wl = IsWall(p + Vector2Int.left), wr = IsWall(p + Vector2Int.right);
                    string fb = $"{(wl && wu ? 1 : 0)}{(wr && wu ? 1 : 0)}{(wl && wd ? 1 : 0)}{(wr && wd ? 1 : 0)}";
                    if (fb != "0000") shadowMap.SetTile(cell, GetTile(null, "Common/fillet_" + fb));
                    if (c == 'o') decorMap.SetTile(cell, GetTile(key, "obstacle"));
                    if (c == 'X') decorMap.SetTile(cell, GetTile(key, "hazard"));
                    if (c == 'S') stationViews[data.stations.IndexOf(p)] = MakeSprite("Istasyon", "Common/station", World(p), Color.white, 2);
                    if (c == 'K') MakeSprite("VincKumandasi", "Common/console", World(p), Color.white, 2);
                    if (c == 'T') MakeSprite("CopKutusu", "Common/trash", World(p), Color.white, 2);
                    if (c == 'B') MakeSprite("KorsanKutusu", "Common/pirate_box", World(p), Color.white, 2);
                    if (c != 'Y' && data.goals.TryGetValue(c, out var req))
                        AddSlot(MakeSprite("Yuva_" + c, $"Slots/slot_{req}", World(p), c == 'g' ? CloneColor : SlotColor, 2), c, req);
                }

            // dusman rotalari: iri noktalar + donus halkasi + bir sonraki adim cercevesi
            string enemyPrefix = data.theme == 5 ? "paw" : "ugly";
            foreach (var e in data.enemies)
            {
                int len = Mathf.Abs(e.b.x - e.a.x) + Mathf.Abs(e.b.y - e.a.y);
                for (int k = 1; k < len; k++) MakeSprite("Rota", "Common/path_dot", World(LevelData.EnemyPos(e, k)), PathColor, 3);
                MakeSprite("RotaUc", "Common/path_end", World(e.a), PathColor, 3);
                MakeSprite("RotaUc", "Common/path_end", World(e.b), PathColor, 3);
                nextFrames.Add(MakeSprite("SonrakiAdim", "Common/next_frame", World(e.a), Color.white, 3));
                enemyViews.Add(EnemyView.Create(objectsRoot, enemyPrefix, World(e.a), 5));
            }

            // lazerler
            foreach (var l in data.lasers)
            {
                MakeSprite("LazerKaynagi", "Common/emitter_" + "NESW"[l.dir], World(l.emitter), Color.white, 4);
                var list = new List<SpriteRenderer>();
                foreach (var c in l.cells) list.Add(MakeSprite("Lazer", "Common/laser_h", World(c), Color.white, 4));
                laserCells.Add(list);
            }

            // ates hatlari
            foreach (var f in data.fires)
            {
                for (int k = 1; k < f.cells.Count; k++) MakeSprite("AtesYolu", "Common/path_dot", World(f.cells[k]), FireColor, 3);
                fireWarns.Add(MakeSprite("AtesUyari", "Common/fire_warn", World(f.spawn), Color.white, 4));
            }

            // ejderha + kalp
            if (data.hasDragon)
            {
                var r = data.dragonRect;
                dragon = MakeSprite("Ejderha", "Common/dragon_awake", new Vector3(r.x + r.width / 2f, -r.y + 1 - r.height / 2f, 0), Color.white, 4);
                dragon.transform.localScale = new Vector3(r.width / 4f, r.height / 6f, 1);
                chestCover = MakeSprite("GogusKapagi", "Common/chest_closed", World(data.yamali), Color.white, 5);
                chestGlow = MakeSprite("GogusIsik", "Common/chest_glow", World(data.yamali), Color.white, 6);
                heartSlot = data.goals.TryGetValue('Y', out var hreq) ? MakeSprite("KalpYuvasi", $"Slots/slot_{hreq}", World(data.yamali), SlotColor, 5) : null;
                if (heartSlot) AddSlot(heartSlot, 'Y', hreq);
                yamaliSr = MakeSprite("Yamali", "Common/yamali", World(data.yamali), Color.white, 10);
                yamaliSr.gameObject.AddComponent<SpriteAnim>().follow = true;
                // kafanin ustunde: bir sonraki atese kalan tur
                if (data.fires.Count > 0) countdown = MakeSprite("GeriSayim", "Common/num_1", World(new Vector2Int(r.x, Mathf.Max(0, r.y - 1))), Color.white, 11);
            }

            // kayip parca
            if (data.hasLost)
            {
                lostSr = MakeSprite("KayipParca", "Common/lost_sad", World(data.lostStart), Color.white, 6);
                lostSr.gameObject.AddComponent<SpriteAnim>().follow = true;
            }

            // vinc
            if (data.hasCrane)
            {
                for (int x = 1; x < data.width - 1; x++) MakeSprite("Ray", "Common/rail", World(new Vector2Int(x, 0)), Color.white, 9);
                trolley = MakeSprite("Araba", "Common/trolley", World(new Vector2Int(data.hookStart.x, 0)), Color.white, 10);
                cable = MakeSprite("Halat", "Common/cable", World(data.hookStart), Color.white, 9);
                hook = MakeSprite("Kanca", "Common/hook", World(data.hookStart), Color.white, 10);
                hook.gameObject.AddComponent<SpriteAnim>().follow = true;
                trolley.gameObject.AddComponent<SpriteAnim>().follow = true;
            }

            player = PieceView.Create(piecePrefab, objectsRoot, data.shape, true, Color.white, 6);
            clone = data.hasClone ? PieceView.Create(piecePrefab, objectsRoot, Shape.Mirror(data.shape), false, CloneTint, 6) : null;

            SetupCameraAndBackground(key);
            ResetState();
            if (titleText) titleText.text = TitleText(entries[index].chapter, LevelNumber(), data.name);
            SetupShapeHud();
        }

        bool IsWall(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < data.width && p.y < data.height && data.grid[p.x, p.y] == '#';
        bool Inside(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < data.width && p.y < data.height;

        /// duvar autotile: sol, sag, ust, alt komsu zemin mi (1) degil mi (0). (Haritada y asagi dogru artar.)
        string WallBits(Vector2Int p)
        {
            var n = new[] { p + Vector2Int.left, p + Vector2Int.right, p + Vector2Int.down, p + Vector2Int.up };
            var b = new char[4];
            for (int k = 0; k < 4; k++) b[k] = !IsWall(n[k]) ? '1' : '0';   // harita disi da acik sayilir: dis koseler yuvarlak
            return new string(b);
        }

        void EnsureShadowMap()
        {
            if (shadowMap) return;
            var go = new GameObject("Golgeler");
            go.transform.SetParent(floorMap.transform.parent, false);
            shadowMap = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>().sortingOrder = 0;
            var fr = floorMap.GetComponent<TilemapRenderer>(); if (fr) fr.sortingOrder = -1;
        }

        /// Tahtayi ust ve alt HUD bantlarinin arasina sigdirir; pencere boyutu degisince LateUpdate tekrar cagirir.
        void SetupCameraAndBackground(string key, bool snap = true)
        {
            themeKey = key;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            levelCenter = new Vector3(data.width / 2f, -data.height / 2f + 1f, 0);
            float k = UIKit.ScaleFactor();
            float top = Mathf.Clamp01(TopReserve * k / Mathf.Max(1, Screen.height));
            float bottom = Mathf.Clamp01(BottomReserve * k / Mathf.Max(1, Screen.height));
            float free = Mathf.Max(.4f, 1f - top - bottom);              // ekran yuksekliginin tahtaya kalan payi
            float marginX = data.theme == 5 ? 2.2f : 1.4f, marginY = data.theme == 5 ? 1.2f : .45f;
            cam.orthographicSize = Mathf.Max((data.height / 2f + marginY) / free, (data.width / 2f + marginX) / cam.aspect);
            // serbest bolgenin ortasi ekran ortasindan (top - bottom) / 2 kadar asagida: kamerayi o kadar yukari al
            camCenter = levelCenter + new Vector3(0, (top - bottom) * cam.orthographicSize, 0);
            if (snap) cam.transform.position = new Vector3(camCenter.x, camCenter.y, -10);

            float viewH = cam.orthographicSize * 2f, viewW = viewH * cam.aspect;
            foreach (var (sr, file, factor) in new[] { (bgFar, "bg_far", .9f), (bgNear, "bg_near", .75f) })
            {
                if (!sr) continue;
                sr.sprite = SpriteBank.Get($"Themes/{key}/{file}");
                if (!sr.sprite) continue;
                var size = sr.sprite.bounds.size;
                float sc = Mathf.Max(viewW * 1.3f / size.x, viewH * 1.3f / size.y);
                sr.transform.localScale = new Vector3(sc, sc, 1);
                var origin = camCenter;
                if (data.theme == 5 && sr == bgNear) origin.y += viewH * .43f - size.y * sc * .236f;
                var px = sr.GetComponent<ParallaxLayer>();
                if (!px) px = sr.gameObject.AddComponent<ParallaxLayer>();
                px.factor = factor;
                px.origin = origin;
                sr.transform.position = new Vector3(origin.x, origin.y, sr.transform.position.z);
            }
            if (bgNear)
            {
                var blink = bgNear.GetComponent<BlinkSprite>();
                if (data.theme == 5)
                {
                    if (!blink) blink = bgNear.gameObject.AddComponent<BlinkSprite>();
                    blink.open = SpriteBank.Get($"Themes/{key}/bg_near");
                    blink.closed = SpriteBank.Get($"Themes/{key}/bg_near_blink");
                    blink.enabled = true;
                }
                else if (blink) blink.enabled = false;
            }
        }

        void ResetState()
        {
            history.Clear();
            s = new State
            {
                p = data.playerStart, ps = data.shape,
                c = data.cloneStart, cs = data.hasClone ? Shape.Mirror(data.shape) : null,
                hook = data.hookStart, t = 0, moves = data.moves,
                f = data.lostStart, following = false
            };
            foreach (var fb in fireballs.Values) if (fb) Destroy(fb.gameObject);
            fireballs.Clear();
            player.Restore(); if (clone) clone.Restore();
            ApplyVisuals(snap: true);
        }

        // ------------------------------------------------------------------ kurallar
        bool IsPlayerGoal(char c) => c != 'g' && data.goals.ContainsKey(c);

        bool Solid(Vector2Int q, bool calm)
        {
            char c = data.At(q);
            if (c == '#' || c == 'o' || c == 'T' || c == 'B') return true;
            return (c == 'D' || c == 'Y') && !calm;
        }

        /// spent: bu turdan onceki kullanilmis istasyonlar; used: bu hamlede harcanan istasyonun biti (yoksa 0)
        bool TryMove(Vector2Int pos, string shape, int dir, bool isClone, bool calm, int spent, out Vector2Int np, out string ns, out bool dead, out int used)
        {
            np = pos + Shape.Dirs[dir]; ns = shape; dead = false; used = 0;
            if (Solid(np, calm)) return false;
            char c = data.At(np);
            bool own = isClone ? c == 'g' : IsPlayerGoal(c);
            if (own && data.goals[c] != shape) return false;
            if (c == 'S' && (spent & data.StationBit(np)) == 0) { ns = Shape.Toggle(shape, dir); used = data.StationBit(np); Play(sfxStation); }
            dead = c == 'X';
            return true;
        }

        bool HookCanEnter(Vector2Int h) =>
            h.x >= 1 && h.y >= 1 && h.x < data.width - 1 && h.y < data.height - 1 && data.At(h) != 'D';

        void Step(int a)
        {
            var n = s;
            var death = Death.None;
            bool burned = false, grabbed = false, dropped = false, moved = false, picked = false, scared = false;
            if (!s.crane)
            {
                if (a < 4)
                {
                    if (!TryMove(s.p, s.ps, a, false, s.calm, s.spent, out var np, out var nps, out var pd, out var pu))
                    {
                        player.Bump(Shape.Dirs[a]); Play(sfxBump); return;   // carpmak hamle harcamaz
                    }
                    n.p = np; n.ps = nps; n.spent |= pu; moved = true; if (pd) death = Death.Hazard;
                    if (data.hasClone)
                    {
                        int ma = Shape.MirrorDir[a];
                        if (TryMove(s.c, s.cs, ma, true, s.calm, s.spent, out var nc, out var ncs, out var cd, out var cu)) { n.c = nc; n.cs = ncs; n.spent |= cu; if (cd) death = Death.Hazard; }
                        else clone.Bump(Shape.Dirs[ma]);
                    }
                    Play(sfxMove);
                }
                else if (a == 4 || a == 5)
                {
                    bool cw = a == 4;
                    n.ps = cw ? Shape.RotCW(s.ps) : Shape.RotCCW(s.ps);
                    if (data.hasClone) n.cs = cw ? Shape.RotCCW(s.cs) : Shape.RotCW(s.cs);
                    player.Squash(); if (clone) clone.Squash();
                    Play(sfxRotate);
                }
                // a == 8: bekle
            }
            else
            {
                if (a < 4)
                {
                    var h = s.hook + Shape.Dirs[a];
                    if (!HookCanEnter(h)) { Play(sfxBump); return; }
                    n.hook = h; Play(sfxMove);
                }
                else if (a == 8)
                {
                    if (!s.carrying && s.hook == data.yamali && data.ChestOpen(s.t)) { n.carrying = true; n.calm = true; grabbed = true; }
                    else if (s.carrying && (s.hook == data.trash || s.hook == data.box))
                    {
                        n.ending = s.hook == data.trash ? 'T' : 'B'; n.carrying = false; n.crane = false; dropped = true;
                    }
                }
                else return;   // vinc modunda dondurme yok
            }
            n.t = s.t + 1;
            n.moves = s.moves - 1;

            foreach (var e in data.enemies)
            {
                var e0 = LevelData.EnemyPos(e, s.t); var e1 = LevelData.EnemyPos(e, n.t);
                if (e1 == n.p || (e1 == s.p && e0 == n.p)) death = Death.Enemy;
                if (data.hasClone && (e1 == n.c || (e1 == s.c && e0 == n.c))) death = Death.Enemy;
            }
            foreach (var l in data.lasers)
                if (l.IsOn(n.t) && (l.cells.Contains(n.p) || (data.hasClone && l.cells.Contains(n.c)))) death = Death.Laser;
            if (!n.calm)
                foreach (var f in data.fires)
                {
                    if (f.At(n.t, n.p) || f.Swap(s.t, s.p, n.p)) death = Death.Fire;
                    if (n.crane && !n.carrying && (f.At(n.t, n.hook) || f.Swap(s.t, s.hook, n.hook))) { n.hook = data.hookStart; burned = true; }
                }
            // kayip parca: bir kare arkadan izler; ustune basinca alinir; tehlike degerse yerine kacar
            if (data.hasLost)
            {
                if (moved && s.following) n.f = s.p;
                if (!s.following && n.p == data.lostStart) { n.following = true; n.f = data.lostStart; picked = true; }
                if (n.following)
                {
                    bool hit = false;
                    foreach (var e in data.enemies)
                    {
                        var e0 = LevelData.EnemyPos(e, s.t); var e1 = LevelData.EnemyPos(e, n.t);
                        if (e1 == n.f || (s.following && e1 == s.f && e0 == n.f)) hit = true;
                    }
                    foreach (var l in data.lasers) if (l.IsOn(n.t) && l.cells.Contains(n.f)) hit = true;
                    if (!n.calm) foreach (var fl in data.fires) if (fl.At(n.t, n.f) || (s.following && fl.Swap(s.t, s.f, n.f))) hit = true;
                    if (hit) { n.following = false; n.f = data.lostStart; scared = true; picked = false; }
                }
            }
            if (!n.crane && data.hasCrane && !n.calm && data.At(n.p) == 'K') { n.crane = true; n.hook = data.hookStart; }

            history.Push(s);
            s = n;
            ApplyVisuals(snap: false);
            if (s.crane && !history.Peek().crane) Flash("Vinç senin kontrolünde! Ejderha kanadını kaldırınca Yamalı'yı eyerden yakala.");
            if (burned) { Play(sfxFire); Flash("Halat yandı! Kanca başa döndü."); }
            if (grabbed) { Play(sfxGrab); Flash("Yamalı'yı yakaladın! Ejderha sakinleşti. Onu nereye bırakacaksın?"); }
            if (picked) Flash("Kayıp parça seni takip ediyor! Onu yuvaya kadar götür.");
            if (scared) { Play(sfxBump); Flash("Kayıp parça korktu ve yerine kaçtı!"); }
            if (dropped) Flash(s.ending == 'T' ? "Yamalı çöpe gitti. Şimdi eyere otur!" : "Yamalı kendi kutusuna döndü. Şimdi eyere otur!");

            if (death != Death.None) { StartCoroutine(Die(death)); return; }

            char pc = data.At(s.p);
            bool win = IsPlayerGoal(pc) && data.goals[pc] == s.ps &&
                       (!data.hasClone || (data.At(s.c) == 'g' && s.cs == data.goals['g'])) &&
                       (pc != 'Y' || (s.calm && s.ending != '\0'));
            if (win) { StartCoroutine(Win()); return; }
            if (s.moves <= 0) StartCoroutine(Die(Death.Moves));
        }

        void Undo()
        {
            if (history.Count == 0) return;
            s = history.Pop();
            ApplyVisuals(snap: false);
            Play(sfxRotate);
        }

        void ApplyVisuals(bool snap)
        {
            for (int i = 0; i < stationViews.Length; i++)
                if (stationViews[i]) stationViews[i].color = (s.spent & data.StationBit(data.stations[i])) != 0 ? SpentStationColor : Color.white;
            player.SetShape(s.ps);
            if (snap) player.SnapTo(World(s.p)); else player.MoveTo(World(s.p));
            if (clone)
            {
                clone.SetShape(s.cs);
                if (snap) clone.SnapTo(World(s.c)); else clone.MoveTo(World(s.c));
            }
            for (int i = 0; i < enemyViews.Count; i++)
            {
                var w = World(LevelData.EnemyPos(data.enemies[i], s.t));
                if (snap) enemyViews[i].SnapTo(w); else enemyViews[i].MoveTo(w);
                nextFrames[i].transform.position = World(LevelData.EnemyPos(data.enemies[i], s.t + 1));
            }
            for (int i = 0; i < data.lasers.Count; i++)
            {
                var l = data.lasers[i];
                bool on = l.IsOn(s.t), warn = !on && l.IsOn(s.t + 1);
                string o = l.dir == 0 || l.dir == 2 ? "v" : "h";
                var spr = SpriteBank.Get($"Common/laser_{o}" + (on ? "" : "_warn"));
                foreach (var sr in laserCells[i]) { sr.enabled = on || warn; sr.sprite = spr; }
            }
            UpdateFire(snap);
            UpdateDragonAndCrane(snap);
            if (lostSr)
            {
                lostSr.sprite = SpriteBank.Get(s.following ? "Common/lost_happy" : "Common/lost_sad");
                var la = lostSr.GetComponent<SpriteAnim>(); la.target = World(s.f);
                if (snap || !s.following) lostSr.transform.position = World(s.f);
            }
            foreach (var (sr, key) in slots)
            {
                if (!sr) continue;
                bool fits = key == 'g' ? s.cs == data.goals['g'] : s.ps == data.goals[key];
                sr.color = fits ? MatchColor : key == 'g' ? CloneColor : SlotColor;
                if (slotGlow.TryGetValue(sr, out var gl) && gl) gl.enabled = fits && sr.enabled;
            }
            if (movesText)
            {
                movesText.text = $"{s.moves} <size=26>hamle</size>";
                // hap yaziya gore daralir/genisler (TextBackdrop zemini bu kutuya oturtur)
                movesText.rectTransform.sizeDelta = new Vector2(movesText.preferredWidth + 6, IconSize);
                UpdateRescuedRow(LostTotal(), RescuedCount());
            }
            if (hudNow) hudNow.sprite = SpriteBank.Get($"Player/body_{s.ps}");
            if (hintText) hintText.text = string.IsNullOrEmpty(data.hint) ? "" : data.hint;
            if (controlsText) controlsText.text = s.crane ? CraneControls : Controls;
        }

        void UpdateFire(bool snap)
        {
            var alive = new HashSet<long>();
            if (!s.calm)
                for (int li = 0; li < data.fires.Count; li++)
                {
                    var f = data.fires[li];
                    foreach (int spawn in f.Alive(s.t))
                    {
                        long id = li * 100000L + spawn;
                        alive.Add(id);
                        var pos = World(f.cells[s.t - spawn]);
                        if (!fireballs.TryGetValue(id, out var fb) || !fb)
                        {
                            var sr = MakeSprite("AtesTopu", "Common/fireball_0", pos, Color.white, 7);
                            sr.flipX = f.dir == 1;
                            sr.transform.rotation = Quaternion.Euler(0, 0, f.dir == 0 ? -90 : f.dir == 2 ? 90 : 0);
                            fb = sr.gameObject.AddComponent<SpriteAnim>();
                            fb.prefix = "Common/fireball"; fb.follow = true; fb.target = pos;
                            fireballs[id] = fb;
                        }
                        fb.target = pos;
                        if (snap) fb.transform.position = pos;
                    }
                    fireWarns[li].enabled = f.At(s.t + 1, f.spawn);
                }
            else foreach (var w in fireWarns) w.enabled = false;
            var dead = new List<long>();
            foreach (var kv in fireballs) if (!alive.Contains(kv.Key)) { if (kv.Value) Destroy(kv.Value.gameObject); dead.Add(kv.Key); }
            foreach (var k in dead) fireballs.Remove(k);
        }

        void UpdateDragonAndCrane(bool snap)
        {
            if (dragon)
            {
                dragon.sprite = SpriteBank.Get(s.calm ? "Common/dragon_sleep" : "Common/dragon_awake");
                bool open = !s.calm && data.ChestOpen(s.t);
                chestCover.enabled = !s.calm && !open;
                chestGlow.enabled = open;
                if (heartSlot) heartSlot.enabled = s.calm;
                var ya = yamaliSr.GetComponent<SpriteAnim>();
                Vector3 yp = s.carrying ? World(s.hook) + new Vector3(0, -.25f, 0)
                           : s.ending == 'T' ? World(data.trash) + new Vector3(0, .35f, 0)
                           : s.ending == 'B' ? World(data.box) + new Vector3(0, .35f, 0)
                           : World(data.yamali);
                yamaliSr.enabled = open || s.calm;
                if (countdown)
                {
                    countdown.enabled = !s.calm;
                    var f = data.fires[0];
                    int k = 1;
                    while (k < 9 && !f.At(s.t + k, f.spawn)) k++;
                    countdown.sprite = SpriteBank.Get("Common/num_" + k);
                }
                ya.target = yp; if (snap) yamaliSr.transform.position = yp;
            }
            if (hook)
            {
                var hp = World(s.hook);
                var ha = hook.GetComponent<SpriteAnim>(); ha.target = hp; if (snap) hook.transform.position = hp;
                var tp = World(new Vector2Int(s.hook.x, 0));
                var ta = trolley.GetComponent<SpriteAnim>(); ta.target = tp; if (snap) trolley.transform.position = tp;
            }
        }

        Coroutine flashing;
        void Flash(string msg) { if (flashing != null) StopCoroutine(flashing); flashing = StartCoroutine(FlashRoutine(msg)); }
        IEnumerator FlashRoutine(string msg)
        {
            ShowMessage("<size=40>" + msg + "</size>");
            yield return new WaitForSeconds(2.2f);
            if (!busy && !inScreen) ShowMessage("");
        }

        IEnumerator Die(Death why)
        {
            busy = true;
            if (flashing != null) StopCoroutine(flashing);
            string msg;
            switch (why)
            {
                case Death.Laser: msg = "Lazer! Parçalara ayrıldın."; Play(sfxLaser ? sfxLaser : sfxDeath); break;
                case Death.Fire: msg = "Yandın!"; Play(sfxFire ? sfxFire : sfxDeath); break;
                case Death.Enemy: msg = "Yakalandın!"; Play(sfxDeath); break;
                case Death.Moves: msg = "Hamlen bitti."; Play(sfxDeath); break;
                default: msg = "Ah! Tekrar dene."; Play(sfxDeath); break;
            }
            if (why == Death.Moves) { player.SetFace(2); if (clone) clone.SetFace(2); }
            else { player.Shatter(); if (clone) clone.Shatter(); }
            ShowMessage(msg);
            yield return new WaitForSeconds(1.0f);
            ShowMessage("");
            ResetState();
            busy = false;
        }

        IEnumerator Win()
        {
            busy = true;
            player.SetFace(1); if (clone) clone.SetFace(1);
            Play(sfxFit);
            yield return new WaitForSeconds(0.35f);
            Play(sfxWin);
            bool saved = data.hasLost && s.following;
            if (saved) { rescued.Add(entries[index].file); SaveRescued(); ApplyVisuals(snap: false); }
            ShowMessage("Tık!" + (saved ? "\n<size=34>Kayıp parçayı kurtardın!</size>" : "") + (string.IsNullOrEmpty(data.outro) ? "" : "\n<size=34>" + data.outro + "</size>"));
            yield return new WaitForSeconds(string.IsNullOrEmpty(data.outro) ? 0.8f : 2.6f);

            PickEnding(out string endText, out string endImg);
            bool hasEnding = endText != null;
            bool lastOfChapter = index + 1 >= entries.Count || entries[index + 1].chapterIndex != entries[index].chapterIndex;
            if (hasEnding || lastOfChapter)
            {
                ShowMessage("");
                if (themes) yield return themes.FadeOut();
                string text = hasEnding ? endText : entries[index].chapter + "|tamamlandı!";
                string img = hasEnding ? endImg : "Story/title";
                yield return ShowScreen(img, text, "[Enter] devam");
                if (index + 1 < entries.Count)
                {
                    yield return ShowScreen("Story/title", entries[index + 1].chapter, "[Enter] Başla");
                    HideScreen();
                    yield return LoadLevel(index + 1);
                }
                else
                {
                    yield return ShowScreen("Story/title", "HATPIECE|Oynadığın için teşekkürler!", "[Enter] baştan başla");
                    HideScreen();
                    firstLoad = true;
                    yield return TitleAndIntro();
                }
                yield break;
            }
            yield return LoadLevel(index + 1);
        }

        /// 4 son: hic parca kurtarilmadiysa gizli son; az kurtarildiysa yalniz kalp; coksa Yamali'nin nereye birakildigina gore.
        void PickEnding(out string text, out string img)
        {
            text = img = null;
            if (s.ending == '\0') return;
            int r = RescuedCount(), total = LostTotal();
            int need = Mathf.CeilToInt(total * 2f / 3f);
            if (total > 0 && r == 0 && !string.IsNullOrEmpty(data.endDark)) { text = data.endDark; img = "Story/ending_dark"; }
            else if (total > 0 && r < need && !string.IsNullOrEmpty(data.endAlone)) { text = data.endAlone; img = "Story/ending_alone"; }
            else if (s.ending == 'T' && !string.IsNullOrEmpty(data.endT)) { text = data.endT; img = "Story/ending_t"; }
            else if (s.ending == 'B' && !string.IsNullOrEmpty(data.endB)) { text = data.endB; img = "Story/ending_b"; }
            if (text != null && total > 0) text += $"||Kurtarılan parçalar: {r}/{total}";
        }

        int LostTotal() => entries != null && lostLevels.TryGetValue(entries[index].chapterIndex, out var l) ? l.Count : 0;
        int RescuedCount()
        {
            if (entries == null || !lostLevels.TryGetValue(entries[index].chapterIndex, out var l)) return 0;
            int n = 0; foreach (var f in l) if (rescued.Contains(f)) n++;
            return n;
        }

        static void LoadRescued()
        {
            rescued.Clear();
            foreach (var f in PlayerPrefs.GetString(RescuedKey, "").Split(',')) if (f.Length > 0) rescued.Add(f);
        }

        static void SaveRescued() { PlayerPrefs.SetString(RescuedKey, string.Join(",", rescued)); PlayerPrefs.Save(); }

        // ------------------------------------------------------------------ sekil gostergesi (Sen / Hedef)
        void SetupShapeHud()
        {
            if (!hudNow) return;
            hudNow.sprite = SpriteBank.Get($"Player/body_{data.shape}");
            char g1 = data.goals.ContainsKey('G') ? 'G' : data.goals.ContainsKey('Y') ? 'Y' : data.goals.ContainsKey('E') ? 'E' : '\0';
            hudGoal.gameObject.SetActive(g1 != '\0');
            if (g1 != '\0')
            {
                hudGoal.sprite = SpriteBank.Get($"Player/frame_{data.goals[g1]}");   // hedef: ici bos blob cerceve
                hudGoal.color = Color.white;
                hudGoal.GetComponentInChildren<Text>().text = g1 == 'Y' ? "Eyer" : "Hedef";
            }
            bool two = g1 == 'G' && data.goals.ContainsKey('E');
            hudGoal2.gameObject.SetActive(two);
            if (two) { hudGoal2.sprite = SpriteBank.Get($"Player/frame_{data.goals['E']}"); hudGoal2.color = Color.white; }
            // hamle sayaci ve baslik, gorunen ikon sayisina gore yer acar
            int icons = 1 + (g1 != '\0' ? 1 : 0) + (two ? 1 : 0);
            PlaceRight(movesText, rescuedRow, icons);
            if (titleText) UIKit.TopBand(titleText.rectTransform, UIKit.Margin, UIKit.Margin + MovesBlockWidth(icons) + 30, HudTop, IconSize);
        }

        static Image MakeIcon(Transform parent, string label)
        {
            var img = UIKit.MakeImage(parent, "Ikon_" + label, Color.white);
            img.preserveAspect = true;
            img.rectTransform.sizeDelta = new Vector2(IconSize, IconSize);
            if (label == "Sen")   // kendi parcan: govde + yuz + sapka katmanlari (oyundaki gibi)
                foreach (var (n, sp) in new[] { ("Yuz", "Player/face_idle_0"), ("Sapka", "Player/hat") })
                {
                    var layer = UIKit.MakeImage(img.transform, n, Color.white);
                    layer.sprite = SpriteBank.Get(sp); layer.preserveAspect = true;
                    UIKit.Stretch(layer.rectTransform, 0, 0, 1, 1);
                }
            var tx = UIKit.MakeText(img.transform, "Etiket", UIKit.HudLight, 24, TextAnchor.UpperCenter);
            tx.color = UIKit.Mute;
            tx.horizontalOverflow = HorizontalWrapMode.Overflow;
            var trt = tx.rectTransform;   // ikonun hemen altinda, ikonla ortali
            trt.anchorMin = new Vector2(0, 0); trt.anchorMax = new Vector2(1, 0); trt.pivot = new Vector2(.5f, 1);
            trt.anchoredPosition = new Vector2(0, -2); trt.sizeDelta = new Vector2(0, IconLabel);
            tx.text = label;
            return img;
        }

        // ------------------------------------------------------------------ yardimcilar
        static Vector3 World(Vector2Int p) => new Vector3(p.x + .5f, -p.y + .5f, 0);

        void UpdateCable()
        {
            if (!cable || !hook || !trolley) return;
            Vector3 top = trolley.transform.position, bottom = hook.transform.position + new Vector3(0, .3f, 0);
            float len = Mathf.Max(.05f, top.y - bottom.y);
            cable.transform.position = new Vector3(top.x, (top.y + bottom.y) / 2f, 0);
            cable.transform.localScale = new Vector3(1, len, 1);
        }

        Tile GetTile(string themeKey, string name)
        {
            string path = themeKey == null ? name : $"Themes/{themeKey}/{name}";
            if (tileCache.TryGetValue(path, out var t)) return t;
            t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = SpriteBank.Get(path);
            tileCache[path] = t;
            return t;
        }

        void AddSlot(SpriteRenderer sr, char key, string shape)
        {
            slots.Add((sr, key));
            var gl = MakeSprite("YuvaIsigi", $"Slots/glow_{shape}", sr.transform.position, Color.white, sr.sortingOrder - 1);
            gl.transform.SetParent(sr.transform, true);
            gl.enabled = false;
            slotGlow[sr] = gl;
        }

        SpriteRenderer MakeSprite(string n, string spritePath, Vector3 pos, Color col, int order)
        {
            var go = new GameObject(n);
            go.transform.SetParent(objectsRoot, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteBank.Get(spritePath);
            sr.color = col;
            sr.sortingOrder = order;
            return sr;
        }

        // ------------------------------------------------------------------ hileler
        /// Bir hile tusu basildiysa isler ve true doner (o karede normal girdi okunmaz).
        bool HandleCheats()
        {
            int c = ReadCheat();
            if (c < 0) return false;
            bool loaded = data != null;   // baslik ekraninda henuz seviye yok
            if (c == CheatMenu) cheatMenu = !cheatMenu;
            else if (c == CheatNext) CheatJump(loaded ? index + 1 : 0);
            else if (c == CheatPrev) CheatJump(loaded ? index - 1 : entries.Count - 1);
            else if (c == CheatWin) { if (loaded && !busy && !inScreen) StartCoroutine(Win()); }
            else if (c < entries.Count) CheatJump(c);
            return true;
        }

        /// Hikaye ekranlarini, olum/kazanma animasyonunu yarida keser ve dogrudan seviyeye gider. Bastan/sondan tasar.
        void CheatJump(int i)
        {
            int n = entries.Count;
            i = (i % n + n) % n;
            StopAllCoroutines(); flashing = null;
            inScreen = false; HideScreen();
            cheatMenu = false;
            StartCoroutine(LoadLevel(i));
        }

        /// Tab ile acilan seviye listesi (IMGUI: sahnede nesne uretmez).
        void OnGUI()
        {
            if (!cheats || !cheatMenu || entries == null) return;
            if (cheatLabel == null)
            {
                cheatLabel = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
                cheatButton = new GUIStyle(GUI.skin.button) { fontSize = 15, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 6, 6) };
            }
            float scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUILayout.BeginArea(new Rect(16, 16, 300, 720 - 32), GUI.skin.box);
            GUILayout.Label("HİLELER   (Tab: kapat)", cheatLabel);
            cheatScroll = GUILayout.BeginScrollView(cheatScroll);
            int chapter = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].chapterIndex != chapter) { chapter = entries[i].chapterIndex; GUILayout.Label(entries[i].chapter, cheatLabel); }
                GUI.color = data != null && i == index ? new Color(1f, .82f, .3f) : Color.white;
                if (GUILayout.Button((i < 9 ? $"[{i + 1}]  " : "") + levelNames[i], cheatButton)) CheatJump(i);
            }
            GUI.color = Color.white;
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("[B] Önceki", cheatButton)) CheatJump(data != null ? index - 1 : entries.Count - 1);
            if (GUILayout.Button("[N] Sonraki", cheatButton)) CheatJump(data != null ? index + 1 : 0);
            GUILayout.EndHorizontal();
            GUI.enabled = data != null && !busy && !inScreen;
            if (GUILayout.Button("[G] Seviyeyi geçmiş say", cheatButton)) StartCoroutine(Win());
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        /// -1 yok, 0-8 o seviye (1-9 tuslari), yoksa Cheat* sabitleri
        static int ReadCheat()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return -1;
            if (k.nKey.wasPressedThisFrame) return CheatNext;
            if (k.bKey.wasPressedThisFrame) return CheatPrev;
            if (k.gKey.wasPressedThisFrame) return CheatWin;
            if (k.tabKey.wasPressedThisFrame) return CheatMenu;
            for (int i = 0; i < 9; i++)
                if (k[Key.Digit1 + i].wasPressedThisFrame || k[Key.Numpad1 + i].wasPressedThisFrame) return i;
#else
            if (Input.GetKeyDown(KeyCode.N)) return CheatNext;
            if (Input.GetKeyDown(KeyCode.B)) return CheatPrev;
            if (Input.GetKeyDown(KeyCode.G)) return CheatWin;
            if (Input.GetKeyDown(KeyCode.Tab)) return CheatMenu;
            for (int i = 0; i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) return i;
#endif
            return -1;
        }

        void ShowMessage(string m) { if (messageText) messageText.text = m; }
        void Play(AudioClip c) { if (sfx && c) sfx.PlayOneShot(c); }

        /// -1 yok, 0-3 yon (N E S W), 4 saat yonu, 5 ters, 6 geri al, 7 yeniden, 8 bosluk/enter, 10 esc (N artik ReadCheat'te)
        static int ReadAction()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return -1;
            if (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) return 0;
            if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) return 1;
            if (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame) return 2;
            if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame) return 3;
            if (k.eKey.wasPressedThisFrame) return 4;
            if (k.qKey.wasPressedThisFrame) return 5;
            if (k.zKey.wasPressedThisFrame) return 6;
            if (k.rKey.wasPressedThisFrame) return 7;
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame) return 8;
            if (k.escapeKey.wasPressedThisFrame) return 10;
#else
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) return 0;
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) return 1;
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) return 2;
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) return 3;
            if (Input.GetKeyDown(KeyCode.E)) return 4;
            if (Input.GetKeyDown(KeyCode.Q)) return 5;
            if (Input.GetKeyDown(KeyCode.Z)) return 6;
            if (Input.GetKeyDown(KeyCode.R)) return 7;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) return 8;
            if (Input.GetKeyDown(KeyCode.Escape)) return 10;
#endif
            return -1;
        }
    }
}
