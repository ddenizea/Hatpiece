using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KayipParca
{
    /// <summary>
    /// Oyunun tum kurallari burada (tur bazli, izgara hareketi).
    /// Kurallar Tools/solver.py ile birebir aynidir; seviyeler o cozucuyle test edildi.
    ///  - WASD / Ok tuslari: bir kare ilerle (duvara carpmak hamle harcamaz)
    ///  - E: saat yonunde dondur, Q: saat yonunun tersine dondur (1 hamle)
    ///  - Sekil istasyonuna (S) girerken one bakan kenarin tersine doner (cikinti <-> girinti)
    ///  - Yuvaya (G/E) yalnizca sekil birebir uyuyorsa girilir
    ///  - Ayna klon (C) yatayda ters hareket eder, ters doner; ikisi de yuvadaysa kazanilir
    ///  - Her hamleden sonra dusmanlar bir kare devriye atar
    ///  - Z: geri al, R: seviyeyi yeniden baslat
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

        [Header("Sesler (sonradan ekleyin, bos kalabilir)")]
        public AudioClip sfxMove;
        public AudioClip sfxRotate, sfxStation, sfxBump, sfxDeath, sfxFit, sfxWin;

        [Header("Ayarlar")]
        [Range(1, 6)] public int startLevel = 1;
        public int levelCount = 6;
        [Tooltip("Kamera oyuncuya dogru ne kadar kaysin (parallax icin)")]
        public float cameraDrift = 0.12f;

        public static readonly string[] ThemeKeys = { "T0_Kutu", "T1_Hali", "T2_Mutfak", "T3_Oyuncak", "T4_Gece", "T5_Kedi" };
        static readonly string[] Accents = { "d94a3a", "f0c27a", "3b82c4", "f94144", "ffe8a3", "ff5a36" };

        struct State { public Vector2Int p, c; public string ps, cs; public int t, moves; }

        LevelData data;
        State s;
        readonly Stack<State> history = new Stack<State>();
        PieceView player, clone;
        readonly List<EnemyView> enemyViews = new List<EnemyView>();
        readonly Dictionary<string, Tile> tileCache = new Dictionary<string, Tile>();
        int level;
        bool busy, ended, firstLoad = true;
        Vector3 levelCenter;
        Camera cam;
        Image hudNow, hudGoal, hudGoal2;

        // ------------------------------------------------------------------ yasam dongusu
        void Start()
        {
            cam = Camera.main;
            StartCoroutine(LoadLevel(startLevel));
        }

        void Update()
        {
            int a = ReadAction();
            if (a < 0) return;
            if (ended) { if (a == 8) { ended = false; StartCoroutine(LoadLevel(1)); } return; }
            if (busy) return;
            if (a <= 5) Step(a);
            else if (a == 6) Undo();
            else if (a == 7) { Play(sfxBump); ResetState(); }
#if UNITY_EDITOR
            else if (a == 9) StartCoroutine(LoadLevel(level % levelCount + 1));
#endif
        }

        void LateUpdate()
        {
            if (!cam || !player) return;
            var target = levelCenter + (player.transform.position - levelCenter) * cameraDrift;
            target.z = -10;
            cam.transform.position = Vector3.Lerp(cam.transform.position, target, 1f - Mathf.Exp(-3f * Time.deltaTime));
            if (themes) themes.FollowPlayer(player.transform.position);
        }

        // ------------------------------------------------------------------ seviye yukleme
        IEnumerator LoadLevel(int n)
        {
            busy = true;
            if (!firstLoad && themes) yield return themes.FadeOut();
            firstLoad = false;
            Build(n);
            ShowMessage($"{level}. {data.name}");
            if (themes) yield return themes.FadeIn(data.theme);
            busy = false;
            yield return new WaitForSeconds(0.8f);
            if (!busy && !ended) ShowMessage("");
        }

        void Build(int n)
        {
            level = n;
            var ta = Resources.Load<TextAsset>($"KayipParca/Levels/level{n}");
            data = LevelData.Parse(ta.text);
            string key = ThemeKeys[data.theme];
            ColorUtility.TryParseHtmlString("#" + Accents[data.theme], out var accent);

            floorMap.ClearAllTiles(); wallMap.ClearAllTiles(); decorMap.ClearAllTiles();
            for (int i = objectsRoot.childCount - 1; i >= 0; i--) Destroy(objectsRoot.GetChild(i).gameObject);
            enemyViews.Clear();

            for (int y = 0; y < data.height; y++)
                for (int x = 0; x < data.width; x++)
                {
                    char c = data.grid[x, y];
                    var cell = new Vector3Int(x, -y, 0);
                    if (c == '#') { wallMap.SetTile(cell, GetTile(key, "wall")); continue; }
                    floorMap.SetTile(cell, GetTile(key, c == ',' ? "floor_alt" : "floor"));
                    if (c == 'o') decorMap.SetTile(cell, GetTile(key, "obstacle"));
                    if (c == 'X') decorMap.SetTile(cell, GetTile(key, "hazard"));
                    if (c == 'S') MakeSprite("Istasyon", "Common/station", World(new Vector2Int(x, y)), accent, 2);
                    if (data.goals.TryGetValue(c, out var req))
                    {
                        var col = c == 'E' ? new Color(.49f, .95f, .61f) : c == 'g' ? new Color(.56f, .83f, 1f) : Color.Lerp(accent, Color.white, .25f);
                        MakeSprite("Yuva_" + c, $"Slots/slot_{req}", World(new Vector2Int(x, y)), col, 2);
                    }
                }

            // dusmanlar + rota noktalari (oyuncu nereye gidecegini gorsun)
            string enemyPrefix = data.theme == 5 ? "paw" : "ugly";
            var dotCol = accent; dotCol.a = .55f;
            foreach (var e in data.enemies)
            {
                int len = Mathf.Abs(e.b.x - e.a.x) + Mathf.Abs(e.b.y - e.a.y);
                for (int k = 0; k <= len * 2; k++)
                {
                    var p = LevelData.EnemyPos(e, k);
                    MakeSprite("Rota", "Common/path_dot", World(p), dotCol, 1);
                    if (k >= len) break;
                }
                enemyViews.Add(EnemyView.Create(objectsRoot, enemyPrefix, World(e.a), 5));
            }

            player = PieceView.Create(objectsRoot, data.shape, true, Color.white, 6);
            clone = data.hasClone ? PieceView.Create(objectsRoot, Shape.Mirror(data.shape), false, new Color(.56f, .83f, 1f, .85f), 6) : null;

            SetupCameraAndBackground(key);
            ResetState();
            if (titleText) titleText.text = $"{level}. {data.name}";
            if (hintText)
            {
                hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
                hintText.verticalOverflow = VerticalWrapMode.Overflow;
                hintText.text = (string.IsNullOrEmpty(data.hint) ? "" : data.hint + "\n") +
                                "<size=22>WASD / Oklar: hareket    Q / E: döndür    Z: geri al    R: yeniden</size>";
            }
            SetupShapeHud(accent);
        }

        void SetupCameraAndBackground(string key)
        {
            levelCenter = new Vector3(data.width / 2f, -data.height / 2f + 1f, 0);
            float margin = data.theme == 5 ? 2.2f : 1.4f;
            cam.orthographicSize = Mathf.Max(data.height / 2f + margin, (data.width / 2f + margin) / cam.aspect);
            cam.transform.position = new Vector3(levelCenter.x, levelCenter.y, -10);

            float viewH = cam.orthographicSize * 2f, viewW = viewH * cam.aspect;
            foreach (var (sr, file, factor) in new[] { (bgFar, "bg_far", .9f), (bgNear, "bg_near", .75f) })
            {
                if (!sr) continue;
                sr.sprite = SpriteBank.Get($"Themes/{key}/{file}");
                if (!sr.sprite) continue;
                var size = sr.sprite.bounds.size;
                float sc = Mathf.Max(viewW * 1.3f / size.x, viewH * 1.3f / size.y);
                sr.transform.localScale = new Vector3(sc, sc, 1);
                var origin = levelCenter;
                // kedinin kafasi yatagin arkasindan baksin
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
                t = 0, moves = data.moves
            };
            ApplyVisuals(snap: true);
            player.SetFace(0);
            if (clone) clone.SetFace(0);
        }

        // ------------------------------------------------------------------ kurallar
        bool IsPlayerGoal(char c) => c != 'g' && data.goals.ContainsKey(c);

        bool TryMove(Vector2Int pos, string shape, int dir, bool isClone, out Vector2Int np, out string ns, out bool dead)
        {
            np = pos + Shape.Dirs[dir]; ns = shape; dead = false;
            if (np.x < 0 || np.y < 0 || np.x >= data.width || np.y >= data.height) return false;
            char c = data.At(np);
            if (c == '#' || c == 'o') return false;
            bool own = isClone ? c == 'g' : IsPlayerGoal(c);
            if (own && data.goals[c] != shape) return false;
            if (c == 'S') { ns = Shape.Toggle(shape, dir); Play(sfxStation); }
            dead = c == 'X';
            return true;
        }

        void Step(int a)
        {
            var n = s;
            bool dead = false;
            if (a < 4)
            {
                if (!TryMove(s.p, s.ps, a, false, out var np, out var nps, out var pd))
                {
                    player.Bump(Shape.Dirs[a]); Play(sfxBump); return;   // carpmak hamle harcamaz
                }
                n.p = np; n.ps = nps; dead |= pd;
                if (data.hasClone)
                {
                    int ma = Shape.MirrorDir[a];
                    if (TryMove(s.c, s.cs, ma, true, out var nc, out var ncs, out var cd)) { n.c = nc; n.cs = ncs; dead |= cd; }
                    else clone.Bump(Shape.Dirs[ma]);
                }
                Play(sfxMove);
            }
            else
            {
                bool cw = a == 4;
                n.ps = cw ? Shape.RotCW(s.ps) : Shape.RotCCW(s.ps);
                if (data.hasClone) n.cs = cw ? Shape.RotCCW(s.cs) : Shape.RotCW(s.cs);
                player.Squash(); if (clone) clone.Squash();
                Play(sfxRotate);
            }
            n.t = s.t + 1;
            n.moves = s.moves - 1;

            foreach (var e in data.enemies)
            {
                var e0 = LevelData.EnemyPos(e, s.t); var e1 = LevelData.EnemyPos(e, n.t);
                if (e1 == n.p || (e1 == s.p && e0 == n.p)) dead = true;
                if (data.hasClone && (e1 == n.c || (e1 == s.c && e0 == n.c))) dead = true;
            }

            history.Push(s);
            s = n;
            ApplyVisuals(snap: false);

            if (dead) { StartCoroutine(Die("Ah! Tekrar dene.")); return; }

            char pc = data.At(s.p);
            bool win = IsPlayerGoal(pc) && data.goals[pc] == s.ps &&
                       (!data.hasClone || (data.At(s.c) == 'g' && s.cs == data.goals['g']));
            if (win) { StartCoroutine(Win(pc)); return; }
            if (s.moves <= 0) StartCoroutine(Die("Hamlen bitti."));
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
            }
            if (movesText) movesText.text = $"Hamle: {s.moves}";
            if (hudNow) hudNow.sprite = SpriteBank.Get($"Player/body_{s.ps}");
        }

        IEnumerator Die(string msg)
        {
            busy = true;
            player.SetFace(2); if (clone) clone.SetFace(2);
            Play(sfxDeath);
            ShowMessage(msg);
            yield return new WaitForSeconds(0.9f);
            ShowMessage("");
            ResetState();
            busy = false;
        }

        IEnumerator Win(char goal)
        {
            busy = true;
            player.SetFace(1); if (clone) clone.SetFace(1);
            Play(sfxFit);
            yield return new WaitForSeconds(0.35f);
            Play(sfxWin);
            if (level >= levelCount)
            {
                ended = true;
                ShowMessage(goal == 'E'
                    ? "SON 2: KENDİ KUTUNA DÖNDÜN\nEksik olan sen değildin. Sadece yanlış yerdeydin.\n\n[Enter] baştan başla"
                    : "SON 1: YERLEŞTİN\nResim tamamlandı... ama bu resim hiç senin değildi.\n\n[Enter] baştan başla");
                yield break;
            }
            ShowMessage("Tık!");
            yield return new WaitForSeconds(0.6f);
            yield return LoadLevel(level + 1);
        }

        // ------------------------------------------------------------------ sekil gostergesi (Sen / Hedef)
        void SetupShapeHud(Color accent)
        {
            if (!movesText) return;
            if (!hudNow)
            {
                var parent = movesText.canvas.transform;
                hudNow = MakeIcon(parent, "Sen", new Vector2(-230, -110));
                hudGoal = MakeIcon(parent, "Hedef", new Vector2(-70, -110));
                hudGoal2 = MakeIcon(parent, "Son 2", new Vector2(-70, -270));
            }
            hudNow.sprite = SpriteBank.Get($"Player/body_{data.shape}");
            char g1 = data.goals.ContainsKey('G') ? 'G' : '\0';
            hudGoal.gameObject.SetActive(g1 != '\0');
            if (g1 != '\0')
            {
                hudGoal.sprite = SpriteBank.Get($"Player/body_{data.goals['G']}");
                hudGoal.color = Color.Lerp(accent, Color.white, .25f);
                hudGoal.GetComponentInChildren<Text>().text = data.goals.ContainsKey('E') ? "Son 1" : "Hedef";
            }
            bool two = data.goals.ContainsKey('E');
            hudGoal2.gameObject.SetActive(two);
            if (two)
            {
                hudGoal2.sprite = SpriteBank.Get($"Player/body_{data.goals['E']}");
                hudGoal2.color = new Color(.49f, .95f, .61f);
            }
        }

        Image MakeIcon(Transform parent, string label, Vector2 pos)
        {
            var go = new GameObject("Ikon_" + label, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(110, 110);
            var img = go.GetComponent<Image>();
            img.preserveAspect = true;
            var t = new GameObject("Etiket", typeof(RectTransform), typeof(Text));
            t.transform.SetParent(go.transform, false);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = new Vector2(0, 0); trt.anchorMax = new Vector2(1, 0);
            trt.pivot = new Vector2(.5f, 1);
            trt.anchoredPosition = Vector2.zero;
            trt.sizeDelta = new Vector2(0, 34);
            var tx = t.GetComponent<Text>();
            tx.font = movesText.font; tx.fontSize = 24; tx.fontStyle = FontStyle.Bold;
            tx.alignment = TextAnchor.UpperCenter; tx.color = Color.white;
            tx.horizontalOverflow = HorizontalWrapMode.Overflow;
            tx.text = label;
            return img;
        }

        // ------------------------------------------------------------------ yardimcilar
        static Vector3 World(Vector2Int p) => new Vector3(p.x + .5f, -p.y + .5f, 0);

        Tile GetTile(string themeKey, string name)
        {
            string path = $"Themes/{themeKey}/{name}";
            if (tileCache.TryGetValue(path, out var t)) return t;
            t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = SpriteBank.Get(path);
            tileCache[path] = t;
            return t;
        }

        void MakeSprite(string n, string spritePath, Vector3 pos, Color col, int order)
        {
            var go = new GameObject(n);
            go.transform.SetParent(objectsRoot, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteBank.Get(spritePath);
            sr.color = col;
            sr.sortingOrder = order;
        }

        void ShowMessage(string m) { if (messageText) messageText.text = m; }
        void Play(AudioClip c) { if (sfx && c) sfx.PlayOneShot(c); }

        /// -1 yok, 0-3 yon (N E S W), 4 saat yonu, 5 ters, 6 geri al, 7 yeniden, 8 onay, 9 seviye atla (editor)
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
            if (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame) return 8;
            if (k.nKey.wasPressedThisFrame) return 9;
#else
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) return 0;
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) return 1;
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) return 2;
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) return 3;
            if (Input.GetKeyDown(KeyCode.E)) return 4;
            if (Input.GetKeyDown(KeyCode.Q)) return 5;
            if (Input.GetKeyDown(KeyCode.Z)) return 6;
            if (Input.GetKeyDown(KeyCode.R)) return 7;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) return 8;
            if (Input.GetKeyDown(KeyCode.N)) return 9;
#endif
            return -1;
        }
    }
}
