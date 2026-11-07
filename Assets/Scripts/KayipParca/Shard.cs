using UnityEngine;

namespace KayipParca
{
    /// <summary>Kirik parcasi: savrulur, doner, yavasca kaybolur.</summary>
    public class Shard : MonoBehaviour
    {
        public Vector2 velocity;
        float life, spin;
        SpriteRenderer sr;
        void Start() { sr = GetComponent<SpriteRenderer>(); spin = Random.Range(-540f, 540f); }
        void Update()
        {
            life += Time.deltaTime;
            velocity *= Mathf.Exp(-4f * Time.deltaTime);
            transform.position += (Vector3)(velocity * Time.deltaTime);
            transform.Rotate(0, 0, spin * Time.deltaTime);
            var c = sr.color; c.a = Mathf.Clamp01(1.2f - life / 0.7f); sr.color = c;
            if (life > 0.9f) Destroy(gameObject);
        }
    }
}
