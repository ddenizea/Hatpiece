using System.Collections.Generic;
using UnityEngine;

namespace KayipParca
{
    /// <summary>Iki sprite arasinda arada bir gecis yapar (kedinin goz kirpmasi).</summary>
    public class BlinkSprite : MonoBehaviour
    {
        public Sprite open, closed;
        public float interval = 4f, duration = 0.18f;
        SpriteRenderer sr;
        float t;
        void Awake() => sr = GetComponent<SpriteRenderer>();
        void Update()
        {
            t += Time.deltaTime;
            float c = t % interval;
            sr.sprite = c > interval - duration ? closed : open;
        }
    }
}
