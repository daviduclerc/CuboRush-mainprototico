using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum EstadoJogo { Menu, Jogando, Pausado, FimDeJogo }

/// <summary>
/// Cérebro do jogo: estado (menu, jogando, pausa, fim), velocidade que cresce
/// com o tempo, pontuação, moedas, recorde, power-ups, sons e a interface na tela.
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

    [Header("Power-ups (segundos)")]
    public float duracaoIma = 8f;
    public float duracaoDobro = 10f;

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
    public bool TemEscudo { get; private set; }
    public float TempoIma { get; private set; }
    public float TempoDobro { get; private set; }
    public float TempoDeJogo { get; private set; }

    /// <summary>0 no começo, 1 quando chega na velocidade máxima.</summary>
    public float Dificuldade => Mathf.InverseLerp(velocidadeInicial, velocidadeMaxima, Velocidade);

    int Multiplicador => TempoDobro > 0f ? 2 : 1;

    static bool pularMenu;
    bool novoRecorde;
    float momentoFim;
    float tempoImpulsoTermico;
    // Impulso suavizado: a velocidade sobe e desce aos poucos em vez de saltar.
    float impulsoSuave, velocidadeImpulso;
    const float SuavidadeImpulso = 0.22f;

    AudioSource audioFonte;
    AudioClip somMoeda, somPulo, somBatida, somPowerUp, somEscudo, somCor, somTiro;
    GUIStyle estilo;

    void Awake()
    {
        Instancia = this;
        if (GetComponent<GameplayHud>() == null) gameObject.AddComponent<GameplayHud>();
        Time.timeScale = 1f;
        Velocidade = velocidadeInicial;
        impulsoSuave = velocidadeImpulso = 0f;
        Estabilidade = 100f;
        Recorde = PlayerPrefs.GetInt("Recorde", 0);
        MoedasTotal = PlayerPrefs.GetInt("MoedasTotal", 0);

        // Ao jogar de novo, começa direto, sem passar pelo menu.
        if (pularMenu)
        {
            pularMenu = false;
            Estado = EstadoJogo.Jogando;
        }

        audioFonte = gameObject.AddComponent<AudioSource>();
        audioFonte.playOnAwake = false;
        somMoeda = CriarTom("Moeda", 900f, 1400f, 0.08f);
        somPulo = CriarTom("Pulo", 300f, 620f, 0.12f);
        somBatida = CriarTom("Batida", 220f, 40f, 0.45f);
        somPowerUp = CriarTom("PowerUp", 500f, 1100f, 0.28f);
        somEscudo = CriarTom("Escudo", 500f, 150f, 0.22f);
        somCor = CriarTom("Cor", 700f, 760f, 0.06f);
        somTiro = CriarTom("Tiro", 1200f, 250f, 0.2f);
    }

    void Update()
    {
        switch (Estado)
        {
            case EstadoJogo.Menu:
                if (Confirmou()) Estado = EstadoJogo.Jogando;
                break;

            case EstadoJogo.Jogando:
                TempoDeJogo += Time.deltaTime;
                // Começa subindo devagar e acelera cada vez mais perto do final.
                float velocidadeBase = velocidadeInicial
                    + aceleracao * TempoDeJogo
                    + aceleracaoFinal * TempoDeJogo * TempoDeJogo;
                tempoImpulsoTermico = Mathf.Max(0f, tempoImpulsoTermico - Time.deltaTime);
                float pulso = Mathf.Clamp01(tempoImpulsoTermico / 0.55f);
                // O pulso térmico é o alvo; a velocidade chega nele com aceleração e
                // desaceleração suaves, sem tranco nem queda brusca entre pulsos seguidos.
                impulsoSuave = Mathf.SmoothDamp(impulsoSuave, pulso, ref velocidadeImpulso,
                    SuavidadeImpulso, Mathf.Infinity, Time.deltaTime);
                float multiplicadorPulso = 1f + 0.22f * impulsoSuave;
                Velocidade = Mathf.Min(velocidadeMaxima * multiplicadorPulso, velocidadeBase * multiplicadorPulso);
                Pontos += Velocidade * Time.deltaTime * Multiplicador;
                if (TempoIma > 0f) TempoIma -= Time.deltaTime;
                if (TempoDobro > 0f) TempoDobro -= Time.deltaTime;

                Estabilidade = Mathf.Max(0f, Estabilidade - perdaEstabilidadePorSegundo * Time.deltaTime);
                if (Estabilidade <= 0f)
                {
                    FimDeJogo();
                    PlayerController jogador = Object.FindAnyObjectByType<PlayerController>();
                    if (jogador != null) jogador.Morrer();
                    CameraFollow.Tremer(0.55f, 0.7f);
                    break;
                }

                if (ApertouPausa())
                {
                    Estado = EstadoJogo.Pausado;
                    Time.timeScale = 0f;
                }
                break;

            case EstadoJogo.Pausado:
                if (ApertouPausa() || Confirmou())
                {
                    Estado = EstadoJogo.Jogando;
                    Time.timeScale = 1f;
                }
                break;

            case EstadoJogo.FimDeJogo:
                // Pequena espera para não reiniciar sem querer.
                if (Time.unscaledTime - momentoFim > 0.6f && Confirmou())
                {
                    pularMenu = true;
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
                break;
        }
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
        Pontos += pontosPorMoeda * Multiplicador;
        if (corCorreta)
        {
            Estabilidade = Mathf.Min(100f, Estabilidade + estabilidadePorMoeda);
            Tocar(somMoeda, 0.5f);
        }
        else
        {
            Estabilidade = Mathf.Max(0f, Estabilidade - perdaPorMoedaDeOutraCor);
            Tocar(somBatida, 0.28f);
            if (Estabilidade <= 0f)
            {
                FimDeJogo();
                PlayerController jogador = Object.FindAnyObjectByType<PlayerController>();
                if (jogador != null) jogador.Morrer();
                CameraFollow.Tremer(0.55f, 0.7f);
            }
        }
    }
    public void AtivarEscudo() { TemEscudo = true; Tocar(somPowerUp); }
    public void AtivarIma() { TempoIma = duracaoIma; Tocar(somPowerUp); }
    public void AtivarDobro() { TempoDobro = duracaoDobro; Tocar(somPowerUp); }

    /// <summary>Se houver escudo, gasta ele e devolve true (o jogador sobrevive).</summary>
    public bool ConsumirEscudo()
    {
        if (!TemEscudo) return false;
        TemEscudo = false;
        Tocar(somEscudo);
        return true;
    }

    public void SomDePulo() => Tocar(somPulo, 0.5f);
    public void SomDeCor() => Tocar(somCor, 0.4f);
    public void SomDeTiro() => Tocar(somTiro, 0.6f);
    public void PulsoTermico() { tempoImpulsoTermico = 0.55f; }

    /// <summary>Atravessou uma parede com a cor certa.</summary>
    public void BonusDeCor()
    {
        Pontos += pontosPorParedeDeCor * Multiplicador;
        Tocar(somPowerUp, 0.5f);
    }

    public void FimDeJogo()
    {
        if (Estado == EstadoJogo.FimDeJogo) return;
        Estado = EstadoJogo.FimDeJogo;
        momentoFim = Time.unscaledTime;
        Tocar(somBatida);

        int pontos = Mathf.FloorToInt(Pontos);
        if (pontos > Recorde)
        {
            Recorde = pontos;
            novoRecorde = true;
            PlayerPrefs.SetInt("Recorde", Recorde);
        }
        MoedasTotal += Moedas;
        PlayerPrefs.SetInt("MoedasTotal", MoedasTotal);
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

    void OnGUI()
    {
        if (estilo == null)
        {
            estilo = new GUIStyle(GUI.skin.label);
            estilo.fontStyle = FontStyle.Bold;
            estilo.wordWrap = false;
        }

        float w = Screen.width, h = Screen.height;
        // A escala respeita altura e largura para o HUD não ultrapassar a tela.
        Rect areaSegura = Screen.safeArea;
        float s = Mathf.Clamp(Mathf.Min(areaSegura.height / 720f, areaSegura.width / 1280f), 0.45f, 1.2f);
        float m = 14f * s;
        Color ciano = new Color(0.3f, 0.95f, 1f);
        Color ouro = new Color(1f, 0.85f, 0.2f);

        Rect centro = new Rect(0, 0, w, h);
        switch (Estado)
        {
            case EstadoJogo.Menu:
                Texto(new Rect(0, h * 0.18f, w, 90 * s), "CUBO RUSH", 72, TextAnchor.MiddleCenter, ciano, s);
                Texto(new Rect(0, h * 0.36f, w, 40 * s), "Espaço, clique ou toque para começar", 28, TextAnchor.MiddleCenter, Color.white, s);
                Texto(new Rect(0, h * 0.50f, w, 34 * s), "A / D ou setas: trocar de faixa", 22, TextAnchor.MiddleCenter, Color.white, s);
                Texto(new Rect(0, h * 0.55f, w, 34 * s), "W, seta para cima ou Espaço: pular", 22, TextAnchor.MiddleCenter, Color.white, s);
                Texto(new Rect(0, h * 0.60f, w, 34 * s), "S ou seta para baixo: deslizar", 22, TextAnchor.MiddleCenter, Color.white, s);
                Texto(new Rect(0, h * 0.65f, w, 34 * s), "Esc ou P: pausar   |   No celular: deslize o dedo", 22, TextAnchor.MiddleCenter, Color.white, s);
                Texto(new Rect(0, h * 0.70f, w, 34 * s), "Cor: E avança / Q volta | Clique esquerdo ou toque também avança", 20, TextAnchor.MiddleCenter, Color.white, s);
                Texto(new Rect(0, h * 0.75f, w, 34 * s), "Portais: atravesse com o Sol na mesma cor para não levar dano", 22, TextAnchor.MiddleCenter, ciano, s);
                Texto(new Rect(0, h * 0.79f, w, 30 * s), "Moedas: cor igual recupera estabilidade; outra cor reduz estabilidade", 19, TextAnchor.MiddleCenter, ouro, s);
                Texto(new Rect(0, h * 0.84f, w, 34 * s), "Recorde: " + Recorde + "     Moedas acumuladas: " + MoedasTotal, 22, TextAnchor.MiddleCenter, ouro, s);
                break;

            case EstadoJogo.Pausado:
                Texto(centro, "PAUSADO", 64, TextAnchor.MiddleCenter, Color.white, s);
                Texto(new Rect(0, h * 0.58f, w, 40 * s), "Esc, P ou Espaço para continuar", 24, TextAnchor.MiddleCenter, Color.white, s);
                break;

            case EstadoJogo.FimDeJogo:
                Texto(new Rect(0, h * 0.25f, w, 90 * s), "FIM DE JOGO", 64, TextAnchor.MiddleCenter, new Color(1f, 0.35f, 0.35f), s);
                Texto(new Rect(0, h * 0.42f, w, 50 * s), "Pontos: " + Mathf.FloorToInt(Pontos) + "     Moedas: " + Moedas, 32, TextAnchor.MiddleCenter, Color.white, s);
                if (novoRecorde)
                    Texto(new Rect(0, h * 0.50f, w, 50 * s), "NOVO RECORDE!", 32, TextAnchor.MiddleCenter, ouro, s);
                Texto(new Rect(0, h * 0.62f, w, 40 * s), "Espaço, clique ou toque para jogar de novo", 26, TextAnchor.MiddleCenter, Color.white, s);
                break;
        }
    }

    void Texto(Rect r, string texto, int tamanho, TextAnchor alinhamento, Color cor, float s)
    {
        estilo.fontSize = Mathf.RoundToInt(tamanho * s);
        estilo.alignment = alinhamento;

        // Sombra para o texto ficar legível sobre qualquer fundo.
        estilo.normal.textColor = Color.black;
        GUI.Label(new Rect(r.x + 2 * s, r.y + 2 * s, r.width, r.height), texto, estilo);

        estilo.normal.textColor = cor;
        GUI.Label(r, texto, estilo);
    }
}
