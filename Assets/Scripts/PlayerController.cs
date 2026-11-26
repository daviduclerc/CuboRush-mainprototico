using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controla o personagem: avança sempre para frente (na velocidade do GameManager),
/// rola de lado para trocar entre as 5 faixas, pula, desliza e troca de cor.
///
/// Teclado: A/D ou setas (faixa), W/seta cima/Espaço (pulo), S/seta baixo (deslize),
///          E avança e Q volta a cor; clique esquerdo também avança.
/// Celular: deslizar o dedo (faixa, pulo, deslize) e toque rápido (cor).
/// </summary>
public class PlayerController : MonoBehaviour
{
    public const float VidaMaxima = 100f;
    public const float TempoPreparoPulo = 0.075f;
    public const int CorDourada = 3;
    const float DuracaoInvulnerabilidadeDano = 1.8f;
    static readonly Color TomSolarDourado = new Color(1f, 0.52f, 0.09f);
    /// <summary>Quatro cores que identificam o Sol, as moedas e as paredes.</summary>
    public static readonly Color[] Cores =
    {
        new Color(1f, 0.035f, 0.045f), // 0 - vermelho
        new Color(0.04f, 1f, 0.10f),   // 1 - verde
        new Color(0.06f, 0.24f, 1f),   // 2 - azul
        new Color(1f, 0.78f, 0.025f)   // 3 - amarelo
    };

    [Header("Faixas")]
    public int quantidadeFaixas = 5;
    public float larguraFaixa = 2f;
    [Tooltip("Velocidade lateral ao trocar de faixa.")]
    public float velocidadeTroca = 12f;

    [Header("Pulo")]
    public float forcaPulo = 10f;
    public float gravidade = 28f;
    [Tooltip("Velocidade de descida ao apertar para baixo no ar.")]
    public float quedaRapida = 22f;

    [Header("Deslize")]
    public float duracaoDeslize = 0.7f;

    [Header("Toque")]
    [Tooltip("Distância mínima (em pixels) para contar como deslize do dedo.")]
    public float deslizeMinimo = 50f;

    /// <summary>Faixa atual, de 0 (esquerda) até quantidadeFaixas - 1 (direita).</summary>
    public int FaixaAtual { get; private set; }
    /// <summary>Índice da cor atual dentro de "Cores".</summary>
    public int CorId { get; private set; } = CorDourada;
    public Color CorAtual => Cores[CorId];
    public float Vida { get; private set; } = VidaMaxima;
    float NivelSolar => Mathf.Min(Vida, GameManager.Instancia != null ? GameManager.Instancia.Estabilidade : 100f);
    public Color CorVisualAtual
    {
        get
        {
            float perigo = NivelSolar;
            float faseDano = Mathf.InverseLerp(100f, 0f, perigo);
            Color corBase = CorId == CorDourada ? TomSolarDourado : CorAtual;
            Color cor = Color.Lerp(corBase, new Color(1f, 0.025f, 0.035f), faseDano * 0.72f);
            if (perigo <= 30f)
                cor *= 0.86f + 0.14f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f));
            return cor;
        }
    }
    public Transform VisualSolar => visualSolarRaiz;
    public bool Deslizando { get; private set; }
    public float ProporcaoAbaixado => 1f - achatamento * 0.30f;
    public bool NoChao => altura <= 0f && velocidadeY <= 0f;
    public float VelocidadeVertical => velocidadeY;
    public bool Invencivel => Time.time < invencivelAte;

    /// <summary>Altura máxima do pulo (usada para posicionar moedas em arco).</summary>
    public float AlturaMaximaPulo => forcaPulo * forcaPulo / (2f * gravidade);
    /// <summary>Tempo total no ar em um pulo.</summary>
    public float TempoNoAr => 2f * forcaPulo / gravidade;

    float AlturaCorpo => ProporcaoAbaixado;

    /// <summary>Caixa lógica do personagem, independente do tamanho do modelo visual.</summary>
    public Bounds Limites => new Bounds(
        new Vector3(transform.position.x, altura + AlturaCorpo / 2f, transform.position.z),
        new Vector3(0.8f * ProporcaoAbaixado, AlturaCorpo * 0.9f, 0.8f * ProporcaoAbaixado));

    float altura;          // altura lógica do personagem em relação à pista
    float velocidadeY;
    float tempoDeslize;
    float esperaPulo;      // guarda o comando de pulo por um instante
    bool deslizarAoPousar;
    float giro;
    float achatamento;     // 0 = normal, 1 = achatado durante o deslize
    float giroDeslize;
    float rolamentoLateral;
    float giroSolar;
    float raioSolarBase = 0.24f;


    float velocidadeLateralSuavizada;
    float tempoDecolagem;
    float tempoSquash = -1f;
    float preparacaoPulo;
    bool preparandoPulo;
    Vector3 posicaoAnterior;
    float invencivelAte;

    Renderer visual;
    Transform visualSolarRaiz;
    Transform deformacaoSolar;
    Vector3 escalaSolarOriginal;
    ColorWheelUI rodaCores;
    Renderer[] visuais;
    MaterialPropertyBlock blocoDePropriedades;
    [HideInInspector] public bool visualSolar;
    Transform orbeEscudo;
    Vector2 inicioToque;
    bool toqueValido, toqueUsado;
    bool morto;

    void Start()
    {
        visuais = GetComponentsInChildren<Renderer>(true);
        if (visualSolar)
        {
            visualSolarRaiz = transform.Find("VisualDoSol");
            if (visualSolarRaiz != null)
            {
                escalaSolarOriginal = visualSolarRaiz.localScale;
                Transform superficie = visualSolarRaiz.Find("Fotosfera");
                MeshFilter filtroSolar = superficie != null ? superficie.GetComponent<MeshFilter>() : null;
                if (filtroSolar != null && filtroSolar.sharedMesh != null)
                    raioSolarBase = filtroSolar.sharedMesh.bounds.extents.y * superficie.localScale.y * escalaSolarOriginal.y;
                // Separa o achatamento vertical da rotação da esfera. Assim,
                // rolar para os lados não vira um achatamento lateral.
                Vector3 centroVisual = visualSolarRaiz.localPosition;
                deformacaoSolar = new GameObject("DeformacaoVerticalDoSol").transform;
                deformacaoSolar.SetParent(transform, false);
                deformacaoSolar.localPosition = centroVisual;
                visualSolarRaiz.SetParent(deformacaoSolar, false);
                visualSolarRaiz.localPosition = Vector3.zero;
            }
        }
        visual = GetComponent<Renderer>();
        if (visual == null && visuais.Length > 0) visual = visuais[0];
        if (!visualSolar && visual != null)
            visual.sharedMaterial = Visual.MatJogador(CorAtual);
        else if (visualSolar)
            AtualizarBrilhoSolar();

        FaixaAtual = quantidadeFaixas / 2;
        Vida = VidaMaxima;
        Vector3 p = transform.position;
        p.x = PosicaoXDaFaixa(FaixaAtual);
        p.y = 0.5f;
        transform.position = p;
        posicaoAnterior = p;

        // Bolinha que gira em volta do cubo enquanto houver escudo.
        orbeEscudo = Visual.Forma(PrimitiveType.Sphere, "OrbeEscudo", null,
            Vector3.zero, Vector3.one * 0.3f, new Color(0.75f, 0.95f, 1f), true).transform;
        orbeEscudo.gameObject.SetActive(false);
        if (visualSolar && GetComponent<SolarMotionFlames>() == null)
            gameObject.AddComponent<SolarMotionFlames>();
        rodaCores = GetComponent<ColorWheelUI>();
        if (rodaCores != null) { rodaCores.Cancelar(); Destroy(rodaCores); rodaCores = null; }

    }

    void Update()
    {
        GameManager jogo = GameManager.Instancia;
        if (jogo == null || jogo.Estado != EstadoJogo.Jogando || Time.deltaTime <= 0f) return;

        // Ignora o primeiro instante para o comando que inicia o jogo não virar ação.
        if (jogo.TempoDeJogo > 0.1f) LerEntrada(jogo);

        Vector3 p = transform.position;

        // Sempre para frente.
        p.z += jogo.Velocidade * Time.deltaTime;

        // Troca de faixa.
        float alvoX = PosicaoXDaFaixa(FaixaAtual);
        p.x = Mathf.SmoothDamp(p.x, alvoX, ref velocidadeLateralSuavizada,
            0.08f, velocidadeTroca * 2.5f, Time.deltaTime);
        if (Mathf.Abs(p.x - alvoX) < 0.001f)
        {
            p.x = alvoX;
            velocidadeLateralSuavizada = 0f;
        }

        // Pulo.
        if (esperaPulo > 0f && !preparandoPulo) esperaPulo -= Time.deltaTime;
        if (NoChao && esperaPulo > 0f)
        {
            if (!preparandoPulo)
            {
                preparandoPulo = true;
                preparacaoPulo = TempoPreparoPulo;
            }
            preparacaoPulo -= Time.deltaTime;
            if (preparacaoPulo <= 0f)
            {
                preparandoPulo = false;
                esperaPulo = 0f;
                velocidadeY = forcaPulo;
                Deslizando = false;
                deslizarAoPousar = false;
                tempoDecolagem = 0.001f;
                jogo.SomDePulo();
            }
        }

        if (!NoChao)
        {
            velocidadeY -= gravidade * Time.deltaTime;
            altura += velocidadeY * Time.deltaTime;
            giro += 360f / TempoNoAr * Time.deltaTime;

            if (altura <= 0f)
            {
                altura = 0f;
                velocidadeY = 0f;
                giro = 0f;
                tempoSquash = 0f;
                if (deslizarAoPousar) IniciarDeslize();
            }
        }

        // Deslize.
        if (Deslizando)
        {
            tempoDeslize -= Time.deltaTime;
            if (tempoDeslize <= 0f) Deslizando = false;
        }

        // No chão, o Sol se esparrama um pouco. Ao decolar, abre para os lados,
        // comprime no eixo vertical e recupera a forma esférica.
        achatamento = Mathf.MoveTowards(achatamento, Deslizando ? 1f : 0f, 9f * Time.deltaTime);

        if (Deslizando)
        {
            giroDeslize += 900f * Time.deltaTime;
        }
        else if (giroDeslize != 0f)
        {
            // Termina a volta no próximo "encaixe" de 90° para não dar tranco.
            float encaixe = Mathf.Ceil(giroDeslize / 90f) * 90f;
            giroDeslize = Mathf.MoveTowards(giroDeslize, encaixe, 900f * Time.deltaTime);
            if (Mathf.Approximately(giroDeslize, encaixe) && achatamento <= 0f) giroDeslize = 0f;
        }

        // Ao abaixar, o Sol encolhe por igual e volta suavemente ao tamanho base.
        // As deformações dos eixos ficam reservadas para o preparo e o impulso do pulo.
        float escalaAbaixado = 1f - achatamento * 0.30f;
        float squashLateral = preparandoPulo ? 0.22f : 0f;
        float squashVertical = preparandoPulo ? 0.18f : 0f;
        if (!NoChao && tempoDecolagem > 0f)
        {
            tempoDecolagem += Time.deltaTime;
            float t = tempoDecolagem;
            if (t < 0.14f)
            {
                float fase = Mathf.Sin(t / 0.14f * Mathf.PI);
                squashLateral = fase * 0.24f;
                squashVertical = fase * 0.10f;
            }
            else if (t < 0.31f)
            {
                float fase = Mathf.Sin((t - 0.14f) / 0.17f * Mathf.PI);
                squashLateral = fase * 0.08f;
                squashVertical = fase * 0.25f;
            }
            else if (t < 0.49f)
            {
                float fase = Mathf.Sin((t - 0.31f) / 0.18f * Mathf.PI);
                // Recuperação sem encolher os lados: o efeito anterior fazia
                // o Sol parecer achatado quando o jogador trocava de faixa no ar.
                squashLateral = fase * 0.025f;
                squashVertical = 0f;
            }
            else tempoDecolagem = 0f;
        }

        // No pouso e na troca de faixa durante o pulo, o Sol comprime na
        // vertical, abre levemente para os lados e recupera a forma esférica.
        if (visualSolarRaiz != null && tempoSquash >= 0f)
        {
            tempoSquash += Time.deltaTime;
            float squashPouso = 0f;
            if (tempoSquash < 0.09f)
                squashPouso = Mathf.Lerp(0f, 0.30f, Mathf.SmoothStep(0f, 1f, tempoSquash / 0.09f));
            else if (tempoSquash < 0.27f)
                squashPouso = Mathf.Lerp(0.30f, -0.055f, Mathf.SmoothStep(0f, 1f, (tempoSquash - 0.09f) / 0.18f));
            else if (tempoSquash < 0.39f)
                squashPouso = Mathf.Lerp(-0.055f, 0f, Mathf.SmoothStep(0f, 1f, (tempoSquash - 0.27f) / 0.12f));
            else
                tempoSquash = -1f;

            squashVertical = Mathf.Clamp(squashPouso, -0.12f, 0.32f);
        }

        // Rola pelo eixo perpendicular ao deslocamento real. Somar giros separados
        // nos eixos X e Z criava uma torção diagonal quando avançava e trocava de faixa.
        float deslocamentoLateral = p.x - posicaoAnterior.x;

        float raioVisual = visualSolarRaiz != null ? Mathf.Max(0.18f, raioSolarBase) : 1f;
        float anguloRolagem = -deslocamentoLateral / raioVisual * Mathf.Rad2Deg;
        rolamentoLateral = Mathf.Repeat(rolamentoLateral + anguloRolagem, 360f);
        // Giro próprio constante, independente do avanço, troca de faixa ou pulo.
        giroSolar = Mathf.Repeat(giroSolar + 75f * Time.deltaTime, 360f);
        float alturaVisual = 1f - squashVertical + squashLateral * 0.32f;
        float larguraVisual = 1f + squashVertical * 0.42f + squashLateral;
        float profundidadeVisual = 1f + squashVertical * 0.42f + squashLateral;
        transform.localScale = Vector3.one;
        if (visualSolarRaiz != null)
        {
            float perigo = Mathf.Min(Vida, jogo.Estabilidade);
            float faseGigante = Mathf.InverseLerp(60f, 30f, perigo);
            float fasePulso = Mathf.InverseLerp(30f, 0f, perigo);
            float pulsoPerigo = perigo <= 30f
                ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f) : 0f;
            float escalaPerigo = 1f + 0.18f * faseGigante + 0.035f * fasePulso + 0.04f * pulsoPerigo * fasePulso;
            // O pai aplica a compressão no eixo vertical fixo da pista; a esfera
            // gira como filha sem girar junto o eixo do squash.
            deformacaoSolar.localScale = new Vector3(
                escalaPerigo * escalaAbaixado * (1f + squashVertical * 0.50f + squashLateral),
                escalaPerigo * escalaAbaixado * (1f - squashVertical),
                escalaPerigo * escalaAbaixado * (1f + squashVertical * 0.50f + squashLateral));
            visualSolarRaiz.localScale = escalaSolarOriginal;
            visualSolarRaiz.localRotation = Quaternion.AngleAxis(rolamentoLateral, Vector3.forward)
                * Quaternion.AngleAxis(giroSolar, Vector3.up);
            transform.rotation = Quaternion.identity;
            // Apoio pela fotosfera, sem oscilar com a coroa, a rotação ou a piscada.
            p.y = altura + raioSolarBase * deformacaoSolar.localScale.y - deformacaoSolar.localPosition.y;
        }
        else
        {
            p.y = altura + alturaVisual / 2f;
            transform.rotation = Quaternion.Euler(giro, giroDeslize, rolamentoLateral);
            transform.localScale = new Vector3(larguraVisual, alturaVisual, profundidadeVisual);
        }
        transform.position = p;
        posicaoAnterior = p;

        // Pisca-pisca depois de perder o escudo.
        bool visivel = !Invencivel || Mathf.FloorToInt(Time.time * 10f) % 2 == 0;
        for (int i = 0; i < visuais.Length; i++)
            if (visuais[i] != null) visuais[i].enabled = visivel;

        // Orbe do escudo.
        if (orbeEscudo.gameObject.activeSelf != jogo.TemEscudo)
            orbeEscudo.gameObject.SetActive(jogo.TemEscudo);
        if (jogo.TemEscudo)
        {
            float a = Time.time * 6f;
            orbeEscudo.position = Limites.center + new Vector3(Mathf.Cos(a), 0.15f, Mathf.Sin(a)) * 0.95f;
        }
    }

    void LerEntrada(GameManager jogo)
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Touchscreen.current == null) TrocarCor(+1);
        Keyboard t = Keyboard.current;
        if (t != null)
        {
            if (t.aKey.wasPressedThisFrame || t.leftArrowKey.wasPressedThisFrame) MudarFaixa(-1);
            if (t.dKey.wasPressedThisFrame || t.rightArrowKey.wasPressedThisFrame) MudarFaixa(+1);
            if (t.wKey.wasPressedThisFrame || t.upArrowKey.wasPressedThisFrame || t.spaceKey.wasPressedThisFrame) Pular();
            if (t.sKey.wasPressedThisFrame || t.downArrowKey.wasPressedThisFrame) Deslizar();
            if (t.eKey.wasPressedThisFrame) TrocarCor(+1);
            if (t.qKey.wasPressedThisFrame) TrocarCor(-1);

        }

        Touchscreen tela = Touchscreen.current;
        if (tela != null)
        {
            var toque = tela.primaryTouch;
            if (toque.press.wasPressedThisFrame)
            {
                inicioToque = toque.position.ReadValue();
                toqueValido = true;
                toqueUsado = false;
            }
            else if (toque.press.isPressed && toqueValido && !toqueUsado)
            {
                // Reage assim que o dedo andou o suficiente, sem esperar soltar.
                Vector2 d = toque.position.ReadValue() - inicioToque;
                if (d.magnitude >= deslizeMinimo)
                {
                    toqueUsado = true;
                    if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) MudarFaixa(d.x > 0f ? +1 : -1);
                    else if (d.y > 0f) Pular();
                    else Deslizar();
                }
            }
            else if (toque.press.wasReleasedThisFrame && toqueValido)
            {
                // Soltou sem deslizar: toque rápido troca a cor.
                if (!toqueUsado) TrocarCor(+1);
                toqueValido = false;
            }
        }
    }

    /// <summary>Move uma faixa para a esquerda (-1) ou direita (+1), sem sair da pista.</summary>
    public void MudarFaixa(int direcao)
    {
        int novaFaixa = Mathf.Clamp(FaixaAtual + direcao, 0, quantidadeFaixas - 1);
        if (novaFaixa == FaixaAtual) return;
        FaixaAtual = novaFaixa;
        if (visualSolarRaiz != null && !NoChao) tempoSquash = 0f;
    }

    /// <summary>Passa para a próxima cor (+1) ou a anterior (-1).</summary>
    public void TrocarCor(int direcao)
    {
        DefinirCor((CorId + direcao + Cores.Length) % Cores.Length);
    }

    public void DefinirCor(int indice)
    {
        if (indice < 0 || indice >= Cores.Length) return;
        if (rodaCores != null) rodaCores.Cancelar();
        if (indice == CorId) return;
        CorId = indice;
        if (visualSolar) AtualizarBrilhoSolar();
        else if (visual != null) visual.sharedMaterial = Visual.MatJogador(CorAtual);
        if (GameManager.Instancia != null) GameManager.Instancia.SomDeCor();
    }

    void AtualizarBrilhoSolar()
    {
        if (blocoDePropriedades == null) blocoDePropriedades = new MaterialPropertyBlock();
        Color corVisual = CorVisualAtual;
        float plasma = CorId == CorDourada ? Mathf.InverseLerp(30f, 90f, NivelSolar) : 0f;
        for (int i = 0; i < visuais.Length; i++)
        {
            Renderer r = visuais[i];
            if (r == null) continue;
            Material[] mats = r.sharedMaterials;
            for (int j = 0; j < mats.Length; j++)
            {
                if (mats[j] == null) continue;
                r.GetPropertyBlock(blocoDePropriedades, j);
                if (mats[j].HasProperty("_Tint"))
                {
                    blocoDePropriedades.SetColor("_Tint", corVisual);
                    blocoDePropriedades.SetFloat("_TintInfluence", 0.92f);
                }
                if (mats[j].HasProperty("_GlowColor"))
                    blocoDePropriedades.SetColor("_GlowColor", corVisual * 1.35f);
                if (mats[j].HasProperty("_PlasmaDourado"))
                    blocoDePropriedades.SetFloat("_PlasmaDourado", plasma);
                r.SetPropertyBlock(blocoDePropriedades, j);
            }
        }
    }

    public void Pular()
    {
        // Se apertar um pouco antes de pousar, o pulo sai assim que tocar o chão.
        esperaPulo = 0.15f;
    }

    public void Deslizar()
    {
        if (NoChao) IniciarDeslize();
        else
        {
            // No ar: desce rápido e já pousa deslizando.
            velocidadeY = -quedaRapida;
            deslizarAoPousar = true;
        }
    }

    void IniciarDeslize()
    {
        Deslizando = true;
        deslizarAoPousar = false;
        tempoDeslize = duracaoDeslize;
    }

    public void FicarInvencivel(float segundos)
    {
        invencivelAte = Mathf.Max(invencivelAte, Time.time + Mathf.Max(0f, segundos));
    }

    /// <summary>Aplica dano e ativa a piscada de invulnerabilidade temporária.</summary>
    public bool ReceberDano(float quantidade)
    {
        if (morto || Invencivel || Vida <= 0f) return false;
        Vida = Mathf.Max(0f, Vida - Mathf.Max(0f, quantidade));
        FicarInvencivel(DuracaoInvulnerabilidadeDano);
        if (Vida <= 0f)
        {
            GameManager.Instancia?.FimDeJogo();
            Morrer();
        }
        return true;
    }

    /// <summary>Esconde o Sol e dispara uma pequena explosão de plasma.</summary>
    public void Morrer()
    {
        if (morto) return;
        morto = true;
        for (int i = 0; i < visuais.Length; i++)
            if (visuais[i] != null) visuais[i].enabled = false;
        if (orbeEscudo != null) orbeEscudo.gameObject.SetActive(false);
        Vector3 centro = Limites.center;
        SolarDeathExplosion.Criar(centro, CorAtual, visualSolar ? 1f : 0.6f);
    }

    void LateUpdate()
    {
        if (visualSolar && visualSolarRaiz != null) AtualizarBrilhoSolar();
    }

    /// <summary>Posição X do centro de uma faixa (a pista é centrada em X = 0).</summary>
    public float PosicaoXDaFaixa(int faixa)
    {
        float meio = (quantidadeFaixas - 1) / 2f;
        return (faixa - meio) * larguraFaixa;
    }
}
