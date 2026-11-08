using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KayipParca
{
    /// <summary>
    /// Bolum 1 konusmalari: dunyadaki bir noktanin (Yamali, Sapkali) ustunde beliren konusma balonlari ve
    /// 1-7. seviyelerde ekranin saginda ejderhanin sirtinda bekleyen Yamali (seviye bitince saga kacar).
    /// LevelManager kurar ve kullanir; metinler seviye dosyalarindaki YAMALI= / SAPKALI= / FLEE= satirlarindan gelir.
    /// </summary>
    public class Dialogue : MonoBehaviour
    {
        class Bubble
        {
            public RectTransform rt; public Text text; public Transform anchor; public Vector3 offset;
            public float until; public CanvasGroup group;
        }

        Camera cam;
        RectTransform canvasRt;
        Font font;
        Bubble yamaliBubble, heroBubble;

        // ejderhanin sirtindaki Yamali
        Transform rider; SpriteRenderer riderDragon, riderYamali;
        Vector3 riderHome; bool fleeing;

        public Transform RiderYamali => riderYamali ? riderYamali.transform : null;

        public void Init(Camera c, Canvas canvas, Font f)
        {
            cam = c; canvasRt = (RectTransform)canvas.transform; font = f;
            yamaliBubble = MakeBubble("YamaliBalon");
            heroBubble = MakeBubble("SapkaliBalon");
        }

        Bubble MakeBubble(string n)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(canvasRt, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f); rt.pivot = new Vector2(.5f, 0);
            var img = go.GetComponent<Image>(); img.color = Color.white;
            var ol = go.AddComponent<Outline>(); ol.effectColor = Color.black; ol.effectDistance = new Vector2(4, -4);
            // kuyruk: kucuk dondurulmus kare
            var tail = new GameObject("Kuyruk", typeof(RectTransform), typeof(Image));
            tail.transform.SetParent(rt, false);
            var trt = (RectTransform)tail.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(.5f, 0); trt.sizeDelta = new Vector2(26, 26);
            trt.anchoredPosition = new Vector2(0, 2); trt.localRotation = Quaternion.Euler(0, 0, 45);
            tail.GetComponent<Image>().color = Color.white;
            var tgo = new GameObject("Yazi", typeof(RectTransform), typeof(Text));
            tgo.transform.SetParent(rt, false);
            var t = tgo.GetComponent<Text>();
            t.font = font; t.fontSize = 30; t.color = Color.black; t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.supportRichText = true;
            var tr = (RectTransform)tgo.transform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(22, 16); tr.offsetMax = new Vector2(-22, -16);
            go.SetActive(false);
            return new Bubble { rt = rt, text = t, group = go.GetComponent<CanvasGroup>() };
        }

        /// Bir konusmaci ustunde balon goster. who: "yamali" ya da "sapkali".
        public void Say(bool yamali, Transform anchor, string line, float seconds, float yOffset = 1.1f)
        {
            if (string.IsNullOrEmpty(line) || !anchor) return;
            var b = yamali ? yamaliBubble : heroBubble;
            if (b == null) return;
            b.anchor = anchor; b.offset = new Vector3(0, yOffset, 0);
            b.text.text = (yamali ? "<b>Yamalı:</b> " : "<b>Şapkalı:</b> ") + line;
            float w = Mathf.Min(620, Mathf.Max(260, b.text.preferredWidth * .6f + 60));
            b.rt.sizeDelta = new Vector2(w, 100);
            b.rt.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            b.rt.sizeDelta = new Vector2(w, b.text.preferredHeight + 34);
            b.until = Time.time + seconds;
            b.rt.SetAsLastSibling();
            Place(b);
        }

        public void HideAll() { foreach (var b in new[] { yamaliBubble, heroBubble }) if (b != null) b.rt.gameObject.SetActive(false); }

        void Place(Bubble b)
        {
            if (!cam || !b.anchor) { b.rt.gameObject.SetActive(false); return; }
            var sp = cam.WorldToScreenPoint(b.anchor.position + b.offset);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out var local)) return;
            var half = canvasRt.rect.size / 2f;
            float hw = b.rt.sizeDelta.x / 2f + 16;
            local.x = Mathf.Clamp(local.x, -half.x + hw, half.x - hw);
            local.y = Mathf.Clamp(local.y, -half.y + 20, half.y - b.rt.sizeDelta.y - 20);
            b.rt.anchoredPosition = local;
        }

        void LateUpdate()
        {
            foreach (var b in new[] { yamaliBubble, heroBubble })
            {
                if (b == null || !b.rt.gameObject.activeSelf) continue;
                float left = b.until - Time.time;
                if (left <= 0) { b.rt.gameObject.SetActive(false); continue; }
                b.group.alpha = Mathf.Clamp01(left / .25f);
                Place(b);
            }
            if (rider && !fleeing)
                rider.position = riderHome + new Vector3(0, Mathf.Sin(Time.time * 2f) * .15f, 0);
        }

        // ------------------------------------------------------------------ ejderhanin sirtindaki Yamali
        /// Ekranin saginda, tahtanin disinda bekler. home: dunya konumu (ejderhanin merkezi).
        public void ShowRider(Vector3 home, float width)
        {
            HideRider();
            fleeing = false;
            rider = new GameObject("YamaliEjderhada").transform;
            riderHome = home; rider.position = home;
            riderDragon = new GameObject("Ejderha").AddComponent<SpriteRenderer>();
            riderDragon.transform.SetParent(rider, false);
            riderDragon.sprite = SpriteBank.Get("Common/dragon_awake"); riderDragon.sortingOrder = 30;
            float sc = riderDragon.sprite ? width / riderDragon.sprite.bounds.size.x : .15f;
            riderDragon.transform.localScale = new Vector3(sc, sc, 1);
            riderYamali = new GameObject("Yamali").AddComponent<SpriteRenderer>();
            riderYamali.transform.SetParent(rider, false);
            riderYamali.sprite = SpriteBank.Get("Common/yamali"); riderYamali.sortingOrder = 31;
            // eyer: ejderha resminde basin arkasi, govdenin ust kenari (yaklasik %48, ustten %36)
            var sz = riderDragon.sprite ? riderDragon.sprite.bounds.size * sc : new Vector3(width, width * .66f, 1);
            riderYamali.transform.localPosition = new Vector3(-.02f * sz.x, .3f * sz.y, 0);
            float ys = width * .3f / Mathf.Max(.01f, riderYamali.sprite ? riderYamali.sprite.bounds.size.x : 1);
            riderYamali.transform.localScale = new Vector3(ys, ys, 1);
        }

        public void HideRider() { if (rider) Destroy(rider.gameObject); rider = null; riderDragon = null; riderYamali = null; }

        /// Yamali ejderhayi saga cevirir ve ekrandan ucarak cikar.
        public IEnumerator Flee(float seconds = 1.1f)
        {
            if (!rider) yield break;
            fleeing = true;
            riderDragon.flipX = true; riderYamali.flipX = true;
            var start = rider.position;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                rider.position = start + new Vector3(k * k * 14f, Mathf.Sin(k * Mathf.PI) * 1.2f + k * 2f, 0);
                yield return null;
            }
            HideRider();
        }
    }
}
