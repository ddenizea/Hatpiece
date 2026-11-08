using System.Collections.Generic;
using UnityEngine;

namespace KayipParca
{
    /// <summary>
    /// Dil: Turkce (kaynak metinler) ya da Ingilizce. Ceviriler Resources/KayipParca/Loc/en.txt dosyasinda,
    /// her satir "Turkce metin&lt;TAB&gt;English text". T(tr) Ingilizcede karsiligi varsa onu, yoksa Turkceyi dondurur.
    /// Seviye ve hikaye metinleri okunurken, HUD yazilari kurulurken cagrilir.
    /// </summary>
    public static class Loc
    {
        const string LangKey = "hatpiece_lang";
        static Dictionary<string, string> en;
        static string lang;

        public static string Lang
        {
            get { if (lang == null) lang = PlayerPrefs.GetString(LangKey, "tr"); return lang; }
            set { lang = value; PlayerPrefs.SetString(LangKey, value); PlayerPrefs.Save(); }
        }
        public static bool English => Lang == "en";

        static void Load()
        {
            en = new Dictionary<string, string>();
            var ta = Resources.Load<TextAsset>("KayipParca/Loc/en");
            if (ta == null) return;
            foreach (var raw in ta.text.Replace("\r", "").Split('\n'))
            {
                if (raw.StartsWith("#")) continue;
                int tab = raw.IndexOf('\t');
                if (tab <= 0) continue;
                en[raw.Substring(0, tab).Trim()] = raw.Substring(tab + 1).Trim();
            }
        }

        /// Turkce kaynak metni secili dile cevirir (Ingilizce karsiligi yoksa Turkce kalir).
        public static string T(string tr)
        {
            if (!English || string.IsNullOrEmpty(tr)) return tr;
            if (en == null) Load();
            return en.TryGetValue(tr.Trim(), out var v) ? v : tr;
        }

        /// Bicimli metin: once sablon cevrilir, sonra string.Format uygulanir.
        public static string F(string tr, params object[] args) => string.Format(T(tr), args);
    }
}
