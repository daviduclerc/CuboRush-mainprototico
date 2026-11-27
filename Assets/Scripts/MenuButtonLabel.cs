using UnityEngine;
using UnityEngine.UI;

/// <summary>Alinha o contorno das letras ao centro da moldura do botão.</summary>
public sealed class MenuButtonLabel : Text
{
    public static MenuButtonLabel Criar(RectTransform botao, string rotulo, int tamanho, Color cor)
    {
        var objeto = new GameObject(rotulo, typeof(RectTransform));
        var rect = objeto.GetComponent<RectTransform>();
        rect.SetParent(botao, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        var label = objeto.AddComponent<MenuButtonLabel>();
        label.text = rotulo;
        label.font = EstiloUI.Fonte;
        label.fontSize = tamanho;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.color = cor;
        label.raycastTarget = false;
        return label;
    }

    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        base.OnPopulateMesh(vertices);
        int total = vertices.currentVertCount;
        Vector2 minimo = Vector2.positiveInfinity;
        Vector2 maximo = Vector2.negativeInfinity;
        UIVertex vertice = default;

        // A primeira e a terceira posição são cantos opostos de cada glifo.
        for (int i = 0; i + 3 < total; i += 4)
        {
            vertices.PopulateUIVertex(ref vertice, i);
            Vector2 canto = vertice.position;
            vertices.PopulateUIVertex(ref vertice, i + 2);
            Vector2 oposto = vertice.position;
            if (canto.x == oposto.x || canto.y == oposto.y) continue;
            minimo = Vector2.Min(minimo, Vector2.Min(canto, oposto));
            maximo = Vector2.Max(maximo, Vector2.Max(canto, oposto));
        }

        if (minimo.x > maximo.x) return;
        // Compensa a linha de base e os acentos antes de a sombra ser aplicada.
        Vector3 deslocamento = rectTransform.rect.center - (minimo + maximo) * 0.5f;
        for (int i = 0; i < total; i++)
        {
            vertices.PopulateUIVertex(ref vertice, i);
            vertice.position += deslocamento;
            vertices.SetUIVertex(vertice, i);
        }
    }
}
