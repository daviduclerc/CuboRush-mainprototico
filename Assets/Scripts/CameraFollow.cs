using UnityEngine;

/// <summary>
/// Câmera atrás e acima do cubo, acompanhando o avanço dele.
/// Abre o campo de visão conforme a velocidade aumenta e treme nas batidas.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform alvo;

    [Tooltip("Posição da câmera em relação ao cubo.")]
    public Vector3 deslocamento = new Vector3(0f, 4.2f, -6f);

    [Tooltip("Inclinação da câmera para baixo, em graus.")]
    public float inclinacao = 14f;

    [Tooltip("0 = câmera fixa no meio da pista; 1 = acompanha o cubo totalmente de lado.")]
    [Range(0f, 1f)]
    public float acompanhamentoLateral = 0.35f;

    [Header("Distância de visão")]
    [Tooltip("Até onde a câmera enxerga. Além disso, tudo some no escuro.")]
    public float distanciaVisao = 70f;
    [Tooltip("Em que fração dessa distância as coisas começam a escurecer.")]
    [Range(0f, 1f)]
    public float inicioDaNevoa = 0.55f;

    [Header("Campo de visão")]
    public float fovNormal = 60f;
    public float fovRapido = 74f;

    static CameraFollow instancia;
    Camera cam;
    float tremorInicio, tremorAte, tremorForca, tremorSemente;

    void Awake()
    {
        instancia = this;
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        // Alcance de visão curto sem mexer no ângulo: uma névoa preta esconde
        // o que está longe, e os objetos surgem aos poucos em vez de aparecerem de repente.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Color.black;
        RenderSettings.fogStartDistance = distanciaVisao + 30f;
        RenderSettings.fogEndDistance = distanciaVisao + 160f;
        if (cam != null) cam.farClipPlane = distanciaVisao + 180f;
    }

    /// <summary>Faz a câmera tremer por alguns instantes.</summary>
    public static void Tremer(float duracao, float forca)
    {
        if (instancia == null) return;
        float agora = Time.unscaledTime;
        if (agora >= instancia.tremorAte)
        {
            instancia.tremorInicio = agora;
            instancia.tremorForca = 0f;
            instancia.tremorSemente = Random.Range(0f, 1000f);
        }
        instancia.tremorAte = Mathf.Max(instancia.tremorAte, agora + duracao);
        instancia.tremorForca = Mathf.Max(instancia.tremorForca, forca);
    }

    Vector3 PosicaoDaCorrida() => new Vector3(
        alvo.position.x * acompanhamentoLateral + deslocamento.x,
        deslocamento.y,
        alvo.position.z + deslocamento.z);

    float CampoDeVisaoDaCorrida()
    {
        GameManager jogo = GameManager.Instancia;
        return Mathf.Lerp(fovNormal, fovRapido, jogo != null ? jogo.Dificuldade : 0f);
    }

    public void EnquadrarAbertura(float progresso)
    {
        if (alvo == null || cam == null) return;
        float distanciaDaCorrida = 1f - progresso;
        Vector3 posicao = PosicaoDaCorrida() + new Vector3(3.6f, 6.8f, -6.5f) * distanciaDaCorrida;
        Quaternion rotacao = Quaternion.Euler(inclinacao + 13f * distanciaDaCorrida,
            -8f * distanciaDaCorrida, -2.4f * distanciaDaCorrida);
        transform.SetPositionAndRotation(posicao, rotacao);
        cam.fieldOfView = Mathf.Lerp(49f, CampoDeVisaoDaCorrida(), progresso);
    }

    void LateUpdate()
    {
        if (alvo == null) return;

        Vector3 p = PosicaoDaCorrida();

        float agora = Time.unscaledTime;
        if (agora < tremorAte && Time.timeScale > 0f)
        {
            float restante = Mathf.Clamp01((tremorAte - agora) / 0.14f);
            float entrada = Mathf.Clamp01((agora - tremorInicio) / 0.06f);
            float envelope = Mathf.SmoothStep(0f, 1f, restante) * Mathf.SmoothStep(0f, 1f, entrada);
            float ruidoX = (Mathf.PerlinNoise(tremorSemente, agora * 24f) - 0.5f) * 2f;
            float ruidoY = (Mathf.PerlinNoise(tremorSemente + 37.4f, agora * 24f) - 0.5f) * 2f;
            p += new Vector3(ruidoX, 0f, 0f) * tremorForca * envelope;
        }
        else if (agora >= tremorAte)
        {
            tremorForca = 0f;
        }

        transform.position = p;
        transform.rotation = Quaternion.Euler(inclinacao, 0f, 0f);

        GameManager jogo = GameManager.Instancia;
        if (cam != null && jogo != null)
        {
            // O estilingue gravitacional abre a visão por um instante (sensação de arrancada).
            float alvoFov = Mathf.Lerp(fovNormal, fovRapido, jogo.Dificuldade) + 9f * jogo.ImpulsoEstilingue;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, alvoFov, 2f * Time.deltaTime);
        }
    }
}
