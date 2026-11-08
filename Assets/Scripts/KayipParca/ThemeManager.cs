using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KayipParca
{
    /// <summary>
    /// Her seviyenin isik + ses atmosferi. Seviye atlarken eski tema karartilip
    /// muzik kisilir, yeni tema renk/isik/muzikle yavasca acilir.
    /// Muzik ve ambiyans kliplerini Inspector'dan her temaya surukleyip birakin.
    /// </summary>
    public class ThemeManager : MonoBehaviour
    {
        [Serializable]
        public class Theme
        {
            public string name;
            [Header("Isik")]
            public Color globalColor = Color.white;
            public float globalIntensity = 1f;
            public Color playerLightColor = Color.white;
            public float playerLightIntensity = 0.4f;
            public float playerLightRadius = 3f;
            [Tooltip("0 = sabit isik. 0.1 = hafif nabiz gibi atan isik (gerilim).")]
            public float pulse;
            public Color cameraColor = Color.black;
            [Header("Ses")]
            public AudioClip music;
            [Range(0, 1)] public float musicVolume = 0.6f;
            public AudioClip ambience;
            [Range(0, 1)] public float ambienceVolume = 0.4f;
        }

        public Light2D globalLight;
        public Light2D playerLight;
        public AudioSource musicSource;
        public AudioSource ambienceSource;
        public Camera cam;
        public float fadeOutTime = 0.6f;
        public float fadeInTime = 1.2f;

        public Theme[] themes = Defaults();

        [Tooltip("Siyah-beyaz doodle stili: isiklar beyaz, arka plan siyah. Kapatilirsa temalarin renkli isiklari geri gelir.")]
        public bool monochrome = true;

        Color GlobalColor(Theme th) => monochrome ? Color.white : th.globalColor;
        Color PlayerColor(Theme th) => monochrome ? Color.white : th.playerLightColor;
        Color CameraColor(Theme th) => monochrome ? Color.black : th.cameraColor;
        // gece temasi (karanlik mekanigi) karanlik kalir, digerleri tam parlak beyaz
        float GlobalIntensity(Theme th) => monochrome ? (th.globalIntensity < .5f ? th.globalIntensity : 1f) : th.globalIntensity;
        float PlayerIntensity(Theme th) => monochrome ? (th.globalIntensity < .5f ? th.playerLightIntensity : .15f) : th.playerLightIntensity;

        /// Tema sirasi = seviye dosyasindaki THEME numarasi.
        public static Theme[] Defaults() => new[]
        {
            new Theme { name = "Kutunun Dibi",      globalColor = new Color(1f, .92f, .80f),  globalIntensity = 1.0f,  playerLightColor = new Color(1f, .85f, .6f), playerLightIntensity = .35f, playerLightRadius = 3f,   cameraColor = new Color(.18f, .11f, .07f) },
            new Theme { name = "Halı Ormanı",       globalColor = new Color(1f, .68f, .58f),  globalIntensity = .75f,  playerLightColor = new Color(1f, .8f, .55f), playerLightIntensity = .6f,  playerLightRadius = 3.5f, cameraColor = new Color(.10f, .06f, .14f) },
            new Theme { name = "Mutfak Fayansları", globalColor = new Color(.86f, .95f, 1f),  globalIntensity = 1.1f,  playerLightColor = Color.white,              playerLightIntensity = .2f,  playerLightRadius = 2.5f, cameraColor = new Color(.08f, .16f, .21f) },
            new Theme { name = "Oyuncak Kalesi",    globalColor = new Color(1f, .98f, .92f),  globalIntensity = 1.0f,  playerLightColor = new Color(1f, .9f, .7f),  playerLightIntensity = .3f,  playerLightRadius = 3f,   cameraColor = new Color(.12f, .16f, .18f) },
            new Theme { name = "Gece Koridoru",     globalColor = new Color(.35f, .45f, .80f), globalIntensity = .18f, playerLightColor = new Color(1f, .9f, .6f),  playerLightIntensity = 1.3f, playerLightRadius = 4.5f, cameraColor = new Color(.02f, .03f, .07f) },
            new Theme { name = "Kedinin Yatağı",    globalColor = new Color(1f, .45f, .40f),  globalIntensity = .7f,   playerLightColor = new Color(1f, .75f, .5f), playerLightIntensity = .8f,  playerLightRadius = 3f,   pulse = .12f, cameraColor = new Color(.10f, .03f, .04f) },
            new Theme { name = "Çalışma Masası",    globalColor = new Color(1f, .93f, .82f),  globalIntensity = .85f,  playerLightColor = new Color(1f, .9f, .7f),  playerLightIntensity = .5f,  playerLightRadius = 3.5f, cameraColor = new Color(.10f, .07f, .05f) },
            new Theme { name = "Resmin İçi",        globalColor = new Color(1f, .97f, .9f),   globalIntensity = 1.05f, playerLightColor = new Color(1f, .8f, .5f),  playerLightIntensity = .3f,  playerLightRadius = 3f,   pulse = .05f, cameraColor = new Color(.35f, .55f, .70f) },
        };

        void Awake()
        {
            // Eski sahnelerde daha az tema kayitli olabilir: eksikleri varsayilanlarla tamamla, girilen sesleri koru.
            var def = Defaults();
            if (themes == null || themes.Length < def.Length)
            {
                var list = new System.Collections.Generic.List<Theme>(themes ?? new Theme[0]);
                for (int i = list.Count; i < def.Length; i++) list.Add(def[i]);
                themes = list.ToArray();
            }
            // Muzik bos birakilan temalara ana melodinin uygun surumu (Resources/KayipParca/Audio)
            string[] music = { "muzik_muzik_kutusu", "muzik_kalimba", "muzik_ksilofon", "muzik_ksilofon", "muzik_muzik_kutusu", "muzik_kalimba", "muzik_muzik_kutusu", "muzik_muzik_kutusu" };
            for (int i = 0; i < themes.Length && i < music.Length; i++)
                if (!themes[i].music) { themes[i].music = Resources.Load<AudioClip>("KayipParca/Audio/" + music[i]); themes[i].musicVolume = Mathf.Min(themes[i].musicVolume, .35f); }
        }

        Theme current;
        float intensityScale = 1f;   // 0..1 gecisler sirasinda

        public IEnumerator FadeOut()
        {
            float m0 = musicSource ? musicSource.volume : 0, a0 = ambienceSource ? ambienceSource.volume : 0;
            for (float t = 0; t < fadeOutTime; t += Time.deltaTime)
            {
                float k = 1 - t / fadeOutTime;
                intensityScale = k;
                if (musicSource) musicSource.volume = m0 * k;
                if (ambienceSource) ambienceSource.volume = a0 * k;
                yield return null;
            }
            intensityScale = 0;
        }

        public IEnumerator FadeIn(int index)
        {
            var th = themes[Mathf.Clamp(index, 0, themes.Length - 1)];
            var prevCam = cam ? cam.backgroundColor : Color.black;
            current = th;
            SwapClip(musicSource, th.music);
            SwapClip(ambienceSource, th.ambience);
            if (playerLight) { playerLight.color = PlayerColor(th); playerLight.pointLightOuterRadius = th.playerLightRadius; playerLight.pointLightInnerRadius = th.playerLightRadius * .2f; }
            if (globalLight) globalLight.color = GlobalColor(th);
            for (float t = 0; t < fadeInTime; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / fadeInTime);
                intensityScale = k;
                if (cam) cam.backgroundColor = Color.Lerp(prevCam, CameraColor(th), k);
                if (musicSource) musicSource.volume = th.musicVolume * k;
                if (ambienceSource) ambienceSource.volume = th.ambienceVolume * k;
                yield return null;
            }
            intensityScale = 1;
            if (cam) cam.backgroundColor = CameraColor(th);
        }

        static void SwapClip(AudioSource src, AudioClip clip)
        {
            if (!src) return;
            if (src.clip == clip && src.isPlaying) return;
            src.clip = clip;
            src.loop = true;
            if (clip) src.Play(); else src.Stop();
        }

        public void FollowPlayer(Vector3 p)
        {
            if (playerLight) playerLight.transform.position = new Vector3(p.x, p.y, playerLight.transform.position.z);
        }

        void Update()
        {
            if (current == null) return;
            float pulse = 1f + current.pulse * Mathf.Sin(Time.time * 2.4f);
            if (globalLight) globalLight.intensity = GlobalIntensity(current) * intensityScale * pulse;
            if (playerLight) playerLight.intensity = PlayerIntensity(current) * intensityScale;
        }
    }
}
