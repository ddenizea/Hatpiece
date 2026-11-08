using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace KayipParca
{
    /// <summary>
    /// "[W][A][S][D] hareket    [Q][E] döndür" bicimindeki metni tus kapaklari (beyaz, siyah harf) ve gri
    /// etiketlere cevirir. Gruplar iki ya da daha fazla boslukla ayrilir. Kaynak Text cizilmez ama .text
    /// ozelligi calismaya devam eder; satir onun kutusuna ortalanir (UIKit.AddKeyHints kurar).
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class KeyHints : MonoBehaviour
    {
        public float capHeight = 44f, gap = 34f;
        public int capFont = 22, labelFont = 27;

        static readonly Regex KeyPattern = new Regex(@"\[([^\]]+)\]");
        Text source;
        RectTransform row;
        string last;
        void Awake() { source = GetComponent<Text>(); }
        void OnEnable() { last = null; }

        void LateUpdate()
        {
            if (!source) source = GetComponent<Text>();
            if (source && source.text != last) Rebuild();
        }

        /// Editor'de (onizleme) ya da hemen yenilemek icin.
        public void Refresh() { if (!source) source = GetComponent<Text>(); Rebuild(); }

        void Rebuild()
        {
            last = source.text;
            source.enabled = false;
            if (!row)
            {
                row = UIKit.MakeRect(transform, "Tuslar");
                UIKit.Stretch(row, 0, 0, 1, 1);
                var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.childAlignment = TextAnchor.MiddleCenter; h.spacing = 6;
                h.childControlWidth = h.childControlHeight = false;
                h.childForceExpandWidth = h.childForceExpandHeight = false;
            }
            for (int i = row.childCount - 1; i >= 0; i--)
            {
                var go = row.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            }
            if (string.IsNullOrWhiteSpace(last)) return;
            var groups = Regex.Split(last.Trim(), @"\s{2,}");
            for (int gi = 0; gi < groups.Length; gi++)
            {
                string g = groups[gi];
                foreach (Match m in KeyPattern.Matches(g)) Cap(m.Groups[1].Value);
                string label = KeyPattern.Replace(g, "").Trim();
                if (label.Length > 0) Label(label);
                if (gi < groups.Length - 1) Spacer();
            }
        }

        void Cap(string key)
        {
            var img = UIKit.MakeImage(row, "Tus_" + key, Color.white);
            img.sprite = UIKit.RoundedSprite; img.type = Image.Type.Sliced;
            UIKit.SetCorner(img, 12);
            float w = key.Length > 1 ? key.Length * 12.5f + 24f : capHeight;
            img.rectTransform.sizeDelta = new Vector2(w, capHeight);
            var t = UIKit.MakeText(img.transform, "Yazi", UIKit.Hud, capFont, TextAnchor.MiddleCenter);
            UIKit.Stretch(t.rectTransform, 0, 0, 1, 1);
            t.color = Color.black; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = key;
        }

        void Label(string s)
        {
            var t = UIKit.MakeText(row, "Etiket", UIKit.BodyLight, labelFont, TextAnchor.MiddleLeft);
            t.color = UIKit.Mute; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = s;
            t.rectTransform.sizeDelta = new Vector2(t.preferredWidth + 8, capHeight);
        }

        void Spacer()
        {
            var r = UIKit.MakeRect(row, "Bosluk");
            r.sizeDelta = new Vector2(gap, capHeight);
        }
    }
}
