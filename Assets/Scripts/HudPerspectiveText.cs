using UnityEngine;
using UnityEngine.UI;

/// <summary>Projeta os glifos no mesmo plano das barras orbitais.</summary>
[DisallowMultipleComponent]
public sealed class HudPerspectiveText : BaseMeshEffect
{
    [SerializeField] RectTransform referencia;
    [SerializeField] float escalaDistante = 0.68f;

    public void Configurar(RectTransform painel, float escala)
    {
        referencia = painel;
        escalaDistante = Mathf.Clamp(escala, 0.4f, 1f);
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || referencia == null || vertices.currentVertCount == 0) return;
        Rect rect = referencia.rect;
        if (rect.width <= 0f) return;
        Matrix4x4 paraPainel = referencia.worldToLocalMatrix * transform.localToWorldMatrix;
        Matrix4x4 paraTexto = transform.worldToLocalMatrix * referencia.localToWorldMatrix;
        float profundidade = 1f / Mathf.Clamp(escalaDistante, 0.4f, 1f);
        UIVertex vertice = default;
        for (int i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertice, i);
            Vector3 ponto = paraPainel.MultiplyPoint3x4(vertice.position);
            float u = (ponto.x - rect.xMin) / rect.width;
            float divisor = Mathf.Max(0.01f, 1f + (profundidade - 1f) * u);
            ponto.x = rect.xMin + rect.width * profundidade * u / divisor;
            ponto.y = rect.center.y + (ponto.y - rect.center.y) / divisor;
            vertice.position = paraTexto.MultiplyPoint3x4(ponto);
            vertices.SetUIVertex(vertice, i);
        }
    }
}
