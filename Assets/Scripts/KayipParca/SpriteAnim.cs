using UnityEngine;

namespace KayipParca
{
    /// <summary>Basit sprite animasyonu (ates topu gibi).</summary>
    public class SpriteAnim : MonoBehaviour
    {
        public string prefix; public int frames = 4; public float fps = 10f;
        public Vector3 target; public bool follow;
        SpriteRenderer sr; float t;
        void Awake() => sr = GetComponent<SpriteRenderer>();
        void Update()
        {
            t += Time.deltaTime;
            if (sr && !string.IsNullOrEmpty(prefix)) sr.sprite = SpriteBank.Get($"{prefix}_{(int)(t * fps) % frames}");
            if (follow) transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-16f * Time.deltaTime));
        }
    }
}
