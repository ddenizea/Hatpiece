using System.Collections.Generic;
using UnityEngine;

namespace KayipParca
{
    /// <summary>Oyuncu (veya ayna klon) parcasi: govde + yuz + sapka katmanlari.</summary>
    public class PieceView : MonoBehaviour
    {
        SpriteRenderer body, face, hat;
        Vector3 target;
        Vector3 punch;
        float scalePunch;
        float animTime;
        int faceMode; // 0 idle, 1 happy, 2 dead
        static readonly int[] blinkSeq = { 0, 2, 3, 2, 0 };

        public static PieceView Create(Transform parent, string shape, bool withHat, Color tint, int order)
        {
            var go = new GameObject(withHat ? "Oyuncu" : "AynaKlon");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<PieceView>();
            v.body = MakeLayer(go.transform, "Govde", order, tint);
            v.face = MakeLayer(go.transform, "Yuz", order + 1, Color.white);
            if (withHat) v.hat = MakeLayer(go.transform, "Sapka", order + 2, Color.white);
            if (v.hat) v.hat.sprite = SpriteBank.Get("Player/hat");
            v.SetShape(shape);
            v.face.sprite = SpriteBank.Get("Player/face_idle_0");
            return v;
        }

        static SpriteRenderer MakeLayer(Transform p, string n, int order, Color c)
        {
            var go = new GameObject(n);
            go.transform.SetParent(p, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            sr.color = c;
            return sr;
        }

        public void SetShape(string s) => body.sprite = SpriteBank.Get($"Player/body_{s}");
        public void SnapTo(Vector3 p) { target = p; transform.position = p; }
        public void MoveTo(Vector3 p) => target = p;
        public void Bump(Vector2Int dir) => punch = new Vector3(dir.x, -dir.y, 0) * 0.18f;
        public void Squash() => scalePunch = 0.25f;
        public void SetFace(int mode) { faceMode = mode; animTime = 0; }

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
            // hafif nefes alma
            float breathe = Mathf.Sin(animTime * 2.2f) * 0.02f;
            face.transform.localPosition = new Vector3(0, breathe, 0) + punch;
            if (hat) hat.transform.localPosition = new Vector3(0, breathe * 1.5f, 0) + punch;
        }
    }
}
