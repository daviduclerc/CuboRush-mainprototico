using UnityEngine;

/// <summary>
/// Fenômenos gravitacionais que curvam a pista, além do poço aberto pelo Sol:
///  - Curvatura do horizonte: a atração do buraco negro dobra a pista para baixo lá na frente.
///    Começa em "InicioCurva" unidades à frente do jogador, então perto dele tudo é plano.
///  - Onda gravitacional plana: uma crista que vem do buraco negro em direção ao jogador.
///  - Mini buraco negro: um funil fundo em volta de um ponto da pista.
/// A mesma conta existe na GPU (Resources/Space/PocoGravitacional.hlsl).
/// A onda e o funil entram em PocoGravitacional.Deslocamento (são locais e presos às vigas);
/// a curvatura é aplicada por fora, porque dobra também as vigas e tudo o que está na pista.
/// </summary>
public static class CampoGravitacional
{
    public const float InicioCurva = 18f;
    public const float OndaLargura = 1.1f;
    public const float BuracoNucleo = 0.85f;
    public const float BuracoAlcance = 3.6f;

    /// <summary>Z do jogador: a curva é medida a partir dele.</summary>
    public static float ZReferencia { get; private set; }
    /// <summary>Intensidade da curva (k em queda = k · distância²).</summary>
    public static float Curvatura { get; private set; }

    public static float OndaFrente { get; private set; }
    public static float OndaAmplitude { get; private set; }
    public static bool OndaAtiva => Mathf.Abs(OndaAmplitude) > 0.0005f;

    public static Vector2 BuracoCentro { get; private set; }
    public static float BuracoProfundidade { get; private set; }
    public static bool BuracoAtivo => BuracoProfundidade > 0.0005f;

    static readonly int idCurvatura = Shader.PropertyToID("_CampoCurvatura");
    static readonly int idOnda = Shader.PropertyToID("_CampoOnda");
    static readonly int idBuraco = Shader.PropertyToID("_CampoBuraco");
    static readonly int idBuracoForma = Shader.PropertyToID("_CampoBuracoForma");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reiniciar()
    {
        ZReferencia = Curvatura = 0f;
        OndaFrente = OndaAmplitude = 0f;
        BuracoCentro = Vector2.zero;
        BuracoProfundidade = 0f;
        Publicar();
    }

    public static void DefinirCurvatura(float zReferencia, float curvatura)
    {
        ZReferencia = zReferencia;
        Curvatura = Mathf.Max(0f, curvatura);
        Publicar();
    }

    public static void DefinirOnda(float frente, float amplitude)
    {
        OndaFrente = frente;
        OndaAmplitude = amplitude;
        Publicar();
    }

    public static void DefinirBuraco(Vector2 centro, float profundidade)
    {
        BuracoCentro = centro;
        BuracoProfundidade = Mathf.Max(0f, profundidade);
        Publicar();
    }

    static void Publicar()
    {
        Shader.SetGlobalVector(idCurvatura, new Vector4(ZReferencia, InicioCurva, Curvatura, 0f));
        Shader.SetGlobalVector(idOnda, new Vector4(OndaFrente, OndaAmplitude, OndaLargura, 0f));
        Shader.SetGlobalVector(idBuraco, new Vector4(BuracoCentro.x, BuracoCentro.y, BuracoProfundidade, BuracoNucleo));
        Shader.SetGlobalVector(idBuracoForma, new Vector4(BuracoAlcance, 0f, 0f, 0f));
    }

    // ---------- Contas (iguais às do shader) ----------

    /// <summary>Quanto a pista desce no ponto z por causa da atração do buraco negro.</summary>
    public static float Queda(float z)
    {
        float d = Mathf.Max(0f, z - ZReferencia - InicioCurva);
        return Curvatura * d * d;
    }

    /// <summary>Inclinação da pista (graus) no ponto z: positiva = descendo para a frente.</summary>
    public static float Inclinacao(float z)
    {
        float d = Mathf.Max(0f, z - ZReferencia - InicioCurva);
        return Mathf.Atan(2f * Curvatura * d) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// Deslocamento (para baixo) da onda plana em z: uma crista que sobe na frente
    /// e um vale raso logo atrás dela.
    /// </summary>
    public static float OndaPlana(float z)
    {
        if (!OndaAtiva) return 0f;
        float u = (z - OndaFrente) / OndaLargura;
        float v = u - 1.8f;
        return OndaAmplitude * (-Mathf.Exp(-u * u) + 0.4f * Mathf.Exp(-v * v));
    }

    /// <summary>Profundidade do funil do mini buraco negro no ponto (x, z).</summary>
    public static float Buraco(float x, float z)
    {
        if (!BuracoAtivo) return 0f;
        float dx = x - BuracoCentro.x, dz = z - BuracoCentro.y;
        float r2 = dx * dx + dz * dz;
        float funil = BuracoProfundidade / (1f + r2 / (BuracoNucleo * BuracoNucleo));
        float r = Mathf.Sqrt(r2);
        return funil * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(BuracoAlcance * 0.5f, BuracoAlcance, r)));
    }

    /// <summary>Soma da onda e do funil (a curvatura do horizonte fica de fora).</summary>
    public static float DeslocamentoLocal(float x, float z) => OndaPlana(z) + Buraco(x, z);

    /// <summary>A onda ou o funil chegam perto de algo em "posicao" com este raio no plano?</summary>
    public static bool Alcanca(Vector3 posicao, float raio)
    {
        if (OndaAtiva && Mathf.Abs(posicao.z - OndaFrente) < OndaLargura * 4.5f + raio) return true;
        if (BuracoAtivo)
        {
            float dx = posicao.x - BuracoCentro.x, dz = posicao.z - BuracoCentro.y;
            float alcance = BuracoAlcance + raio;
            if (dx * dx + dz * dz < alcance * alcance) return true;
        }
        return false;
    }
}
