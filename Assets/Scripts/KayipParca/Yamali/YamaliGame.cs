using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KayipParca
{
    /// <summary>
    /// Bolum 2: Yamali'nin Intikami. Yandan bakisli, gercek zamanli platform oyunu.
    ///  - A / D (veya oklar): yuru.  Bosluk: zipla (basili tutarsan daha yuksek).  S: tek yonlu platformdan in.
    ///  - R: seviyeyi bastan baslat.  Hile (cheats acikken): N / B sonraki-onceki, 1-8 seviye, G seviyeyi gec.
    ///  - Her seviyede (1,2,4,5,6,7) istege bagli bir yama parcasi var. Toplananlar Yamali'nin ordusu olur ve arkasindan yurur.
    ///  - 8. seviyenin sonunda: Bolum 1'de kurtarilan parca sayisi ile yama ordusu karsilastirilir.
    ///    Fazla olan kazanir, esitse ikisi birlikte duser.
    /// Seviyeler: Resources/KayipParca/Yamali/yamali1..8.txt   Metinler: Resources/KayipParca/Story/yamali.txt
    /// Sahne Kayip Parca > 9) menusuyle kurulur; her sey calisma aninda olusturulur.
    /// </summary>
    public class YamaliGame : MonoBehaviour
    {
        [Header("Sesler (bos kalabilir)")]
        public AudioClip music;
        public AudioClip sfxJump, sfxPickup, sfxDie, sfxWin, sfxStomp, sfxSpring, sfxFlag, sfxStep, sfxLand;
        // Inspector'da bos birakilanlar Resources/KayipParca/Audio'dan yuklenir (Bolum 1 ile ayni sesler)
        static AudioClip Clip(AudioClip c, string name) => c ? c : Resources.Load<AudioClip>("KayipParca/Audio/" + name);
        void LoadDefaultSounds()
        {
            sfxJump = Clip(sfxJump, "hareket"); sfxPickup = Clip(sfxPickup, "kayip_bulundu"); sfxDie = Clip(sfxDie, "olum");
            sfxWin = Clip(sfxWin, "seviye_bitti"); sfxStomp = Clip(sfxStomp, "carpma"); sfxSpring = Clip(sfxSpring, "istasyon");
            sfxFlag = Clip(sfxFlag, "kayip_kurtarildi"); sfxStep = Clip(sfxStep, "cirkin_adim"); sfxLand = Clip(sfxLand, "carpma");
        }
        // tema -> muzik (Bolum 1'in ThemeManager eslemesiyle ayni)
        static readonly string[] ThemeMusic = { "muzik_muzik_kutusu", "muzik_kalimba", "muzik_ksilofon", "muzik_ksilofon", "muzik_muzik_kutusu", "muzik_kalimba", "muzik_muzik_kutusu", "muzik_muzik_kutusu" };
        void SetMusic(int theme)
        {
            if (!musicSrc) return;
            var clip = music ? music : Resources.Load<AudioClip>("KayipParca/Audio/" + ThemeMusic[Mathf.Clamp(theme, 0, ThemeMusic.Length - 1)]);
            if (!clip || musicSrc.clip == clip) return;
            musicSrc.clip = clip; musicSrc.volume = .35f; musicSrc.Play();
        }
        float stepT;

        [Header("Ayarlar")]
        [Tooltip("0 = giris hikayesinden basla, 1-8 = dogrudan o seviyeden (test icin)")]
        [Range(0, 8)] public int startAt = 0;
        [Tooltip("Hileler: N / B / 1-8 / G. Oyunu dagitmadan once kapatin.")]
        public bool cheats = true;

        [Header("Hareket")]
        public float runSpeed = 7.5f;
        public float groundAccel = 70f, airAccel = 45f, friction = 60f;
        public float gravity = 42f, maxFall = 19f, jumpSpeed = 15.5f, springSpeed = 23f;
        public float coyoteTime = .1f, jumpBuffer = .12f;

        public const string YamaKey = "hatpiece_yama", RescuedKey = "hatpiece_rescued";
        static readonly string[] ThemeKeys = { "T0_Kutu", "T1_Hali", "T2_Mutfak", "T3_Oyuncak", "T4_Gece", "T5_Kedi", "T6_Masa", "T7_Resim" };
        const float HW = .34f, HH = .42f;          // oyuncu kutusu (yarim genislik / yukseklik)
        const float Step = 1f / 120f;

        // ------------------------------------------------------------------ seviye verisi
        class Level
        {
            public string name = "", story = "", hint = "", outro = "";
            public float moverSpeed = 2.4f;   // SPEED= (asansor / kayan platform hizi)
            public bool dark;                 // DARK=<ortam isigi>: karanlik seviye; oyuncu, cikis ve bayraklar kendi isiginda
            public float ambient = .12f;      // karanlikta global isik siddeti (DARK=0 tam karanlik)
            public int theme, W, H;
            public char[,] g;   // [x, y], y = 0 en alt satir
        }

        class Mover
        {
            public Transform tr; public Vector2 a, b, pos, delta; public float hw, len, p, dir = 1, wait;
            public float Top => pos.y + .25f;
        }

        class Enemy { public Transform tr; public SpriteRenderer sr; public Vector2 pos; public float vy; public int dir = -1; public bool dead; public float anim; }
        class Pickup { public Transform tr; public SpriteRenderer sr; public Vector2 pos; public bool taken; public float t; }
        class Spring { public Vector2 pos; public SpriteRenderer sr; public float t; }
        class Flag { public Vector2 pos; public SpriteRenderer sr; public bool on; }

        // ------------------------------------------------------------------ durum
        Camera cam;
        Light2D globalLight;
        Level L;

        /// Kodla kurulan 2D isik butun siralama katmanlarini etkilesin (Target Sorting Layers = All).
        static void AllSortingLayers(Light2D l)
        {
            var f = typeof(Light2D).GetField("m_ApplyToSortingLayers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (f == null) return;
            var layers = SortingLayer.layers; var ids = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++) ids[i] = layers[i].id;
            f.SetValue(l, ids);
        }

        Light2D PointLight(string n, Transform parent, Vector3 pos, float radius, float intensity)
        {
            var l = new GameObject(n).AddComponent<Light2D>();
            l.transform.SetParent(parent, false); l.transform.position = pos;
            l.lightType = Light2D.LightType.Point; l.intensity = intensity; l.color = Color.white;
            l.pointLightOuterRadius = radius; l.pointLightInnerRadius = radius * .25f;
            AllSortingLayers(l);
            return l;
        }
        int levelIndex;          // 1..8
        int levelCount;
        Transform world;
        Tilemap wallMap, decoMap;
        readonly Dictionary<string, Tile> tileCache = new Dictionary<string, Tile>();
        readonly List<Mover> movers = new List<Mover>();
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Pickup> pickups = new List<Pickup>();
        readonly List<Spring> springs = new List<Spring>();
        readonly List<Flag> flags = new List<Flag>();
        readonly HashSet<string> yama = new HashSet<string>();
        readonly Dictionary<string, string> text = new Dictionary<string, string>();
        readonly List<string> introLines = new List<string>();
        readonly List<(Transform tr, float f, Vector2 size)> parallax = new List<(Transform, float, Vector2)>();

        // oyuncu
        Transform playerTr; SpriteRenderer playerSr;
        Vector2 pos, vel, spawn;
        bool grounded, dropping, dead, levelDone, playing;
        Mover riding;
        float coyote, buffer, animT, deadT, squash;
        int facing = 1;
        Vector2 exitPos;
        readonly List<Vector2> trail = new List<Vector2>();
        readonly List<SpriteRenderer> army = new List<SpriteRenderer>();
        float trailT;

        // arayuz
        Text levelText, chapterText, hintText, msgText, controlsText;
        RectTransform armyRow; int armyShown = -1;
        GameObject overlay; Text overlayTitle, overlayBody, overlayPrompt;
        GameObject screenRoot; Image screenPic; Text screenText, screenPrompt;
        bool inScreen, screenNext, screenSkip, waitOverlay;
        AudioSource audioSrc, musicSrc;
        Coroutine flashing;

        // ------------------------------------------------------------------ kurulum
        void Start()
        {
#if UNITY_EDITOR
            int test = UnityEditor.SessionState.GetInt("YamaliTestStartAt", -1);   // Kayip Parca > 9/10 test menuleri
            if (test >= 0) { startAt = test; UnityEditor.SessionState.EraseInt("YamaliTestStartAt"); }
#endif
            LoadDefaultSounds();
            LoadText();
            LoadSave();
            while (Resources.Load<TextAsset>($"KayipParca/Yamali/yamali{levelCount + 1}") != null) levelCount++;
            SetupCamera();
            SetupUI();
            audioSrc = gameObject.AddComponent<AudioSource>();
            musicSrc = gameObject.AddComponent<AudioSource>(); musicSrc.loop = true; musicSrc.volume = .5f;
            audioSrc.mute = !Settings.Sfx; musicSrc.mute = !Settings.Music;   // baslangic menusu ayarlari
            if (music) { musicSrc.clip = music; musicSrc.Play(); }
            StartCoroutine(Flow());
        }

        void LoadText()
        {
            var ta = Resources.Load<TextAsset>("KayipParca/Story/yamali");
            if (ta == null) return;
            foreach (var raw in ta.text.Replace("\r", "").Split('\n'))
            {
                if (raw.StartsWith("#")) continue;
                int eq = raw.IndexOf('=');
                if (eq <= 0) continue;
                string k = raw.Substring(0, eq).Trim(), v = raw.Substring(eq + 1).Trim();
                if (k == "INTRO") { int bar = v.IndexOf('|'); introLines.Add(bar < 0 ? v : v.Substring(0, bar + 1) + Loc.T(v.Substring(bar + 1))); }
                else text[k] = Loc.T(v);
            }
        }

        string T(string key, string fallback) => text.TryGetValue(key, out var v) ? v : Loc.T(fallback);

        void LoadSave()
        {
            yama.Clear();
            foreach (var f in PlayerPrefs.GetString(YamaKey, "").Split(',')) if (f.Length > 0) yama.Add(f);
        }

        void SaveYama() { PlayerPrefs.SetString(YamaKey, string.Join(",", yama)); PlayerPrefs.Save(); }

        static int RescuedCount()
        {
            int n = 0;
            foreach (var f in PlayerPrefs.GetString(RescuedKey, "").Split(',')) if (f.Length > 0) n++;
            return n;
        }

        void SetupCamera()
        {
            cam = Camera.main;
            if (!cam)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>(); go.AddComponent<AudioListener>();
            }
            cam.orthographic = true; cam.orthographicSize = 7.5f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            globalLight = FindAnyObjectByType<Light2D>();
            if (globalLight == null)
            {
                globalLight = new GameObject("Global Light 2D").AddComponent<Light2D>();
                globalLight.lightType = Light2D.LightType.Global; globalLight.intensity = 1f;
                AllSortingLayers(globalLight);
            }
        }

        // ------------------------------------------------------------------ akis
        IEnumerator Flow()
        {
            int first = startAt;
            if (first <= 0)
            {
                foreach (var line in introLines)
                {
                    int bar = line.IndexOf('|');
                    if (bar < 0) continue;
                    yield return ShowScreen("Story/" + line.Substring(0, bar).Trim(), line.Substring(bar + 1).Trim(), Loc.T("[Enter] devam    [Esc] geç"));
                    if (screenSkip) break;
                }
                HideScreen();
                first = 1;
            }
            yield return RunLevel(Mathf.Clamp(first, 1, Mathf.Max(1, levelCount)));
        }

        IEnumerator RunLevel(int i)
        {
            StopCoroutineSafe();
            levelIndex = i;
            BuildLevel(i);
            // seviye girisi: ad + kisa hikaye
            playing = false;
            ShowOverlay($"{i}. {L.name}", L.story, Loc.T("[Boşluk] başla"));
            waitOverlay = true;
            yield return null;
            while (waitOverlay) yield return null;
            HideOverlay();
            playing = true;
        }

        IEnumerator LevelComplete()
        {
            levelDone = true; playing = false;
            vel = Vector2.zero; grounded = true; squash = 0;   // fizik durur: bekleme karesine don
            Play(sfxWin);
            string m = T("CLEAR", "Seviye tamam!");
            if (!string.IsNullOrEmpty(L.outro)) m += "\n<size=34>" + L.outro.Replace("|", "\n") + "</size>";
            Flash(m, string.IsNullOrEmpty(L.outro) ? 1.2f : 3f);
            yield return new WaitForSeconds(string.IsNullOrEmpty(L.outro) ? 1.2f : 3f);
            if (levelIndex < levelCount) { yield return RunLevel(levelIndex + 1); yield break; }
            yield return Finale();
        }

        IEnumerator Finale()
        {
            int hero = RescuedCount(), army = yama.Count;
            string score = Loc.F("Şapkalı'nın kurtardığı parçalar: {0}    ·    Yama ordusu: {1}", hero, army);
            yield return WarAnimation(hero, army);
            yield return ShowScreen("Story/y_war", T("WAR", "Yamalı ordusuyla resmin önüne vardı. Şapkalı da kurtardığı parçalarla bekliyordu.") + "||" + score, Loc.T("[Enter] devam"));
            string img, body;
            if (army > hero) { img = "Story/y_end_yama"; body = T("END_YAMA", "Yama ordusu daha kalabalıktı. Yamalı ejderhanın eyerine yeniden oturdu."); }
            else if (hero > army) { img = "Story/y_end_hero"; body = T("END_HERO", "Şapkalı'nın arkasında daha çok parça vardı. Yamalı'nın ordusu dağıldı."); }
            else { img = "Story/y_end_both"; body = T("END_BOTH", "İki ordu eşitti. Çarpıştılar ve ikisi birlikte masadan düştü."); }
            yield return ShowScreen(img, body + "||" + score, Loc.T("[Enter] devam"));
            yield return ShowScreen("Story/title", T("THANKS", "HATPIECE|Oynadığın için teşekkürler!"), Loc.T("[Enter] başlık ekranı"));
            LevelManager.pendingStart = -1;
            SceneManager.LoadScene("KayipParca");
        }

        // ------------------------------------------------------------------ final: iki ordunun savasi (animasyon)
        /// Seviye gizlenir, siyah zeminde Sapkali + kurtardigi parcalar solda, Yamali + yama ordusu sagda.
        /// Konusur, yururler, ortada carpisirlar; kalabalik olmayan taraf sacilir, esitse ikisi de masadan duser.
        IEnumerator WarAnimation(int hero, int army)
        {
            playing = false; HideOverlay(); StopCoroutineSafe();
            if (world) world.gameObject.SetActive(false);
            var root = new GameObject("Savas").transform;
            var c = (Vector2)cam.transform.position;
            float ground = c.y - 3.2f;
            SpriteRenderer Sp(string path, Vector3 p, int order, float scale = 1f, Transform parent = null)
            {
                var go = new GameObject(path); go.transform.SetParent(parent ? parent : root, false);
                go.transform.position = p; go.transform.localScale = Vector3.one * scale;
                var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = SpriteBank.Get(path); sr.sortingOrder = order;
                return sr;
            }
            // zemin: masa kenari
            var table = new GameObject("Masa").AddComponent<SpriteRenderer>(); table.transform.SetParent(root, false);
            table.sprite = UIKit.RoundedSprite; table.drawMode = SpriteDrawMode.Sliced; table.size = new Vector2(30, .7f);
            table.transform.position = new Vector3(c.x, ground - .55f, 0); table.sortingOrder = 40; table.color = Color.white;
            // Sapkali (govde + yuz + sapka) ve parcalari
            var heroT = new GameObject("Sapkali").transform; heroT.SetParent(root, false); heroT.position = new Vector3(c.x - 8f, ground + .5f, 0);
            Sp("Player/body_1010", heroT.position, 52, 1f, heroT); Sp("Player/face_idle_0", heroT.position, 53, 1f, heroT); Sp("Player/hat", heroT.position, 54, 1f, heroT);
            heroT.localScale = Vector3.one * 1.6f;
            var heroSide = new List<Transform>();
            for (int i = 0; i < hero; i++) heroSide.Add(Sp("Common/lost_happy", new Vector3(c.x - 10.2f - i * .95f, ground + .35f, 0), 51, .9f).transform);
            // Yamali ve ordusu
            var yamT = Sp("Common/yamali", new Vector3(c.x + 8f, ground + .5f, 0), 52, 1.6f).transform;
            var armySide = new List<Transform>();
            for (int i = 0; i < army; i++) { var a = Sp("Enemies/ugly_" + (i % 4), new Vector3(c.x + 10.2f + i * .95f, ground + .35f, 0), 51, .9f); armySide.Add(a.transform); }
            var cam0 = cam.transform.position;

            // 1) konusma
            Flash(Loc.T("Yamalı: \"Eyerimi geri almaya geldim.\""), 1.8f); yield return new WaitForSeconds(1.9f);
            Flash(Loc.T("Şapkalı: \"Eyer kimsenin değil. Ejderhanın uykusu için.\""), 1.9f); yield return new WaitForSeconds(2.1f);
            // 2) hucum: iki taraf ortaya yurur
            float dur = 1.6f;
            var heroStart = heroT.position; var yamStart = yamT.position;
            var hs0 = new List<Vector3>(); foreach (var tr in heroSide) hs0.Add(tr.position);
            var as0 = new List<Vector3>(); foreach (var tr in armySide) as0.Add(tr.position);
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float k = t / dur, bob = Mathf.Abs(Mathf.Sin(t * 14f)) * .25f;
                heroT.position = Vector3.Lerp(heroStart, new Vector3(c.x - 1.3f, ground + .5f, 0), k) + Vector3.up * bob;
                yamT.position = Vector3.Lerp(yamStart, new Vector3(c.x + 1.3f, ground + .5f, 0), k) + Vector3.up * bob;
                for (int i = 0; i < heroSide.Count; i++) heroSide[i].position = Vector3.Lerp(hs0[i], new Vector3(c.x - 2.6f - i * .95f, ground + .35f, 0), k) + Vector3.up * Mathf.Abs(Mathf.Sin(t * 14f + i)) * .25f;
                for (int i = 0; i < armySide.Count; i++) armySide[i].position = Vector3.Lerp(as0[i], new Vector3(c.x + 2.6f + i * .95f, ground + .35f, 0), k) + Vector3.up * Mathf.Abs(Mathf.Sin(t * 14f + i)) * .25f;
                yield return null;
            }
            // 3) carpisma: beyaz toz bulutlari + sarsinti
            Play(sfxStomp);
            var puffs = new List<SpriteRenderer>();
            for (int i = 0; i < 7; i++)
            {
                var pf = new GameObject("Toz").AddComponent<SpriteRenderer>(); pf.transform.SetParent(root, false);
                pf.sprite = UIKit.CircleSprite; pf.sortingOrder = 60; pf.color = Color.white;
                pf.transform.position = new Vector3(c.x + Random.Range(-1.6f, 1.6f), ground + .4f + Random.Range(0f, 1.8f), 0);
                pf.transform.localScale = Vector3.zero; puffs.Add(pf);
            }
            for (float t = 0; t < .7f; t += Time.deltaTime)
            {
                float k = t / .7f;
                foreach (var pf in puffs) { pf.transform.localScale = Vector3.one * Mathf.Lerp(.2f, 2.2f, k); pf.color = new Color(1, 1, 1, 1 - k); }
                cam.transform.position = cam0 + new Vector3(Random.Range(-.12f, .12f), Random.Range(-.12f, .12f), 0) * (1 - k);
                yield return null;
            }
            cam.transform.position = cam0;
            foreach (var pf in puffs) Destroy(pf.gameObject);
            // 4) sonuc: kaybeden taraf sacilir, esitse iki lider masadan duser, kazananlar ziplar
            var losers = new List<Transform>(); var winners = new List<Transform>();
            if (army > hero) { losers.AddRange(heroSide); losers.Add(heroT); winners.AddRange(armySide); winners.Add(yamT); }
            else if (hero > army) { losers.AddRange(armySide); losers.Add(yamT); winners.AddRange(heroSide); winners.Add(heroT); }
            else { losers.Add(heroT); losers.Add(yamT); }
            var vel = new List<Vector3>(); var start = new List<Vector3>();
            foreach (var l in losers)
            {
                float dir = l.position.x < c.x ? -1f : 1f;
                vel.Add(new Vector3(dir * Random.Range(4f, 9f), hero == army ? -2f : Random.Range(5f, 9f), 0)); start.Add(l.position);
            }
            for (float t = 0; t < 1.6f; t += Time.deltaTime)
            {
                for (int i = 0; i < losers.Count; i++)
                {
                    var l = losers[i];
                    l.position = start[i] + vel[i] * t + Vector3.down * (hero == army ? 9f : 14f) * t * t * .5f;
                    l.rotation = Quaternion.Euler(0, 0, (vel[i].x < 0 ? 1 : -1) * 420f * t);
                }
                foreach (var w in winners) w.position = new Vector3(w.position.x, ground + .45f + Mathf.Abs(Mathf.Sin(t * 10f + w.position.x)) * .45f, 0);
                yield return null;
            }
            yield return new WaitForSeconds(.4f);
            Destroy(root.gameObject);
        }

        void StopCoroutineSafe() { if (flashing != null) { StopCoroutine(flashing); flashing = null; if (msgText) msgText.text = ""; } }

        // ------------------------------------------------------------------ seviye kurma
        Level Parse(int i)
        {
            var ta = Resources.Load<TextAsset>($"KayipParca/Yamali/yamali{i}");
            var lv = new Level();
            var rows = new List<string>();
            bool inMap = false;
            foreach (var raw in ta.text.Replace("\r", "").Split('\n'))
            {
                if (inMap) { if (raw.Length > 0) rows.Add(raw); continue; }
                if (raw == "MAP") { inMap = true; continue; }
                int eq = raw.IndexOf('=');
                if (eq <= 0 || raw.StartsWith("#")) continue;
                string k = raw.Substring(0, eq), v = raw.Substring(eq + 1);
                switch (k)
                {
                    case "NAME": lv.name = Loc.T(v); break;
                    case "THEME": int.TryParse(v, out lv.theme); break;
                    case "STORY": lv.story = Loc.T(v); break;
                    case "HINT": lv.hint = Loc.T(v); break;
                    case "OUTRO": lv.outro = Loc.T(v); break;
                    case "SPEED": float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out lv.moverSpeed); break;
                    case "DARK":
                        lv.dark = true;
                        if (float.TryParse(v.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var amb))
                            lv.ambient = amb >= 1f ? .12f : amb;   // DARK=1 eski yazim: varsayilan losluk
                        break;
                }
            }
            lv.H = rows.Count; lv.W = 0;
            foreach (var r in rows) lv.W = Mathf.Max(lv.W, r.Length);
            lv.g = new char[lv.W, lv.H];
            for (int r = 0; r < lv.H; r++)
                for (int x = 0; x < lv.W; x++)
                    lv.g[x, lv.H - 1 - r] = x < rows[r].Length ? rows[r][x] : '.';
            return lv;
        }

        char At(int x, int y) => (x < 0 || x >= L.W || y < 0 || y >= L.H) ? (y < 0 || y >= L.H ? '.' : '#') : L.g[x, y];
        bool Solid(int x, int y) { if (x < 0 || x >= L.W) return true; if (y < 0 || y >= L.H) return false; return L.g[x, y] == '#'; }
        bool OneWay(int x, int y) => x >= 0 && x < L.W && y >= 0 && y < L.H && L.g[x, y] == '=';
        bool WallForArt(int x, int y) => x < 0 || x >= L.W || y < 0 || (y < L.H && L.g[x, y] == '#');

        void BuildLevel(int i)
        {
            if (world) Destroy(world.gameObject);
            movers.Clear(); enemies.Clear(); pickups.Clear(); springs.Clear(); flags.Clear(); parallax.Clear(); army.Clear(); trail.Clear();
            L = Parse(i);
            world = new GameObject("Seviye_" + i).transform;
            string key = ThemeKeys[Mathf.Clamp(L.theme, 0, ThemeKeys.Length - 1)];

            var grid = new GameObject("Grid", typeof(Grid)); grid.transform.SetParent(world, false);
            wallMap = MakeTilemap(grid.transform, "Duvarlar", 0);
            decoMap = MakeTilemap(grid.transform, "Kose", 0);

            for (int y = 0; y < L.H; y++)
                for (int x = 0; x < L.W; x++)
                {
                    char c = L.g[x, y];
                    var cell = new Vector3Int(x, y, 0);
                    var ctr = new Vector2(x + .5f, y + .5f);
                    if (c == '#')
                    {
                        string bits = $"{(WallForArt(x - 1, y) ? 0 : 1)}{(WallForArt(x + 1, y) ? 0 : 1)}{(WallForArt(x, y + 1) ? 0 : 1)}{(WallForArt(x, y - 1) ? 0 : 1)}";
                        wallMap.SetTile(cell, GetTile($"Themes/{key}/" + ((x * 7 + y * 3) % 5 == 0 ? "wallm_" : "wall_") + bits));
                        continue;
                    }
                    bool wu = WallForArt(x, y + 1), wd = WallForArt(x, y - 1), wl = WallForArt(x - 1, y), wr = WallForArt(x + 1, y);
                    string fb = $"{(wl && wu ? 1 : 0)}{(wr && wu ? 1 : 0)}{(wl && wd ? 1 : 0)}{(wr && wd ? 1 : 0)}";
                    if (fb != "0000") decoMap.SetTile(cell, GetTile("Common/fillet_" + fb));
                    switch (c)
                    {
                        case '=': MakeSprite("Platform", "Yamali/oneway", ctr, 1); break;
                        case '^': MakeSprite("Diken", "Yamali/spike_up", ctr, 1); break;
                        case 'v': MakeSprite("Diken", "Yamali/spike_down", ctr, 1); break;
                        case '<': MakeSprite("Diken", "Yamali/spike_left", ctr, 1); break;
                        case '>': MakeSprite("Diken", "Yamali/spike_right", ctr, 1); break;
                        case '|': MakeSprite("Ray", "Yamali/rail_v", ctr, -1); break;
                        case '-': MakeSprite("Ray", "Yamali/rail_h", ctr, -1); break;
                        case 'P': spawn = new Vector2(x + .5f, y + HH + .001f); break;
                        case 'E': exitPos = new Vector2(x + .5f, y + 1f); MakeSprite("Cikis", "Yamali/exit", exitPos, 1); break;
                        case 'F': flags.Add(new Flag { pos = ctr, sr = MakeSprite("Bayrak", "Yamali/flag_down", ctr, 1) }); break;
                        case 'J': springs.Add(new Spring { pos = ctr, sr = MakeSprite("Yay", "Yamali/spring_0", ctr, 1) }); break;
                        case 'Y':
                            {
                                var p = new Pickup { pos = ctr, sr = MakeSprite("YamaParcasi", "Enemies/ugly_0", ctr, 3) };
                                p.tr = p.sr.transform; p.tr.localScale = Vector3.one * .8f;
                                if (yama.Contains(LevelId)) { p.taken = true; p.sr.color = new Color(1, 1, 1, .2f); }
                                pickups.Add(p);
                                break;
                            }
                        case 'M':
                            {
                                var e = new Enemy { pos = new Vector2(x + .5f, y + .5f) };
                                e.sr = MakeSprite("Silgi", "Yamali/eraser_0", e.pos, 3); e.tr = e.sr.transform;
                                enemies.Add(e);
                                break;
                            }
                    }
                }
            BuildMovers('A', '|', true);
            BuildMovers('H', '-', false);
            BuildParallax(key);

            // oyuncu
            playerSr = MakeSprite("Yamali", "Yamali/yamali_idle_0", spawn, 5);
            playerTr = playerSr.transform;
            // karanlik seviye: ortam isigi kisik, oyuncu / cikis / bayraklar kendi isiginda
            if (globalLight) globalLight.intensity = L.dark ? L.ambient : 1f;
            if (L.dark)
            {
                PointLight("OyuncuIsigi", playerTr, playerTr.position, 5.5f, 1.3f);
                PointLight("CikisIsigi", world, new Vector3(exitPos.x, exitPos.y, 0), 3f, .9f);
                foreach (var f in flags) PointLight("BayrakIsigi", world, new Vector3(f.pos.x, f.pos.y, 0), 2f, .6f);
            }
            pos = spawn; vel = Vector2.zero; dead = false; levelDone = false; riding = null; grounded = false; facing = 1; squash = 0;
            if (levelText) levelText.text = $"{levelIndex}. {L.name}";
            SetMusic(L.theme);
            if (hintText) hintText.text = string.IsNullOrEmpty(L.hint) ? T("HINT", "A / D: yürü    Boşluk: zıpla    S: platformdan in    R: yeniden") : L.hint;
            UpdateArmyHud();
            SnapCamera();
        }

        string LevelId => "y" + levelIndex;

        void BuildMovers(char body, char rail, bool vertical)
        {
            for (int y = 0; y < L.H; y++)
                for (int x = 0; x < L.W; x++)
                {
                    if (L.g[x, y] != body || (x > 0 && L.g[x - 1, y] == body)) continue;
                    int w = 0; while (x + w < L.W && L.g[x + w, y] == body) w++;
                    int lo = 0, hi = 0;
                    if (vertical)
                    {
                        while (y + hi + 1 < L.H && L.g[x, y + hi + 1] == rail) hi++;
                        while (y - lo - 1 >= 0 && L.g[x, y - lo - 1] == rail) lo++;
                    }
                    else
                    {
                        while (x - lo - 1 >= 0 && L.g[x - lo - 1, y] == rail) lo++;
                        while (x + w + hi < L.W && L.g[x + w + hi, y] == rail) hi++;
                    }
                    var m = new Mover { hw = w * .5f };
                    var start = new Vector2(x + w * .5f, y + .75f);
                    m.a = vertical ? start - new Vector2(0, lo) : start - new Vector2(lo, 0);
                    m.b = vertical ? start + new Vector2(0, hi) : start + new Vector2(hi, 0);
                    m.len = Vector2.Distance(m.a, m.b);
                    m.p = lo; m.pos = start;
                    if (lo > 0 && hi > 0) m.dir = -1;   // ortada baslayan platform once sola / asagi (oyuncuya dogru) gider
                    m.tr = new GameObject(vertical ? "Asansor" : "KayanPlatform").transform; m.tr.SetParent(world, false);
                    m.tr.position = start;
                    for (int k = 0; k < w; k++)
                    {
                        var s = MakeSprite("Parca", "Yamali/lift", new Vector2(start.x - m.hw + k + .5f, start.y - .25f), 2);
                        s.transform.SetParent(m.tr, true);
                    }
                    movers.Add(m);
                }
        }

        void BuildParallax(string key)
        {
            foreach (var (name, f, order) in new[] { ("bg_far", .85f, -100), ("bg_near", .6f, -90) })
            {
                var sp = SpriteBank.Get($"Themes/{key}/{name}");
                if (!sp) continue;
                var root = new GameObject("Parallax_" + name).transform; root.SetParent(world, false);
                var size = sp.bounds.size;
                for (int gx = 0; gx < 4; gx++)
                    for (int gy = 0; gy < 4; gy++)
                    {
                        var sr = new GameObject("p").AddComponent<SpriteRenderer>();
                        sr.sprite = sp; sr.sortingOrder = order; sr.transform.SetParent(root, false);
                        sr.transform.localPosition = new Vector3(gx * size.x, gy * size.y, 0);
                    }
                parallax.Add((root, f, size));
            }
        }

        Tilemap MakeTilemap(Transform parent, string n, int order)
        {
            var go = new GameObject(n, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<TilemapRenderer>().sortingOrder = order;
            return go.GetComponent<Tilemap>();
        }

        Tile GetTile(string path)
        {
            if (tileCache.TryGetValue(path, out var t) && t) return t;
            t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = SpriteBank.Get(path);
            t.colliderType = Tile.ColliderType.None;
            tileCache[path] = t;
            return t;
        }

        SpriteRenderer MakeSprite(string n, string path, Vector2 p, int order)
        {
            var go = new GameObject(n);
            go.transform.SetParent(world, false);
            go.transform.position = p;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteBank.Get(path); sr.sortingOrder = order;
            return sr;
        }

        // ------------------------------------------------------------------ oyun dongusu
        float acc;

        void Update()
        {
            int a = ReadAction();
            if (inScreen)
            {
                if (a == 8 || a == 9) screenNext = true;
                else if (a == 10) screenSkip = true;
                return;
            }
            if (L != null && cheats && HandleCheats()) return;
            if (waitOverlay)
            {
                if (a == 8 || a == 9) waitOverlay = false;
                return;
            }
            if (L == null) return;
            if (playing && !levelDone && a == 11) { StartCoroutine(RestartLevel()); return; }

            if (playing && !levelDone)
            {
                if (a == 9) jumpLatch = true;
                acc += Mathf.Min(Time.deltaTime, 1f / 20f);
                while (acc >= Step && playing && !levelDone) { acc -= Step; Tick(Step); }
            }
            else acc = 0;
            Animate(Time.deltaTime);
        }

        IEnumerator RestartLevel() { playing = false; yield return RunLevel(levelIndex); }

        void Tick(float dt)
        {
            foreach (var m in movers) MoveMover(m, dt);
            foreach (var e in enemies) MoveEnemy(e, dt);
            foreach (var s in springs) s.t = Mathf.Max(0, s.t - dt);

            if (levelDone) return;
            if (dead)
            {
                deadT -= dt;
                if (deadT <= 0) Respawn();
                return;
            }

            float ix = InputX();
            bool jumpPressed = jumpLatch, jumpHeld = JumpHeld();
            jumpLatch = false;
            dropping = DownHeld();
            if (ix != 0) facing = ix > 0 ? 1 : -1;

            // binilen platformla birlikte hareket
            if (riding != null)
            {
                MoveX(riding.delta.x);
                float top = riding.Top + HH;
                if (!HitsCeiling(pos.x, top)) pos.y = top; else riding = null;
            }

            float target = ix * runSpeed;
            float accel = ix != 0 ? (grounded ? groundAccel : airAccel) : (grounded ? friction : airAccel * .35f);
            vel.x = Mathf.MoveTowards(vel.x, target, accel * dt);

            coyote = grounded ? coyoteTime : coyote - dt;
            buffer = jumpPressed ? jumpBuffer : buffer - dt;
            if (buffer > 0 && coyote > 0)
            {
                vel.y = jumpSpeed; buffer = 0; coyote = 0; grounded = false; riding = null;
                Play(sfxJump); squash = -.25f;
            }

            vel.y -= gravity * dt * (vel.y > 0 && !jumpHeld ? 2.4f : 1f);
            vel.y = Mathf.Max(vel.y, -maxFall);

            MoveX(vel.x * dt);
            bool wasGrounded = grounded;
            MoveY(vel.y * dt);
            if (grounded && !wasGrounded) squash = .25f;

            // yaylar
            foreach (var s in springs)
                if (vel.y <= 0 && Mathf.Abs(pos.x - s.pos.x) < .6f && pos.y - HH < s.pos.y + .1f && pos.y - HH > s.pos.y - .55f)
                {
                    vel.y = springSpeed; grounded = false; riding = null; s.t = .25f; Play(sfxSpring); squash = -.35f;
                }

            CheckHazards();
            if (dead) return;
            CheckPickups();
            if (Mathf.Abs(pos.x - exitPos.x) < .6f && Mathf.Abs(pos.y - (exitPos.y)) < 1.1f) StartCoroutine(LevelComplete());
        }

        void MoveX(float dx)
        {
            if (dx == 0) return;
            float nx = pos.x + dx;
            int y0 = Mathf.FloorToInt(pos.y - HH + .01f), y1 = Mathf.FloorToInt(pos.y + HH - .01f);
            if (dx > 0)
            {
                int cx = Mathf.FloorToInt(nx + HW);
                for (int y = y0; y <= y1; y++) if (Solid(cx, y)) { nx = cx - HW - .001f; vel.x = 0; break; }
            }
            else
            {
                int cx = Mathf.FloorToInt(nx - HW);
                for (int y = y0; y <= y1; y++) if (Solid(cx, y)) { nx = cx + 1 + HW + .001f; vel.x = 0; break; }
            }
            pos.x = nx;
        }

        bool HitsCeiling(float x, float y)
        {
            int cy = Mathf.FloorToInt(y + HH - .01f);
            int x0 = Mathf.FloorToInt(x - HW + .01f), x1 = Mathf.FloorToInt(x + HW - .01f);
            for (int cx = x0; cx <= x1; cx++) if (Solid(cx, cy)) return true;
            return false;
        }

        void MoveY(float dy)
        {
            float oldBottom = pos.y - HH;
            float ny = pos.y + dy;
            int x0 = Mathf.FloorToInt(pos.x - HW + .01f), x1 = Mathf.FloorToInt(pos.x + HW - .01f);
            grounded = false;
            if (dy > 0)
            {
                riding = null;
                int cy = Mathf.FloorToInt(ny + HH);
                for (int cx = x0; cx <= x1; cx++) if (Solid(cx, cy)) { ny = cy - HH - .001f; vel.y = 0; break; }
                pos.y = ny;
                return;
            }
            float bottom = ny - HH;
            int by = Mathf.FloorToInt(bottom);
            float landY = float.NegativeInfinity;
            for (int cx = x0; cx <= x1; cx++)
            {
                if (Solid(cx, by)) landY = Mathf.Max(landY, by + 1);
                else if (!dropping && OneWay(cx, by) && oldBottom >= by + 1 - .02f) landY = Mathf.Max(landY, by + 1);
            }
            Mover onM = null;
            if (!dropping)
                foreach (var m in movers)
                {
                    if (Mathf.Abs(pos.x - m.pos.x) > m.hw + HW - .05f) continue;
                    float top = m.Top, prevTop = top - m.delta.y;
                    if (oldBottom >= Mathf.Min(top, prevTop) - .03f && bottom <= top && top > landY) { landY = top; onM = m; }
                }
            if (landY > float.NegativeInfinity && bottom <= landY)
            {
                if (!grounded && vel.y < -6f) Play(sfxLand, .35f);   // yuksekten inis
                ny = landY + HH; vel.y = 0; grounded = true; riding = onM;
            }
            else riding = null;
            pos.y = ny;
        }

        void MoveMover(Mover m, float dt)
        {
            var old = m.pos;
            if (m.len <= 0) { m.delta = Vector2.zero; return; }
            if (m.wait > 0) m.wait -= dt;
            else
            {
                m.p += m.dir * L.moverSpeed * dt;
                if (m.p >= m.len) { m.p = m.len; m.dir = -1; m.wait = .45f; }
                if (m.p <= 0) { m.p = 0; m.dir = 1; m.wait = .45f; }
            }
            m.pos = Vector2.Lerp(m.a, m.b, m.p / m.len);
            m.delta = m.pos - old;
            m.tr.position = m.pos;
        }

        void MoveEnemy(Enemy e, float dt)
        {
            if (e.dead) return;
            float sp = 1.8f;
            float nx = e.pos.x + e.dir * sp * dt;
            int front = Mathf.FloorToInt(nx + e.dir * .4f);
            int fy = Mathf.FloorToInt(e.pos.y - .6f);
            bool wall = Solid(front, Mathf.FloorToInt(e.pos.y));
            bool floorAhead = Solid(front, fy) || OneWay(front, fy);
            if (wall || !floorAhead) e.dir = -e.dir; else e.pos.x = nx;
            e.tr.position = e.pos;
        }

        void CheckHazards()
        {
            if (pos.y < -3) { Die(); return; }
            int x0 = Mathf.FloorToInt(pos.x - HW), x1 = Mathf.FloorToInt(pos.x + HW);
            int y0 = Mathf.FloorToInt(pos.y - HH), y1 = Mathf.FloorToInt(pos.y + HH);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    char c = At(x, y);
                    if (c != '^' && c != 'v' && c != '<' && c != '>') continue;
                    // dikenin sivri yarisi
                    float ax = x + .2f, bx = x + .8f, ay = y + .15f, by = y + .85f;
                    if (c == '^') by = y + .55f; else if (c == 'v') ay = y + .45f; else if (c == '<') bx = x + .55f; else ax = x + .45f;
                    if (pos.x + HW > ax && pos.x - HW < bx && pos.y + HH > ay && pos.y - HH < by) { Die(); return; }
                }
            foreach (var e in enemies)
            {
                if (e.dead) continue;
                if (Mathf.Abs(pos.x - e.pos.x) > HW + .36f || Mathf.Abs(pos.y - e.pos.y) > HH + .32f) continue;
                if (vel.y < 0 && pos.y - HH > e.pos.y - .05f)
                {
                    e.dead = true; e.sr.sprite = SpriteBank.Get("Yamali/eraser_flat"); vel.y = 11f; Play(sfxStomp);
                    StartCoroutine(FadeOut(e.sr, .6f));
                }
                else { Die(); return; }
            }
        }

        IEnumerator FadeOut(SpriteRenderer sr, float d)
        {
            float t = 0;
            while (t < d && sr) { t += Time.deltaTime; sr.color = new Color(1, 1, 1, 1 - t / d); yield return null; }
            if (sr) sr.enabled = false;
        }

        void CheckPickups()
        {
            foreach (var p in pickups)
            {
                if (p.taken || Vector2.Distance(pos, p.pos) > 1f) continue;
                p.taken = true; p.sr.enabled = false;
                yama.Add(LevelId); SaveYama();
                var a = MakeSprite("YamaAskeri", "Enemies/ugly_" + (army.Count % 4), p.pos, 4);
                a.transform.localScale = Vector3.one * .62f; army.Add(a);
                Play(sfxPickup);
                Flash(T("PICKUP", "Bir yama orduya katıldı!"), 1.6f);
                UpdateArmyHud();
            }
            foreach (var f in flags)
                if (!f.on && Mathf.Abs(pos.x - f.pos.x) < .7f && Mathf.Abs(pos.y - f.pos.y) < 1f)
                {
                    f.on = true; f.sr.sprite = SpriteBank.Get("Yamali/flag_up"); Play(sfxFlag);
                    spawn = new Vector2(f.pos.x, f.pos.y - .5f + HH + .001f);
                }
        }

        void Die()
        {
            if (dead) return;
            dead = true; deadT = .7f; vel = Vector2.zero; riding = null;
            Play(sfxDie);
        }

        void Respawn()
        {
            dead = false; pos = spawn; vel = Vector2.zero; grounded = false; riding = null;
            if (playerSr) { playerSr.enabled = true; playerSr.color = Color.white; }
        }

        // ------------------------------------------------------------------ gorunum
        void Animate(float dt)
        {
            if (!playerTr) return;
            animT += dt;
            squash = Mathf.MoveTowards(squash, 0, dt * 2.2f);
            playerTr.position = pos + new Vector2(0, .2f);   // 40px kare: ayaklar kutunun altina denk gelir
            if (dead)
            {
                float k = Mathf.Clamp01(deadT / .7f);
                playerTr.localScale = new Vector3(k, k, 1) * 1.1f;
                playerTr.rotation = Quaternion.Euler(0, 0, (1 - k) * 360f * facing);
                playerSr.color = (int)(animT * 20) % 2 == 0 ? Color.white : new Color(1, 1, 1, .3f);
            }
            else
            {
                playerTr.rotation = Quaternion.identity;
                playerTr.localScale = new Vector3(1 + squash * .6f, 1 - squash * .6f, 1);
                string frame;
                if (!grounded) frame = vel.y > 0 ? "yamali_jump" : "yamali_fall";
                else if (Mathf.Abs(vel.x) > .5f)
                {
                    frame = "yamali_run_" + ((int)(animT * 12) % 4);
                    stepT += dt; if (stepT > .3f) { stepT = 0; Play(sfxStep, .25f); }   // adim sesi
                }
                else { frame = "yamali_idle_" + ((int)(animT * 2.5f) % 2); stepT = .2f; }
                playerSr.sprite = SpriteBank.Get("Yamali/" + frame);
                playerSr.flipX = facing < 0;
            }

            // ordu: oyuncunun izinden yurur
            trailT += dt;
            if (trailT > .04f) { trailT = 0; trail.Insert(0, pos); if (trail.Count > 200) trail.RemoveAt(trail.Count - 1); }
            for (int k = 0; k < army.Count; k++)
            {
                Vector3 target; bool flip;
                if (riding != null && Mathf.Abs(vel.x) < .5f)
                {
                    // asansor / kayan platformda: oyuncunun yaninda, platformun ustunde dizilir
                    float x = pos.x - facing * (k + 1) * .62f;
                    x = Mathf.Clamp(x, riding.pos.x - riding.hw + .3f, riding.pos.x + riding.hw - .3f);
                    target = new Vector3(x, pos.y - .12f, 0); flip = facing < 0;
                    army[k].transform.position = Vector3.Lerp(army[k].transform.position, target, 1f - Mathf.Exp(-12f * dt));
                }
                else
                {
                    int idx = Mathf.Min(trail.Count - 1, 7 + k * 6);
                    if (idx < 0) break;
                    var tp = trail[idx];
                    target = new Vector3(tp.x, tp.y - .12f + Mathf.Abs(Mathf.Sin(animT * 8 + k)) * .08f, 0);
                    flip = (k + 1 < trail.Count && idx + 1 < trail.Count) && trail[idx].x < trail[idx + 1].x;
                    army[k].transform.position = target;
                }
                army[k].sprite = SpriteBank.Get("Enemies/ugly_" + ((int)(animT * 6 + k) % 4));
                army[k].flipX = flip;
            }

            foreach (var p in pickups)
            {
                if (!p.sr || !p.sr.enabled) continue;
                p.t += dt;
                p.tr.position = p.pos + new Vector2(0, Mathf.Sin(p.t * 3f) * .12f);
                if (!p.taken) p.sr.sprite = SpriteBank.Get("Enemies/ugly_" + ((int)(p.t * 5) % 4));
            }
            foreach (var e in enemies)
            {
                if (e.dead || !e.sr) continue;
                e.anim += dt;
                e.sr.sprite = SpriteBank.Get("Yamali/eraser_" + ((int)(e.anim * 6) % 2));
                e.sr.flipX = e.dir > 0;
            }
            foreach (var s in springs) s.sr.sprite = SpriteBank.Get(s.t > 0 ? "Yamali/spring_1" : "Yamali/spring_0");
            UpdateCamera(dt);
        }

        void SnapCamera() { camPos = CameraTarget(); ApplyCamera(); }
        Vector2 camPos;

        // HUD bantlari (1080'lik tuvalde): altta ipucu + tus kapaklari paneli, ustte bolum + seviye adi paneli.
        // Harita bu bantlarin ARASINA sigdirilir: zemin satiri alt panelin ustunde kalir.
        const float BottomBand = 140f / 1080f, TopBand = 150f / 1080f;

        Vector2 CameraTarget()
        {
            var t = pos + new Vector2(facing * 1.5f, 1.2f);
            float hh = cam.orthographicSize, hw = hh * cam.aspect;
            float rb = 2f * hh * BottomBand, rt = 2f * hh * TopBand;      // bantlarin dunya birimi yuksekligi
            t.x = L.W <= hw * 2 ? L.W * .5f : Mathf.Clamp(t.x, hw, L.W - hw);
            float usable = 2f * hh - rb - rt;                               // bantlar arasindaki gorunur yukseklik
            t.y = L.H <= usable ? L.H * .5f - (rb - rt) * .5f : Mathf.Clamp(t.y, hh - rb, L.H - hh + rt);
            return t;
        }

        void UpdateCamera(float dt)
        {
            if (!cam || L == null) return;
            camPos = Vector2.Lerp(camPos, CameraTarget(), 1f - Mathf.Exp(-5f * dt));
            ApplyCamera();
        }

        void ApplyCamera()
        {
            cam.transform.position = new Vector3(camPos.x, camPos.y, -10);
            foreach (var (tr, f, size) in parallax)
            {
                if (!tr) continue;
                float ox = Mathf.Repeat(camPos.x * (1 - f), size.x), oy = Mathf.Repeat(camPos.y * (1 - f), size.y);
                tr.position = new Vector3(camPos.x - ox - size.x, camPos.y - oy - size.y, 0);
            }
        }

        // ------------------------------------------------------------------ arayuz (Bolum 1 ile ayni dil: UIKit, blob stili)
        const string ChapterName = "Bölüm 2: Yamalı'nın İntikamı";

        void SetupUI()
        {
            var root = UIKit.MakeCanvas("UI").transform;
            // sol ust: bolum adi + seviye adi
            // yazilarin arkasina siyah paneller: beyaz duvar ustune dusseler de okunur
            var topPanel = UIKit.MakePanel(root, "BaslikZemin", Color.black);
            UIKit.Pin(topPanel.rectTransform, new Vector2(0, 1), new Vector2(UIKit.Margin - 24, -12), new Vector2(760, 132));
            UIKit.SetCorner(topPanel, 28);
            chapterText = UIKit.MakeText(root, "Bolum", UIKit.HudLight, 26, TextAnchor.LowerLeft);
            UIKit.TopBand(chapterText.rectTransform, UIKit.Margin, 700, 26, 34);
            chapterText.color = UIKit.Mute;
            chapterText.text = LevelManager.Upper(Loc.T(ChapterName)).Replace(":", " ·");
            levelText = UIKit.MakeText(root, "Baslik", UIKit.Hud, 54, TextAnchor.UpperLeft);
            UIKit.TopBand(levelText.rectTransform, UIKit.Margin, 700, 60, 70);
            // sag ust: yama ordusu halkalari (UpdateArmyHud doldurur)
            armyRow = UIKit.MakeRect(root, "OrduSatiri");
            UIKit.Pin(armyRow, new Vector2(1, 1), new Vector2(-UIKit.Margin, -44), new Vector2(640, 40));
            var row = armyRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleRight; row.spacing = 10;
            row.childControlWidth = row.childControlHeight = false; row.childForceExpandWidth = row.childForceExpandHeight = false;
            // orta mesaj: yari saydam gecisli beyaz konusma balonu, siyah yazi
            msgText = UIKit.MakeText(root, "Mesaj", UIKit.Body, 48, TextAnchor.MiddleCenter);
            UIKit.Stretch(msgText.rectTransform, .17f, .42f, .83f, .74f);
            UIKit.AddBubble(msgText);   // yaziya gore boyutlanir
            msgText.color = Color.black;
            // alt: seviye ipucu, altinda tus kapaklari
            var bottomPanel = UIKit.MakePanel(root, "IpucuZemin", Color.black);
            UIKit.Pin(bottomPanel.rectTransform, new Vector2(.5f, 0), new Vector2(0, 6), new Vector2(1500, 128));
            UIKit.SetCorner(bottomPanel, 32);
            hintText = UIKit.MakeText(root, "Ipucu", UIKit.Body, 30, TextAnchor.LowerCenter);
            UIKit.BottomBand(hintText.rectTransform, 80, 80, 68, 54);
            controlsText = UIKit.MakeText(root, "Kontroller", UIKit.BodyLight, 26, TextAnchor.MiddleCenter);
            UIKit.BottomBand(controlsText.rectTransform, 80, 80, 16, 46);
            UIKit.AddKeyHints(controlsText);
            controlsText.text = T("HINT", "[A][D] yürü    [Boşluk] zıpla    [S] platformdan in    [R] yeniden");
            // seviye girisi: siyah perde, baslik, hikaye, tus kapagi
            var ov = UIKit.MakeImage(root, "SeviyeGirisi", new Color(0, 0, 0, .9f));
            ov.raycastTarget = true; overlay = ov.gameObject;
            Stretch((RectTransform)overlay.transform, 0, 0, 1, 1);
            overlayTitle = UIKit.MakeText(overlay.transform, "Baslik", UIKit.Hud, 64, TextAnchor.LowerCenter);
            Stretch(overlayTitle.rectTransform, .1f, .56f, .9f, .75f);
            overlayBody = UIKit.MakeText(overlay.transform, "Hikaye", UIKit.Body, 36, TextAnchor.UpperCenter);
            Stretch(overlayBody.rectTransform, .15f, .3f, .85f, .54f);
            overlayPrompt = UIKit.MakeText(overlay.transform, "Tus", UIKit.BodyLight, 26, TextAnchor.MiddleCenter);
            Stretch(overlayPrompt.rectTransform, .1f, .18f, .9f, .26f);
            UIKit.AddKeyHints(overlayPrompt);
            overlay.SetActive(false);
            UpdateArmyHud();
        }

        /// Sag ust: "Yama ordusu" + 6 halka; toplanan her yama siyah govdeli, beyaz gozlu bir top olur.
        void UpdateArmyHud()
        {
            if (!armyRow || armyShown == yama.Count) return;
            armyShown = yama.Count;
            for (int i = armyRow.childCount - 1; i >= 0; i--) Destroy(armyRow.GetChild(i).gameObject);
            var label = UIKit.MakeText(armyRow, "Etiket", UIKit.HudLight, 24, TextAnchor.MiddleRight);
            label.color = UIKit.Mute; label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.text = Loc.T("Yama ordusu");
            label.rectTransform.sizeDelta = new Vector2(label.preferredWidth + 10, 32);
            for (int i = 0; i < 6; i++) UIKit.MakeDot(armyRow, i < yama.Count, 26, inverted: true);
        }

        void ShowOverlay(string title, string body, string prompt)
        {
            overlay.SetActive(true); overlay.transform.SetAsLastSibling();
            overlayTitle.text = title; overlayBody.text = (body ?? "").Replace("|", "\n"); overlayPrompt.text = prompt;
        }

        void HideOverlay() { overlay.SetActive(false); }

        static void Stretch(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void Flash(string m, float dur) { if (flashing != null) StopCoroutine(flashing); flashing = StartCoroutine(FlashRoutine(m.Replace("|", "\n"), dur)); }
        IEnumerator FlashRoutine(string m, float dur) { msgText.text = m; yield return new WaitForSeconds(dur); msgText.text = ""; flashing = null; }

        void Play(AudioClip c, float vol = 1f) { if (c && audioSrc) audioSrc.PlayOneShot(c, vol); }

        IEnumerator ShowScreen(string image, string body, string prompt)
        {
            EnsureScreen();
            screenRoot.SetActive(true); screenRoot.transform.SetAsLastSibling();
            screenPic.sprite = SpriteBank.Get(image); screenPic.enabled = screenPic.sprite != null;
            screenText.text = body.Replace("|", "\n"); screenPrompt.text = prompt;
            inScreen = true; screenNext = false; screenSkip = false;
            yield return null;
            while (!screenNext && !screenSkip) yield return null;
            inScreen = false;
        }

        void HideScreen() { if (screenRoot) screenRoot.SetActive(false); }

        void EnsureScreen()
        {
            if (screenRoot) return;
            screenRoot = UIKit.MakeStoryScreen(msgText.canvas.transform, out screenPic, out screenText, out screenPrompt);
        }

        // ------------------------------------------------------------------ hileler
        bool HandleCheats()
        {
            int c = ReadCheat();
            if (c == 0) return false;
            StopAllCoroutines(); flashing = null; if (msgText) msgText.text = "";
            HideOverlay(); HideScreen(); inScreen = false; waitOverlay = false;
            if (c == -1) StartCoroutine(RunLevel(Mathf.Min(levelCount, levelIndex + 1)));
            else if (c == -2) StartCoroutine(RunLevel(Mathf.Max(1, levelIndex - 1)));
            else if (c == -3) StartCoroutine(LevelComplete());
            else if (c <= levelCount) StartCoroutine(RunLevel(c));
            return true;
        }

        /// 0 yok, -1 sonraki, -2 onceki, -3 gec, 1-9 seviye
        static int ReadCheat()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return 0;
            if (k.nKey.wasPressedThisFrame) return -1;
            if (k.bKey.wasPressedThisFrame) return -2;
            if (k.gKey.wasPressedThisFrame) return -3;
            Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
            for (int i = 0; i < digits.Length; i++) if (k[digits[i]].wasPressedThisFrame) return i + 1;
#else
            if (Input.GetKeyDown(KeyCode.N)) return -1;
            if (Input.GetKeyDown(KeyCode.B)) return -2;
            if (Input.GetKeyDown(KeyCode.G)) return -3;
            for (int i = 0; i < 9; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) return i + 1;
#endif
            return 0;
        }

        // ------------------------------------------------------------------ girdi
        /// -1 yok, 8 enter, 9 bosluk, 10 esc, 11 R
        static int ReadAction()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return -1;
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) return 8;
            if (k.spaceKey.wasPressedThisFrame) return 9;
            if (k.escapeKey.wasPressedThisFrame) return 10;
            if (k.rKey.wasPressedThisFrame) return 11;
#else
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) return 8;
            if (Input.GetKeyDown(KeyCode.Space)) return 9;
            if (Input.GetKeyDown(KeyCode.Escape)) return 10;
            if (Input.GetKeyDown(KeyCode.R)) return 11;
#endif
            return -1;
        }

        // Ziplama basisi Update'te yakalanir, ilk fizik adiminda tuketilir (adim olmayan karelerde kaybolmasin).
        bool jumpLatch;

        static bool JumpHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current; return k != null && k.spaceKey.isPressed;
#else
            return Input.GetKey(KeyCode.Space);
#endif
        }

        static bool DownHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current; return k != null && (k.sKey.isPressed || k.downArrowKey.isPressed);
#else
            return Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
#endif
        }

        static float InputX()
        {
            float x = 0;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return 0;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1;
#else
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1;
#endif
            return x;
        }
    }
}
