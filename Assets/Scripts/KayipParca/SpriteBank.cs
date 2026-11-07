using System.Collections.Generic;
using UnityEngine;

namespace KayipParca
{
    /// <summary>Resources/KayipParca/Art altindaki spritelari onbellekli yukler.</summary>
    public static class SpriteBank
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        public static Sprite Get(string path)
        {
            if (cache.TryGetValue(path, out var s) && s != null) return s;
            s = Resources.Load<Sprite>("KayipParca/Art/" + path);
            if (s == null) Debug.LogWarning($"[KayipParca] Sprite bulunamadi: {path}. Menu: Kayip Parca > Gorsel Ayarlarini Uygula");
            cache[path] = s;
            return s;
        }
    }
}
