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
        Volume volume = null;
        foreach (Volume candidato in Object.FindObjectsByType<Volume>())
            if (candidato.isGlobal && candidato.weight > 0f && (volume == null || candidato.priority < volume.priority))
                volume = candidato;
        if (volume == null) return;
        if (volume.profile == null) volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        if (!volume.profile.TryGet(out ajustesCor)) ajustesCor = volume.profile.Add<ColorAdjustments>(true);
        ajustesCor.colorFilter.overrideState = true;
        ajustesCor.saturation.overrideState = true;
        ajustesCor.postExposure.overrideState = true;
        // Um pouco mais de contraste deixa o vazio mais profundo e o neon mais vivo.
        ajustesCor.contrast.overrideState = true;
        ajustesCor.contrast.value = 12f;
        if (!volume.profile.TryGet(out vinheta)) vinheta = volume.profile.Add<Vignette>(true);
        vinheta.intensity.overrideState = true;
        vinheta.color.overrideState = true;
        vinheta.smoothness.overrideState = true;
        vinheta.smoothness.value = 0.45f;
        if (!volume.profile.TryGet(out aberracao)) aberracao = volume.profile.Add<ChromaticAberration>(true);
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
        if (jogo == null || jogo.Estado == EstadoJogo.Pausado || jogo.Estado == EstadoJogo.Vitoria) return;
        float estabilidade = jogo.Estabilidade;
        float vida = jogador != null ? jogador.Vida : PlayerController.VidaMaxima;
        float perigo = Mathf.Min(estabilidade, vida);
        bool giganteAtiva = perigo <= 60f && perigo > 0f;
        float faseGigante = Mathf.InverseLerp(60f, 30f, perigo);
        float fasePulso = Mathf.InverseLerp(30f, 0f, perigo);

        if (jogo.Estado == EstadoJogo.FimDeJogo)
        {
            float colapso = jogo.EscurecimentoFinal;
            colapsoFinal = colapso >= 0.65f;
            AtualizarGigante(false, 0f, 0f, 0f);
            AtualizarNebulosa(colapso);
            return;
        }

        // Regra das cores: nada aqui muda a matiz do que está na pista. Moedas, portais e o
        // Sol continuam com a cor exata; o perigo é mostrado pelo fundo, pelas bordas da tela,
        // pelo tremor e pelo batimento, que tem o mesmo ritmo no Sol e na vinheta.
        float batida = jogador != null ? jogador.BatimentoPerigo : 0f;
        Shader.SetGlobalFloat("_SolarDeathTrackPulse", fasePulso * batida * 0.16f);

        if (ceu != null)
        {
            // Só o céu distante esquenta; ele fica atrás de tudo e não confunde as cores.
            float calor = giganteAtiva ? Mathf.Lerp(0f, 0.22f, faseGigante) : 0f;
            if (perigo <= 30f) calor = Mathf.Lerp(0.22f, 0.34f, fasePulso);
            ceu.SetColor("_Tint", Color.Lerp(corCeuOriginal, new Color(1f, 0.48f, 0.26f), calor));
            ceu.SetFloat("_Exposure", exposicaoOriginal * (1f - 0.28f * fasePulso));
        }

        float pulsoVisual = batida * fasePulso;
        if (ajustesCor != null)
        {
            // Sem filtro de cor, sem tirar saturação: só escurece um pouco o mundo no fim.
            ajustesCor.colorFilter.value = Color.white;
            ajustesCor.saturation.value = 16f;
            ajustesCor.postExposure.value = -0.25f * fasePulso;
        }
        if (vinheta != null)
        {
            // As bordas da tela são o alarme: fecham aos poucos e pulsam no batimento.
            // Cor quase preta (vinho bem escuro) para escurecer sem tingir a pista.
            vinheta.intensity.value = VinhetaBase + 0.08f * faseGigante + 0.16f * fasePulso + 0.14f * batida;
            vinheta.color.value = Color.Lerp(Color.black, new Color(0.16f, 0.0f, 0.015f),
                Mathf.Clamp01(faseGigante * 0.5f + fasePulso * 0.5f + batida * 0.5f));
        }
        // A aberração cromática separava as cores das moedas; fica desligada na partida.
        if (aberracao != null) aberracao.intensity.value = 0f;

        // A luz ambiente esquenta pouco: só o metal da pista sente, as moedas e portais brilham sozinhos.
        RenderSettings.ambientLight = Color.Lerp(luzAmbienteOriginal,
            new Color(0.30f, 0.10f, 0.06f), Mathf.Clamp01(faseGigante * 0.20f + fasePulso * 0.25f));
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
            materialGigante.color = new Color(1f, 0.92f, 0.82f);
            if (materialGigante.HasProperty("_BaseColor")) materialGigante.SetColor("_BaseColor", materialGigante.color);
            if (materialGigante.HasProperty("_EmissionColor")) materialGigante.SetColor("_EmissionColor", materialGigante.color * 2.5f);
            return;
        }

        float diametro = Mathf.Lerp(2.5f, 27f, faseGigante);
        if (fasePulso > 0f) diametro *= 1f + pulso * 0.06f;
        gigante.localScale = Vector3.one * diametro;
        Color cor = Color.Lerp(new Color(1f, 0.31f, 0.055f), new Color(0.72f, 0.012f, 0.035f), fasePulso);
        // Mais escura: é cenário ao fundo e não pode competir com as moedas vermelhas.
        cor *= 0.55f + pulso * 0.12f;
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

    void CriarNebulosaFinal()
    {
        Shader shader = Resources.Load<Shader>("Gameplay/SolarDeathNebula");
        if (shader == null || !shader.isSupported) return;
        renderNebulosa = new Renderer[2];
        materiaisNebulosa = new Material[2];
        texturasNebulosa = new Texture2D[2];
        Color[] paleta = { new Color(0.68f, 0.18f, 0.10f), new Color(0.76f, 0.37f, 0.16f) };
        for (int camada = 0; camada < renderNebulosa.Length; camada++)
        {
            GameObject plano = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plano.name = camada == 0 ? "NebulosaFinalCobre" : "NebulosaFinalDourada";
            Collider colisor = plano.GetComponent<Collider>();
            if (colisor != null) Destroy(colisor);
            plano.transform.SetParent(transform, false);
            plano.SetActive(false);

            Texture2D textura = GerarTexturaNebulosa(camada, paleta[camada]);
            texturasNebulosa[camada] = textura;
            Material material = new Material(shader) { name = "Nuvens da nebulosa final" };
            material.mainTexture = textura;
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
            float raio = new Vector2(2f * u - 1f, 2f * v - 1f).magnitude;
            float borda = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.98f, raio));
            float alpha = nuvem * borda;
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
            plano.localScale = new Vector3(largura * 0.74f, altura * 0.57f, 1f);
            bool deveAparecer = intensidade > 0.01f;
            if (plano.gameObject.activeSelf != deveAparecer) plano.gameObject.SetActive(deveAparecer);
            float opacidade = intensidade * (i == 0 ? 0.44f : 0.32f);
            materiaisNebulosa[i].SetFloat("_Opacity", opacidade);
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
