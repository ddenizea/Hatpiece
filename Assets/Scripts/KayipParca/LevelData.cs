using System.Collections.Generic;
using UnityEngine;

namespace KayipParca
{
    /// <summary>
    /// Bir seviye dosyasini (Resources/KayipParca/Levels/levelN.txt) okur.
    /// Harita karakterleri:
    ///  #  duvar            .  zemin           ,  desenli zemin
    ///  o  engel (kati)     X  tehlike (olum)  S  sekil istasyonu
    ///  P  oyuncu           G  hedef yuva      E  2. son yuvasi (kendi kutun)
    ///  C  ayna klon        g  klonun yuvasi
    /// Sekil dizisi "NESW" sirasinda: 1 = cikinti, 0 = girinti.
    /// </summary>
    public class LevelData
    {
        public struct EnemyPath { public Vector2Int a, b; }

        public string name;
        public int theme;
        public int moves;
        public string shape;
        public string hint = "";
        public readonly Dictionary<char, string> goals = new Dictionary<char, string>();
        public readonly List<EnemyPath> enemies = new List<EnemyPath>();
        public char[,] grid;
        public int width, height;
        public Vector2Int playerStart;
        public bool hasClone;
        public Vector2Int cloneStart;

        public char At(Vector2Int p) => grid[p.x, p.y];

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
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string k = line.Substring(0, eq), v = line.Substring(eq + 1).Trim();
                switch (k)
                {
                    case "NAME": d.name = v; break;
                    case "THEME": d.theme = int.Parse(v); break;
                    case "MOVES": d.moves = int.Parse(v); break;
                    case "SHAPE": d.shape = v; break;
                    case "HINT": d.hint = v; break;
                    case "ENEMY":
                        var n = v.Split(',');
                        d.enemies.Add(new EnemyPath
                        {
                            a = new Vector2Int(int.Parse(n[0]), int.Parse(n[1])),
                            b = new Vector2Int(int.Parse(n[2]), int.Parse(n[3]))
                        });
                        break;
                    default:
                        if (k.StartsWith("GOAL_") && k.Length == 6) d.goals[k[5]] = v;
                        break;
                }
            }
            d.height = rows.Count;
            d.width = 0;
            foreach (var r in rows) d.width = Mathf.Max(d.width, r.Length);
            d.grid = new char[d.width, d.height];
            for (int y = 0; y < d.height; y++)
                for (int x = 0; x < d.width; x++)
                {
                    char c = x < rows[y].Length ? rows[y][x] : '#';
                    if (c == 'P') { d.playerStart = new Vector2Int(x, y); c = '.'; }
                    if (c == 'C') { d.hasClone = true; d.cloneStart = new Vector2Int(x, y); c = '.'; }
                    d.grid[x, y] = c;
                }
            return d;
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
}
