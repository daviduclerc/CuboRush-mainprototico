using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;

/// <summary>
/// Monta um buraco negro ao longe, com horizonte escuro, corona e disco de acreção.
/// Os objetos são decorativos e não interferem na jogabilidade.
/// </summary>
public class BlackHoleBackdrop : MonoBehaviour
{
    public Transform jogador;
    public float distancia = 48f;
    public float altura = 2.2f;
    public float tamanho = 32f;

    Transform conjunto;
    CameraFollow cameraFollow;
    VideoPlayer video;
    RenderTexture texturaVideo;
    Material materialVideo;
    Renderer telaVideo;
    Transform planoVideo;
    bool videoPronto;
    Material materialEspera;
    float proporcaoVideo = 16f / 9f;

    void Start()
    {
        Camera cameraPrincipal = Camera.main;
        if (cameraPrincipal != null) cameraFollow = cameraPrincipal.GetComponent<CameraFollow>();

        conjunto = new GameObject("BuracoNegro").transform;
        conjunto.SetParent(transform, false);

        // O disco texturizado substitui os anéis lisos que faziam o objeto parecer um planeta.
        GameObject plasma = GameObject.CreatePrimitive(PrimitiveType.Quad);
        plasma.name = "DiscoDeAcrecaoPlasmatico";
        plasma.transform.SetParent(conjunto, false);
        plasma.transform.localPosition = new Vector3(0f, 0f, 0.8f);
        Texture2D discoRealista = Resources.Load<Texture2D>("Space/BN_Loop_Preview");
        if (discoRealista == null) discoRealista = Resources.Load<Texture2D>("Space/blackhole_realistic");
        if (discoRealista == null) discoRealista = CriarTexturaPlasma();
        // Aumenta bastante o buraco negro sem deformar o PNG original.
        float alturaDisco = tamanho * 2.25f;
        float larguraDisco = alturaDisco * discoRealista.width / discoRealista.height;
        plasma.transform.localScale = new Vector3(larguraDisco, alturaDisco, 1f);
        Collider colisorPlasma = plasma.GetComponent<Collider>();
        if (colisorPlasma != null) Destroy(colisorPlasma);
        Shader shaderEspera = Resources.Load<Shader>("Space/BlackHoleVideo");
        materialEspera = shaderEspera != null ? new Material(shaderEspera) : CriarMaterialPlasma(discoRealista);
        materialEspera.mainTexture = discoRealista;
        if (materialEspera.HasProperty("_Aspect")) materialEspera.SetFloat("_Aspect", (float)discoRealista.width / discoRealista.height);
        plasma.GetComponent<Renderer>().sharedMaterial = materialEspera;

        // O centro escuro do horizonte de eventos já faz parte da imagem do plasma.
        ConfigurarVideo(plasma);
        AtualizarPosicao();
    }

    void ConfigurarVideo(GameObject plano)
    {
        VideoClip clip = Resources.Load<VideoClip>("Space/BN_BlackHole_Loop");
        Shader shader = Resources.Load<Shader>("Space/BlackHoleVideo");
        if (clip == null || shader == null) return;
        planoVideo = plano.transform;
        telaVideo = plano.GetComponent<Renderer>();
        int larguraVideo = Mathf.Max(2, (int)clip.width);
        int alturaVideo = Mathf.Max(2, (int)clip.height);
        proporcaoVideo = (float)larguraVideo / alturaVideo;
        texturaVideo = new RenderTexture(larguraVideo, alturaVideo, 0, RenderTextureFormat.ARGB32);
        texturaVideo.name = "Video do buraco negro BN limpo";
        texturaVideo.Create();
        materialVideo = new Material(shader);
        materialVideo.mainTexture = texturaVideo;
        materialVideo.SetFloat("_Aspect", proporcaoVideo);
        video = plano.AddComponent<VideoPlayer>();
        video.playOnAwake = false;
        video.source = VideoSource.VideoClip;
        video.clip = clip;
        video.isLooping = true;
        video.audioOutputMode = VideoAudioOutputMode.None;
        video.renderMode = VideoRenderMode.RenderTexture;
        video.targetTexture = texturaVideo;
        video.waitForFirstFrame = true;
        video.skipOnDrop = true;
        video.prepareCompleted += VideoPreparado;
        video.errorReceived += ErroVideo;
        video.Prepare();
    }

    void VideoPreparado(VideoPlayer player)
    {
        // Mantém a imagem estática até o primeiro quadro estar disponível.
        player.sendFrameReadyEvents = true;
        player.frameReady += PrimeiroQuadro;
        player.Play();
    }

    void PrimeiroQuadro(VideoPlayer player, long quadro)
    {
        if (videoPronto) return;
        videoPronto = true;
        telaVideo.sharedMaterial = materialVideo;
        float alturaVideo = tamanho * 2.25f;
        planoVideo.localScale = new Vector3(alturaVideo * proporcaoVideo, alturaVideo, 1f);
        player.frameReady -= PrimeiroQuadro;
        player.sendFrameReadyEvents = false;
    }

    void ErroVideo(VideoPlayer player, string mensagem)
    {
        Debug.LogWarning("Não foi possível reproduzir o buraco negro BN: " + mensagem);
    }

    void OnDestroy()
    {
        if (video != null)
        {
            video.prepareCompleted -= VideoPreparado;
            video.frameReady -= PrimeiroQuadro;
            video.errorReceived -= ErroVideo;
            video.Stop();
            video.targetTexture = null;
        }
        if (texturaVideo != null) { texturaVideo.Release(); Destroy(texturaVideo); }
        if (materialVideo != null) Destroy(materialVideo);
        if (materialEspera != null) Destroy(materialEspera);
    }

    Texture2D CriarTexturaPlasma()
    {
        const int lado = 512;
        Texture2D tex = new Texture2D(lado, lado, TextureFormat.RGBA32, true);
        tex.name = "PlasmaAzulDoBuracoNegro";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 8;
        Color[] pixels = new Color[lado * lado];

        for (int y = 0; y < lado; y++)
        {
            float ny = (y / (float)(lado - 1) - 0.5f) * 2f;
            for (int x = 0; x < lado; x++)
            {
                float nx = (x / (float)(lado - 1) - 0.5f) * 2f;
                float raio = Mathf.Sqrt(nx * nx + ny * ny * 2.65f);
                float angulo = Mathf.Atan2(ny * 1.63f, nx);
                float n1 = Mathf.PerlinNoise(nx * 5.2f + 7f, ny * 8.4f + 11f);
                float n2 = Mathf.PerlinNoise(nx * 14f + 19f, ny * 19f + 3f);
                float espiral = 0.5f + 0.5f * Mathf.Sin(angulo * 4f - raio * 29f + (n1 - 0.5f) * 7f);
                float bracos = Mathf.Pow(Mathf.Clamp01(espiral), 4f) * (0.45f + n1 * 0.55f);

                float bordaExterior = 1f - Mathf.SmoothStep(0.78f, 0.99f, raio);
                float bordaInterior = Mathf.SmoothStep(0.18f, 0.27f, raio);
                float banda = Mathf.Exp(-Mathf.Pow((raio - (0.43f + (n1 - 0.5f) * 0.09f)) / 0.19f, 2f));
                float anelDeLuz = Mathf.Exp(-Mathf.Pow((raio - 0.265f) / 0.022f, 2f));
                float emissao = Mathf.Clamp01(banda * (0.23f + bracos * 0.8f + n2 * 0.25f) + anelDeLuz * 0.9f);
                float doppler = Mathf.Clamp(0.78f + 0.32f * Mathf.Cos(angulo - 0.55f), 0.45f, 1.1f);
                float alpha = bordaExterior * bordaInterior * Mathf.Clamp01(0.10f + emissao * 0.95f);

                Color gas = Color.Lerp(new Color(0.18f, 0.025f, 0.52f),
                    new Color(0.025f, 0.25f, 0.92f), Mathf.Clamp01(n1 * 0.7f + raio * 0.25f));
                gas = Color.Lerp(gas, new Color(0.28f, 0.78f, 1f), Mathf.Clamp01(anelDeLuz * 0.8f + bracos * n2 * 0.25f));
                gas *= (0.38f + emissao * 1.05f) * doppler;
                pixels[y * lado + x] = new Color(gas.r, gas.g, gas.b, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(true, true);
        return tex;
    }

    Material CriarMaterialPlasma(Texture2D textura)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        Material mat = new Material(shader);
        mat.mainTexture = textura;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", textura);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        mat.color = Color.white;
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", textura);
            mat.SetColor("_EmissionColor", new Color(0.48f, 0.72f, 1f) * 1.4f);
        }
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", (float)CullMode.Off);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        return mat;
    }

    void LateUpdate()
    {
        if (jogador != null) AtualizarPosicao();
        // Movimento quase imperceptível: a imagem representa um disco distante,
        // então não deve girar como um objeto sólido.
        // A câmera e o horizonte ficam fixos; o movimento pertence ao gás do vídeo.
        if (video != null && videoPronto)
        {
            bool pausado = GameManager.Instancia != null && GameManager.Instancia.Estado == EstadoJogo.Pausado;
            if (pausado && video.isPlaying) video.Pause();
            else if (!pausado && !video.isPlaying) video.Play();
        }
    }

    void AtualizarPosicao()
    {
        Vector3 alvo = jogador != null ? jogador.position : Vector3.zero;
        float x = alvo.x * 0.35f;
        float y = altura;
        if (cameraFollow != null)
        {
            // Centraliza o buraco negro na câmera e o assenta um pouco acima do ponto de fuga da pista.
            x = alvo.x * cameraFollow.acompanhamentoLateral + cameraFollow.deslocamento.x;
            float distanciaDaCamera = Mathf.Max(1f, distancia - cameraFollow.deslocamento.z);
            y = cameraFollow.deslocamento.y + Mathf.Tan(0.5f * Mathf.Deg2Rad) * distanciaDaCamera;
        }
        conjunto.position = new Vector3(x, y, alvo.z + distancia);
    }
}

