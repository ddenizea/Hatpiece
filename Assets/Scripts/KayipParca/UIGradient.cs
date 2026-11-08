using UnityEngine;
using UnityEngine.UI;

namespace KayipParca
{
    /// <summary>
    /// Bir UI grafigine dikey renk gecisi verir (UIKit.AddGradient kurar). Kose noktalarinin rengini,
    /// kutudaki yuksekligine gore top ile bottom arasinda harmanlayip Image.color ile carpar.
    /// 9-dilim sprite'larda da calisir (dilim sinirlarindaki noktalar arasinda dogrusal gecis).
    /// </summary>
    [AddComponentMenu("KayipParca/UI Gradient")]
    public class UIGradient : BaseMeshEffect
    {
        public Color top = Color.white;
        public Color bottom = new Color(1, 1, 1, .6f);

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            var rect = graphic.rectTransform.rect;
            float h = Mathf.Max(rect.height, 1e-4f);
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                float t = Mathf.Clamp01((v.position.y - rect.yMin) / h);
                v.color = (Color32)((Color)v.color * Color.Lerp(bottom, top, t));
                vh.SetUIVertex(v, i);
            }
        }

        public void Set(Color topCol, Color bottomCol)
        {
            top = topCol; bottom = bottomCol;
            if (graphic) graphic.SetVerticesDirty();
        }
    }
}
