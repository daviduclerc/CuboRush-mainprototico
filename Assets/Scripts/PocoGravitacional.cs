using UnityEngine;

/// <summary>
/// Poço gravitacional aberto pelo Sol ao abaixar: o piso afunda em funil ao redor dele,
/// como nos desenhos de buraco negro, e uma onda percorre o espaço quando ele entra e sai.
/// A mesma conta roda na GPU (Resources/Space/PocoGravitacional.hlsl) para o piso e as
/// linhas neon, e aqui na CPU para o Sol e as paredes (DeformacaoPeloPoco).
/// </summary>
public static class PocoGravitacional
{
    const float DuracaoOnda = 0.85f;
    const float VelocidadeOnda = 9f;
    const float LarguraOnda = 0.6f;

    /// <summary>Centro do funil no plano XZ (x, z).</summary>
    public static Vector2 Centro { get; private set; }
    /// <summary>Profundidade atual do funil em unidades de mundo.</summary>
    public static float Profundidade { get; private set; }
    /// <summary>0 = espaço plano, 1 = poço totalmente aberto.</summary>
    public static float Intensidade { get; private set; }
    public static float Nucleo { get; private set; } = 1.6f;
    public static float Alcance { get; private set; } = 5f;
    public static float MeiaLargura { get; private set; } = 5f;

    public static Vector2 CentroOnda { get; private set; }
    public static float RaioOnda { get; private set; }
    public static float AmplitudeOnda { get; private set; }

    static float idadeOnda = -1f;
    static float amplitudeInicialOnda;
    static float intensidadeAnterior;

    /// <summary>Há alguma curvatura no espaço neste quadro?</summary>
    public static bool Ativo => Profundidade > 0.0005f || Mathf.Abs(AmplitudeOnda) > 0.0005f ||
        CampoGravitacional.OndaAtiva || CampoGravitacional.BuracoAtivo;

    static readonly int idCentro = Shader.PropertyToID("_PocoCentro");
    static readonly int idForma = Shader.PropertyToID("_PocoForma");
    static readonly int idOnda = Shader.PropertyToID("_PocoOnda");
    static readonly int idOndaForma = Shader.PropertyToID("_PocoOndaForma");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reiniciar()
    {
        Profundidade = Intensidade = AmplitudeOnda = RaioOnda = 0f;
        idadeOnda = -1f;
        intensidadeAnterior = 0f;
        Publicar();
    }

    /// <summary>Chamado pelo jogador uma vez por quadro, já com a posição nova.</summary>
    public static void Atualizar(Vector2 centro, float intensidade, float profundidadeMaxima,
        float nucleo, float alcance, float meiaLargura, float amplitudeOnda, float dt)
    {
        Centro = centro;
        Nucleo = Mathf.Max(0.05f, nucleo);
        Alcance = Mathf.Max(0.1f, alcance);
        MeiaLargura = meiaLargura;

        // Uma onda forte quando o Sol rompe o espaço e outra menor quando ele sai.
        if (intensidade > 0.05f && intensidadeAnterior <= 0.05f) DispararOnda(centro, amplitudeOnda);
        else if (intensidade < 0.95f && intensidadeAnterior >= 0.95f) DispararOnda(centro, amplitudeOnda * 0.6f);
        intensidadeAnterior = intensidade;

        Intensidade = Mathf.Clamp01(intensidade);
        Profundidade = profundidadeMaxima * Intensidade;

        if (idadeOnda >= 0f)
        {
            idadeOnda += dt;
            float t = idadeOnda / DuracaoOnda;
            if (t >= 1f)
            {
                idadeOnda = -1f;
                AmplitudeOnda = 0f;
            }
            else
            {
                RaioOnda = Nucleo + VelocidadeOnda * idadeOnda;
                AmplitudeOnda = amplitudeInicialOnda * (1f - t) * (1f - t);
            }
        }
        Publicar();
    }

    static void DispararOnda(Vector2 centro, float amplitude)
    {
        CentroOnda = centro;
        idadeOnda = 0f;
        // Nasce na borda do fundo do funil, para não empurrar o Sol para cima.
        RaioOnda = Nucleo;
        amplitudeInicialOnda = amplitude;
        AmplitudeOnda = amplitude;
    }

    static void Publicar()
    {
        Shader.SetGlobalVector(idCentro, new Vector4(Centro.x, Centro.y, Profundidade, Nucleo));
        Shader.SetGlobalVector(idForma, new Vector4(Alcance, MeiaLargura, Intensidade, 0f));
        Shader.SetGlobalVector(idOnda, new Vector4(CentroOnda.x, CentroOnda.y, RaioOnda, AmplitudeOnda));
        Shader.SetGlobalVector(idOndaForma, new Vector4(LarguraOnda, 0f, 0f, 0f));
    }

    /// <summary>Quanto o espaço afunda (para baixo) no ponto (x, z). Igual ao shader.</summary>
    public static float Deslocamento(float x, float z)
    {
        float dx = x - Centro.x, dz = z - Centro.y;
        float r = Mathf.Sqrt(dx * dx + dz * dz);
        float funil = Profundidade / (1f + (r * r) / (Nucleo * Nucleo));
        funil *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Alcance * 0.5f, Alcance, r));

        float ox = x - CentroOnda.x, oz = z - CentroOnda.y;
        float u = (Mathf.Sqrt(ox * ox + oz * oz) - RaioOnda) / LarguraOnda;
        float onda = AmplitudeOnda * 2f * u * Mathf.Exp(-u * u);

        float fixacao = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(MeiaLargura - 0.9f, MeiaLargura, Mathf.Abs(x)));
        // A onda gravitacional plana e o mini buraco negro somam aqui; a curvatura do
        // horizonte é aplicada por fora (CampoGravitacional.Queda), pois dobra a pista inteira.
        return (funil + onda + CampoGravitacional.DeslocamentoLocal(x, z)) * fixacao;
    }

    /// <summary>O poço ou a onda chegam perto de algo em "posicao" com este raio no plano?</summary>
    public static bool Alcanca(Vector3 posicao, float raio)
    {
        if (CampoGravitacional.Alcanca(posicao, raio)) return true;
        if (Profundidade > 0.0005f)
        {
            float dx = posicao.x - Centro.x, dz = posicao.z - Centro.y;
            if (dx * dx + dz * dz < (Alcance + raio) * (Alcance + raio)) return true;
        }
        if (Mathf.Abs(AmplitudeOnda) > 0.0005f)
        {
            float ox = posicao.x - CentroOnda.x, oz = posicao.z - CentroOnda.y;
            float d = Mathf.Sqrt(ox * ox + oz * oz);
            float faixa = LarguraOnda * 3f + raio;
            if (d > RaioOnda - faixa && d < RaioOnda + faixa) return true;
        }
        return false;
    }
}
