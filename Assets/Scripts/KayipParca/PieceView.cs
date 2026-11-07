using UnityEngine;

namespace KayipParca
{
    /// <summary>
    /// Oyuncu (veya ayna klon) parcasi: govde + yuz + sapka katmanlari.
    /// Katmanlar kodla uretilmez; Parca prefabinda Inspector'dan baglanir
    /// (Kayıp Parça > 4) Parça Prefabını Kur menusu prefabi olusturur).
    /// </summary>
    public class PieceView : MonoBehaviour
    {
        [Header("Katmanlar (prefab uzerinde baglanir)")]
        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteRenderer face;
        [SerializeField] SpriteRenderer hat;

        Vector3 target;
        Vector3 punch;
        float scalePunch;
        float animTime;
        int faceMode; // 0 idle, 1 happy, 2 dead
        static readonly int[] blinkSeq = { 0, 2, 3, 2, 0 };

        /// <summary>Prefabdan bir parca uretir. Ayna klon icin sapka kapatilir.</summary>
        public static PieceView Create(PieceView prefab, Transform parent, string shape, bool withHat, Color tint, int order)
        {
            var v = Instantiate(prefab, parent);
            v.name = withHat ? "Oyuncu" : "AynaKlon";
            v.body.sortingOrder = order;
            v.face.sortingOrder = order + 1;
            v.hat.sortingOrder = order + 2;
            v.body.color = tint;
            v.hat.gameObject.SetActive(withHat);
            v.SetShape(shape);
            v.face.sprite = SpriteBank.Get("Player/face_idle_0");
            return v;
        }

        public void SetShape(string s) => body.sprite = SpriteBank.Get($"Player/body_{s}");
        public void SnapTo(Vector3 p) { target = p; transform.position = p; }
        public void MoveTo(Vector3 p) => target = p;
        public void Bump(Vector2Int dir) => punch = new Vector3(dir.x, -dir.y, 0) * 0.18f;
        public void Squash() => scalePunch = 0.25f;
        public void SetFace(int mode) { faceMode = mode; animTime = 0; }

        /// <summary>Parca kartona bolunup etrafa sacilir (lazer, ates, dusman...).</summary>
        public void Shatter()
        {
            SetVisible(false);
            for (int i = 0; i < 6; i++)
            {
                var go = new GameObject("Kirik");
                go.transform.position = transform.position;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteBank.Get($"Player/shard_{i}");
                sr.sortingOrder = body.sortingOrder + 3;
                sr.color = body.color;
                float a = i * Mathf.PI / 3f + Random.Range(-.3f, .3f);
                go.AddComponent<Shard>().velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(2.5f, 4f);
            }
        }

        public void Restore() { SetVisible(true); SetFace(0); }

        void SetVisible(bool v)
        {
            body.enabled = v; face.enabled = v;
            if (hat) hat.enabled = v;
        }

        void Update()
        {
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-18f * Time.deltaTime));
            punch = Vector3.Lerp(punch, Vector3.zero, 1f - Mathf.Exp(-14f * Time.deltaTime));
            scalePunch = Mathf.Lerp(scalePunch, 0, 1f - Mathf.Exp(-12f * Time.deltaTime));
            body.transform.localPosition = punch;
            body.transform.localScale = new Vector3(1 + scalePunch, 1 - scalePunch * 0.6f, 1);

            animTime += Time.deltaTime;
            if (faceMode == 1) face.sprite = SpriteBank.Get("Player/face_happy");
            else if (faceMode == 2) face.sprite = SpriteBank.Get("Player/face_dead");
            else
            {
                // 3 saniyede bir 4 kareli goz kirpma animasyonu
                float cyc = animTime % 3f;
                int f = cyc < 2.6f ? 0 : blinkSeq[Mathf.Min(blinkSeq.Length - 1, (int)((cyc - 2.6f) / 0.08f))];
                face.sprite = SpriteBank.Get($"Player/face_idle_{f}");
            }
            // hafif nefes alma yalnizca yuzde; sapka govdeye gore sabit durur (yalnizca carpma itmesini izler)
            float breathe = Mathf.Sin(animTime * 2.2f) * 0.02f;
            face.transform.localPosition = new Vector3(0, breathe, 0) + punch;
            hat.transform.localPosition = punch;
        }
    }

}
