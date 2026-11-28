using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum EstadoJogo { Menu, Jogando, Pausado, FimDeJogo, Vitoria }

/// <summary>
/// Cérebro do jogo: estado (menu, jogando, pausa, fim), velocidade que cresce
/// com o tempo, pontuação, moedas, recorde, sons e a interface na tela.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instancia { get; private set; }

    [Header("Velocidade")]
    public float velocidadeInicial = 10f;
    public float velocidadeMaxima = 40f;
    [Tooltip("Quanto a velocidade aumenta por segundo de jogo.")]
    public float aceleracao = 0.2f;
    [Tooltip("Ganho extra que cresce com o tempo: quanto mais longa a partida, mais rápido acelera.")]
    public float aceleracaoFinal = 0.0015f;

    [Header("Pontuação")]
    public int pontosPorMoeda = 10;
    public int pontosPorParedeDeCor = 25;

    [Header("Estabilidade solar")]
    [Tooltip("Estabilidade perdida por segundo enquanto o Sol não coleta energia.")]
    public float perdaEstabilidadePorSegundo = 1.5f;
    [Tooltip("Estabilidade recuperada ao coletar uma moeda da cor correta.")]
    public float estabilidadePorMoeda = 6f;
    [Tooltip("Estabilidade perdida ao coletar uma moeda de cor diferente.")]
    public float perdaPorMoedaDeOutraCor = 6f;

    public EstadoJogo Estado { get; private set; } = EstadoJogo.Menu;
    public float Velocidade { get; private set; }
    public float Pontos { get; private set; }
    public int Moedas { get; private set; }
    public int Recorde { get; private set; }
    public int MoedasTotal { get; private set; }
    public float Estabilidade { get; private set; } = 100f;
    public float TempoDeJogo { get; private set; }
    public const int MetaMoedas = 50;
    public const int MetaPontos = 1000;
    public const int MetaRaids = 2;
    public int RaidsSuperadas { get; private set; }
    public bool EmResultado => Estado == EstadoJogo.FimDeJogo || Estado == EstadoJogo.Vitoria;
    public bool NovoRecorde { get; private set; }
    public bool AbrirHistoricoNoInicio { get; private set; }
    public bool TemPartidaSalva => UltimaPartida != null;
    public int UltimaPontuacao => UltimaPartida?.pontos ?? 0;
    public int UltimasMoedas => UltimaPartida?.moedas ?? 0;
    public int UltimasOndas => UltimaPartida?.ondas ?? 0;
    public string UltimoResultado => UltimaPartida?.Rotulo ?? "";
    public bool ResultadoPronto => EmResultado && (morteSolar == null || !morteSolar.EmExecucao);
    public float EscurecimentoFinal => morteSolar != null ? morteSolar.Escurecimento : 0f;

    /// <summary>0 no começo, 1 quando chega na velocidade máxima.</summary>
    public float Dificuldade => Mathf.InverseLerp(velocidadeInicial, velocidadeMaxima, Velocidade);

    static bool abrirHistoricoAoCarregar;
    bool carregandoCena;
    bool progressoSalvo;
    HistoricoPartidas historico;
    PartidaSalva UltimaPartida => PartidaNoHistorico(0);
    internal PartidaSalva PartidaNoHistorico(int posicao) => historico?.Obter(posicao);
    float tempoImpulsoTermico;
    // Impulso suavizado: a velocidade sobe e desce aos poucos em vez de saltar.
    float impulsoSuave, velocidadeImpulso;
    const float SuavidadeImpulso = 0.22f;
    // Estilingue gravitacional: impulso de velocidade ao passar rente ao asteroide indicado.
    float tempoEstilingue, forcaEstilingue, estilingueSuave, velocidadeEstilingue;

    /// <summary>0..1: quanto do impulso do estilingue está atuando agora.</summary>
    public float ImpulsoEstilingue => estilingueSuave;
    const float GanhoMaximoPulso = 0.22f;

    MainMenuUI menuInicial;
    PauseMenuUI menuPausa;
    SunDeathSequence morteSolar;
    AudioSource audioFonte;
    AudioClip somMoeda, somPulo, somBatida, somBonus, somCor, somTiro, somGravidade, somEstilingue, somTravessia;
    GUIStyle estilo;

    void Awake()
    {
        Instancia = this;
        if (GetComponent<GameplayHud>() == null) gameObject.AddComponent<GameplayHud>();
        if (GetComponent<VinhetaDeVida>() == null) gameObject.AddComponent<VinhetaDeVida>();
        menuInicial = GetComponent<MainMenuUI>();
        if (menuInicial == null) menuInicial = gameObject.AddComponent<MainMenuUI>();
        menuPausa = GetComponent<PauseMenuUI>();
        if (menuPausa == null) menuPausa = gameObject.AddComponent<PauseMenuUI>();
        if (GetComponent<ResultadoPartidaUI>() == null) gameObject.AddComponent<ResultadoPartidaUI>();
        if (!TryGetComponent(out morteSolar)) morteSolar = Object.FindAnyObjectByType<SunDeathSequence>();
        if (morteSolar == null) morteSolar = gameObject.AddComponent<SunDeathSequence>();
        Time.timeScale = 1f;
        Velocidade = velocidadeInicial;
        impulsoSuave = velocidadeImpulso = 0f;
        Estabilidade = 100f;
        Recorde = PlayerPrefs.GetInt("Recorde", 0);
        MoedasTotal = PlayerPrefs.GetInt("MoedasTotal", 0);
        historico = HistoricoPartidas.Carregar();

        AbrirHistoricoNoInicio = abrirHistoricoAoCarregar;
        abrirHistoricoAoCarregar = false;

        audioFonte = gameObject.AddComponent<AudioSource>();
        audioFonte.playOnAwake = false;
        somMoeda = CriarTom("Moeda", 900f, 1400f, 0.08f);
        somPulo = CriarTom("Pulo", 300f, 620f, 0.12f);
        somBatida = CriarTom("Batida", 220f, 40f, 0.45f);
        somBonus = CriarTom("Bonus", 500f, 1100f, 0.28f);
        somCor = CriarTom("Cor", 700f, 760f, 0.06f);
        somTiro = CriarTom("Tiro", 1200f, 250f, 0.2f);
        somGravidade = CriarTom("Gravidade", 140f, 55f, 0.6f);
        somEstilingue = CriarTom("Estilingue", 320f, 1500f, 0.45f);
        somTravessia = CriarTom("Travessia limpa", 530f, 920f, 0.12f);
    }

    void Update()
    {
        if (carregandoCena) return;
        switch (Estado)
        {
            case EstadoJogo.Menu:
                if ((menuInicial == null || !menuInicial.isActiveAndEnabled) && Confirmou()) IniciarPartida();
                break;

            case EstadoJogo.Jogando:
                TempoDeJogo += Time.deltaTime;
                // Começa subindo devagar e acelera cada vez mais perto do final.
                float velocidadeBase = VelocidadeNormal(TempoDeJogo);
                tempoImpulsoTermico = Mathf.Max(0f, tempoImpulsoTermico - Time.deltaTime);
                float pulso = Mathf.Clamp01(tempoImpulsoTermico / 0.55f);
                // O pulso térmico é o alvo; a velocidade chega nele com aceleração e
                // desaceleração suaves, sem tranco nem queda brusca entre pulsos seguidos.
                impulsoSuave = Mathf.SmoothDamp(impulsoSuave, pulso, ref velocidadeImpulso,
                    SuavidadeImpulso, Mathf.Infinity, Time.deltaTime);
                tempoEstilingue = Mathf.Max(0f, tempoEstilingue - Time.deltaTime);
                estilingueSuave = Mathf.SmoothDamp(estilingueSuave, tempoEstilingue > 0f ? forcaEstilingue : 0f,
                    ref velocidadeEstilingue, 0.25f, Mathf.Infinity, Time.deltaTime);
                float multiplicadorPulso = 1f + GanhoMaximoPulso * impulsoSuave + 0.30f * estilingueSuave;
                Velocidade = Mathf.Min(velocidadeMaxima * multiplicadorPulso, velocidadeBase * multiplicadorPulso);
                Pontos += Velocidade * Time.deltaTime;

                Estabilidade = Mathf.Max(0f, Estabilidade - perdaEstabilidadePorSegundo * Time.deltaTime);
                if (Estabilidade <= 0f)
                {
                    FimDeJogo();
                    break;
                }

                VerificarVitoria();
                if (Estado == EstadoJogo.Jogando && ApertouPausa()) Pausar();
                break;

            case EstadoJogo.Pausado:
                if (ApertouPausa() || ((menuPausa == null || !menuPausa.isActiveAndEnabled) && Confirmou()))
                    RetomarPartida();
                break;

        }
    }

    float VelocidadeNormal(float tempo) =>
        velocidadeInicial + aceleracao * tempo + aceleracaoFinal * tempo * tempo;

    public float VelocidadePrevista(float distancia)
    {
        float tempoMaximo = Mathf.Max(0f, distancia) / Mathf.Max(1f, VelocidadeNormal(TempoDeJogo));
        float normal = Mathf.Min(velocidadeMaxima, VelocidadeNormal(TempoDeJogo + tempoMaximo));
        return Mathf.Max(Velocidade, normal * (1f + GanhoMaximoPulso));
    }

    public void IniciarPartida()
    {
        if (Estado != EstadoJogo.Menu) return;
        Estado = EstadoJogo.Jogando;
        Time.timeScale = 1f;
    }

    public void Pausar()
    {
        if (Estado != EstadoJogo.Jogando || carregandoCena) return;
        Estado = EstadoJogo.Pausado;
        Time.timeScale = 0f;
    }

    public void RetomarPartida()
    {
        if (Estado != EstadoJogo.Pausado || carregandoCena) return;
        Estado = EstadoJogo.Jogando;
        Time.timeScale = 1f;
    }

    public void VoltarAoMenu()
    {
        if (Estado == EstadoJogo.Pausado || ResultadoPronto) RecarregarCena(false);
    }

    public void JogarNovamente()
    {
        if (ResultadoPronto) RecarregarCena(true);
    }

    void RecarregarCena(bool mostrarHistorico)
    {
        if (carregandoCena) return;
        int cena = Mathf.Max(0, SceneManager.GetActiveScene().buildIndex);
        if (cena >= SceneManager.sceneCountInBuildSettings) return;
        carregandoCena = true;
        if (Estado == EstadoJogo.Pausado) SalvarProgressoDaPartida();
        abrirHistoricoAoCarregar = mostrarHistorico;
        Time.timeScale = 1f;
        SceneManager.LoadScene(cena);
    }

    // ---------- Entrada ----------

    static bool Confirmou()
    {
        Keyboard t = Keyboard.current;
        if (t != null && (t.spaceKey.wasPressedThisFrame || t.enterKey.wasPressedThisFrame))
            return true;

        Mouse m = Mouse.current;
        if (m != null && m.leftButton.wasPressedThisFrame) return true;

        Touchscreen tela = Touchscreen.current;
        return tela != null && tela.primaryTouch.press.wasPressedThisFrame;
    }

    static bool ApertouPausa()
    {
        Keyboard t = Keyboard.current;
        return t != null && (t.escapeKey.wasPressedThisFrame || t.pKey.wasPressedThisFrame);
    }

    // ---------- Eventos do jogo ----------

    public void ColetarMoeda(bool corCorreta = true)
    {
        if (Estado != EstadoJogo.Jogando) return;
        Moedas++;
        Pontos += pontosPorMoeda;
        if (corCorreta)
        {
            Estabilidade = Mathf.Min(100f, Estabilidade + estabilidadePorMoeda);
            Tocar(somMoeda, 0.5f);
        }
        else
        {
            Estabilidade = Mathf.Max(0f, Estabilidade - perdaPorMoedaDeOutraCor);
            Tocar(somBatida, 0.28f);
            if (Estabilidade <= 0f) FimDeJogo();
        }
        VerificarVitoria();
    }

    public void RegistrarRaidDeMeteoros()
    {
        if (Estado != EstadoJogo.Jogando) return;
        RaidsSuperadas++;
        VerificarVitoria();
    }

    void VerificarVitoria()
    {
        if (Estado != EstadoJogo.Jogando || Moedas < MetaMoedas || Pontos < MetaPontos || RaidsSuperadas < MetaRaids) return;
        Estado = EstadoJogo.Vitoria;
        SalvarProgressoDaPartida();
    }
    public void SomDePulo() => Tocar(somPulo, 0.5f);
    public void SomDeCor() => Tocar(somCor, 0.4f);
    public void SomDeTiro() => Tocar(somTiro, 0.6f);
    public void SomDeImpacto() => Tocar(somBatida, 0.55f);
    public void SomDeTravessia() => Tocar(somTravessia, 0.32f);
    public void PulsoTermico() { tempoImpulsoTermico = 0.55f; }

    /// <summary>Estilingue gravitacional: impulso de velocidade (forca 0..1) e pontos. Devolve os pontos ganhos.</summary>
    public int Estilingue(int pontosBase, float forca)
    {
        if (Estado != EstadoJogo.Jogando) return 0;
        int ganho = pontosBase;
        Pontos += ganho;
        forcaEstilingue = Mathf.Clamp01(Mathf.Max(forca, tempoEstilingue > 0f ? forcaEstilingue : 0f));
        tempoEstilingue = 1.4f + 0.6f * forca;
        Tocar(somEstilingue, 0.6f);
        return ganho;
    }

    /// <summary>Pontos por escapar de um fenômeno gravitacional (ex.: surfar a onda). Devolve o ganho.</summary>
    public int BonusGravitacional(int pontosBase)
    {
        if (Estado != EstadoJogo.Jogando) return 0;
        int ganho = pontosBase;
        Pontos += ganho;
        Tocar(somBonus, 0.4f);
        return ganho;
    }

    public void SomGravitacional() => Tocar(somGravidade, 0.7f);

    /// <summary>Atravessou uma parede com a cor certa.</summary>
    public void BonusDeCor()
    {
        Pontos += pontosPorParedeDeCor;
        Tocar(somBonus, 0.5f);
    }

    public void FimDeJogo()
    {
        if (Estado != EstadoJogo.Jogando || carregandoCena) return;
        Estado = EstadoJogo.FimDeJogo;
        morteSolar.Iniciar();
        SalvarProgressoDaPartida();
    }

    void SalvarProgressoDaPartida()
    {
        if (progressoSalvo) return;
        progressoSalvo = true;
        int pontos = Mathf.FloorToInt(Pontos);
        if (pontos > Recorde)
        {
            Recorde = pontos;
            NovoRecorde = true;
            PlayerPrefs.SetInt("Recorde", Recorde);
        }
        MoedasTotal += Moedas;
        PlayerPrefs.SetInt("MoedasTotal", MoedasTotal);
        ResultadoPartida resultado = Estado == EstadoJogo.Vitoria ? ResultadoPartida.Vitoria :
            Estado == EstadoJogo.FimDeJogo ? ResultadoPartida.Derrota : ResultadoPartida.Interrompida;
        historico.Registrar(new PartidaSalva(pontos, Moedas, RaidsSuperadas, resultado, usaRaids: true));
        PlayerPrefs.Save();
    }

    // ---------- Sons gerados por código ----------

    void Tocar(AudioClip clip, float volume = 0.8f)
    {
        if (clip != null) audioFonte.PlayOneShot(clip, volume);
    }

    static AudioClip CriarTom(string nome, float freqInicial, float freqFinal, float duracao)
    {
        const int taxa = 44100;
        int total = Mathf.CeilToInt(taxa * duracao);
        float[] dados = new float[total];
        float fase = 0f;
        for (int i = 0; i < total; i++)
        {
            float t = i / (float)total;
            float freq = Mathf.Lerp(freqInicial, freqFinal, t);
            fase += 2f * Mathf.PI * freq / taxa;
            dados[i] = Mathf.Sin(fase) * (1f - t) * 0.5f; // some aos poucos
        }
        AudioClip clip = AudioClip.Create(nome, total, 1, taxa, false);
        clip.SetData(dados, 0);
        return clip;
    }

    // ---------- Interface ----------

    GUIStyle estiloCartao, estiloTecla, estiloContorno;

    void OnGUI()
    {
        if (Estado == EstadoJogo.Menu && menuInicial != null && menuInicial.isActiveAndEnabled) return;
        if (Estado == EstadoJogo.Pausado && menuPausa != null && menuPausa.isActiveAndEnabled) return;
        if (estilo == null)
        {
            estilo = new GUIStyle(GUI.skin.label);
            estilo.font = EstiloUI.Fonte;
            estilo.wordWrap = false;
            estilo.clipping = TextClipping.Overflow;

            estiloCartao = new GUIStyle();
            estiloCartao.normal.background = EstiloUI.TexturaArredondada;
            estiloCartao.border = new RectOffset(20, 20, 20, 20);

            estiloTecla = new GUIStyle(estiloCartao);

            estiloContorno = new GUIStyle();
            estiloContorno.normal.background = EstiloUI.TexturaContorno;
            estiloContorno.border = new RectOffset(20, 20, 20, 20);
        }
        if (Estado == EstadoJogo.Jogando || EmResultado) return;

        float w = Screen.width, h = Screen.height;
        // A escala respeita altura e largura para nada ultrapassar a tela.
        Rect segura = Screen.safeArea;
        float s = Mathf.Clamp(Mathf.Min(segura.height / 720f, segura.width / 1280f), 0.45f, 1.6f);
        float pisca = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3.2f);
        float cx = segura.center.x;
        // OnGUI conta Y de cima para baixo; safeArea conta de baixo para cima.
        float topo = h - segura.yMax;

        Color laranjaSolar = new Color(1f, 0.62f, 0.22f);

        switch (Estado)
        {
            case EstadoJogo.Menu:
            {
                Fundo(w, h, 1f);
                float yTitulo = topo + segura.height * 0.13f;
                TextoBrilhante(new Rect(0, yTitulo, w, 110 * s), "CUBO RUSH", 92, Color.white, laranjaSolar, s);
                Texto(new Rect(0, yTitulo + 100 * s, w, 30 * s), "UMA CORRIDA ESTELAR ATÉ O BURACO NEGRO", 18,
                    TextAnchor.MiddleCenter, new Color(1f, 0.78f, 0.55f, 0.9f), s, false);

                Texto(new Rect(0, yTitulo + 150 * s, w, 40 * s), "ESPAÇO, CLIQUE OU TOQUE PARA COMEÇAR", 26,
                    TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, pisca), s);

                // Cartão de controles em duas colunas.
                float larguraCartao = 760f * s, alturaCartao = 274f * s;
                Rect cartao = new Rect(cx - larguraCartao * 0.5f, yTitulo + 215 * s, larguraCartao, alturaCartao);
                Cartao(cartao, s);

                float colA = cartao.x + 28 * s, colB = cartao.x + cartao.width * 0.5f + 10 * s;
                float linha = cartao.y + 22 * s, passo = 38 * s;
                Controle(colA, linha + passo * 0, "A / D", "trocar de faixa (ou setas)", s);
                Controle(colA, linha + passo * 1, "W", "pular (ou espaço)", s);
                Controle(colA, linha + passo * 2, "S", "agachar / deslizar", s);
                Controle(colA, linha + passo * 3, "ESC / P", "pausar", s);
                Controle(colB, linha + passo * 0, "E", "próxima cor do Sol", s);
                Controle(colB, linha + passo * 1, "Q", "cor anterior", s);
                Controle(colB, linha + passo * 2, "CLIQUE", "também avança a cor", s);
                Controle(colB, linha + passo * 3, "TOQUE", "deslize o dedo no celular", s);

                Rect dicas = new Rect(cartao.x + 24 * s, cartao.y + passo * 4 + 32 * s, cartao.width - 48 * s, 50 * s);
                Texto(new Rect(dicas.x, dicas.y, dicas.width, 24 * s),
                    "Portais: atravesse com o Sol na mesma cor para não levar dano.", 17,
                    TextAnchor.MiddleCenter, EstiloUI.Ciano, s, false);
                Texto(new Rect(dicas.x, dicas.y + 24 * s, dicas.width, 24 * s),
                    "Moedas da mesma cor recuperam estabilidade; de outra cor, reduzem.", 17,
                    TextAnchor.MiddleCenter, EstiloUI.Ouro, s, false);
                Texto(new Rect(dicas.x, dicas.y + 48 * s, dicas.width, 24 * s),
                    "Gravidade: pule as ondas, resista aos mini buracos negros e siga as setas.", 17,
                    TextAnchor.MiddleCenter, new Color(0.85f, 0.6f, 1f), s, false);

                Texto(new Rect(0, cartao.yMax + 18 * s, w, 28 * s),
                    "RECORDE  " + Recorde + "        MOEDAS ACUMULADAS  " + MoedasTotal, 18,
                    TextAnchor.MiddleCenter, EstiloUI.TextoSuave, s, false);
                break;
            }

            case EstadoJogo.Pausado:
            {
                Fundo(w, h, 0.8f);
                float y = topo + segura.height * 0.5f;
                TextoBrilhante(new Rect(0, y - 70 * s, w, 90 * s), "PAUSADO", 72, Color.white, EstiloUI.Ciano, s);
                Texto(new Rect(0, y + 22 * s, w, 34 * s), "ESC, P OU ESPAÇO PARA CONTINUAR", 22,
                    TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, pisca), s);
                break;
            }

        }
    }

    /// <summary>Escurece a tela com um degradê (mais escuro embaixo).</summary>
    void Fundo(float w, float h, float opacidade)
    {
        Color antes = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, antes.a * opacidade);
        GUI.DrawTexture(new Rect(0, 0, w, h), EstiloUI.TexturaGradiente, ScaleMode.StretchToFill);
        GUI.color = antes;
    }

    /// <summary>Cartão escuro com moldura neon fina.</summary>
    void Cartao(Rect r, float s)
    {
        Color antes = GUI.color;
        GUI.color = new Color(0.03f, 0.04f, 0.09f, 0.86f * antes.a);
        GUI.Box(r, GUIContent.none, estiloCartao);
        GUI.color = new Color(1f, 0.55f, 0.25f, 0.45f * antes.a);
        GUI.Box(r, GUIContent.none, estiloContorno);
        GUI.color = antes;
    }

    /// <summary>Tecla desenhada como uma pastilha, seguida da ação.</summary>
    void Controle(float x, float y, string tecla, string acao, float s)
    {
        estilo.fontSize = Mathf.RoundToInt(15 * s);
        float larguraTecla = Mathf.Max(estilo.CalcSize(new GUIContent(tecla)).x + 22 * s, 44 * s);
        Rect pastilha = new Rect(x, y, larguraTecla, 28 * s);
        Color antes = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.10f * antes.a);
        GUI.Box(pastilha, GUIContent.none, estiloTecla);
        GUI.color = antes;
        Texto(pastilha, tecla, 15, TextAnchor.MiddleCenter, Color.white, s, false);
        Texto(new Rect(x + larguraTecla + 12 * s, y, 300 * s, 28 * s), acao, 18, TextAnchor.MiddleLeft,
            new Color(0.86f, 0.90f, 0.98f), s, false);
    }

    /// <summary>Título com um halo colorido em volta das letras.</summary>
    void TextoBrilhante(Rect r, string texto, int tamanho, Color cor, Color halo, float s)
    {
        estilo.fontSize = Mathf.RoundToInt(tamanho * s);
        estilo.alignment = TextAnchor.MiddleCenter;
        for (int i = 0; i < 12; i++)
        {
            float ang = i * Mathf.PI * 2f / 12f;
            float raio = (i % 2 == 0 ? 5f : 2.5f) * s;
            estilo.normal.textColor = new Color(halo.r, halo.g, halo.b, i % 2 == 0 ? 0.10f : 0.22f);
            GUI.Label(new Rect(r.x + Mathf.Cos(ang) * raio, r.y + Mathf.Sin(ang) * raio, r.width, r.height), texto, estilo);
        }
        estilo.normal.textColor = cor;
        GUI.Label(r, texto, estilo);
    }

    void Texto(Rect r, string texto, int tamanho, TextAnchor alinhamento, Color cor, float s, bool sombra = true)
    {
        estilo.fontSize = Mathf.RoundToInt(tamanho * s);
        estilo.alignment = alinhamento;
        // Sombra para o texto ficar legível sobre qualquer fundo.
        if (sombra)
        {
            estilo.normal.textColor = new Color(0f, 0f, 0f, 0.8f * cor.a);
            GUI.Label(new Rect(r.x + 2 * s, r.y + 2 * s, r.width, r.height), texto, estilo);
        }

        estilo.normal.textColor = cor;
        GUI.Label(r, texto, estilo);
    }
}

