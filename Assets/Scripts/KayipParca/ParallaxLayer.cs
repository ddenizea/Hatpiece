using System.Collections.Generic;
using UnityEngine;

namespace KayipParca
{
    /// <summary>Arka plan parallax katmani: kameradan daha yavas hareket eder.</summary>
    public class ParallaxLayer : MonoBehaviour
    {
        public float factor = 0.8f;   // 1 = kamerayla birlikte (uzak), 0 = sabit (yakin)
        public Vector3 origin;
        Transform cam;
        void LateUpdate()
        {
            if (cam == null) cam = Camera.main ? Camera.main.transform : null;
            if (cam == null) return;
            var c = cam.position - origin;
            transform.position = new Vector3(origin.x + c.x * factor, origin.y + c.y * factor, transform.position.z);
        }
    }
}
