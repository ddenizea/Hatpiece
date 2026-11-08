using UnityEngine;

namespace KayipParca
{
    /// <summary>Baslangic menusu ayarlari (PlayerPrefs): ses efektleri, muzik, dil.</summary>
    public static class Settings
    {
        const string SfxKey = "hatpiece_sfx", MusicKey = "hatpiece_music";

        public static bool Sfx
        {
            get => PlayerPrefs.GetInt(SfxKey, 1) == 1;
            set { PlayerPrefs.SetInt(SfxKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool Music
        {
            get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
            set { PlayerPrefs.SetInt(MusicKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
