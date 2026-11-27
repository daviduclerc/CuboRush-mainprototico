using UnityEngine;
using UnityEngine.UI;

/// <summary>Moldura vetorial chanfrada, sem texto gravado na arte.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class NeonMenuGraphic : MaskableGraphic
{
    Color preenchimento = new Color(0.015f, 0.025f, 0.065f, 0.92f);
    float espessura = 1.5f;
    float proporcaoChanfro = 0.23f;

    public void Configurar(Color fundo, float borda, float chanfro)
    {
        preenchimento = fundo;
        espessura = borda;
        proporcaoChanfro = chanfro;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 0f || r.height <= 0f) return;
        float border = Mathf.Min(espessura, r.height * 0.2f);
        Vector2[] outside = Corners(r, Mathf.Min(r.width * 0.45f, r.height * proporcaoChanfro));
        Rect inner = new Rect(r.x + border, r.y + border, r.width - border * 2, r.height - border * 2);
        Vector2[] inside = Corners(inner, Mathf.Min(inner.width * 0.45f, inner.height * proporcaoChanfro));
        for (int i = 0; i < 8; i++)
        {
            vh.AddVert(outside[i], color, Vector2.zero);
            vh.AddVert(inside[i], color, Vector2.zero);
        }
        Color fundo = preenchimento;
        for (int i = 0; i < 8; i++) vh.AddVert(inside[i], fundo, Vector2.zero);
        vh.AddVert(r.center, fundo, Vector2.zero);
        for (int i = 0; i < 8; i++)
        {
            int a = i * 2, b = ((i + 1) % 8) * 2;
            vh.AddTriangle(a, b, a + 1);
            vh.AddTriangle(a + 1, b, b + 1);
            vh.AddTriangle(24, 16 + i, 16 + (i + 1) % 8);
        }
    }

    static Vector2[] Corners(Rect r, float c) => new[] {
        new Vector2(r.xMin+c,r.yMin), new Vector2(r.xMax-c,r.yMin),
        new Vector2(r.xMax,r.yMin+c), new Vector2(r.xMax,r.yMax-c),
        new Vector2(r.xMax-c,r.yMax), new Vector2(r.xMin+c,r.yMax),
        new Vector2(r.xMin,r.yMax-c), new Vector2(r.xMin,r.yMin+c)
    };
}



