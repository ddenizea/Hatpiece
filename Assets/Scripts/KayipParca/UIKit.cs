using UnityEngine;
using UnityEngine.UI;

namespace KayipParca
{
    /// <summary>
    /// Ortak arayuz yardimcilari (Bolum 1 ve Bolum 2).
    /// Tum HUD 1920x1080 referansinda tasarlanir. CanvasScaler "Expand" modunda oldugu icin tuval her en-boy
    /// oraninda EN AZ 1920x1080 birimdir: 4:3'te 1920x1440, 21:9'da 2520x1080 olur. Boylece kenara sabitlenen
    /// ogeler hicbir ekranda tasmaz; genis ekranda yanlara, uzun ekranda ust/alta bosluk acilir.
    /// Kural: her oge ait oldugu kenara/koseye sabitlenir, genis metinler sabit genislik yerine kenarlara gerilir.
    /// </summary>
    public static class UIKit
    {
        public static readonly Vector2 Reference = new Vector2(1920, 1080);
        public const float Margin = 40f;
        public static readonly Color OutlineColor = new Color(0, 0, 0, .8f);
        public static readonly Color BackdropColor = new Color(0, 0, 0, .62f);
        public static readonly Vector2 BackdropPad = new Vector2(44, 20);

        public static Font DefaultFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ------------------------------------------------------------------ yazi tipleri (Resources/KayipParca/Fonts, OFL)
        // Basliklar ve HUD: Pixelify Sans (piksel sanatiyla uyumlu). Hikaye, ipucu, mesaj: Nunito (uzun metinde okunakli).
        // Dosyalar zaten kalin/yari kalin kesimler; FontStyle.Bold ile ayrica kalinlastirilmaz.
        static Font hud, hudLight, body, bodyLight;
        public static Font Hud => hud ? hud : hud = LoadFont("PixelifySans-Bold");             // baslik, sayaclar
        public static Font HudLight => hudLight ? hudLight : hudLight = LoadFont("PixelifySans-Medium"); // kucuk etiketler, tus ipuclari
        public static Font Body => body ? body : body = LoadFont("Nunito-ExtraBold");          // mesajlar, hikaye metni
        public static Font BodyLight => bodyLight ? bodyLight : bodyLight = LoadFont("Nunito-SemiBold"); // ipucu satiri

        static Font LoadFont(string file)
        {
            var f = Resources.Load<Font>("KayipParca/Fonts/" + file);
            return f ? f : DefaultFont;
        }
        static bool IsCustom(Font f) => f && f.name != "LegacyRuntime";

        public static void ConfigureScaler(CanvasScaler sc)
        {
            if (!sc) return;
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = Reference;
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        /// Expand modundaki tuval olcegi (1 tuval birimi = bu kadar ekran pikseli). CanvasScaler'in henuz
        /// guncellenmedigi karelerde de dogru sonuc verir.
        public static float ScaleFactor() =>
            Mathf.Max(.01f, Mathf.Min(Screen.width / Reference.x, Screen.height / Reference.y));

        public static Canvas MakeCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ConfigureScaler(go.GetComponent<CanvasScaler>());
            return canvas;
        }

        // ------------------------------------------------------------------ yerlesim

        /// Tek bir noktaya sabitle (anchor = pivot). pos o noktadan iceri dogru kayma, size sabit boyut.
        public static RectTransform Pin(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        /// Ust kenara yatayda gerilmis bant: soldan / sagdan bosluk, ustten uzaklik ve yukseklik.
        public static RectTransform TopBand(RectTransform rt, float left, float right, float top, float height)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(.5f, 1);
            rt.offsetMin = new Vector2(left, -top - height); rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// Alt kenara yatayda gerilmis bant.
        public static RectTransform BottomBand(RectTransform rt, float left, float right, float bottom, float height)
        {
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(.5f, 0);
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, bottom + height);
            return rt;
        }

        /// Ebeveynin oransal bir bolgesine gerilir (0-1 arasi kesirler).
        public static RectTransform Stretch(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
            rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        // ------------------------------------------------------------------ ogeler

        public static RectTransform MakeRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image MakeImage(Transform parent, string name, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = col; img.raycastTarget = false;
            return img;
        }

        public static Text MakeText(Transform parent, string name, Font font, int size, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            Style(t, font, size, align);
            return t;
        }

        /// Var olan (sahnede kayitli) metinlere de ayni stili uygular. Yatayda sarar, dikeyde tasabilir.
        public static void Style(Text t, Font font, int size, TextAnchor align)
        {
            if (!t) return;
            if (font) t.font = font;
            t.fontSize = size; t.alignment = align; t.color = Color.white;
            // ozel yazi tipleri zaten kalin kesim; yerlesik Arial'de kalinlik FontStyle ile verilir
            t.fontStyle = IsCustom(t.font) ? FontStyle.Normal : FontStyle.Bold;
            t.supportRichText = true; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.resizeTextForBestFit = false;
            t.lineSpacing = 1f;
            var o = t.GetComponent<Outline>();
            if (!o) o = t.gameObject.AddComponent<Outline>();
            o.effectColor = OutlineColor; o.effectDistance = new Vector2(3, -3);
        }

        /// Kutuya sigmazsa yaziyi kucultur (zengin metin <size> etiketleri olmayan metinler icin).
        public static void BestFit(Text t, int min, int max)
        {
            t.resizeTextForBestFit = true; t.resizeTextMinSize = min; t.resizeTextMaxSize = max;
            t.fontSize = max;
            t.verticalOverflow = VerticalWrapMode.Truncate;
        }

        /// Arka plan + soldan dolan cubuk. Dolulugu SetFill ile ver (genislikten bagimsiz, anchor ile).
        public static Image MakeBar(Transform parent, string name, Color back, Color fill, out RectTransform fillRt)
        {
            var bg = MakeImage(parent, name, back);
            var f = MakeImage(bg.transform, name + "_Dolu", fill);
            fillRt = f.rectTransform;
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one; fillRt.pivot = new Vector2(0, .5f);
            fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
            return bg;
        }

        public static void SetFill(RectTransform fill, float v)
        {
            if (fill) fill.anchorMax = new Vector2(Mathf.Clamp01(v), 1);
        }

        /// Metnin arkasina, yaziya gore boyutlanan yari saydam zemin koyar (acik renkli duvar / gokyuzu
        /// ustunde okunabilirlik icin). Metin bosken zemin gizlenir. Metin ortali (MiddleCenter) olmali.
        public static Image AddBackdrop(Text t, Color col, Vector2 pad)
        {
            var bg = MakeImage(t.transform.parent, t.name + "_Zemin", col);
            bg.sprite = RoundedSprite; bg.type = Image.Type.Sliced;
            bg.transform.SetSiblingIndex(t.transform.GetSiblingIndex());   // metnin hemen arkasinda
            var fit = bg.gameObject.AddComponent<TextBackdrop>();
            fit.text = t; fit.pad = pad;
            fit.Refresh();
            return bg;
        }

        // ------------------------------------------------------------------ panel

        static Sprite rounded;
        /// Kenarlari yuvarlatilmis beyaz 9-dilim sprite (calisma aninda uretilir, renk Image.color ile verilir).
        public static Sprite RoundedSprite
        {
            get
            {
                if (rounded) return rounded;
                const int n = 32, r = 12;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "UIKit_Rounded" };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float cx = Mathf.Clamp(x + .5f, r, n - r), cy = Mathf.Clamp(y + .5f, r, n - r);
                        float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy));
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d + .5f) * 255));
                    }
                tex.SetPixels32(px); tex.Apply();
                rounded = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return rounded;
            }
        }

        public static Image MakePanel(Transform parent, string name, Color col)
        {
            var img = MakeImage(parent, name, col);
            img.sprite = RoundedSprite; img.type = Image.Type.Sliced;
            return img;
        }

        // ------------------------------------------------------------------ hikaye ekrani

        /// Tam ekran hikaye ekrani: ustte resim (orani korunur), altta metin ve tus ipucu. Iki bolum de kullanir.
        public static GameObject MakeStoryScreen(Transform canvas, out Image pic, out Text body, out Text prompt)
        {
            var root = MakeImage(canvas, "HikayeEkrani", new Color(.04f, .04f, .06f, 1f));
            root.raycastTarget = true;   // arkadaki oyuna tiklanmasin
            Stretch(root.rectTransform, 0, 0, 1, 1);

            pic = MakeImage(root.transform, "Resim", Color.white);
            pic.preserveAspect = true;
            var prt = pic.rectTransform;
            prt.anchorMin = new Vector2(0, .3f); prt.anchorMax = new Vector2(1, 1);
            prt.offsetMin = new Vector2(120, 0); prt.offsetMax = new Vector2(-120, -30);

            body = MakeText(root.transform, "Yazi", Body, 40, TextAnchor.MiddleCenter);
            var brt = body.rectTransform;
            brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, .3f);
            brt.offsetMin = new Vector2(160, 90); brt.offsetMax = new Vector2(-160, -10);
            BestFit(body, 22, 40);

            prompt = MakeText(root.transform, "Ipucu", Hud, 28, TextAnchor.MiddleCenter);
            BottomBand(prompt.rectTransform, 160, 160, 24, 50);
            prompt.color = new Color(1f, .82f, .3f);

            root.gameObject.SetActive(false);
            return root.gameObject;
        }
    }
}
