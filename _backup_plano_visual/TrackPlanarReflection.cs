using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Espelha os objetos da pista em uma textura vista pelo piso.</summary>
[DefaultExecutionOrder(1000)]
public sealed class TrackPlanarReflection : MonoBehaviour
{
    public const int CamadaObjetos = 28;
    public PlayerController jogador;
    Camera principal, espelho;
    RenderTexture imagem;
    readonly UniversalRenderPipeline.SingleCameraRequest pedido = new UniversalRenderPipeline.SingleCameraRequest();
    int largura, altura;
    const float PlanoY = 0.025f;

    public static void IncluirObjeto(GameObject objeto)
    {
        foreach (Transform parte in objeto.GetComponentsInChildren<Transform>(true))
            parte.gameObject.layer = CamadaObjetos;
    }

    void OnEnable()
    {
        Shader.SetGlobalFloat("_TrackReflectionStrength", 0f);
    }

    void Start()
    {
        principal = GetComponent<Camera>();
        if (principal == null) principal = Camera.main;
        if (principal == null) { enabled = false; return; }
        principal.cullingMask |= 1 << CamadaObjetos;
        var objeto = new GameObject("CameraDeReflexoDaPista");
        objeto.hideFlags = HideFlags.DontSave;
        espelho = objeto.AddComponent<Camera>();
        espelho.enabled = false;
        var dados = espelho.GetUniversalAdditionalCameraData();
        dados.renderPostProcessing = false;
        dados.renderShadows = false;
        dados.requiresColorOption = CameraOverrideOption.Off;
        dados.requiresDepthOption = CameraOverrideOption.Off;
    }

    void GarantirImagem()
    {
        int w = Mathf.Clamp(principal.pixelWidth / 2, 320, 1280);
        int h = Mathf.Max(180, Mathf.RoundToInt(w / principal.aspect));
        if (imagem != null && largura == w && altura == h) return;
        if (imagem != null) { imagem.Release(); Destroy(imagem); }
        largura = w; altura = h;
        imagem = new RenderTexture(w, h, 24, RenderTextureFormat.ARGBHalf);
        imagem.name = "Reflexo real de moedas obstaculos e portais";
        imagem.wrapMode = TextureWrapMode.Clamp;
        imagem.filterMode = FilterMode.Bilinear;
        imagem.Create();
        pedido.destination = imagem;
    }

    void LateUpdate()
    {
        if (principal == null || espelho == null || jogador == null) return;
        GarantirImagem();
        espelho.CopyFrom(principal);
        espelho.enabled = false;
        espelho.targetTexture = imagem;
        espelho.cullingMask = 1 << CamadaObjetos;
        espelho.clearFlags = CameraClearFlags.SolidColor;
        espelho.backgroundColor = Color.clear;
        espelho.allowHDR = true;
        espelho.allowMSAA = false;
        espelho.useOcclusionCulling = false;
        espelho.aspect = principal.aspect;

        Matrix4x4 reflexao = Matrix4x4.identity;
        reflexao.m11 = -1f;
        reflexao.m13 = PlanoY * 2f;
        Vector3 posicao = principal.transform.position;
        posicao.y = PlanoY * 2f - posicao.y;
        espelho.transform.position = posicao;
        Vector3 frente = principal.transform.forward;
        Vector3 cima = principal.transform.up;
        espelho.transform.rotation = Quaternion.LookRotation(Vector3.Reflect(frente, Vector3.up), Vector3.Reflect(cima, Vector3.up));
        espelho.worldToCameraMatrix = principal.worldToCameraMatrix * reflexao;
        Matrix4x4 vista = espelho.worldToCameraMatrix;
        Vector3 ponto = vista.MultiplyPoint(new Vector3(0f, PlanoY + 0.008f, 0f));
        Vector3 normal = vista.MultiplyVector(Vector3.up).normalized;
        Vector4 plano = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(ponto, normal));
        espelho.projectionMatrix = principal.CalculateObliqueMatrix(plano);

        if (!RenderPipeline.SupportsRenderRequest(espelho, pedido)) return;
        bool inverterAntes = GL.invertCulling;
        try
        {
            GL.invertCulling = !inverterAntes;
            RenderPipeline.SubmitRenderRequest(espelho, pedido);
        }
        finally { GL.invertCulling = inverterAntes; }

        Shader.SetGlobalTexture("_TrackPlanarTexture", imagem);
        Shader.SetGlobalMatrix("_TrackReflectionVP", GL.GetGPUProjectionMatrix(espelho.projectionMatrix, true) * espelho.worldToCameraMatrix);
        Shader.SetGlobalVector("_TrackPlanarTexel", new Vector4(1f / largura, 1f / altura, largura, altura));
        Transform sol = jogador.VisualSolar != null ? jogador.VisualSolar : jogador.transform;
        Shader.SetGlobalVector("_TrackSunPosition", sol.position);
        Shader.SetGlobalColor("_TrackSunColor", jogador.CorVisualAtual);
        GameManager jogo = GameManager.Instancia;
        float energia = jogo != null ? Mathf.Clamp01(jogo.Estabilidade / 100f) : 1f;
        Shader.SetGlobalFloat("_TrackReflectionStrength", jogo != null && jogo.Estado == EstadoJogo.FimDeJogo ? 0f : Mathf.Lerp(0.15f, 0.75f, energia));
    }

    void OnDisable()
    {
        Shader.SetGlobalFloat("_TrackReflectionStrength", 0f);
    }

    void OnDestroy()
    {
        if (espelho != null) Destroy(espelho.gameObject);
        if (imagem != null) { imagem.Release(); Destroy(imagem); }
    }
}
