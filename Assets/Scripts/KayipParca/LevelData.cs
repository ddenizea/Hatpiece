using System.Collections.Generic;
using UnityEngine;

namespace KayipParca
{
    /// <summary>
    /// Bir seviye dosyasini (Resources/KayipParca/Levels/levelN.txt) okur.
    /// Harita karakterleri:
    ///  #  duvar            .  zemin           ,  desenli zemin
    ///  o  engel (kati)     X  tehlike (olum)  S  sekil istasyonu
    ///  P  oyuncu           G  hedef yuva      E  ikinci yuva
    ///  C  ayna klon        g  klonun yuvasi
    ///  D  ejderha (kati)   Y  ejderhanin kalbi / Yamali
    ///  K  vinc kumandasi   H  kancanin baslangici
    ///  T  cop kutusu       B  Yamali'nin kendi kutusu
    ///  L  kayip parca (ustune basinca seni takip eder; yuvaya kadar goturursen kurtarilir)
    /// Satirlar: NAME, THEME, MOVES, SHAPE, GOAL_x, ENEMY, LASER, FIRE, CHEST, STORY, HINT, OUTRO, END_T, END_B, END_ALONE, END_DARK
    /// Sekil dizisi "NESW" sirasinda: 1 = cikinti, 0 = girinti.
    /// Kurallar Tools/solver.py ile birebir aynidir.
    /// </summary>
    public class LevelData
    {
        public struct EnemyPath { public Vector2Int a, b; }

        public class Laser
        {
            public Vector2Int emitter; public int dir, on, off, phase;
            public readonly List<Vector2Int> cells = new List<Vector2Int>();
            public bool IsOn(int t) => off == 0 || ((t + phase) % (on + off)) < on;
        }

        public class FireLane
        {
            public Vector2Int spawn; public int dir, period, phase;
            public readonly List<Vector2Int> cells = new List<Vector2Int>();
            bool Spawned(int s) => s >= 0 && ((s + phase) % period) == 0;
            public bool At(int t, Vector2Int c) { int k = cells.IndexOf(c); return k >= 0 && Spawned(t - k); }
            /// fireball moved from newPos (t0) to oldPos (t0+1) while the piece moved the other way
            public bool Swap(int t0, Vector2Int oldPos, Vector2Int newPos)
            {
                int k = cells.IndexOf(newPos);
                return k >= 0 && k + 1 < cells.Count && cells[k + 1] == oldPos && Spawned(t0 - k);
            }
            /// spawn times of fireballs alive at time t (index = t - s)
            public IEnumerable<int> Alive(int t)
            {
                for (int k = 0; k < cells.Count; k++) if (Spawned(t - k)) yield return t - k;
            }
        }

        public string name, story = "", hint = "", outro = "", endT = "", endB = "", endAlone = "", endDark = "";
        public string yamaliLine = "", heroLine = "", flee = "";   // konusma balonlari (Dialogue.cs)
        public int theme, moves;
        public string shape;
        public readonly Dictionary<char, string> goals = new Dictionary<char, string>();
        public readonly List<EnemyPath> enemies = new List<EnemyPath>();
        public readonly List<Laser> lasers = new List<Laser>();
        public readonly List<FireLane> fires = new List<FireLane>();
        /// Sekil istasyonlari (satir satir okuma sirasiyla). Her biri tek kullanimlik; kullanilanlar State'te bit maskesiyle tutulur.
        public readonly List<Vector2Int> stations = new List<Vector2Int>();
        public int StationBit(Vector2Int p) { int i = stations.IndexOf(p); return i < 0 || i > 30 ? 0 : 1 << i; }
        public int chestOpen = 1, chestCycle = 4, chestPhase;
        public char[,] grid;
        public int width, height;
        public Vector2Int playerStart, cloneStart, hookStart, console, yamali, trash, box, lostStart;
        public bool hasClone, hasCrane, hasDragon, hasLost;
        public RectInt dragonRect;

        public char At(Vector2Int p) =>
            p.x < 0 || p.y < 0 || p.x >= width || p.y >= height ? '#' : grid[p.x, p.y];

        public bool ChestOpen(int t) => ((t + chestPhase) % chestCycle) < chestOpen;

        static int Dir(string s) => "NESW".IndexOf(s.Trim()[0]);

        public static LevelData Parse(string text)
        {
            var d = new LevelData();
            var rows = new List<string>();
            bool inMap = false;
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                var line = raw;
                if (inMap) { if (line.Length > 0) rows.Add(line); continue; }
                if (line == "MAP") { inMap = true; continue; }
                if (line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string k = line.Substring(0, eq), v = line.Substring(eq + 1).Trim();
                var n = v.Split(',');
                switch (k)
                {
                    case "NAME": d.name = v; break;
                    case "THEME": d.theme = int.Parse(v); break;
                    case "MOVES": d.moves = int.Parse(v); break;
                    case "SHAPE": d.shape = v; break;
                    case "STORY": d.story = v; break;
                    case "HINT": d.hint = v; break;
                    case "OUTRO": d.outro = v; break;
                    case "YAMALI": d.yamaliLine = v; break;
                    case "SAPKALI": d.heroLine = v; break;
                    case "FLEE": d.flee = v; break;
                    case "END_T": d.endT = v; break;
                    case "END_B": d.endB = v; break;
                    case "END_ALONE": d.endAlone = v; break;
                    case "END_DARK": d.endDark = v; break;
                    case "ENEMY":
                        d.enemies.Add(new EnemyPath { a = new Vector2Int(int.Parse(n[0]), int.Parse(n[1])), b = new Vector2Int(int.Parse(n[2]), int.Parse(n[3])) });
                        break;
                    case "LASER":
                        d.lasers.Add(new Laser { emitter = new Vector2Int(int.Parse(n[0]), int.Parse(n[1])), dir = Dir(n[2]), on = int.Parse(n[3]), off = int.Parse(n[4]), phase = int.Parse(n[5]) });
                        break;
                    case "FIRE":
                        d.fires.Add(new FireLane { spawn = new Vector2Int(int.Parse(n[0]), int.Parse(n[1])), dir = Dir(n[2]), period = int.Parse(n[3]), phase = int.Parse(n[4]) });
                        break;
                    case "CHEST":
                        d.chestOpen = int.Parse(n[0]); d.chestCycle = int.Parse(n[1]); d.chestPhase = int.Parse(n[2]);
                        break;
                    default:
                        if (k.StartsWith("GOAL_") && k.Length == 6) d.goals[k[5]] = v;
                        break;
                }
            }
            d.height = rows.Count;
            foreach (var r in rows) d.width = Mathf.Max(d.width, r.Length);
            d.grid = new char[d.width, d.height];
            int dxMin = int.MaxValue, dyMin = int.MaxValue, dxMax = -1, dyMax = -1;
            for (int y = 0; y < d.height; y++)
                for (int x = 0; x < d.width; x++)
                {
                    char c = x < rows[y].Length ? rows[y][x] : '#';
                    var p = new Vector2Int(x, y);
                    switch (c)
                    {
                        case 'P': d.playerStart = p; c = '.'; break;
                        case 'C': d.hasClone = true; d.cloneStart = p; c = '.'; break;
                        case 'H': d.hookStart = p; c = '.'; break;
                        case 'L': d.hasLost = true; d.lostStart = p; c = '.'; break;
                        case 'K': d.hasCrane = true; d.console = p; break;
                        case 'Y': d.yamali = p; break;
                        case 'T': d.trash = p; break;
                        case 'B': d.box = p; break;
                        case 'S': d.stations.Add(p); break;
                    }
                    if (c == 'D' || c == 'Y')
                    {
                        d.hasDragon = true;
                        dxMin = Mathf.Min(dxMin, x); dyMin = Mathf.Min(dyMin, y); dxMax = Mathf.Max(dxMax, x); dyMax = Mathf.Max(dyMax, y);
                    }
                    d.grid[x, y] = c;
                }
            if (d.hasDragon) d.dragonRect = new RectInt(dxMin, dyMin, dxMax - dxMin + 1, dyMax - dyMin + 1);
            foreach (var l in d.lasers) d.Ray(l.emitter + Shape.Dirs[l.dir], l.dir, l.cells);
            foreach (var f in d.fires) d.Ray(f.spawn, f.dir, f.cells);
            // metinler secili dile (Loc): Turkce kaynak, Ingilizce karsiligi varsa o
            d.name = Loc.T(d.name); d.story = Loc.T(d.story); d.hint = Loc.T(d.hint); d.outro = Loc.T(d.outro);
            d.endT = Loc.T(d.endT); d.endB = Loc.T(d.endB); d.endAlone = Loc.T(d.endAlone); d.endDark = Loc.T(d.endDark);
            d.yamaliLine = Loc.T(d.yamaliLine); d.heroLine = Loc.T(d.heroLine); d.flee = Loc.T(d.flee);
            return d;
        }

        void Ray(Vector2Int p, int dir, List<Vector2Int> into)
        {
            while ("#oDTBY".IndexOf(At(p)) < 0) { into.Add(p); p += Shape.Dirs[dir]; }
        }

        /// <summary>Dusman ileri-geri devriye: t. turdaki konum.</summary>
        public static Vector2Int EnemyPos(EnemyPath e, int t)
        {
            int n = Mathf.Abs(e.b.x - e.a.x) + Mathf.Abs(e.b.y - e.a.y);
            if (n == 0) return e.a;
            int k = t % (2 * n);
            int s = k <= n ? k : 2 * n - k;
            var dir = new Vector2Int(System.Math.Sign(e.b.x - e.a.x), System.Math.Sign(e.b.y - e.a.y));
            return e.a + dir * s;
        }
    }

    /// <summary>Sekil yardimcilari. Sira: N E S W.</summary>
    public static class Shape
    {
        public static string RotCW(string s) => $"{s[3]}{s[0]}{s[1]}{s[2]}";
        public static string RotCCW(string s) => $"{s[1]}{s[2]}{s[3]}{s[0]}";
        public static string Mirror(string s) => $"{s[0]}{s[3]}{s[2]}{s[1]}";
        public static string Toggle(string s, int side)
        {
            var c = s.ToCharArray();
            c[side] = c[side] == '1' ? '0' : '1';
            return new string(c);
        }
        // N E S W; harita satirlari asagi dogru arttigi icin N = y-1
        public static readonly Vector2Int[] Dirs = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) };
        public static readonly int[] MirrorDir = { 0, 3, 2, 1 };
    }

    /// <summary>Bolum listesi: Levels/bolumler.txt</summary>
    public static class Chapters
    {
        public struct Entry { public string chapter, file; public int chapterIndex; }

        public static List<Entry> Load()
        {
            var list = new List<Entry>();
            var ta = Resources.Load<TextAsset>("KayipParca/Levels/bolumler");
            string chapter = "Bölüm 1"; int ci = 0;
            if (ta == null)
            {
                for (int i = 1; i <= 8; i++) list.Add(new Entry { chapter = chapter, file = "level" + i, chapterIndex = 0 });
                return list;
            }
            foreach (var raw in ta.text.Replace("\r", "").Split('\n'))
            {
                var l = raw.Trim();
                if (l.Length == 0 || l.StartsWith("#")) continue;
                if (l.StartsWith("BOLUM=")) { if (list.Count > 0) ci++; chapter = Loc.T(l.Substring(6).Trim()); continue; }
                list.Add(new Entry { chapter = chapter, file = l, chapterIndex = ci });
            }
            return list;
        }
    }
}
