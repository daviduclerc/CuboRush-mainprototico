using UnityEngine;

/// <summary>
/// Efeitos da gravidade do buraco negro ao longo da corrida:
///  - Atração crescente: com o tempo de partida a pista se curva para baixo lá na frente
///    e a gravidade sobre o Sol aumenta (pulos mais curtos e mais rápidos).
///  - Ondas gravitacionais: uma crista vem do buraco negro pela pista. Quem estiver no ar
///    (pulando) ou com o poço aberto (agachado) passa ileso e ganha pontos; quem estiver
///    parado no chão leva dano.
///  - Ímã gravitacional: um anel de acreção em volta do Sol enquanto o ímã estiver ativo.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class FenomenosGravitacionais : MonoBehaviour
{
    public PlayerController jogador;

    [Header("Atração do buraco negro")]
    [Tooltip("Segundos de partida até a atração chegar ao máximo.")]
    public float tempoAtracaoMaxima = 160f;
    [Tooltip("Curvatura máxima da pista (queda = k · distância²).")]
    public float curvaturaMaxima = 0.0016f;
    [Tooltip("Quanto a gravidade do pulo aumenta na atração máxima (0,35 = 35%).")]
    public float ganhoGravidade = 0.35f;

    [Header("Ondas gravitacionais")]
    public float primeiraOnda = 30f;
    [Tooltip("Intervalo entre ondas no começo e com atração máxima (segundos).")]
    public Vector2 intervaloOndas = new Vector2(18f, 9f);
    public float velocidadeOnda = 16f;
    public float amplitudeOnda = 0.5f;
    public float danoOnda = 12f;
    public int pontosOnda = 30;

    /// <summary>0 no começo da partida, 1 na atração máxima.</summary>
    public static float Atracao { get; private set; }

    static readonly Color CorOnda = new Color(0.45f, 0.9f, 1f);
    static readonly Color CorAtracao = new Color(1f, 0.6f, 0.25f);
    static readonly Color CorIma = new Color(0.3f, 1f, 0.45f);

    float proximaOnda;
    bool ondaViva, ondaResolvida;
    float frenteOnda, amplitudeAtual;
    int ondasAvisadas;
    int marcoAtracao;
    LineRenderer anelIma;
    float giroAnel;

    void Start()
    {
        Atracao = 0f;
        CampoGravitacional.Reiniciar();
        proximaOnda = primeiraOnda;
        CriarAnelIma();
    }

    void Update()
    {
        GameManager jogo = GameManager.Instancia;
        if (jogo == null || jogador == null) return;
        bool jogando = jogo.Estado == EstadoJogo.Jogando;
        float dt = jogando ? Time.deltaTime : 0f;
        float zJogador = jogador.transform.position.z;

        // ----- Atração crescente -----
        if (jogando)
        {
            float progresso = Mathf.Clamp01((jogo.TempoDeJogo - 8f) / Mathf.Max(1f, tempoAtracaoMaxima));
            Atracao = Mathf.SmoothStep(0f, 1f, progresso);
            AvisarMarcos();
        }
        CampoGravitacional.DefinirCurvatura(zJogador, curvaturaMaxima * Atracao);
        jogador.multiplicadorGravidade = 1f + ganhoGravidade * Atracao;

        // ----- Ondas gravitacionais -----
        if (jogando && !ondaViva && jogo.TempoDeJogo >= proximaOnda) IniciarOnda(jogo, zJogador);
        if (ondaViva)
        {
            if (jogando)
            {
                frenteOnda -= velocidadeOnda * dt;
                bool passou = frenteOnda < zJogador - 10f;
                amplitudeAtual = Mathf.MoveTowards(amplitudeAtual, passou ? 0f : amplitudeOnda, dt * 1.4f);
                if (!ondaResolvida && frenteOnda <= zJogador) ResolverOnda(jogo);
                if (passou && amplitudeAtual <= 0f) ondaViva = false;
            }
            else if (jogo.Estado == EstadoJogo.FimDeJogo)
            {
                // O Sol se desfez: a onda se dissipa.
                amplitudeAtual = Mathf.MoveTowards(amplitudeAtual, 0f, Time.deltaTime * 1.5f);
                if (amplitudeAtual <= 0f) ondaViva = false;
            }
        }
        CampoGravitacional.DefinirOnda(frenteOnda, ondaViva ? amplitudeAtual : 0f);

        AtualizarAnelIma(jogo);
    }

    void AvisarMarcos()
    {
        int marco = Atracao >= 0.95f ? 3 : (Atracao >= 0.66f ? 2 : (Atracao >= 0.33f ? 1 : 0));
        if (marco <= marcoAtracao) return;
        marcoAtracao = marco;
        string texto = marco == 3 ? "ATRAÇÃO MÁXIMA DO BURACO NEGRO" : "A ATRAÇÃO DO BURACO NEGRO AUMENTA";
        GameplayHud.Avisar(texto, "a pista se curva e os pulos ficam mais curtos", CorAtracao);
        CameraFollow.Tremer(0.35f, 0.18f);
    }

    void IniciarOnda(GameManager jogo, float zJogador)
    {
        ondaViva = true;
        ondaResolvida = false;
        // Nasce perto do buraco negro, no fim do trecho visível da pista.
        frenteOnda = zJogador + 95f;
        amplitudeAtual = 0f;
        float intervalo = Mathf.Lerp(intervaloOndas.x, intervaloOndas.y, Atracao);
        proximaOnda = jogo.TempoDeJogo + intervalo + Random.Range(0f, 5f);

        if (ondasAvisadas < 2) GameplayHud.Avisar("ONDA GRAVITACIONAL!", "pule ou agache quando ela chegar", CorOnda);
        else GameplayHud.Avisar("ONDA GRAVITACIONAL!", null, CorOnda);
        ondasAvisadas++;
        jogo.SomGravitacional();
    }

    void ResolverOnda(GameManager jogo)
    {
        ondaResolvida = true;
        if (jogo.Estado != EstadoJogo.Jogando) return;
        // No ar ou com o poço aberto, a crista passa por baixo sem machucar.
        bool protegido = !jogador.NoChao || jogador.Afundamento > 0.5f;
        if (protegido)
        {
            int pontos = jogo.BonusGravitacional(pontosOnda);
            GameplayHud.Avisar("ONDA SURFADA  +" + pontos, null, CorOnda);
            return;
        }
        if (jogador.Invencivel) return;
        if (jogo.ConsumirEscudo())
        {
            jogador.FicarInvencivel(1f);
            CameraFollow.Tremer(0.2f, 0.25f);
            GameplayHud.Avisar("O ESCUDO SEGUROU A ONDA", null, EstiloUI.Ciano);
            return;
        }
        if (jogador.ReceberDano(danoOnda))
        {
            CameraFollow.Tremer(0.32f, 0.45f);
            GameplayHud.Avisar("ATINGIDO PELA ONDA", null, EstiloUI.Perigo);
        }
    }

    // ---------- Anel do ímã gravitacional ----------

    void CriarAnelIma()
    {
        var objeto = new GameObject("AnelDoImaGravitacional");
        objeto.transform.SetParent(transform, false);
        anelIma = objeto.AddComponent<LineRenderer>();
        anelIma.useWorldSpace = false;
        anelIma.loop = true;
        const int pontos = 48;
        anelIma.positionCount = pontos;
        for (int i = 0; i < pontos; i++)
        {
            float a = i * Mathf.PI * 2f / pontos;
            anelIma.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
        }
        anelIma.widthMultiplier = 0.05f;
        anelIma.sharedMaterial = Visual.Mat(CorIma, true);
        anelIma.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        anelIma.receiveShadows = false;
        anelIma.enabled = false;
    }

    void AtualizarAnelIma(GameManager jogo)
    {
        if (anelIma == null) return;
        bool ativo = jogo.TempoIma > 0f && jogo.Estado != EstadoJogo.FimDeJogo;
        // Pisca no último segundo e meio para avisar que vai acabar.
        if (ativo && jogo.TempoIma < 1.5f) ativo = Mathf.FloorToInt(Time.time * 8f) % 2 == 0;
        anelIma.enabled = ativo;
        if (!ativo) return;
        if (jogo.Estado == EstadoJogo.Jogando) giroAnel += 220f * Time.deltaTime;
        Transform t = anelIma.transform;
        t.position = jogador.Limites.center;
        t.rotation = Quaternion.Euler(14f, 0f, 9f) * Quaternion.Euler(0f, giroAnel, 0f);
        float raio = 1.05f + 0.08f * Mathf.Sin(Time.time * 5f);
        t.localScale = Vector3.one * raio;
    }

    void OnDestroy()
    {
        Atracao = 0f;
        CampoGravitacional.Reiniciar();
    }
}
