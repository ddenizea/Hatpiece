using UnityEngine;
using UnityEngine.UI;

namespace KayipParca
{
    /// <summary>
    /// Bir metnin arkasindaki zemini yazinin gercek boyutuna oturtur (UIKit.AddBackdrop kurar).
    /// Metin, cozunurluk ya da metin kutusu degisince yeniden boyutlanir; metin bosken zemin kapanir.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class TextBackdrop : MonoBehaviour
    {
        public Text text;
        public Vector2 pad = new Vector2(40, 18);

        Image img;
        string lastText;
        Vector2 lastBox;
        bool lastActive;

        void LateUpdate()
        {
            if (!text) return;
            var box = text.rectTransform.rect.size;
            bool active = text.isActiveAndEnabled;
            if (text.text != lastText || box != lastBox || active != lastActive) Refresh();
        }

        public void Refresh()
        {
            if (!img) img = GetComponent<Image>();
            if (!text) { img.enabled = false; return; }
            var trt = text.rectTransform;
            lastText = text.text; lastBox = trt.rect.size; lastActive = text.isActiveAndEnabled;
            img.enabled = lastActive && !string.IsNullOrEmpty(lastText);
            if (!img.enabled) return;

            // preferredWidth sarmasiz genisliktir; kutudan genisse yazi sarar ve kutu genisligini kullanir.
            // preferredHeight kutu genisligiyle sarilmis yuksekliktir (zengin metin <size> etiketleri dahil).
            float w = Mathf.Min(text.preferredWidth, lastBox.x);
            float h = text.preferredHeight;
            var rt = (RectTransform)transform;
            rt.anchorMin = trt.anchorMin; rt.anchorMax = trt.anchorMax;
            rt.pivot = new Vector2(.5f, .5f);
            // metin kutusunun ortasi, ayni anchor'larin ortasina gore (pivot ne olursa olsun). Metin MiddleCenter
            // hizali oldugu icin bu yazinin da ortasidir.
            rt.anchoredPosition = trt.anchoredPosition + Vector2.Scale(new Vector2(.5f, .5f) - trt.pivot, trt.sizeDelta);
            var anchorSpan = Vector2.Scale(trt.anchorMax - trt.anchorMin, ((RectTransform)trt.parent).rect.size);
            rt.sizeDelta = new Vector2(w + 2 * pad.x, h + 2 * pad.y) - anchorSpan;
        }
    }
}
