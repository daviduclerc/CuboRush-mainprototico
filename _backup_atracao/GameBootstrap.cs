using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta o jogo sozinho ao apertar Play (e de novo a cada reinício):
/// cria o GameManager, o cubo, o chão, os obstáculos, as estrelas e liga a câmera.
/// Se a cena já tiver um PlayerController, este script não faz nada
/// (assim dá para montar a cena à mão no futuro).
/// </summary>
public static class GameBootstrap
{
    const int QuantidadeFaixas = 5;
    const float LarguraFaixa = 2f;

    static bool inscrito;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        Montar();

        // Reiniciar o jogo recarrega a cena; monta tudo outra vez.
        if (!inscrito)
        {
            inscrito = true;
            SceneManager.sceneLoaded += (cena, modo) => Montar();
        }
    }

    static void Montar()
    {
        if (Object.FindAnyObjectByType<PlayerController>() != null) return;

        Time.timeScale = 1f;

        // Regras do jogo (precisa existir antes dos outros).
        new GameObject("GameManager").AddComponent<GameManager>();

        // A raiz preserva o movimento e as colisões; o Sol3D é apenas a aparência visível.
        GameObject cubo = new GameObject("Jogador");
        cubo.transform.position = new Vector3(0f, 0.5f, 0f);
        GameObject prefabSol = Resources.Load<GameObject>("Sol3D/Sol");
        bool usarSol = prefabSol != null;
        if (usarSol)
        {
            GameObject sol = Object.Instantiate(prefabSol, cubo.transform, false);
            sol.name = "VisualDoSol";
            sol.transform.localPosition = new Vector3(0f, 0.13f, 0f);
            sol.transform.localRotation = Quaternion.identity;
            sol.transform.localScale = Vector3.one * 0.48f;
        }
        else
        {
            Visual.Forma(PrimitiveType.Cube, "VisualDoJogador", cubo.transform,
                Vector3.zero, Vector3.one, PlayerController.Cores[0]);
        }

        PlayerController jogador = cubo.AddComponent<PlayerController>();
        jogador.visualSolar = usarSol;
        jogador.quantidadeFaixas = QuantidadeFaixas;
        jogador.larguraFaixa = LarguraFaixa;

        // Chão infinito.
        FloorGenerator chao = new GameObject("GeradorDeChao").AddComponent<FloorGenerator>();
        chao.jogador = cubo.transform;
        chao.quantidadeFaixas = QuantidadeFaixas;
        chao.larguraFaixa = LarguraFaixa;

        // Obstáculos, moedas e power-ups.
        ObstacleSpawner obstaculos = new GameObject("GeradorDeObstaculos").AddComponent<ObstacleSpawner>();
        obstaculos.jogador = jogador;

        // Estrelas de fundo.
        StarField estrelas = new GameObject("Estrelas").AddComponent<StarField>();
        estrelas.jogador = cubo.transform;

        // Referência distante para dar escala ao cenário espacial.
        BlackHoleBackdrop buracoNegro = new GameObject("CenarioEspacial").AddComponent<BlackHoleBackdrop>();
        buracoNegro.jogador = cubo.transform;

        // Asteroides com contraluz do buraco negro e cristais flutuando dos dois lados da pista.
        CampoDeAsteroides asteroides = new GameObject("CampoDeAsteroides").AddComponent<CampoDeAsteroides>();
        asteroides.jogador = cubo.transform;
        asteroides.meiaLarguraPista = QuantidadeFaixas * LarguraFaixa / 2f;

        // Atração crescente do buraco negro e ondas gravitacionais.
        FenomenosGravitacionais gravidade = new GameObject("FenomenosGravitacionais")
            .AddComponent<FenomenosGravitacionais>();
        gravidade.jogador = jogador;

        SpaceLighting luzesDaPista = new GameObject("IluminacaoEspacial").AddComponent<SpaceLighting>();
        luzesDaPista.alvo = cubo.transform;
        luzesDaPista.jogador = jogador;

        // Câmera.
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject objCamera = new GameObject("Main Camera");
            objCamera.tag = "MainCamera";
            camera = objCamera.AddComponent<Camera>();
            objCamera.AddComponent<AudioListener>();
        }

        CameraFollow seguir = camera.GetComponent<CameraFollow>();
        if (seguir == null) seguir = camera.gameObject.AddComponent<CameraFollow>();
        seguir.alvo = cubo.transform;
        TrackPlanarReflection espelhoDaPista = camera.gameObject.AddComponent<TrackPlanarReflection>();
        espelhoDaPista.jogador = jogador;

        StabilityWorldEffects fasesDaEstrela = new GameObject("FasesDaEstrela")
            .AddComponent<StabilityWorldEffects>();
        fasesDaEstrela.jogador = jogador;
        fasesDaEstrela.obstaculos = obstaculos;
        fasesDaEstrela.cameraJogo = camera;

        // Vazio quase preto e estrelas discretas; o buraco negro fica em primeiro plano.
        Texture2D mapaEstelar = SpaceEnvironment.CriarCeuRealista();
        Shader shaderCeu = Shader.Find("Skybox/Panoramic");
        if (mapaEstelar != null && shaderCeu != null)
        {
            Material ceu = new Material(shaderCeu);
            ceu.SetTexture("_MainTex", mapaEstelar);
            ceu.SetFloat("_Mapping", 1f);
            ceu.SetFloat("_ImageType", 0f);
            ceu.SetFloat("_Exposure", 0.8f);
            RenderSettings.skybox = ceu;
            camera.clearFlags = CameraClearFlags.Skybox;
        }
        else
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.001f, 0.002f, 0.008f);
        }

        // Luz fria e ambiente escuro: o metal recebe volume sem iluminar o vazio.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.055f, 0.065f, 0.085f);
        RenderSettings.ambientIntensity = 0.7f;
        Light luzPrincipal = null;
        Light[] luzes = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light luz in luzes)
            if (luz.type == LightType.Directional) { luzPrincipal = luz; break; }
        if (luzPrincipal == null)
            luzPrincipal = new GameObject("LuzEstelar").AddComponent<Light>();
        luzPrincipal.type = LightType.Directional;
        luzPrincipal.color = new Color(1f, 0.86f, 0.70f);
        luzPrincipal.intensity = 0.9f;
        luzPrincipal.shadows = LightShadows.Soft;
        luzPrincipal.shadowStrength = 0.72f;
        luzPrincipal.transform.rotation = Quaternion.Euler(38f, -28f, 0f);

        // Brilho (bloom): é o que faz as partes luminosas parecerem neon.
        camera.allowHDR = true;
        UniversalAdditionalCameraData dadosCamera = camera.GetUniversalAdditionalCameraData();
        dadosCamera.renderPostProcessing = true;
        // Rede de segurança contra flashes: qualquer pixel NaN/infinito vira preto antes do bloom,
        // em vez de ser espalhado pela tela inteira.
        dadosCamera.stopNaN = true;
        // Suaviza o serrilhado das linhas finas da pista e das bordas dos obstáculos.
        dadosCamera.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        dadosCamera.antialiasingQuality = AntialiasingQuality.High;

        Volume volume = new GameObject("BrilhoNeon").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        VolumeProfile perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        Bloom bloom = perfil.Add<Bloom>(true);
        // Bloom mais forte e espalhado, como na arte de referência: o disco do buraco negro,
        // o Sol e as linhas da pista ganham um halo luminoso.
        bloom.threshold.value = 0.9f;
        bloom.intensity.value = 1.35f;
        bloom.scatter.value = 0.75f;
        bloom.highQualityFiltering.value = true;

        // Tonemapping neutro: as luzes fortes saturam com suavidade em vez de estourar,
        // e o neon continua com a mesma cor.
        Tonemapping tonemapping = perfil.Add<Tonemapping>(true);
        tonemapping.mode.value = TonemappingMode.Neutral;

        // Grão de filme bem leve: tira a cara de "tela lisa" do vazio escuro.
        FilmGrain grao = perfil.Add<FilmGrain>(true);
        grao.type.value = FilmGrainLookup.Thin1;
        grao.intensity.value = 0.16f;
        grao.response.value = 0.85f;
        volume.profile = perfil;

        // Poeira estelar que passa riscando pelas laterais e reforça a sensação de velocidade.
        PoeiraDeVelocidade poeira = camera.GetComponent<PoeiraDeVelocidade>();
        if (poeira == null) poeira = camera.gameObject.AddComponent<PoeiraDeVelocidade>();
    }
}
