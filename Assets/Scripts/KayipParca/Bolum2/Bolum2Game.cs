using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KayipParca
{
    /// <summary>
    /// Bolum 2: Yama Korsanlari. Gercek zamanli, 3 seritli (2.5D) savas.
    ///  - W / S (veya oklar): serit degistir.  Bosluk: ates topu (basili tutulabilir).
    ///  - Yama virusu: sana degen ve yakindan patlattigin yamalar cubugu doldurur. Cubuk dolduğunca guclenirsin:
    ///      %25 hizli ates, %50 mor ve delici ates, %75 gulleleri vurabilme + cift hasar.
    ///  - 3 dalga, sonra boss: Korsan Gemisi (ard arda gulle -> toplari sustur -> Yama Kaptan).
    /// Sahne, Kayıp Parça > 5) Bölüm 2 Sahnesini Kur menusuyle olusturulur. Her sey calisma aninda kurulur.
    /// Metinler: Resources/KayipParca/Story/bolum2.txt
    /// </summary>
    public class Bolum2Game : MonoBehaviour
    {
        [Header("Sesler (bos kalabilir)")]
        public AudioClip music;
        public AudioClip sfxFire, sfxHit, sfxBoom, sfxCannon, sfxLane, sfxCaptainHurt;

        [Header("Ayarlar")]
        [Tooltip("Test icin: 0 = giris hikayesinden basla, 1-3 = o dalgadan, 4 = dogrudan boss")]
        [Range(0, 4)] public int startAt = 0;
        public int maxHearts = 3;

        // ------------------------------------------------------------------ sabitler
        static readonly float[] LaneY = { 2.6f, 0f, -2.6f };
        static readonly float[] LaneScale = { .85f, 1f, 1.15f };
        static readonly int[] LaneOrder = { 10, 20, 30 };
        static readonly Color[] LaneTint = { new Color(.82f, .86f, .95f), Color.white, Color.white };
        const float DragonX = -7f, MouthX = -5.1f, SpawnX = 11.5f;
        static readonly Color FireOrange = new Color(1f, .75f, .35f), FirePurple = new Color(.75f, .45f, 1f);

        class Unit
        {
            public string kind; public GameObject go; public SpriteRenderer sr;
            public int lane; public float x, speed, t; public int hp;
            public float y; public int toLane = -1; public float switchAt, shootAt; public bool fired, warned;
            public SpriteRenderer warn;
        }
        class Shot { public GameObject go; public SpriteRenderer sr; public int lane; public float x; public readonly HashSet<Unit> hit = new HashSet<Unit>(); }
        struct Spawn { public float t; public string kind; public int lane; }

        // ------------------------------------------------------------------ durum
        Camera cam;
        Transform world;
        SpriteRenderer dragon;
        readonly List<SpriteRenderer> patches = new List<SpriteRenderer>();
        readonly List<Unit> units = new List<Unit>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<(Transform tr, float speed, float width)> parallax = new List<(Transform, float, float)>();
        readonly Dictionary<string, string> text = new Dictionary<string, string>();
        AudioSource audioSrc, musicSrc;

        int lane = 1, hearts;
        float dragonY, invul, fireCd, infection, waveStartInfection;
        bool playing, inScreen, screenNext, screenSkip, bossActive;
        int lastLine;   // son gosterilen virus esigi (25/50/75)

        // boss
        GameObject ship; float shipX = 16f;
        readonly SpriteRenderer[] cannons = new SpriteRenderer[3];
        readonly int[] cannonHp = { 3, 3, 3 };
        readonly bool[] cannonOpen = new bool[3];
        SpriteRenderer captain; int captainLane = 1, captainHp; const int CaptainMax = 10;
        int bossPhase;   // 1 zirhli toplar, 2 savunmasiz toplar, 3 kaptan

        // arayuz
        Text msgText, hintText, infText, bossText;
        RectTransform infFill, bossFill; GameObject bossBar;
        readonly List<Image> heartIcons = new List<Image>();
        GameObject screenRoot; Image screenPic; Text screenText, screenPrompt;
        Coroutine flashing;

        // ------------------------------------------------------------------ kurulum
        void Start()
        {
#if UNITY_EDITOR
            int test = UnityEditor.SessionState.GetInt("Bolum2TestStartAt", -1);   // Kayıp Parça > 6/7/8 test menuleri
            if (test >= 0) { startAt = test; UnityEditor.SessionState.EraseInt("Bolum2TestStartAt"); }
#endif
            LoadText();
            SetupCamera();
            SetupWorld();
            SetupUI();
            audioSrc = gameObject.AddComponent<AudioSource>();
            musicSrc = gameObject.AddComponent<AudioSource>(); musicSrc.loop = true; musicSrc.volume = .55f;
            if (music) { musicSrc.clip = music; musicSrc.Play(); }
            hearts = maxHearts;
            StartCoroutine(Flow());
        }

        void LoadText()
        {
            var ta = Resources.Load<TextAsset>("KayipParca/Story/bolum2");
            if (ta == null) return;
            foreach (var raw in ta.text.Replace("\r", "").Split('\n'))
            {
                int eq = raw.IndexOf('=');
                if (eq > 0 && !raw.StartsWith("#")) text[raw.Substring(0, eq).Trim()] = raw.Substring(eq + 1).Trim();
            }
        }

        string T(string key, string fallback) => text.TryGetValue(key, out var v) ? v : fallback;

        void SetupCamera()
        {
            cam = Camera.main;
            if (!cam)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>(); go.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(5.625f, 10.4f / Mathf.Max(.5f, cam.aspect));   // 20+ birim genislik her oranda gorunsun
            cam.transform.position = new Vector3(0, 0, -10);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.56f, .83f, .96f);
            if (FindAnyObjectByType<Light2D>() == null)
            {
                var l = new GameObject("Global Light 2D").AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Global; l.intensity = 1f;
            }
        }

        void SetupWorld()
        {
            world = new GameObject("Dunya").transform;
            float viewH = cam.orthographicSize * 2f, viewW = viewH * cam.aspect;
            float sc = Mathf.Max(viewW / 20f, viewH / 11.25f);
            foreach (var (file, speed, order) in new[] { ("B2/bg_sky", 0f, -50), ("B2/bg_far", .5f, -40), ("B2/bg_near", 2.2f, -5) })
            {
                for (int i = 0; i < 2; i++)
                {
                    var sr = Sprite(file, new Vector3(i * 20f * sc, 0, 0), order);
                    sr.transform.localScale = new Vector3(sc, sc, 1);
                    parallax.Add((sr.transform, speed, 20f * sc));
                }
            }
            dragon = Sprite("B2/dragon_0", new Vector3(DragonX, LaneY[1], 0), LaneOrder[1] + 5);
            dragonY = LaneY[1];
            var patchPos = new[] { new Vector2(-.6f, .1f), new Vector2(.4f, .4f), new Vector2(-1.1f, -.4f), new Vector2(.9f, -.1f) };
            foreach (var p in patchPos)
            {
                var ps = Sprite("B2/patch", Vector3.zero, LaneOrder[1] + 6);
                ps.transform.SetParent(dragon.transform, false);
                ps.transform.localPosition = p; ps.transform.localScale = Vector3.one * .7f;
                ps.enabled = false; patches.Add(ps);
            }
        }

        SpriteRenderer Sprite(string path, Vector3 pos, int order, Color? col = null)
        {
            var go = new GameObject(path.Substring(path.LastIndexOf('/') + 1));
            go.transform.SetParent(world, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteBank.Get(path);
            sr.sortingOrder = order;
            sr.color = col ?? Color.white;
            return sr;
        }

        // ------------------------------------------------------------------ akis
        IEnumerator Flow()
        {
            if (startAt == 0)
            {
                yield return ShowScreen("Story/b2_intro_1", T("INTRO1", "Korsan Gemisi kutusu sarsıldı. Yama Korsanları intikam için dışarı taştı!"), "[Enter] devam    [Esc] geç");
                if (!screenSkip) yield return ShowScreen("Story/b2_intro_2", T("INTRO2", "Ejderha uyandı. Binicisi sensin: artık onu sen sürüyorsun. Havalan!"), "[Enter] Başla");
                HideScreen();
            }
            for (int w = Mathf.Max(1, startAt); w <= 3; w++)
            {
                if (startAt == 4) break;
                yield return RunWave(w);
            }
            yield return RunBoss();
            yield return Finale();
        }

        IEnumerator RunWave(int w)
        {
            waveStartInfection = infection;
            while (true)
            {
                Flash(T("W" + w, w == 1 ? "Dalga 1: Yama Askerleri" : w == 2 ? "Dalga 2: Yama Balonları" : "Dalga 3: Yama Filosu"), 2.2f);
                yield return new WaitForSeconds(1.2f);
                var plan = BuildWave(w);
                int next = 0; float clock = 0; playing = true;
                bool lost = false;
                while (next < plan.Count || EnemiesAlive())
                {
                    clock += Time.deltaTime;
                    while (next < plan.Count && plan[next].t <= clock) { SpawnEnemy(plan[next].kind, plan[next].lane); next++; }
                    if (hearts <= 0) { lost = true; break; }
                    yield return null;
                }
                playing = false;
                if (!lost) { Flash(T("CLEAR", "Dalga temizlendi!"), 1.6f); yield return new WaitForSeconds(1.8f); yield break; }
                yield return Defeat();
                infection = waveStartInfection; ApplyInfection(false);
            }
        }

        bool EnemiesAlive() { foreach (var u in units) if (u.kind != "boom" && u.kind != "spore") return true; return false; }

        List<Spawn> BuildWave(int w)
        {
            var rng = new System.Random(40 + w);
            var list = new List<Spawn>();
            float t = 0;
            int count = w == 1 ? 16 : w == 2 ? 20 : 22;
            for (int i = 0; i < count; i++)
            {
                float gap = Mathf.Lerp(1.7f, 1.05f, i / (float)count);
                t += gap;
                string kind = "soldier";
                if (w >= 2 && i % 3 == 1) kind = "balloon";
                if (w == 3 && i % 4 == 2) kind = "ship";
                int ln = rng.Next(3);
                list.Add(new Spawn { t = t, kind = kind, lane = ln });
                if (w >= 2 && i > count / 2 && i % 5 == 0) list.Add(new Spawn { t = t, kind = "soldier", lane = (ln + 1 + rng.Next(2)) % 3 });
            }
            return list;
        }

        IEnumerator Defeat()
        {
            Flash(T("DEFEAT", "Yenildin! Tekrar dene."), 2f);
            ClearUnits();
            yield return new WaitForSeconds(2f);
            hearts = maxHearts; invul = 1.5f; UpdateHud();
        }

        // ------------------------------------------------------------------ boss
        IEnumerator RunBoss()
        {
            bossActive = true;
            Flash(T("BOSS", "Yama Kaptan: \"Yamalı'yı benden aldın. Şimdi sıra sende!\""), 3.2f);
            ship = new GameObject("KorsanGemisi"); ship.transform.SetParent(world, false);
            var hull = Sprite("B2/boss_ship", Vector3.zero, 8); hull.transform.SetParent(ship.transform, false);
            for (int i = 0; i < 3; i++)
            {
                cannons[i] = Sprite("B2/cannon_closed", new Vector3(-4.3f, LaneY[i], 0), LaneOrder[i] + 1);
                cannons[i].transform.SetParent(ship.transform, false); cannons[i].transform.localPosition = new Vector3(-4.3f, LaneY[i], 0);
                cannonHp[i] = 3; cannonOpen[i] = false;
            }
            captain = Sprite("B2/captain_idle", Vector3.zero, 40); captain.transform.SetParent(ship.transform, false);
            captain.transform.localPosition = new Vector3(-1.2f, 4.3f, 0);
            for (float t = 0; t < 3f; t += Time.deltaTime) { shipX = Mathf.Lerp(16f, 6.2f, Mathf.SmoothStep(0, 1, t / 3f)); ship.transform.position = new Vector3(shipX, 0, 0); yield return null; }
            shipX = 6.2f; ship.transform.position = new Vector3(shipX, 0, 0);
            bossBar.SetActive(true);
            playing = true;

            // asama 1-2: ard arda gulle; 2. asamada toplar ates etmeden hemen once vurulabilir
            int[][] patterns = { new[] { 0 }, new[] { 1 }, new[] { 2 }, new[] { 2 }, new[] { 1 }, new[] { 0 }, new[] { 0, 1 }, new[] { 1, 2 }, new[] { 0, 2 }, new[] { 1 } };
            float[] gaps = { .55f, .55f, 1.1f, .55f, .55f, 1.1f, 1.3f, 1.3f, 1.3f, 1.4f };
            bossPhase = 1;
            float phaseClock = 0;
            while (true)
            {
                for (int k = 0; k < patterns.Length; k++)
                {
                    if (bossPhase == 1 && phaseClock > 16f) { bossPhase = 2; Flash(T("P2", "Topların kapakları açılıyor! Ateş etmeden hemen önce namluyu vur!"), 3f); }
                    foreach (int ln in patterns[k]) if (cannonHp[ln] > 0) StartCoroutine(FireCannon(ln));
                    float g = gaps[k] + .75f;
                    for (float t = 0; t < g; t += Time.deltaTime)
                    {
                        phaseClock += Time.deltaTime;
                        if (hearts <= 0) yield return BossDefeat();
                        if (bossPhase == 2 && AllCannonsDown()) break;
                        yield return null;
                    }
                    if (bossPhase == 2 && AllCannonsDown()) break;
                }
                if (bossPhase == 2 && AllCannonsDown()) break;
            }

            // asama 3: kaptan; guvenli serit hep kaptanin durdugu serit
            bossPhase = 3; captainHp = CaptainMax;
            Flash(T("P3", "Kaptan kendisi geliyor! Zayıf noktası göz yaması. Durduğu şerit güvende."), 3f);
            yield return MoveCaptain(1);
            while (captainHp > 0)
            {
                var targets = new List<int>();
                for (int i = 0; i < 3; i++) if (i != captainLane) targets.Add(i);
                var warns = new List<SpriteRenderer>();
                foreach (int ln in targets) warns.Add(LaneWarn(ln, shipX - 4.8f));
                for (float t = 0; t < .85f && captainHp > 0; t += Time.deltaTime) { Blink(warns, t); yield return null; }
                foreach (var w in warns) Destroy(w.gameObject);
                if (captainHp <= 0) break;
                captain.sprite = SpriteBank.Get("B2/captain_throw");
                foreach (int ln in targets) SpawnBall(ln, shipX - 4.2f, 8f);
                Play(sfxCannon);
                for (float t = 0; t < .8f && captainHp > 0; t += Time.deltaTime) { if (hearts <= 0) yield return BossDefeat(); yield return null; }
                if (captainHp <= 0) break;
                captain.sprite = SpriteBank.Get("B2/captain_idle");
                int nl; do nl = Random.Range(0, 3); while (nl == captainLane);
                yield return MoveCaptain(nl);
            }
            playing = false;
        }

        bool AllCannonsDown() => cannonHp[0] <= 0 && cannonHp[1] <= 0 && cannonHp[2] <= 0;

        IEnumerator FireCannon(int ln)
        {
            cannonOpen[ln] = true;
            cannons[ln].sprite = SpriteBank.Get("B2/cannon_open");
            var w = LaneWarn(ln, shipX - 4.8f);
            for (float t = 0; t < .75f; t += Time.deltaTime)
            {
                if (cannonHp[ln] <= 0) break;
                Blink(new List<SpriteRenderer> { w }, t);
                yield return null;
            }
            Destroy(w.gameObject);
            cannonOpen[ln] = false;
            if (cannonHp[ln] <= 0) yield break;
            SpawnBall(ln, shipX - 5.1f, 7.5f);
            Play(sfxCannon);
            Boom(new Vector3(shipX - 5.3f, LaneY[ln], 0), .6f);
            yield return new WaitForSeconds(.3f);
            if (cannonHp[ln] > 0) cannons[ln].sprite = SpriteBank.Get("B2/cannon_closed");
        }

        IEnumerator MoveCaptain(int to)
        {
            Vector3 a = captain.transform.localPosition, b = new Vector3(-4.6f, LaneY[to], 0);
            for (float t = 0; t < .35f; t += Time.deltaTime) { captain.transform.localPosition = Vector3.Lerp(a, b, t / .35f); yield return null; }
            captain.transform.localPosition = b; captainLane = to;
            captain.sortingOrder = LaneOrder[to] + 3;
        }

        IEnumerator BossDefeat()
        {
            playing = false;
            yield return Defeat();
            if (bossPhase == 3) captainHp = CaptainMax;
            playing = true;
        }

        IEnumerator Finale()
        {
            playing = false;
            ClearUnits();
            captain.sprite = SpriteBank.Get("B2/captain_hurt");
            var patch = Sprite("B2/eyepatch", captain.transform.position, 60);
            Vector3 a = patch.transform.position, b = dragon.transform.position + new Vector3(.8f, -.1f, 0);
            Flash(T("FINAL", "Kaptan'ın göz yaması koptu..."), 2.5f);
            for (float t = 0; t < 1.3f; t += Time.deltaTime)
            {
                patch.transform.position = Vector3.Lerp(a, b, t / 1.3f) + Vector3.up * Mathf.Sin(t / 1.3f * Mathf.PI) * 2.5f;
                patch.transform.Rotate(0, 0, 720 * Time.deltaTime);
                yield return null;
            }
            patch.transform.SetParent(dragon.transform, true);
            infection = 100; ApplyInfection(true);
            dragon.color = new Color(.85f, .7f, 1f);
            yield return new WaitForSeconds(1.2f);
            yield return ShowScreen("Story/b2_end", T("END", "Kaptan'ın yaması ejderhanın binicisine, yani sana yapıştı. \"Artık sen de bizdensin!\" Gözlerin mora döndü..."), "[Enter] devam");

            var list = Chapters.Load();
            int me = list.FindIndex(e => e.file == "@Bolum2");
            if (me >= 0 && me + 1 < list.Count)
            {
                LevelManager.pendingStart = me + 1;
                SceneManager.LoadScene("KayipParca");
                yield break;
            }
            yield return ShowScreen("Story/b3_soon", T("SOON", "BÖLÜM 3: YENİ YAMALI|Devam edecek..."), "[Enter] başlığa dön");
            LevelManager.pendingStart = -1;
            SceneManager.LoadScene("KayipParca");
        }

        // ------------------------------------------------------------------ dusmanlar
        void SpawnEnemy(string kind, int ln)
        {
            var u = new Unit { kind = kind, lane = ln, x = SpawnX, y = LaneY[ln], t = Random.value * 3f };
            switch (kind)
            {
                case "soldier": u.speed = 2.6f; u.hp = 1; break;
                case "balloon": u.speed = 2.0f; u.hp = 1; u.switchAt = Random.Range(1.5f, 4.5f); break;
                case "ship": u.speed = 1.6f; u.hp = 2; u.shootAt = Random.Range(3f, 5f); break;
            }
            u.sr = Sprite(kind == "ship" ? "B2/ship_small" : $"B2/{kind}_0", new Vector3(u.x, u.y, 0), LaneOrder[ln] + 2, LaneTint[ln]);
            u.go = u.sr.gameObject;
            u.go.transform.localScale = Vector3.one * LaneScale[ln];
            units.Add(u);
        }

        void SpawnBall(int ln, float x, float speed)
        {
            var u = new Unit { kind = "ball", lane = ln, x = x, y = LaneY[ln], speed = speed, hp = 1 };
            u.sr = Sprite("B2/cannonball", new Vector3(x, u.y, 0), LaneOrder[ln] + 4);
            u.go = u.sr.gameObject; u.go.transform.localScale = Vector3.one * (LaneScale[ln] * 1.4f);
            units.Add(u);
        }

        SpriteRenderer LaneWarn(int ln, float fromX)
        {
            float len = fromX - (DragonX - 2.5f);
            var w = Sprite("B2/white", new Vector3((fromX + DragonX - 2.5f) / 2f, LaneY[ln], 0), LaneOrder[ln] - 1, new Color(1f, .18f, .3f, .3f));
            w.transform.localScale = new Vector3(len, 1.7f, 1);
            return w;
        }

        static void Blink(List<SpriteRenderer> list, float t)
        {
            float a = .18f + .22f * Mathf.Abs(Mathf.Sin(t * 14f));
            foreach (var w in list) if (w) { var c = w.color; c.a = a; w.color = c; }
        }

        void Boom(Vector3 pos, float size)
        {
            var u = new Unit { kind = "boom", x = pos.x, y = pos.y, t = 0, speed = size };
            u.sr = Sprite("B2/boom", pos, 50); u.go = u.sr.gameObject;
            units.Add(u);
        }

        void Spores(Vector3 from)
        {
            for (int i = 0; i < 3; i++)
            {
                var u = new Unit { kind = "spore", x = from.x + Random.Range(-.4f, .4f), y = from.y + Random.Range(-.4f, .4f), t = -i * .08f };
                u.sr = Sprite("B2/spore", new Vector3(u.x, u.y, 0), 55); u.go = u.sr.gameObject;
                units.Add(u);
            }
        }

        void ClearUnits()
        {
            foreach (var u in units) { if (u.go) Destroy(u.go); if (u.warn) Destroy(u.warn.gameObject); }
            units.Clear();
            foreach (var s in shots) if (s.go) Destroy(s.go);
            shots.Clear();
        }

        // ------------------------------------------------------------------ guncelleme
        void Update()
        {
            int a = ReadAction();
            if (inScreen) { if (a == 8 || a == 9) screenNext = true; if (a == 10) screenSkip = true; }

            // parallax her zaman akar
            foreach (var (tr, speed, width) in parallax)
            {
                var p = tr.position; p.x -= speed * Time.deltaTime;
                if (p.x <= -width) p.x += width * 2f;
                tr.position = p;
            }

            // ejderha kanat cirpar, serit arasinda kayar
            dragon.sprite = SpriteBank.Get("B2/dragon_" + (int)(Time.time * 8f) % 4);
            dragonY = Mathf.Lerp(dragonY, LaneY[lane], 1f - Mathf.Exp(-18f * Time.deltaTime));
            dragon.transform.position = new Vector3(DragonX, dragonY + Mathf.Sin(Time.time * 3f) * .08f, 0);
            float s = Mathf.Lerp(dragon.transform.localScale.x, LaneScale[lane], 1f - Mathf.Exp(-18f * Time.deltaTime));
            dragon.transform.localScale = new Vector3(s, s, 1);
            dragon.sortingOrder = LaneOrder[lane] + 5;
            foreach (var p in patches) p.sortingOrder = dragon.sortingOrder + 1;
            if (invul > 0) { invul -= Time.deltaTime; dragon.enabled = invul <= 0 || (int)(invul * 12) % 2 == 0; }
            else dragon.enabled = true;

            UpdateEffects();
            if (!playing) return;

            if (a == 0 && lane > 0) { lane--; Play(sfxLane); }
            if (a == 2 && lane < 2) { lane++; Play(sfxLane); }
            fireCd -= Time.deltaTime;
            if (FireHeld() && fireCd <= 0) { FireShot(); fireCd = infection >= 25 ? .26f : .4f; }

            UpdateShots();
            UpdateUnits();
        }

        void FireShot()
        {
            var sh = new Shot { lane = lane, x = MouthX };
            sh.sr = Sprite("Common/fireball_0", new Vector3(MouthX, LaneY[lane], 0), LaneOrder[lane] + 7, infection >= 50 ? FirePurple : FireOrange);
            sh.sr.flipX = true;
            sh.go = sh.sr.gameObject; sh.go.transform.localScale = Vector3.one * LaneScale[lane] * 1.3f;
            shots.Add(sh);
            Play(sfxFire);
        }

        void UpdateShots()
        {
            int dmg = infection >= 75 ? 2 : 1;
            bool pierce = infection >= 50, hitBalls = infection >= 75;
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var sh = shots[i];
                sh.x += 13f * Time.deltaTime;
                sh.go.transform.position = new Vector3(sh.x, LaneY[sh.lane], 0);
                sh.sr.sprite = SpriteBank.Get("Common/fireball_" + (int)(Time.time * 12f) % 4);
                bool consumed = sh.x > 12f;

                foreach (var u in units.ToArray())   // Kill/Boom listeyi degistirir: kopya uzerinde don
                {
                    if (consumed) break;
                    if (u.lane != sh.lane || u.hp <= 0 || sh.hit.Contains(u)) continue;
                    if (u.kind == "boom" || u.kind == "spore") continue;
                    if (u.kind == "ball" && !hitBalls) continue;
                    if (Mathf.Abs(u.x - sh.x) > .7f) continue;
                    sh.hit.Add(u);
                    u.hp -= dmg;
                    if (u.hp <= 0) Kill(u); else { Play(sfxHit); StartCoroutine(HitFlash(u.sr)); }
                    if (!pierce) consumed = true;
                }

                if (!consumed && bossActive && ship)
                {
                    if (bossPhase == 3 && sh.lane == captainLane && sh.x >= shipX - 5f)
                    {
                        consumed = true;
                        captainHp -= dmg; Play(sfxCaptainHurt);
                        StartCoroutine(HurtCaptain());
                    }
                    else if (bossPhase < 3 && cannonHp[sh.lane] > 0 && sh.x >= shipX - 4.9f)
                    {
                        consumed = true;
                        if (bossPhase == 2 && cannonOpen[sh.lane])
                        {
                            cannonHp[sh.lane] -= dmg; StartCoroutine(HitFlash(cannons[sh.lane]));
                            if (cannonHp[sh.lane] <= 0)
                            {
                                cannons[sh.lane].sprite = SpriteBank.Get("B2/cannon_broken");
                                Boom(cannons[sh.lane].transform.position, 1.2f); Play(sfxBoom);
                            }
                        }
                        else Boom(new Vector3(sh.x, LaneY[sh.lane], 0), .3f);   // zirha carpti
                    }
                    else if (sh.x >= shipX - 3.6f) consumed = true;
                }
                if (consumed) { Destroy(sh.go); shots.RemoveAt(i); }
            }
            UpdateHud();
        }

        void UpdateUnits()
        {
            for (int i = units.Count - 1; i >= 0; i--)
            {
                var u = units[i];
                if (u.kind == "boom" || u.kind == "spore") continue;
                u.t += Time.deltaTime;
                u.x -= u.speed * Time.deltaTime;

                if (u.kind == "balloon" && u.toLane < 0 && u.x <= u.switchAt)
                {
                    u.toLane = u.lane == 1 ? (Random.value < .5f ? 0 : 2) : 1;
                    u.warn = Sprite("B2/arrow_up", new Vector3(u.x - 1f, LaneY[u.toLane], 0), 60, new Color(1f, .3f, .4f));
                    if (u.toLane > u.lane) u.warn.flipY = true;
                    u.shootAt = u.t + .6f;
                }
                if (u.kind == "balloon" && u.toLane >= 0 && u.toLane != u.lane && u.t >= u.shootAt)
                {
                    u.lane = u.toLane;
                    if (u.warn) Destroy(u.warn.gameObject);
                    u.sr.sortingOrder = LaneOrder[u.lane] + 2; u.sr.color = LaneTint[u.lane];
                }
                if (u.kind == "ship" && !u.fired && u.x <= u.shootAt)
                {
                    u.fired = true; StartCoroutine(ShipShot(u));
                }

                u.y = Mathf.Lerp(u.y, LaneY[u.lane], 1f - Mathf.Exp(-6f * Time.deltaTime));
                float bob = u.kind == "ball" ? 0 : Mathf.Sin(u.t * 4f) * .1f;
                u.go.transform.position = new Vector3(u.x, u.y + bob, 0);
                u.go.transform.localScale = Vector3.one * LaneScale[u.lane] * (u.kind == "ball" ? 1.4f : 1f);
                if (u.kind == "soldier" || u.kind == "balloon") u.sr.sprite = SpriteBank.Get($"B2/{u.kind}_{(int)(u.t * 4f) % 2}");
                if (u.warn) u.warn.transform.position = new Vector3(u.x - 1f, LaneY[u.toLane], 0);

                // ejderhaya carpma
                if (u.lane == lane && u.x < DragonX + 1.7f && u.x > DragonX - 1.8f)
                {
                    if (invul <= 0)
                    {
                        hearts--; invul = 1.4f; Play(sfxHit);
                        AddInfection(u.kind == "ball" ? 5 : 15);
                        StartCoroutine(Shake(.25f));
                    }
                    Boom(new Vector3(u.x, u.y, 0), .7f);
                    Remove(u); continue;
                }
                if (u.x < -12f) Remove(u);
            }
        }

        IEnumerator ShipShot(Unit u)
        {
            var w = LaneWarn(u.lane, u.x - .8f);
            for (float t = 0; t < .8f; t += Time.deltaTime)
            {
                if (u.hp <= 0 || !u.go) break;
                Blink(new List<SpriteRenderer> { w }, t);
                yield return null;
            }
            Destroy(w.gameObject);
            if (u.hp > 0 && u.go) { SpawnBall(u.lane, u.x - .9f, 7f); Play(sfxCannon); }
        }

        void Kill(Unit u)
        {
            Boom(new Vector3(u.x, u.y, 0), u.kind == "ship" ? 1.1f : .8f);
            Play(sfxBoom);
            if (u.kind != "ball" && u.x - DragonX < 4.5f) { AddInfection(5); Spores(new Vector3(u.x, u.y, 0)); }
            Remove(u);
        }

        void Remove(Unit u)
        {
            u.hp = 0;
            if (u.warn) Destroy(u.warn.gameObject);
            if (u.go) Destroy(u.go);
            units.Remove(u);
        }

        void UpdateEffects()
        {
            for (int i = units.Count - 1; i >= 0; i--)
            {
                var u = units[i];
                if (u.kind == "boom")
                {
                    u.t += Time.deltaTime;
                    float k = u.t / .35f;
                    u.go.transform.localScale = Vector3.one * u.speed * (0.6f + k);
                    var c = u.sr.color; c.a = 1f - k; u.sr.color = c;
                    if (k >= 1f) { Destroy(u.go); units.RemoveAt(i); }
                }
                else if (u.kind == "spore")
                {
                    u.t += Time.deltaTime;
                    if (u.t < 0) continue;
                    var p = Vector3.Lerp(new Vector3(u.x, u.y, 0), dragon.transform.position, Mathf.Clamp01(u.t / .5f));
                    u.go.transform.position = p;
                    if (u.t >= .5f) { Destroy(u.go); units.RemoveAt(i); }
                }
            }
        }

        IEnumerator HitFlash(SpriteRenderer sr)
        {
            if (!sr) yield break;
            var c = sr.color; sr.color = new Color(1f, .5f, .5f);
            yield return new WaitForSeconds(.08f);
            if (sr) sr.color = c;
        }

        IEnumerator HurtCaptain()
        {
            if (!captain) yield break;
            captain.sprite = SpriteBank.Get("B2/captain_hurt");
            yield return new WaitForSeconds(.15f);
            if (captain && captainHp > 0) captain.sprite = SpriteBank.Get("B2/captain_idle");
        }

        IEnumerator Shake(float dur)
        {
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                cam.transform.position = new Vector3(Random.Range(-.12f, .12f), Random.Range(-.12f, .12f), -10);
                yield return null;
            }
            cam.transform.position = new Vector3(0, 0, -10);
        }

        // ------------------------------------------------------------------ virus
        void AddInfection(float v)
        {
            infection = Mathf.Min(bossActive ? 95f : 90f, infection + v);
            ApplyInfection(true);
        }

        void ApplyInfection(bool announce)
        {
            for (int i = 0; i < patches.Count; i++) patches[i].enabled = infection >= 25 * (i + 1) || (i == 3 && infection >= 95);
            int tier = infection >= 75 ? 75 : infection >= 50 ? 50 : infection >= 25 ? 25 : 0;
            if (announce && tier > lastLine)
            {
                lastLine = tier;
                string line = tier == 25 ? T("L25", "Bu yamalar... içime işliyor. Ama ateşim hızlandı!")
                            : tier == 50 ? T("L50", "Ateşim mora döndü ve her şeyin içinden geçiyor. Bu güç... hoşuma gitti.")
                            : T("L75", "Gülleleri bile yakabiliyorum! Daha fazla!");
                Flash(line, 3f);
            }
            if (!announce) lastLine = tier;
            UpdateHud();
        }

        // ------------------------------------------------------------------ arayuz
        /// HUD yerlesimi (1920x1080 tuval birimi, Expand: tuval her oranda en az bu kadar):
        ///  - Sol ust: kalpler, altinda yama virusu cubugu + guc yazisi (ejderhanin ustu bos gokyuzu)
        ///  - Ust orta: boss adi + cubugu. Sag taraf bos birakilir: korsan gemisi ve kaptan orada duruyor.
        ///  - Orta: mesajlar (yaziya oturan koyu zemin)   - Alt: tus ipucu (kenarlara gerilir)
        const float HeartSize = 72f, HeartGap = 10f, HeartTop = 26f;
        const float InfTop = HeartTop + HeartSize + 18f, InfWidth = 460f, InfHeight = 24f;
        const float BossTop = 66f, BossWidth = 620f, BossHeight = 26f;

        void SetupUI()
        {
            var root = UIKit.MakeCanvas("UI").transform;

            var hrow = UIKit.MakeRect(root, "Kalpler");
            UIKit.Pin(hrow, new Vector2(0, 1), new Vector2(UIKit.Margin, -HeartTop), new Vector2(maxHearts * (HeartSize + HeartGap), HeartSize));
            var hl = hrow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = HeartGap; hl.childAlignment = TextAnchor.UpperLeft;
            hl.childControlWidth = hl.childControlHeight = false; hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            for (int i = 0; i < maxHearts; i++)
            {
                var img = UIKit.MakeImage(hrow, "Kalp", Color.white);
                img.rectTransform.sizeDelta = new Vector2(HeartSize, HeartSize);
                img.sprite = SpriteBank.Get("Player/body_1111"); img.preserveAspect = true; heartIcons.Add(img);
                var ol = img.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(.1f, .05f, .1f, .9f); ol.effectDistance = new Vector2(3, -3);
            }

            var inf = UIKit.MakeBar(root, "Virus", new Color(.1f, .08f, .15f, .85f), new Color(.62f, .35f, .95f), out infFill);
            UIKit.Pin(inf.rectTransform, new Vector2(0, 1), new Vector2(UIKit.Margin, -InfTop), new Vector2(InfWidth, InfHeight));
            infText = UIKit.MakeText(inf.transform, "VirusYazi", UIKit.Hud, 26, TextAnchor.UpperLeft);
            UnderBar(infText.rectTransform, 360, leftAligned: true);   // cubuktan genis: guc aciklamasi tek satira sigsin

            var boss = UIKit.MakeBar(root, "BossBar", new Color(.15f, .05f, .05f, .85f), new Color(1f, .3f, .3f), out bossFill);
            UIKit.Pin(boss.rectTransform, new Vector2(.5f, 1), new Vector2(0, -BossTop), new Vector2(BossWidth, BossHeight));
            bossBar = boss.gameObject;
            bossText = UIKit.MakeText(boss.transform, "BossYazi", UIKit.Hud, 32, TextAnchor.LowerCenter);
            var brt = bossText.rectTransform;   // adi cubugun hemen ustunde
            brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(.5f, 0);
            brt.anchoredPosition = new Vector2(0, 4); brt.sizeDelta = new Vector2(200, 40);
            bossBar.SetActive(false);

            msgText = UIKit.MakeText(root, "Mesaj", UIKit.Body, 46, TextAnchor.MiddleCenter);
            UIKit.Stretch(msgText.rectTransform, .14f, .42f, .86f, .74f);
            UIKit.AddBackdrop(msgText, UIKit.BackdropColor, UIKit.BackdropPad);   // acik gokyuzu ustunde okunsun
            hintText = UIKit.MakeText(root, "Ipucu", UIKit.BodyLight, 26, TextAnchor.LowerCenter);
            UIKit.BottomBand(hintText.rectTransform, 80, 80, 20, 50);
            hintText.text = T("HINT", "W / S: şerit değiştir    Boşluk: ateş (basılı tutabilirsin)");
            UpdateHud();
        }

        /// Bir cubugun hemen altina yazi. extra: cubuktan ne kadar genis olsun (sola hizaliysa saga dogru uzar).
        static void UnderBar(RectTransform rt, float extra, bool leftAligned = false)
        {
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(leftAligned ? 0 : .5f, 1);
            rt.anchoredPosition = new Vector2(0, -6); rt.sizeDelta = new Vector2(extra, 36);
        }

        void UpdateHud()
        {
            for (int i = 0; i < heartIcons.Count; i++) heartIcons[i].color = i < hearts ? new Color(1f, .32f, .38f) : new Color(.3f, .3f, .35f, .5f);
            UIKit.SetFill(infFill, infection / 100f);
            if (infText)
            {
                string power = infection >= 75 ? "  ·  gülleleri yakar, çift hasar" : infection >= 50 ? "  ·  delici mor ateş" : infection >= 25 ? "  ·  hızlı ateş" : "";
                infText.text = $"YAMA VİRÜSÜ %{Mathf.RoundToInt(infection)}{power}";
            }
            if (bossActive && bossFill)
            {
                float v = bossPhase == 3 ? captainHp / (float)CaptainMax : (cannonHp[0] + cannonHp[1] + cannonHp[2]) / 9f;
                UIKit.SetFill(bossFill, v);
                bossText.text = bossPhase == 3 ? "YAMA KAPTAN" : bossPhase == 1 ? "KORSAN GEMİSİ (zırhlı)" : "KORSAN GEMİSİ: topları sustur";
            }
        }

        void Flash(string m, float dur) { if (flashing != null) StopCoroutine(flashing); flashing = StartCoroutine(FlashRoutine(m.Replace("|", "\n"), dur)); }
        IEnumerator FlashRoutine(string m, float dur) { msgText.text = m; yield return new WaitForSeconds(dur); msgText.text = ""; }

        void Play(AudioClip c) { if (c) audioSrc.PlayOneShot(c); }

        // ------------------------------------------------------------------ hikaye ekrani
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

        // ------------------------------------------------------------------ girdi
        /// -1 yok, 0 yukari, 2 asagi, 8 enter, 9 bosluk, 10 esc
        static int ReadAction()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return -1;
            if (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) return 0;
            if (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame) return 2;
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) return 8;
            if (k.spaceKey.wasPressedThisFrame) return 9;
            if (k.escapeKey.wasPressedThisFrame) return 10;
#else
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) return 0;
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) return 2;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) return 8;
            if (Input.GetKeyDown(KeyCode.Space)) return 9;
            if (Input.GetKeyDown(KeyCode.Escape)) return 10;
#endif
            return -1;
        }

        static bool FireHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && k.spaceKey.isPressed;
#else
            return Input.GetKey(KeyCode.Space);
#endif
        }
    }
}
