using UnityEngine;

/// <summary>Colisão convexa do prisma, sem as áreas vazias da caixa ao redor.</summary>
public sealed class PolyhedronHitShape : MonoBehaviour
{
    Vector3[] locais;
    Vector3[] mundo;
    int lados, ladosPedidos;
    /// <summary>Número de lados pedido na criação (identifica a malha compartilhada).</summary>
    public int QuantidadeLados => ladosPedidos;
    public FormaObstaculo Forma { get; private set; }
    /// <summary>Cor do vidro e do neon (usada para refazer o material ao retomar da pausa).</summary>
    public Color CorVisual { get; set; } = Color.white;

    /// <summary>
    /// Caixa do prisma lógico. Vem do contorno, e não da malha desenhada, para a
    /// curvatura visual do poço gravitacional não mexer na colisão.
    /// </summary>
    public Bounds Limites
    {
        get
        {
            if (locais == null) return new Bounds(transform.position, Vector3.zero);
            var caixa = new Bounds(transform.TransformPoint(locais[0]), Vector3.zero);
            for (int i = 1; i < locais.Length; i++) caixa.Encapsulate(transform.TransformPoint(locais[i]));
            return caixa;
        }
    }

    public void Inicializar(int quantidadeLados, FormaObstaculo forma = FormaObstaculo.Regular)
    {
        Forma = forma;
        ladosPedidos = quantidadeLados;
        Vector3[] contorno = ContornoObstaculo.Obter(forma, quantidadeLados);
        lados = contorno.Length;
        locais = new Vector3[lados * 2];
        mundo = new Vector3[locais.Length];
        for (int i = 0; i < lados; i++)
        {
            Vector3 p = contorno[i];
            p.z = -0.5f;
            locais[i] = p;
            locais[i + lados] = p + Vector3.forward;
        }
    }

    public bool Intersects(Bounds caixa)
    {
        if (locais == null || !Limites.Intersects(caixa)) return false;
        for (int i = 0; i < locais.Length; i++) mundo[i] = transform.TransformPoint(locais[i]);
        Vector3 profundidade = mundo[lados] - mundo[0];
        if (Separados(Vector3.right, caixa) || Separados(Vector3.up, caixa) || Separados(Vector3.forward, caixa)) return false;
        Vector3 borda0 = mundo[1] - mundo[0];
        Vector3 borda1 = mundo[2] - mundo[1];
        if (Separados(Vector3.Cross(borda0, borda1), caixa)) return false;
        for (int i = 0; i < lados; i++)
        {
            Vector3 borda = mundo[(i + 1) % lados] - mundo[i];
            if (Separados(Vector3.Cross(borda, profundidade), caixa) || SeparadoPorAresta(borda, caixa)) return false;
        }
        return !SeparadoPorAresta(profundidade, caixa);
    }

    bool SeparadoPorAresta(Vector3 aresta, Bounds caixa)
    {
        return Separados(Vector3.Cross(aresta, Vector3.right), caixa)
            || Separados(Vector3.Cross(aresta, Vector3.up), caixa)
            || Separados(Vector3.Cross(aresta, Vector3.forward), caixa);
    }

    bool Separados(Vector3 eixo, Bounds caixa)
    {
        if (eixo.sqrMagnitude < 0.000001f) return false;
        eixo.Normalize();
        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        for (int i = 0; i < mundo.Length; i++)
        {
            float projecao = Vector3.Dot(mundo[i], eixo);
            min = Mathf.Min(min, projecao);
            max = Mathf.Max(max, projecao);
        }
        float centro = Vector3.Dot(caixa.center, eixo);
        Vector3 e = caixa.extents;
        float raio = Mathf.Abs(eixo.x) * e.x + Mathf.Abs(eixo.y) * e.y + Mathf.Abs(eixo.z) * e.z;
        return max < centro - raio || min > centro + raio;
    }
}
