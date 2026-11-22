using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Brilho vermelho nas bordas da tela que cresce conforme a vida do Sol cai.
/// - Começa a aparecer abaixo de 60% de vida e vai fechando em direção ao centro.
/// - Entre 60% e 30% "respira" devagar; abaixo de 30% pulsa no batimento do Sol.
/// - Cada dano recebido dá um clarão rápido.
/// O centro da tela fica sempre livre, então as cores da pista não são afetadas.
/// </summary>
public sealed class VinhetaDeVida : MonoBehaviour
{
    [Tooltip("Vida (em %) em que a vinheta começa a aparecer.")]
    public float vidaInicio = 60f;
    [Tooltip("Vida (em %) em que a vinheta atinge a força máxima.")]
    public float vidaMaxima = 10f;
    [Tooltip("Opacidade máxima das bordas.")]
    [Range(0f, 1f)] public float opacidadeMaxima = 0.6f;
    public Color cor = new Color(1f, 0.10f, 0.04f);

    GameManager jogo;
    PlayerController jogador;
    RectTransform raiz;
    RawImage imagem;
    Texture2D textura;
    float vidaAnterior = PlayerController.VidaMaxima;
    float clarao;
    float forcaSuave;
    float visibilidade = 1f;

    void Start()
    {
        jogo = GetComponent<GameManager>();

        var objetoRaiz = new GameObject("Vinheta de vida", typeof(RectTransform));
        raiz = objetoRaiz.GetComponent<RectTransform>();
        raiz.SetParent(transform, false);
        var canvas = objetoRaiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40; // abaixo do HUD (50), acima do jogo

        var objetoImagem = new GameObject("Bordas", typeof(RectTransform));
        var rect = objetoImagem.GetComponent<RectTransform>();
        rect.SetParent(raiz, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        imagem = objetoImagem.AddComponent<RawImage>();
        textura = CriarTextura();
        imagem.texture = textura;
        imagem.raycastTarget = false;
        imagem.color = new Color(cor.r, cor.g, cor.b, 0f);
    }

    void LateUpdate()
    {
        if (imagem == null) return;
        if (jogador == null) jogador = Object.FindAnyObjectByType<PlayerController>();
        if (jogo == null || jogador == null) { imagem.enabled = false; return; }

        float vida = jogador.Vida;
        float dt = Time.unscaledDeltaTime;
        bool pausado = jogo.Estado == EstadoJogo.Pausado;

        // Clarão a cada dano: proporcional ao tamanho do golpe.
        if (vida < vidaAnterior - 0.01f)
            clarao = Mathf.Clamp01(clarao + 0.55f + (vidaAnterior - vida) / 40f);
        vidaAnterior = vida;
        if (!pausado) clarao = Mathf.MoveTowards(clarao, 0f, dt * 2.2f);

        // Some no menu e no fim de jogo (lá o colapso da estrela assume a tela).
        bool ativa = jogo.Estado == EstadoJogo.Jogando || pausado;
        visibilidade = Mathf.MoveTowards(visibilidade, ativa ? 1f : 0f, dt * (ativa ? 4f : 2.5f));

        float forca = Mathf.Clamp01(Mathf.InverseLerp(vidaInicio, vidaMaxima, vida));
        forca = Mathf.SmoothStep(0f, 1f, forca);
        forcaSuave = Mathf.Lerp(forcaSuave, forca, 1f - Mathf.Exp(-6f * dt));

        // Ritmo: respiração lenta no alerta, batimento do Sol quando a vida está crítica.
        float ritmo = 0f;
        if (!pausado)
        {
            if (vida <= 30f) ritmo = jogador.BatimentoPerigo;
            else ritmo = 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f));
        }

        float alfa = forcaSuave * (0.62f + 0.38f * ritmo) + clarao * 0.5f;
        alfa = Mathf.Clamp01(alfa) * opacidadeMaxima * visibilidade;
        imagem.enabled = alfa > 0.003f;
        imagem.color = new Color(cor.r, cor.g, cor.b, alfa);

        // Quanto menor a vida, mais a borda avança para o centro (a textura "encolhe"
        // dentro da tela e as bordas opacas ocupam mais espaço).
        float avanco = 1f + 0.30f * forcaSuave + 0.10f * ritmo * forcaSuave + 0.08f * clarao;
        imagem.uvRect = new Rect(0.5f - avanco * 0.5f, 0.5f - avanco * 0.5f, avanco, avanco);
    }

    /// <summary>Degradê elíptico: transparente no centro, opaco nas bordas e cantos.</summary>
    static Texture2D CriarTextura()
    {
        const int lado = 256;
        var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false)
        {
            name = "VinhetaDeVida",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[lado * lado];
        for (int y = 0; y < lado; y++)
        {
            float v = (y + 0.5f) / lado * 2f - 1f;
            for (int x = 0; x < lado; x++)
            {
                float u = (x + 0.5f) / lado * 2f - 1f;
                // Mistura de círculo e "caixa" para a borda acompanhar os lados da tela.
                float circulo = Mathf.Sqrt(u * u + v * v) / 1.2f;
                float caixa = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                float d = Mathf.Lerp(circulo, caixa, 0.55f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1.0f, d));
                a = Mathf.Pow(a, 1.4f);
                pixels[y * lado + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }

    void OnDestroy()
    {
        if (raiz != null) Destroy(raiz.gameObject);
        if (textura != null) Destroy(textura);
    }
}
