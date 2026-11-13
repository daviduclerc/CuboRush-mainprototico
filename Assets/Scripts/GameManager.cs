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

    public EstadoJogo Estado { get; private set; } = EstadoJogo.Menu;
    public float Velocidade { get; private set; }
    public float Pontos { get; private set; }
    public int Moedas { get; private set; }
    public int Recorde { get; private set; }
    public int MoedasTotal { get; private set; }
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

    AudioSource audioFonte;
    AudioClip somMoeda, somPulo, somBatida, somPowerUp, somEscudo, somCor, somTiro;
    GUIStyle estilo;

    void Awake()
    {
        Instancia = this;
        Time.timeScale = 1f;
        Velocidade = velocidadeInicial;
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
                Velocidade = Mathf.Min(velocidadeMaxima, velocidadeInicial
                    + aceleracao * TempoDeJogo
                    + aceleracaoFinal * TempoDeJogo * TempoDeJogo);
                Pontos += Velocidade * Time.deltaTime * Multiplicador;
                if (TempoIma > 0f) TempoIma -= Time.deltaTime;
                if (TempoDobro > 0f) TempoDobro -= Time.deltaTime;

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

    public void ColetarMoeda()
    {
        Moedas++;
        Pontos += pontosPorMoeda * Multiplicador;
        Tocar(somMoeda, 0.5f);
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

        if (Estado != EstadoJogo.Menu)
        {
            // Pontos, moedas, recorde e buffs ficam juntos em um painel responsivo.
            int quantidadeBuffs = (TemEscudo ? 1 : 0) + (TempoIma > 0f ? 1 : 0) + (TempoDobro > 0f ? 1 : 0);
            float linhaStatus = 22f * s;
            float linhaBuff = 20f * s;
            float alturaPainel = 14f * s + linhaStatus * 3f;
            if (quantidadeBuffs > 0) alturaPainel += 5f * s + linhaBuff * quantidadeBuffs;

            float painelLargura = Mathf.Min(280f * s, Mathf.Max(0f, areaSegura.width - 2f * m));
            float painelX = Mathf.Max(areaSegura.xMin + m, areaSegura.xMax - m - painelLargura);
            float topoSeguro = h - areaSegura.yMax;
            float fundoSeguro = h - areaSegura.yMin;
            float painelY = topoSeguro + m;
            if (painelY + alturaPainel > fundoSeguro - m)
                painelY = Mathf.Max(topoSeguro, fundoSeguro - m - alturaPainel);
            Rect painel = new Rect(painelX, painelY, painelLargura, alturaPainel);
            Color corGUI = GUI.color;
            GUI.color = new Color(0.015f, 0.025f, 0.05f, 0.78f);
            GUI.DrawTexture(painel, Texture2D.whiteTexture);
            GUI.color = corGUI;

            float textoX = painel.x + 9f * s;
            float textoW = Mathf.Max(0f, painel.width - 18f * s);
            float y = painel.y + 5f * s;
            Texto(new Rect(textoX, y, textoW, linhaStatus), "PONTOS  " + Mathf.FloorToInt(Pontos), 18, TextAnchor.MiddleRight, Color.white, s);
            y += linhaStatus;
            Texto(new Rect(textoX, y, textoW, linhaStatus), "MOEDAS  " + Moedas, 17, TextAnchor.MiddleRight, ouro, s);
            y += linhaStatus;
            Texto(new Rect(textoX, y, textoW, linhaStatus), "RECORDE  " + Recorde, 15, TextAnchor.MiddleRight, new Color(0.74f, 0.84f, 0.92f), s);
            y += linhaStatus;

            if (quantidadeBuffs > 0)
            {
                y += 2f * s;
                GUI.color = new Color(0.3f, 0.55f, 0.8f, 0.55f);
                GUI.DrawTexture(new Rect(textoX, y, textoW, Mathf.Max(1f, s)), Texture2D.whiteTexture);
                GUI.color = corGUI;
                y += 3f * s;
            }
            if (TemEscudo)
            {
                Texto(new Rect(textoX, y, textoW, linhaBuff), "ESCUDO", 15, TextAnchor.MiddleRight, ciano, s);
                y += linhaBuff;
            }
            if (TempoIma > 0f)
            {
                Texto(new Rect(textoX, y, textoW, linhaBuff), "ÍMÃ  " + Mathf.CeilToInt(TempoIma) + "s", 15, TextAnchor.MiddleRight, new Color(0.4f, 1f, 0.5f), s);
                y += linhaBuff;
            }
            if (TempoDobro > 0f)
                Texto(new Rect(textoX, y, textoW, linhaBuff), "PONTOS 2X  " + Mathf.CeilToInt(TempoDobro) + "s", 15, TextAnchor.MiddleRight, new Color(1f, 0.5f, 0.9f), s);
        }

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
                Texto(new Rect(0, h * 0.70f, w, 34 * s), "Q / E ou 1-6: trocar a cor do Sol (primárias e inversas)", 22, TextAnchor.MiddleCenter, Color.white, s);
                Texto(new Rect(0, h * 0.75f, w, 34 * s), "Paredes só deixam passar o Sol com a cor correspondente", 22, TextAnchor.MiddleCenter, ciano, s);
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
