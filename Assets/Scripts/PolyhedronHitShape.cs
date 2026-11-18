using UnityEngine;

/// <summary>Colisão convexa do prisma, sem as áreas vazias da caixa ao redor.</summary>
public sealed class PolyhedronHitShape : MonoBehaviour
{
    Vector3[] locais;
    Vector3[] mundo;
    int lados;
    public int QuantidadeLados => lados;
    Renderer visual;

    public Bounds Limites => visual != null ? visual.bounds : new Bounds(transform.position, Vector3.zero);

    public void Inicializar(int quantidadeLados)
    {
        lados = quantidadeLados;
        locais = new Vector3[lados * 2];
        mundo = new Vector3[locais.Length];
        Vector2 minimo = Vector2.one * float.PositiveInfinity;
        Vector2 maximo = Vector2.one * float.NegativeInfinity;
        for (int i = 0; i < lados; i++)
        {
            float a = (90f + i * 360f / lados) * Mathf.Deg2Rad;
            Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            locais[i] = p;
            minimo = Vector2.Min(minimo, p);
            maximo = Vector2.Max(maximo, p);
        }
        for (int i = 0; i < lados; i++)
        {
            Vector3 p = locais[i];
            p.x = (p.x - minimo.x) / (maximo.x - minimo.x) - 0.5f;
            p.y = (p.y - minimo.y) / (maximo.y - minimo.y) - 0.5f;
            p.z = -0.5f;
            locais[i] = p;
            locais[i + lados] = p + Vector3.forward;
        }
        visual = GetComponent<Renderer>();
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
