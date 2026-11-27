using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Menu inicial, opcoes e creditos com controles reais sobre a arte.</summary>
[DefaultExecutionOrder(200)]
public sealed class MainMenuUI : MonoBehaviour
{
    const float Ruptura = 1.36f;
    const float TrocaDeCena = 1.84f;
    const float ChegadaNaPista = 4.24f;
    const float FimDaIntroducao = 4.82f;
    const float ProporcaoDaArteDoMenu = 2f;

    GameManager jogo;
    Canvas canvas;
    RectTransform raiz, segura, quadro, arte, principal, modal, sombraModal, cartao;
    Text rodape, versao, tituloModal, tituloCompacto, volumeTexto, conteudo;
    RectTransform painelHistorico;
    NeonMenuGraphic molduraModal;
    Text recordeHistorico;
    readonly CartaoPartida[] cartoesPartida = new CartaoPartida[HistoricoPartidas.Capacidade];
    Button[] botoes = new Button[4];
    Button voltar, comecar;
    Slider volume;
    Toggle telaCheia;
    Font fonte;
    bool visivelAnterior;
    bool retrato;
    Vector2 posicaoArte;
    string pagina = "";
    static readonly Color Ciano = new Color(0.25f, 0.9f, 1f);
    static readonly Color Ambar = new Color(1f, 0.65f, 0.15f);
    static readonly CultureInfo FormatoBrasileiro = CultureInfo.GetCultureInfo("pt-BR");
    Image fundo;
    RawImage imagemFundo, sol, claridade;
    CanvasGroup grupoPrincipal;
    Material materialDisco, materialSol, materialClarao, materialRastro;
    bool emIntroducao;
    float inicioDaIntroducao, inicioDoMenu;
    CameraFollow cameraDaPista;
    GameplayHud hud;
    Transform solNaPista;
    Vector3 posicaoSolarOriginal;
    Quaternion rotacaoSolarOriginal;
    Renderer[] visuaisNaPista;
    bool[] visibilidadeAnterior;
    LineRenderer rastro;
    static readonly Vector2 CentroBuracoNegro = new Vector2(0.69f, 0.494f);
    static readonly Vector2 SaidaDoSol = new Vector2(0.734f, 0.654f);

    sealed class CartaoPartida
    {
        public NeonMenuGraphic moldura;
        public Text resultado, pontos, detalhes;
    }

    void Start()
    {
        jogo = GetComponent<GameManager>();
        hud = GetComponent<GameplayHud>();
        fonte = EstiloUI.Fonte;
        GarantirEntradaUI();
        raiz = Rect("Menu inicial Supernova", transform);
        canvas = raiz.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        raiz.gameObject.AddComponent<GraphicRaycaster>();
        var scaler = raiz.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        fundo = raiz.gameObject.AddComponent<Image>();
        fundo.color = new Color(0.003f, 0.005f, 0.015f, 1f);
        segura = Rect("Area segura do menu", raiz);
        quadro = Rect("Composicao responsiva", segura);
        quadro.anchorMin = quadro.anchorMax = quadro.pivot = new Vector2(0.5f, 0.5f);
        arte = Rect("Arte de abertura", raiz);
        arte.SetAsFirstSibling();
        imagemFundo = arte.gameObject.AddComponent<RawImage>();
        imagemFundo.texture = Resources.Load<Texture2D>(BlackHoleBackdrop.CaminhoArteDoMenu);
        imagemFundo.raycastTarget = false;
        Shader shaderDisco = Resources.Load<Shader>("Menu/MenuEclipseDisk");
        if (shaderDisco != null && shaderDisco.isSupported)
        {
            materialDisco = new Material(shaderDisco);
            imagemFundo.material = materialDisco;
        }
        CriarSol();
        sombraModal = Rect("Sombra das janelas", raiz);
        Stretch(sombraModal);
        sombraModal.SetSiblingIndex(arte.GetSiblingIndex() + 1);
        sombraModal.gameObject.AddComponent<Image>().color = new Color(0.003f, 0.007f, 0.02f, 0.92f);
        principal = Rect("Botoes principais", quadro);
        Stretch(principal);
        grupoPrincipal = principal.gameObject.AddComponent<CanvasGroup>();
        tituloCompacto = Texto("<color=#86DFFF>IMMINENCE OF A</color>\n<color=#FFC066>SUPERNOVA</color>",
            principal, 36, Color.white);
        string[] nomes = { "JOGAR", "OPÇÕES", "CRÉDITOS", "SAIR" };
        for (int i = 0; i < nomes.Length; i++)
        {
            bool botaoJogar = i == 0;
            botoes[i] = Botao(nomes[i], principal, botaoJogar ? Ambar : Ciano);
            var molduraBotao = botoes[i].GetComponent<NeonMenuGraphic>();
            molduraBotao.Configurar(botaoJogar
                ? new Color(0.40f, 0.14f, 0.025f, 0.98f)
                : new Color(0.013f, 0.043f, 0.055f, 0.94f), 2.2f, 0.46f);
            var rotulo = botoes[i].GetComponentInChildren<Text>();
            rotulo.fontSize = botaoJogar ? 40 : 29;
            rotulo.fontStyle = FontStyle.BoldAndItalic;
        }
        botoes[0].onClick.AddListener(Jogar);
        botoes[1].onClick.AddListener(AbrirOpcoes);
        botoes[2].onClick.AddListener(AbrirCreditos);
        botoes[3].onClick.AddListener(Sair);
        for (int i = 0; i < botoes.Length; i++)
        {
            Navigation nav = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = botoes[(i + 3) % 4], selectOnDown = botoes[(i + 1) % 4] };
            botoes[i].navigation = nav;
        }
        rodape = Texto("", principal, 13, Color.white);
        versao = Texto("VERSÃO " + Application.version, principal, 11, new Color(0.6f, 0.78f, 0.9f));
        modal = Rect("Janela de opcoes e creditos", quadro);
        Stretch(modal);
        cartao = Rect("Painel holografico", modal);
        cartao.anchorMin = cartao.anchorMax = cartao.pivot = new Vector2(0.5f, 0.5f);
        molduraModal = cartao.gameObject.AddComponent<NeonMenuGraphic>();
        molduraModal.color = Ciano;
        tituloModal = Texto("", cartao, 30, Ciano);
        volumeTexto = Texto("", cartao, 19, Color.white);
        volume = CriarSlider(cartao);
        volume.SetValueWithoutNotify(Mathf.Clamp01(PlayerPrefs.GetFloat("VolumeEfeitos", 1f)));
        AudioListener.volume = volume.value;
        volume.onValueChanged.AddListener(AplicarVolume);
        volumeTexto.text = "VOLUME DOS EFEITOS  " + Mathf.RoundToInt(volume.value * 100) + "%";
        telaCheia = CriarToggle(cartao);
        bool fullscreen = PlayerPrefs.GetInt("TelaCheia", Screen.fullScreen ? 1 : 0) == 1;
        telaCheia.SetIsOnWithoutNotify(fullscreen);
        if (!Application.isEditor && PlayerPrefs.HasKey("TelaCheia")) Screen.fullScreen = fullscreen;
        telaCheia.onValueChanged.AddListener(valor => {
            PlayerPrefs.SetInt("TelaCheia", valor ? 1 : 0);
            if (!Application.isEditor) Screen.fullScreen = valor;
        });
        conteudo = Texto("", cartao, 18, new Color(0.86f, 0.92f, 1f));
        conteudo.alignment = TextAnchor.UpperLeft;
        conteudo.horizontalOverflow = HorizontalWrapMode.Wrap;
        CriarHistorico();
        voltar = Botao("VOLTAR", cartao, Ciano);
        voltar.onClick.AddListener(FecharJanela);
        comecar = Botao("COMEÇAR", cartao, Ambar);
        comecar.onClick.AddListener(ComecarPartida);
        MostrarModal(false);
        CriarClarao();
        AtualizarLayout();
        AtualizarVisibilidade();
        if (jogo.AbrirHistoricoNoInicio) Jogar();
    }

    void CriarSol()
    {
        Shader shader = Resources.Load<Shader>("Menu/MenuEclipseSun");
        Texture2D fotosfera = Resources.Load<Texture2D>("Sol3D/Fotosfera");
        if (shader == null || !shader.isSupported || fotosfera == null) return;

        RectTransform figura = Rect("Sol emergindo do buraco negro", arte);
        figura.anchorMin = figura.anchorMax = figura.pivot = new Vector2(0.5f, 0.5f);
        sol = figura.gameObject.AddComponent<RawImage>();
        sol.raycastTarget = false;
        sol.texture = fotosfera;
        sol.color = Color.clear;
        materialSol = new Material(shader);
        sol.material = materialSol;
    }

    void CriarClarao()
    {
        Shader shader = Resources.Load<Shader>("Menu/MenuIntroBurst");
        RectTransform camada = Rect("Clarão da supernova", raiz);
        Stretch(camada);
        claridade = camada.gameObject.AddComponent<RawImage>();
        claridade.raycastTarget = false;
        claridade.texture = Texture2D.whiteTexture;
        claridade.color = Color.clear;
        if (shader == null || !shader.isSupported) return;
        materialClarao = new Material(shader);
        claridade.material = materialClarao;
    }

    void PrepararSolDaPista()
    {
        var camera = Camera.main;
        cameraDaPista = camera != null ? camera.GetComponent<CameraFollow>() : null;
        var jogador = Object.FindAnyObjectByType<PlayerController>();
        solNaPista = jogador != null ? jogador.VisualSolar : null;
        if (solNaPista == null) return;
        posicaoSolarOriginal = solNaPista.localPosition;
        rotacaoSolarOriginal = solNaPista.localRotation;
        visuaisNaPista = solNaPista.GetComponentsInChildren<Renderer>(true);
        visibilidadeAnterior = new bool[visuaisNaPista.Length];
        for (int i = 0; i < visuaisNaPista.Length; i++)
        {
            visibilidadeAnterior[i] = visuaisNaPista[i].forceRenderingOff;
            visuaisNaPista[i].forceRenderingOff = true;
        }
        if (rastro == null) CriarRastro();
        if (materialRastro != null) materialRastro.SetColor("_Tint", jogador.CorVisualAtual * 3.5f);
    }

    void RestaurarSolDaPista()
    {
        if (visuaisNaPista == null) return;
        for (int i = 0; i < visuaisNaPista.Length; i++)
            if (visuaisNaPista[i] != null)
                visuaisNaPista[i].forceRenderingOff = visibilidadeAnterior[i];
        visuaisNaPista = null;
        visibilidadeAnterior = null;
    }

    void CriarRastro()
    {
        Shader shader = Resources.Load<Shader>("Sol3D/Labaredas");
        if (shader == null || !shader.isSupported) return;
        var objeto = new GameObject("Rastro de plasma da abertura");
        objeto.transform.SetParent(transform, false);
        rastro = objeto.AddComponent<LineRenderer>();
        materialRastro = new Material(shader);
        materialRastro.SetFloat("_RastroContinuo", 1f);
        rastro.sharedMaterial = materialRastro;
        rastro.textureMode = LineTextureMode.Stretch;
        rastro.positionCount = 22;
        rastro.startWidth = 0.56f;
        rastro.endWidth = 0.035f;
        rastro.startColor = new Color(1f, 1f, 1f, 0.72f);
        rastro.endColor = new Color(1f, 1f, 1f, 0f);
        rastro.enabled = false;
    }

    void AtualizarIntroducao(float tempo)
    {
        grupoPrincipal.alpha = 1f - Suave(0f, 0.26f, tempo);
        bool mostrarPista = tempo >= TrocaDeCena;
        fundo.color = new Color(0.003f, 0.005f, 0.015f, mostrarPista ? 0f : 1f);
        imagemFundo.gameObject.SetActive(!mostrarPista);
        if (sol != null) sol.gameObject.SetActive(!mostrarPista);
        if (tempo < 2.46f) AnimarSaidaDoBuracoNegro(tempo);
        else claridade.color = Color.clear;
        if (mostrarPista) AnimarDescidaNaPista(tempo);
        if (hud != null) hud.Apresentar(Suave(ChegadaNaPista - 0.1f, FimDaIntroducao, tempo));
        if (tempo < FimDaIntroducao) return;

        jogo.IniciarPartida();
        EncerrarIntroducao();
        AtualizarVisibilidade();
    }

    void AnimarSaidaDoBuracoNegro(float tempo)
    {
        Vector2 tamanho = arte.rect.size;
        float avanco = Suave(0.16f, TrocaDeCena, tempo);
        float zoom = Mathf.Lerp(1f, 1.85f, avanco);
        arte.localScale = Vector3.one * zoom;
        Vector2 foco = arte.rect.min + Vector2.Scale(tamanho, CentroBuracoNegro);
        arte.anchoredPosition = posicaoArte - foco * (zoom - 1f) + Vector2.left * (tamanho.x * 0.175f * avanco);

        float subida = Mathf.InverseLerp(0.60f, 1.76f, tempo);
        subida *= subida;
        Vector2 centroArte = Vector2.Lerp(new Vector2(0.665f, 0.41f), SaidaDoSol, subida);
        Vector2 posicaoLocal = arte.rect.min + Vector2.Scale(centroArte, tamanho);
        Vector3 posicaoSol = arte.TransformPoint(posicaoLocal);
        if (materialDisco != null)
            materialDisco.SetFloat("_Distorcao", Suave(0.60f, 1.68f, tempo));
        AtualizarClarao(tempo, posicaoSol);
        if (sol == null || tempo >= TrocaDeCena) return;

        float ladoSemZoom = tamanho.y * Mathf.Lerp(0.09f, 0.255f, subida);
        sol.rectTransform.anchoredPosition = posicaoLocal;
        sol.rectTransform.sizeDelta = Vector2.one * ladoSemZoom;
        sol.color = new Color(1f, 1f, 1f, Suave(0.65f, 0.94f, tempo));
        materialSol.SetVector("_Center", new Vector4(centroArte.x, centroArte.y, 0f, 0f));
        materialSol.SetVector("_Size", new Vector4(ladoSemZoom / Mathf.Max(1f, tamanho.x),
            ladoSemZoom / Mathf.Max(1f, tamanho.y), 0f, 0f));
        materialSol.SetFloat("_Clock", tempo);
        materialSol.SetFloat("_Occlusion", 1f - Suave(Ruptura, 1.70f, tempo));
    }

    void AtualizarClarao(float tempo, Vector3 origem)
    {
        float intensidade = Suave(1.54f, 1.80f, tempo) * (1f - Suave(1.88f, 2.33f, tempo));
        if (materialClarao == null)
        {
            claridade.color = new Color(1f, 0.97f, 0.9f, intensidade);
            return;
        }

        Vector2 tamanho = raiz.rect.size;
        Vector2 ponto = (Vector2)raiz.InverseTransformPoint(origem) - raiz.rect.min;
        materialClarao.SetVector("_Origem", new Vector4(ponto.x / Mathf.Max(1f, tamanho.x),
            ponto.y / Mathf.Max(1f, tamanho.y), Mathf.Max(1f, tamanho.x) / Mathf.Max(1f, tamanho.y), 0f));
        materialClarao.SetFloat("_Frente", Mathf.InverseLerp(Ruptura, 2.37f, tempo));
        materialClarao.SetFloat("_Clarao", intensidade);
        claridade.color = new Color(1f, 1f, 1f, Suave(Ruptura, 1.52f, tempo) * (1f - Suave(2.24f, 2.46f, tempo)));
    }

    void AnimarDescidaNaPista(float tempo)
    {
        if (cameraDaPista != null) cameraDaPista.EnquadrarAbertura(Suave(TrocaDeCena, ChegadaNaPista + 0.18f, tempo));
        if (solNaPista == null) return;
        RestaurarSolDaPista();
        float progresso = Mathf.InverseLerp(TrocaDeCena, ChegadaNaPista, tempo);
        solNaPista.position = PosicaoDaDescida(progresso);
        solNaPista.localRotation = rotacaoSolarOriginal *
            Quaternion.Euler(Mathf.Sin(progresso * Mathf.PI) * 18f, (1f - progresso) * -155f, 0f);

        if (rastro == null) return;
        float fimDoRastro = 1f - Suave(0.80f, 1f, progresso);
        rastro.enabled = fimDoRastro > 0f && progresso > 0f;
        if (!rastro.enabled) return;
        rastro.widthMultiplier = fimDoRastro;
        for (int i = 0; i < rastro.positionCount; i++)
        {
            float atraso = Mathf.Lerp(0.008f, 0.25f, (float)i / (rastro.positionCount - 1));
            rastro.SetPosition(i, PosicaoDaDescida(Mathf.Max(0f, progresso - atraso)));
        }
    }

    Vector3 PosicaoDaDescida(float progresso)
    {
        Vector3 pista = solNaPista.parent.TransformPoint(posicaoSolarOriginal);
        float t = Mathf.SmoothStep(0f, 1f, progresso);
        float u = 1f - t;
        return pista + new Vector3(5.1f, 13f, 18f) * (u * u * u)
            + new Vector3(3.5f, 7f, 11f) * (3f * u * u * t)
            + new Vector3(0f, 0.45f, 3.6f) * (3f * u * t * t);
    }

    void EncerrarIntroducao()
    {
        emIntroducao = false;
        RestaurarSolDaPista();
        if (solNaPista != null)
        {
            solNaPista.localPosition = posicaoSolarOriginal;
            solNaPista.localRotation = rotacaoSolarOriginal;
        }
        if (cameraDaPista != null) cameraDaPista.EnquadrarAbertura(1f);
        solNaPista = null;
        cameraDaPista = null;
        if (hud != null) hud.Apresentar(0f);
        if (rastro != null) rastro.enabled = false;
        if (claridade != null) claridade.color = Color.clear;
        if (materialDisco != null) materialDisco.SetFloat("_Distorcao", 0f);
        if (arte != null) arte.localScale = Vector3.one;
    }

    static float Suave(float inicio, float fim, float tempo) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inicio, fim, tempo));

    void GarantirEntradaUI()
    {
        var eventos = Object.FindAnyObjectByType<EventSystem>();
        if (eventos == null)
        {
            var obj = new GameObject("Entrada dos menus", typeof(EventSystem));
            obj.transform.SetParent(transform, false);
            eventos = obj.GetComponent<EventSystem>();
        }
        var antigo = eventos.GetComponent<StandaloneInputModule>();
        if (antigo != null) antigo.enabled = false;
        var modulo = eventos.GetComponent<InputSystemUIInputModule>();
        if (modulo == null) modulo = eventos.gameObject.AddComponent<InputSystemUIInputModule>();
        modulo.AssignDefaultActions();
    }

    void Update()
    {
        if (jogo == null || jogo.Estado != EstadoJogo.Menu || emIntroducao) return;
        var teclado = Keyboard.current;
        if (teclado == null) return;
        if (teclado.escapeKey.wasPressedThisFrame && pagina != "") FecharJanela();
        else if (pagina == "" && teclado.spaceKey.wasPressedThisFrame &&
            (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null ||
             EventSystem.current.currentSelectedGameObject == botoes[0].gameObject)) Jogar();
    }

    void LateUpdate()
    {
        if (raiz == null) return;
        AtualizarVisibilidade();
        if (!raiz.gameObject.activeSelf) return;
        AtualizarLayout();
        if (materialDisco != null) materialDisco.SetFloat("_Clock", Time.unscaledTime - inicioDoMenu);
        if (emIntroducao) AtualizarIntroducao(Time.unscaledTime - inicioDaIntroducao);
    }

    void AtualizarVisibilidade()
    {
        bool visivel = jogo != null && jogo.Estado == EstadoJogo.Menu;
        raiz.gameObject.SetActive(visivel);
        if (visivel && !visivelAnterior)
        {
            inicioDoMenu = Time.unscaledTime;
            fundo.color = new Color(0.003f, 0.005f, 0.015f, 1f);
            imagemFundo.gameObject.SetActive(true);
            imagemFundo.color = Color.white;
            if (sol != null)
            {
                sol.gameObject.SetActive(true);
                sol.color = Color.clear;
            }
            grupoPrincipal.alpha = 1f;
            grupoPrincipal.interactable = grupoPrincipal.blocksRaycasts = true;
            pagina = "";
            principal.gameObject.SetActive(true);
            MostrarModal(false);
            rodape.text = "RECORDE  " + jogo.Recorde + "     •     MOEDAS  " + jogo.MoedasTotal;
            botoes[0].Select();
        }
        if (!visivel && emIntroducao) EncerrarIntroducao();
        visivelAnterior = visivel;
    }

    void AtualizarLayout()
    {
        float w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
        UnityEngine.Rect safe = Screen.safeArea;
        if (safe.width <= 0 || safe.height <= 0) safe = new UnityEngine.Rect(0, 0, w, h);
        segura.anchorMin = new Vector2(Mathf.Clamp01(safe.xMin / w), Mathf.Clamp01(safe.yMin / h));
        segura.anchorMax = new Vector2(Mathf.Clamp01(safe.xMax / w), Mathf.Clamp01(safe.yMax / h));
        segura.offsetMin = segura.offsetMax = Vector2.zero;

        float aw = Mathf.Max(1f, segura.rect.width), ah = Mathf.Max(1f, segura.rect.height);
        retrato = aw / Mathf.Max(1, ah) < 1.1f;
        Vector2 design = retrato ? new Vector2(720, 1024) : new Vector2(1280, 720);
        quadro.sizeDelta = design;
        float escalaLayout = Mathf.Max(0.001f, Mathf.Min(aw / design.x, ah / design.y));
        quadro.localScale = Vector3.one * escalaLayout;
        PosicionarArte();
        Vector2 expansao = retrato ? Vector2.zero : Vector2.Max(Vector2.zero,
            (new Vector2(aw, ah) / escalaLayout - design) * 0.5f);
        principal.offsetMin = -expansao;
        principal.offsetMax = expansao;
        tituloCompacto.gameObject.SetActive(retrato);
        if (retrato)
        {
            Top(tituloCompacto.rectTransform, 58f, 110f, 620f);
            for (int i = 0; i < botoes.Length; i++)
                Pos((RectTransform)botoes[i].transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -467f - i * 110f), new Vector2(i == 0 ? 420f : 390f, i == 0 ? 82f : 66f));

            Pos(rodape.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 105f), new Vector2(650f, 24f));
            Pos(versao.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 77f), new Vector2(600f, 18f));
        }
        else
        {
            Pos((RectTransform)botoes[0].transform, new Vector2(0.18f, 0.578f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(345f, 73f));
            for (int i = 1; i < botoes.Length; i++)
                Pos((RectTransform)botoes[i].transform, new Vector2(0.177f, 0.444f - (i - 1) * 0.110f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(306f, 55f));

            Pos(rodape.rectTransform, new Vector2(0.177f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 57f), new Vector2(340f, 22f));
            Pos(versao.rectTransform, new Vector2(0.177f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 33f), new Vector2(300f, 18f));
        }
        bool historico = pagina == "historico";
        cartao.sizeDelta = new Vector2(600, historico ? 455 : retrato ? 650 : 570);
        cartao.anchoredPosition = historico ? Vector2.up * 24f : Vector2.zero;
        Top(tituloModal.rectTransform, 18, 44, 540);
        Top(volumeTexto.rectTransform, 85, 28, 500);
        Top((RectTransform)volume.transform, 124, 28, 440);
        Top((RectTransform)telaCheia.transform, 174, 36, 500);
        Top(conteudo.rectTransform, pagina == "creditos" ? 100 : 235,
            pagina == "creditos" ? 340 : retrato ? 285 : 250, 500);
        Pos((RectTransform)voltar.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(historico ? -127f : 0f, 25), new Vector2(240, 42));
        Pos((RectTransform)comecar.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(127f, 25), new Vector2(240, 42));
    }

    void PosicionarArte()
    {
        Vector2 area = Vector2.Max(Vector2.one, raiz.rect.size);
        // A composição original usa 2:1, independente da resolução importada da textura.
        float altura = Mathf.Max(area.y, area.x / ProporcaoDaArteDoMenu);
        Vector2 tamanho = new Vector2(altura * ProporcaoDaArteDoMenu, altura);
        Vector2 margem = Vector2.Max(Vector2.zero, (tamanho - area) * 0.5f);
        float recuo = retrato
            ? Mathf.Clamp((0.5f - CentroBuracoNegro.x) * tamanho.x, -margem.x, margem.x)
            : margem.x * 0.82f;

        // A arte cobre todo o Canvas. A área segura serve só aos controles.
        posicaoArte = new Vector2(recuo, -margem.y);
        var centro = new Vector2(0.5f, 0.5f);
        Pos(arte, centro, centro, posicaoArte, tamanho);
    }

    public void Jogar()
    {
        if (jogo == null || jogo.Estado != EstadoJogo.Menu || emIntroducao || pagina != "") return;
        Abrir("historico", "HISTÓRICO DE PARTIDAS");
        volume.gameObject.SetActive(false);
        volumeTexto.gameObject.SetActive(false);
        telaCheia.gameObject.SetActive(false);
        AtualizarHistorico();
        AtualizarLayout();
        comecar.Select();
    }

    void ComecarPartida()
    {
        if (jogo.Estado != EstadoJogo.Menu || emIntroducao || pagina != "historico") return;
        FecharJanela();
        emIntroducao = true;
        inicioDaIntroducao = Time.unscaledTime;
        grupoPrincipal.interactable = grupoPrincipal.blocksRaycasts = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        PrepararSolDaPista();
    }

    public void AbrirOpcoes()
    {
        Abrir("opcoes", "OPÇÕES");
        volume.gameObject.SetActive(true);
        volumeTexto.gameObject.SetActive(true);
        telaCheia.gameObject.SetActive(true);
        conteudo.text = (Application.isEditor ? "Tela cheia funciona no jogo exportado.\n\n" : "") +
            "CONTROLES\nA / D ou setas: mover\nW ou Espaço: pular\nS ou seta para baixo: agachar\nE / Q: mudar a cor do Sol\nEsc / P: pausar\n\nColete moedas da sua cor para recuperar estabilidade.";
        AtualizarLayout();
        volume.Select();
    }

    public void AbrirCreditos()
    {
        Abrir("creditos", "CRÉDITOS");
        volume.gameObject.SetActive(false);
        volumeTexto.gameObject.SetActive(false);
        telaCheia.gameObject.SetActive(false);
        conteudo.text = "IMMINENCE OF A SUPERNOVA\n\nDesenvolvimento: criador do projeto.\n\nMotor do jogo: Unity.\nArte do menu: referência do projeto.\n\nObrigado por jogar!";
        AtualizarLayout();
        voltar.Select();
    }

    void Abrir(string qual, string titulo)
    {
        pagina = qual;
        principal.gameObject.SetActive(false);
        MostrarModal(true);
        tituloModal.text = titulo;
        bool historico = qual == "historico";
        molduraModal.Configurar(new Color(0.015f, 0.025f, 0.065f, 0.92f), 1.5f, historico ? 0.07f : 0.23f);
        conteudo.gameObject.SetActive(!historico);
        painelHistorico.gameObject.SetActive(historico);
        comecar.gameObject.SetActive(historico);
    }

    void CriarHistorico()
    {
        painelHistorico = Rect("Histórico de partidas", cartao);
        Stretch(painelHistorico);
        recordeHistorico = Texto("", painelHistorico, 15, EstiloUI.Ouro);
        Top(recordeHistorico.rectTransform, 63f, 24f, 520f);
        Text meta = Texto($"META   {GameManager.MetaMoedas} MOEDAS   ·   {GameManager.MetaPontos.ToString("N0", FormatoBrasileiro)} PONTOS   ·   {GameManager.MetaRaids} RAIDS DE METEOROS",
            painelHistorico, 13, EstiloUI.TextoSuave);
        Top(meta.rectTransform, 355f, 24f, 520f);

        string[] ordem = { "MAIS RECENTE", "2ª MAIS RECENTE", "3ª MAIS RECENTE" };
        Vector2 topoEsquerdo = new Vector2(0f, 1f);
        Vector2 topoDireito = Vector2.one;
        for (int i = 0; i < cartoesPartida.Length; i++)
        {
            RectTransform rect = Rect(ordem[i], painelHistorico);
            Top(rect, 102f + i * 84f, 74f, 526f);
            var ficha = new CartaoPartida();
            ficha.moldura = rect.gameObject.AddComponent<NeonMenuGraphic>();
            ficha.moldura.Configurar(new Color(0.019f, 0.032f, 0.062f, 0.96f), 1.3f, 0.10f);
            ficha.moldura.raycastTarget = false;

            Text posicao = Texto(ordem[i], rect, 12, EstiloUI.TextoSuave);
            posicao.alignment = TextAnchor.MiddleLeft;
            Pos(posicao.rectTransform, topoEsquerdo, topoEsquerdo, new Vector2(18f, -9f), new Vector2(245f, 18f));
            ficha.pontos = Texto("", rect, 23, Color.white);
            ficha.pontos.alignment = TextAnchor.MiddleLeft;
            Pos(ficha.pontos.rectTransform, topoEsquerdo, topoEsquerdo, new Vector2(18f, -31f), new Vector2(270f, 31f));
            ficha.resultado = Texto("", rect, 17, Color.white);
            ficha.resultado.alignment = TextAnchor.MiddleRight;
            Pos(ficha.resultado.rectTransform, topoDireito, topoDireito, new Vector2(-18f, -7f), new Vector2(205f, 27f));
            ficha.detalhes = Texto("", rect, 13, EstiloUI.TextoSuave);
            ficha.detalhes.alignment = TextAnchor.MiddleRight;
            Pos(ficha.detalhes.rectTransform, topoDireito, topoDireito, new Vector2(-18f, -41f), new Vector2(225f, 20f));
            cartoesPartida[i] = ficha;
        }
        painelHistorico.gameObject.SetActive(false);
    }

    void AtualizarHistorico()
    {
        recordeHistorico.text = $"RECORDE   {jogo.Recorde.ToString("N0", FormatoBrasileiro)} PONTOS";
        for (int i = 0; i < cartoesPartida.Length; i++)
        {
            CartaoPartida ficha = cartoesPartida[i];
            PartidaSalva partida = jogo.PartidaNoHistorico(i);
            if (partida == null)
            {
                ficha.pontos.text = "Sem partida salva";
                ficha.pontos.color = EstiloUI.TextoSuave;
                ficha.resultado.text = ficha.detalhes.text = "";
                ficha.moldura.color = new Color(0.17f, 0.28f, 0.40f);
                continue;
            }

            (string rotulo, Color cor) = partida.resultado switch
            {
                ResultadoPartida.Vitoria => ("VITÓRIA", EstiloUI.Verde),
                ResultadoPartida.Derrota => ("DERROTA", EstiloUI.Perigo),
                _ => ("INTERROMPIDA", Ambar)
            };
            ficha.resultado.text = rotulo;
            ficha.resultado.color = cor;
            ficha.moldura.color = Color.Lerp(EstiloUI.FundoPainel, cor, 0.65f);
            ficha.pontos.text = $"{partida.pontos.ToString("N0", FormatoBrasileiro)} PONTOS";
            ficha.pontos.color = Color.white;
            int quantidade = partida.usaRaids ? partida.raids : partida.ondas;
            string unidade = partida.usaRaids ? "RAIDS" : "ONDAS";
            ficha.detalhes.text = $"MOEDAS {partida.moedas.ToString("N0", FormatoBrasileiro)}   ·   {unidade} {quantidade.ToString("N0", FormatoBrasileiro)}";
        }
    }

    public void FecharJanela()
    {
        PlayerPrefs.Save();
        pagina = "";
        principal.gameObject.SetActive(true);
        MostrarModal(false);
        botoes[0].Select();
    }

    void MostrarModal(bool mostrar)
    {
        sombraModal.gameObject.SetActive(mostrar);
        modal.gameObject.SetActive(mostrar);
    }

    void AplicarVolume(float valor)
    {
        AudioListener.volume = Mathf.Clamp01(valor);
        PlayerPrefs.SetFloat("VolumeEfeitos", AudioListener.volume);
        volumeTexto.text = "VOLUME DOS EFEITOS  " + Mathf.RoundToInt(valor * 100) + "%";
    }

    void Sair()
    {
        PlayerPrefs.Save();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    Button Botao(string nome, Transform pai, Color cor)
    {
        var rect = Rect(nome, pai);
        var graphic = rect.gameObject.AddComponent<NeonMenuGraphic>();
        graphic.color = Color.white;
        var b = rect.gameObject.AddComponent<Button>();
        b.targetGraphic = graphic;
        b.colors = new ColorBlock { normalColor = cor * 0.85f, highlightedColor = Color.Lerp(cor, Color.white, 0.45f),
            pressedColor = Color.white, selectedColor = cor, disabledColor = Color.gray, colorMultiplier = 1f, fadeDuration = 0.12f };
        Text label = MenuButtonLabel.Criar(rect, nome, 23, Color.white);
        AdicionarSombra(label);
        return b;
    }

    Slider CriarSlider(Transform pai)
    {
        var rect = Rect("Volume", pai);
        var slider = rect.gameObject.AddComponent<Slider>();
        Image bg = Image("Trilho", rect, new Color(0.04f, 0.09f, 0.15f));
        Stretch(bg.rectTransform);
        var area = Rect("Area do preenchimento", rect); Stretch(area); area.offsetMin = new Vector2(8, 7); area.offsetMax = new Vector2(-8, -7);
        Image fill = Image("Volume preenchido", area, Ciano); Stretch(fill.rectTransform); slider.fillRect = fill.rectTransform;
        var handleArea = Rect("Area do controle", rect); Stretch(handleArea); handleArea.offsetMin = new Vector2(8, 0); handleArea.offsetMax = new Vector2(-8, 0);
        Image handle = Image("Controle do volume", handleArea, Color.white); handle.rectTransform.sizeDelta = new Vector2(16, 28);
        slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
        slider.minValue = 0; slider.maxValue = 1; slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    Toggle CriarToggle(Transform pai)
    {
        var rect = Rect("Tela cheia", pai);
        var t = rect.gameObject.AddComponent<Toggle>();
        Image box = Image("Caixa", rect, new Color(0.10f, 0.19f, 0.28f));
        Pos(box.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(30, 30));
        Image check = Image("Ativado", box.transform, Ciano); Stretch(check.rectTransform); check.rectTransform.offsetMin = new Vector2(5, 5); check.rectTransform.offsetMax = new Vector2(-5, -5);
        t.targetGraphic = box; t.graphic = check;
        Text label = Texto("TELA CHEIA", rect, 19, Color.white);
        Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(44, 0); label.alignment = TextAnchor.MiddleLeft;
        return t;
    }

    Text Texto(string valor, Transform pai, int tamanho, Color cor)
    {
        var text = Rect(valor, pai).gameObject.AddComponent<Text>();
        text.font = fonte; text.fontStyle = FontStyle.Bold; text.fontSize = tamanho;
        text.text = valor; text.color = cor; text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        AdicionarSombra(text);
        return text;
    }

    static void AdicionarSombra(Text texto)
    {
        var sombra = texto.gameObject.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.95f);
        sombra.effectDistance = new Vector2(1.5f, -1.5f);
    }

    static Image Image(string nome, Transform pai, Color cor)
    {
        var image = Rect(nome, pai).gameObject.AddComponent<Image>(); image.color = cor; return image;
    }
    static RectTransform Rect(string nome, Transform pai)
    {
        var r = new GameObject(nome, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(pai, false); return r;
    }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static void Pos(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 p, Vector2 size) { r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.anchoredPosition = p; r.sizeDelta = size; }
    static void Top(RectTransform r, float y, float h, float w) { Pos(r, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(w, h)); }
    void OnDisable()
    {
        if (emIntroducao) EncerrarIntroducao();
        visivelAnterior = false;
        if (raiz != null) raiz.gameObject.SetActive(false);
    }
    void OnDestroy()
    {
        if (raiz != null) Destroy(raiz.gameObject);
        if (materialDisco != null) Destroy(materialDisco);
        if (materialSol != null) Destroy(materialSol);
        if (materialClarao != null) Destroy(materialClarao);
        if (materialRastro != null) Destroy(materialRastro);
        if (rastro != null) Destroy(rastro.gameObject);
    }
}




