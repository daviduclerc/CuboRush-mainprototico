using UnityEngine;

/// <summary>
/// Mantém o enquadramento fixo no centro da pista enquanto avança na corrida.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform alvo;

    [Tooltip("Posição da câmera em relação ao cubo.")]
    public Vector3 deslocamento = new Vector3(0f, 4.2f, -6f);

    [Tooltip("Inclinação da câmera para baixo, em graus.")]
    public float inclinacao = 14f;

    [Header("Distância de visão")]
    [Tooltip("Até onde a câmera enxerga. Além disso, tudo some no escuro.")]
    public float distanciaVisao = 70f;
    [Tooltip("Em que fração dessa distância as coisas começam a escurecer.")]
    [Range(0f, 1f)]
    public float inicioDaNevoa = 0.55f;

    [Header("Campo de visão")]
    public float fovNormal = 60f;
    [Tooltip("Permite que os impactos desloquem a câmera durante a corrida.")]
    public bool tremerDuranteCorrida;

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
        if (instancia == null || !instancia.tremerDuranteCorrida) return;
        instancia.AplicarTremor(duracao, forca);
    }

    internal void AplicarTremor(float duracao, float forca)
    {
        float agora = Time.unscaledTime;
        if (agora >= tremorAte)
        {
            tremorInicio = agora;
            tremorForca = 0f;
            tremorSemente = Random.Range(0f, 1000f);
        }
        tremorAte = Mathf.Max(tremorAte, agora + duracao);
        tremorForca = Mathf.Max(tremorForca, forca);
    }

    internal Vector3 PosicaoDaCorrida(float z) => new Vector3(
        deslocamento.x,
        deslocamento.y,
        z + deslocamento.z);

    public void EnquadrarAbertura(float progresso)
    {
        if (alvo == null || cam == null) return;
        float distanciaDaCorrida = 1f - progresso;
        Vector3 posicao = PosicaoDaCorrida(alvo.position.z) + new Vector3(3.6f, 6.8f, -6.5f) * distanciaDaCorrida;
        Quaternion rotacao = Quaternion.Euler(inclinacao + 13f * distanciaDaCorrida,
            -8f * distanciaDaCorrida, -2.4f * distanciaDaCorrida);
        transform.SetPositionAndRotation(posicao, rotacao);
        cam.fieldOfView = Mathf.Lerp(49f, fovNormal, progresso);
    }

    void LateUpdate()
    {
        if (alvo == null) return;

        Vector3 p = PosicaoDaCorrida(alvo.position.z);

        float agora = Time.unscaledTime;
        if (agora < tremorAte && Time.timeScale > 0f)
        {
            float restante = Mathf.Clamp01((tremorAte - agora) / 0.14f);
            float entrada = Mathf.Clamp01((agora - tremorInicio) / 0.06f);
            float envelope = Mathf.SmoothStep(0f, 1f, restante) * Mathf.SmoothStep(0f, 1f, entrada);
            float ruidoX = (Mathf.PerlinNoise(tremorSemente, agora * 24f) - 0.5f) * 2f;
            p += new Vector3(ruidoX, 0f, 0f) * tremorForca * envelope;
        }
        else if (agora >= tremorAte)
        {
            tremorForca = 0f;
        }

        transform.position = p;
        transform.rotation = Quaternion.Euler(inclinacao, 0f, 0f);

        if (cam != null) cam.fieldOfView = fovNormal;
    }
}
