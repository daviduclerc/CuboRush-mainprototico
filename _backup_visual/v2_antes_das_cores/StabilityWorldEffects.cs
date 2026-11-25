using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Apresenta visualmente as fases de colapso da estrela.</summary>
public sealed class StabilityWorldEffects : MonoBehaviour
{
    public PlayerController jogador;
    public ObstacleSpawner obstaculos;
    public Camera cameraJogo;

    Material ceu;
    Color corCeuOriginal = Color.white;
    float exposicaoOriginal = 0.8f;
    Color luzAmbienteOriginal;
    ColorAdjustments ajustesCor;
    Vignette vinheta;
    ChromaticAberration aberracao;
    Transform gigante;
    Renderer renderGigante;
    Material materialGigante;
    Renderer[] renderNebulosa;
    Material[] materiaisNebulosa;
    Texture2D[] texturasNebulosa;
    float inicioColapso;
    float proximoPulso;
    float proximoMeteoroZ;
    bool faseMeteoros;
    bool colapsoFinal;
    // Vinheta sempre presente, bem suave, para guiar o olhar ao centro da pista.
    const float VinhetaBase = 0.24f;

    void Start()
    {
        if (cameraJogo == null) cameraJogo = Camera.main;
        ceu = RenderSettings.skybox;
        if (ceu != null)
        {
            if (ceu.HasProperty("_Tint")) corCeuOriginal = ceu.GetColor("_Tint");
            if (ceu.HasProperty("_Exposure")) exposicaoOriginal = ceu.GetFloat("_Exposure");
        }
        luzAmbienteOriginal = RenderSettings.ambientLight;
        ConfigurarPosProcessamento();
        CriarGiganteVermelha();
        CriarNebulosaFinal();
        proximoMeteoroZ = jogador != null ? jogador.transform.position.z + 32f : 32f;
    }

    void ConfigurarPosProcessamento()
    {
        Volume volume = Object.FindAnyObjectByType<Volume>();
        if (volume == null) return;
        if (volume.profile == null) volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        ajustesCor = volume.profile.Add<ColorAdjustments>(true);
        ajustesCor.colorFilter.overrideState = true;
        ajustesCor.saturation.overrideState = true;
        ajustesCor.postExposure.overrideState = true;
        // Um pouco mais de contraste deixa o vazio mais profundo e o neon mais vivo.
        ajustesCor.contrast.overrideState = true;
        ajustesCor.contrast.value = 12f;
        vinheta = volume.profile.Add<Vignette>(true);
        vinheta.intensity.overrideState = true;
        vinheta.color.overrideState = true;
        vinheta.smoothness.overrideState = true;
        vinheta.smoothness.value = 0.45f;
        aberracao = volume.profile.Add<ChromaticAberration>(true);
        aberracao.intensity.overrideState = true;
    }

    void CriarGiganteVermelha()
    {
        GameObject objeto = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        objeto.name = "GiganteVermelhaNoHorizonte";
        Collider colisor = objeto.GetComponent<Collider>();
        if (colisor != null) Destroy(colisor);
        gigante = objeto.transform;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        materialGigante = new Material(shader);
        materialGigante.color = new Color(1f, 0.22f, 0.035f);
        if (materialGigante.HasProperty("_BaseColor"))
            materialGigante.SetColor("_BaseColor", new Color(1f, 0.22f, 0.035f));
        if (materialGigante.HasProperty("_EmissionColor"))
        {
            materialGigante.EnableKeyword("_EMISSION");
            materialGigante.SetColor("_EmissionColor", new Color(1f, 0.15f, 0.025f) * 2.2f);
        }
        renderGigante = objeto.GetComponent<Renderer>();
        renderGigante.sharedMaterial = materialGigante;
        gigante.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        GameManager jogo = GameManager.Instancia;
        if (jogo == null || jogo.Estado == EstadoJogo.Pausado) return;
        float estabilidade = jogo.Estabilidade;
        float vida = jogador != null ? jogador.Vida : PlayerController.VidaMaxima;
        float perigo = Mathf.Min(estabilidade, vida);
        bool giganteAtiva = perigo <= 60f && perigo > 0f;
        float faseGigante = Mathf.InverseLerp(60f, 30f, perigo);
        float fasePulso = Mathf.InverseLerp(30f, 0f, perigo);

        if (jogo.Estado == EstadoJogo.FimDeJogo && perigo <= 0f)
        {
            if (!colapsoFinal) inicioColapso = Time.unscaledTime;
            colapsoFinal = true;
            AplicarColapsoFinal();
            return;
        }

        if (ceu != null)
        {
            float calor = giganteAtiva ? Mathf.Lerp(0f, 0.34f, faseGigante) : 0f;
            if (perigo <= 30f) calor = Mathf.Lerp(0.34f, 0.56f, fasePulso);
            ceu.SetColor("_Tint", Color.Lerp(corCeuOriginal, new Color(1f, 0.48f, 0.26f), calor));
            ceu.SetFloat("_Exposure", exposicaoOriginal * (1f - 0.28f * fasePulso));
        }

        float filtroQuente = giganteAtiva ? Mathf.Lerp(0f, 0.30f, faseGigante) : 0f;
        float pulsoVisual = 0f;
        if (perigo <= 30f)
            pulsoVisual = (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)) * fasePulso;
        if (ajustesCor != null)
        {
            float filtro = Mathf.Clamp01(filtroQuente + fasePulso * 0.24f + pulsoVisual * 0.12f);
            ajustesCor.colorFilter.value = Color.Lerp(Color.white, new Color(1f, 0.56f, 0.47f), filtro);
            ajustesCor.saturation.value = Mathf.Lerp(8f, -24f, fasePulso);
            ajustesCor.postExposure.value = -0.8f * fasePulso;
        }
        if (vinheta != null)
        {
            vinheta.intensity.value = VinhetaBase + 0.10f * faseGigante + 0.30f * fasePulso + 0.10f * pulsoVisual;
            vinheta.color.value = Color.Lerp(Color.black, new Color(0.23f, 0.012f, 0.025f), fasePulso);
        }
        if (aberracao != null)
            aberracao.intensity.value = 0.18f * faseGigante + 0.25f * fasePulso + 0.12f * pulsoVisual;

        RenderSettings.ambientLight = Color.Lerp(luzAmbienteOriginal,
            new Color(0.44f, 0.12f, 0.06f), Mathf.Clamp01(filtroQuente + fasePulso * 0.55f));
        AtualizarGigante(giganteAtiva, faseGigante, fasePulso, pulsoVisual);
        AtualizarMeteoros(jogo, perigo);

        if (estabilidade <= 50f && estabilidade > 0f && jogo.Estado == EstadoJogo.Jogando && Time.unscaledTime >= proximoPulso)
        {
            float gravidadeDaFase = Mathf.InverseLerp(50f, 0f, estabilidade);
            float duracaoTremor = Mathf.Lerp(0.20f, 0.48f, gravidadeDaFase);
            float forcaTremor = Mathf.Lerp(0.22f, 0.72f, gravidadeDaFase);
            CameraFollow.Tremer(duracaoTremor, forcaTremor);
            if (perigo <= 30f) jogo.PulsoTermico();
            proximoPulso = Time.unscaledTime + Mathf.Lerp(1.1f, 0.38f, gravidadeDaFase);
        }
    }

    void AtualizarGigante(bool ativa, float faseGigante, float fasePulso, float pulso)
    {
        if (gigante == null || cameraJogo == null) return;
        bool visivel = ativa || colapsoFinal;
        if (gigante.gameObject.activeSelf != visivel) gigante.gameObject.SetActive(visivel);
        if (!visivel) return;

        gigante.position = cameraJogo.transform.TransformPoint(new Vector3(0f, 11f, 105f));
        gigante.rotation = Quaternion.identity;
        if (colapsoFinal)
        {
            gigante.localScale = Vector3.one * 1.5f;
            materialGigante.color = new Color(0.80f, 0.91f, 1f);
            if (materialGigante.HasProperty("_BaseColor")) materialGigante.SetColor("_BaseColor", materialGigante.color);
            if (materialGigante.HasProperty("_EmissionColor")) materialGigante.SetColor("_EmissionColor", materialGigante.color * 2.5f);
            return;
        }

        float diametro = Mathf.Lerp(2.5f, 27f, faseGigante);
        if (fasePulso > 0f) diametro *= 1f + pulso * 0.06f;
        gigante.localScale = Vector3.one * diametro;
        Color cor = Color.Lerp(new Color(1f, 0.31f, 0.055f), new Color(0.72f, 0.012f, 0.035f), fasePulso);
        cor *= 0.88f + pulso * 0.28f;
        materialGigante.color = cor;
        if (materialGigante.HasProperty("_BaseColor")) materialGigante.SetColor("_BaseColor", cor);
        if (materialGigante.HasProperty("_EmissionColor")) materialGigante.SetColor("_EmissionColor", cor * 2.2f);
    }

    void AtualizarMeteoros(GameManager jogo, float estabilidade)
    {
        bool ativa = estabilidade <= 60f && estabilidade > 30f && jogo.Estado == EstadoJogo.Jogando;
        if (!ativa)
        {
            faseMeteoros = false;
            if (jogador != null) proximoMeteoroZ = jogador.transform.position.z + 32f;
            return;
        }
        if (!faseMeteoros)
        {
            faseMeteoros = true;
            proximoMeteoroZ = jogador.transform.position.z + 34f;
        }
        if (jogador == null || obstaculos == null) return;
        float limite = jogador.transform.position.z + 115f;
        while (proximoMeteoroZ <= limite)
        {
            if (proximoMeteoroZ > jogador.transform.position.z + 20f)
                obstaculos.CriarMeteoro(proximoMeteoroZ);
            proximoMeteoroZ += Random.Range(38f, 52f);
        }
    }

    void AplicarColapsoFinal()
    {
        if (ceu != null && ceu.HasProperty("_Exposure")) ceu.SetFloat("_Exposure", 0.08f);
        RenderSettings.ambientLight = Color.black;
        if (ajustesCor != null)
        {
            ajustesCor.colorFilter.value = new Color(0.45f, 0.48f, 0.58f);
            ajustesCor.saturation.value = -85f;
            ajustesCor.postExposure.value = -2.4f;
        }
        if (vinheta != null)
        {
            vinheta.intensity.value = 0.68f;
            vinheta.color.value = Color.black;
        }
        if (aberracao != null) aberracao.intensity.value = 0.04f;
        AtualizarNebulosa(Mathf.Clamp01((Time.unscaledTime - inicioColapso) / 1.15f));
        AtualizarGigante(false, 0f, 0f, 0f);
    }

    void CriarNebulosaFinal()
    {
        renderNebulosa = new Renderer[2];
        materiaisNebulosa = new Material[2];
        texturasNebulosa = new Texture2D[2];
        Color[] paleta = { new Color(0.26f, 0.08f, 0.95f), new Color(0.04f, 0.62f, 1f) };
        for (int camada = 0; camada < renderNebulosa.Length; camada++)
        {
            GameObject plano = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plano.name = camada == 0 ? "NebulosaFinalVioleta" : "NebulosaFinalAzul";
            Collider colisor = plano.GetComponent<Collider>();
            if (colisor != null) Destroy(colisor);
            plano.transform.SetParent(transform, false);
            plano.SetActive(false);

            Texture2D textura = GerarTexturaNebulosa(camada, paleta[camada]);
            texturasNebulosa[camada] = textura;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material material = new Material(shader) { name = "Nuvens da nebulosa final" };
            material.mainTexture = textura;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", textura);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            material.color = Color.white;
            // A nuvem é transparente até o colapso; o brilho próprio mantém a cor no escuro.
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_Surface", 1);
            material.SetInt("_Blend", 0);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", paleta[camada] * 2.8f);
            }
            materiaisNebulosa[camada] = material;
            Renderer renderer = plano.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderNebulosa[camada] = renderer;
        }
    }

    Texture2D GerarTexturaNebulosa(int semente, Color cor)
    {
        const int resolucao = 256;
        Texture2D textura = new Texture2D(resolucao, resolucao, TextureFormat.RGBA32, false);
        textura.name = "Textura procedural de nebulosa";
        textura.wrapMode = TextureWrapMode.Clamp;
        textura.filterMode = FilterMode.Bilinear;
        float deslocamento = 23.7f + semente * 61.3f;
        Color[] pixels = new Color[resolucao * resolucao];
        for (int y = 0; y < resolucao; y++)
        for (int x = 0; x < resolucao; x++)
        {
            float u = x / (float)resolucao;
            float v = y / (float)resolucao;
            float nuvemGrande = Mathf.PerlinNoise(u * 4.8f + deslocamento, v * 4.8f + deslocamento);
            float detalhe = Mathf.PerlinNoise(u * 12f + deslocamento * 0.37f, v * 12f + deslocamento * 0.73f);
            float poeira = Mathf.PerlinNoise(u * 28f + deslocamento, v * 28f + deslocamento * 0.5f);
            float nuvem = Mathf.Clamp01((nuvemGrande * 0.68f + detalhe * 0.25f + poeira * 0.07f - 0.48f) * 2.4f);
            float borda = Mathf.Clamp01(Mathf.Min(Mathf.Min(u, 1f - u) * 5f, Mathf.Min(v, 1f - v) * 5f));
            float alpha = nuvem * borda * 0.86f;
            float brilho = 0.62f + detalhe * 0.7f;
            pixels[y * resolucao + x] = new Color(cor.r * brilho, cor.g * brilho, cor.b * brilho, alpha);
        }
        textura.SetPixels(pixels);
        textura.Apply(false, true);
        return textura;
    }

    void AtualizarNebulosa(float intensidade)
    {
        if (renderNebulosa == null || cameraJogo == null) return;
        float distancia = 230f;
        float altura = 2f * distancia * Mathf.Tan(cameraJogo.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float largura = altura * cameraJogo.aspect;
        for (int i = 0; i < renderNebulosa.Length; i++)
        {
            Renderer renderer = renderNebulosa[i];
            if (renderer == null) continue;
            Transform plano = renderer.transform;
            plano.position = cameraJogo.transform.TransformPoint(new Vector3(i == 0 ? -5f : 7f, i == 0 ? 2f : -3f, distancia + i * 2f));
            plano.rotation = cameraJogo.transform.rotation;
            plano.localScale = new Vector3(largura * 1.22f, altura * 1.22f, 1f);
            bool deveAparecer = intensidade > 0.01f;
            if (plano.gameObject.activeSelf != deveAparecer) plano.gameObject.SetActive(deveAparecer);
            Color cor = Color.white;
            cor.a = intensidade * (i == 0 ? 0.92f : 0.78f);
            Material material = materiaisNebulosa[i];
            if (material != null)
            {
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", cor);
                material.color = cor;
                if (material.HasProperty("_EmissionColor"))
                {
                    Color emissao = i == 0 ? new Color(0.38f, 0.08f, 1f) : new Color(0.05f, 0.52f, 1f);
                    material.SetColor("_EmissionColor", emissao * (3f * intensidade));
                }
            }
        }
    }

    void OnDestroy()
    {
        if (materialGigante != null) Destroy(materialGigante);
        if (materiaisNebulosa != null)
            for (int i = 0; i < materiaisNebulosa.Length; i++) if (materiaisNebulosa[i] != null) Destroy(materiaisNebulosa[i]);
        if (texturasNebulosa != null)
            for (int i = 0; i < texturasNebulosa.Length; i++) if (texturasNebulosa[i] != null) Destroy(texturasNebulosa[i]);
    }
}
