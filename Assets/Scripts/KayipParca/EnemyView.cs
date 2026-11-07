using System.Collections.Generic;
using UnityEngine;

namespace KayipParca
{
    /// <summary>Dusman: cirkin parca ya da kedi patisi. 4 kareli animasyon.</summary>
    public class EnemyView : MonoBehaviour
    {
        SpriteRenderer sr;
        string prefix;
        Vector3 target;
        float t;

        public static EnemyView Create(Transform parent, string prefix, Vector3 pos, int order)
        {
            var go = new GameObject("Dusman_" + prefix);
            go.transform.SetParent(parent, false);
            var e = go.AddComponent<EnemyView>();
            e.sr = go.AddComponent<SpriteRenderer>();
            e.sr.sortingOrder = order;
            e.prefix = prefix;
            e.SnapTo(pos);
            return e;
        }

        public void SnapTo(Vector3 p) { target = p; transform.position = p; }
        public void MoveTo(Vector3 p) => target = p;

        void Update()
        {
            t += Time.deltaTime;
            sr.sprite = SpriteBank.Get($"Enemies/{prefix}_{(int)(t * 6f) % 4}");
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-14f * Time.deltaTime));
        }
    }
}
