using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Mostra o desfecho antes dos controles da partida seguinte.</summary>
[DefaultExecutionOrder(450)]
public sealed class ResultadoPartidaUI : MonoBehaviour
{
    static readonly Vector2 TamanhoPainel = new Vector2(500f, 360f);
    const float SubidaPainel = 24f;
    static readonly Color CorVitoria = new Color(0.49f, 1f, 0.76f);
    static readonly Color CorDerrota = new Color(1f, 0.44f, 0.40f);

    GameManager jogo;
    RectTransform raiz, areaSegura, painel;
    CanvasGroup grupo;
    Image fundo;
    NeonMenuGraphic moldura;
    Text titulo, mensagem, pontuacao, metas, recorde;
    Button repetir, menuInicial;
    float iniciouEm;
    bool acoesVisiveis;

    void Start()
    {
        jogo = GetComponent<GameManager>();
        raiz = NovoRect("Resultado da partida", transform, Vector2.zero, Vector2.zero);
        var canvas = raiz.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        raiz.gameObject.AddComponent<GraphicRaycaster>();
        var escala = raiz.gameObject.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1280f, 720f);
        escala.matchWidthOrHeight = 0.5f;
        grupo = raiz.gameObject.AddComponent<CanvasGroup>();

        RectTransform rectFundo = NovoRect("Fundo do resultado", raiz, Vector2.zero, Vector2.zero);
        rectFundo.anchorMin = Vector2.zero;
        rectFundo.anchorMax = Vector2.one;
        fundo = rectFundo.gameObject.AddComponent<Image>();
        areaSegura = NovoRect("Área segura do resultado", raiz, Vector2.zero, Vector2.zero);
        painel = NovoRect("Painel do resultado", areaSegura, Vector2.zero, TamanhoPainel);
        moldura = painel.gameObject.AddComponent<NeonMenuGraphic>();
        moldura.Configurar(new Color(0.015f, 0.024f, 0.044f, 0.94f), 1.7f, 0.065f);
        moldura.raycastTarget = false;
        titulo = NovoTexto("Título do resultado", painel, 44, 126f, 58f);
        mensagem = NovoTexto("Mensagem do resultado", painel, 18, 79f, 30f);
        pontuacao = NovoTexto("Pontuação final", painel, 31, 25f, 48f);
        metas = NovoTexto("Objetivos da partida", painel, 16, -18f, 28f);
        recorde = NovoTexto("Recorde da partida", painel, 15, -48f, 26f);
        repetir = NovoBotao("JOGAR NOVAMENTE", -103f, EstiloUI.Ouro);
        menuInicial = NovoBotao("MENU INICIAL", -151f, EstiloUI.Ciano);
        repetir.onClick.AddListener(jogo.JogarNovamente);
        menuInicial.onClick.AddListener(jogo.VoltarAoMenu);
        repetir.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = menuInicial, selectOnDown = menuInicial };
        menuInicial.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = repetir, selectOnDown = repetir };
        raiz.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (raiz == null || !jogo.ResultadoPronto) return;
        if (!raiz.gameObject.activeSelf) AbrirResultado();

        Rect safe = Screen.safeArea;
        float w = Mathf.Max(1f, Screen.width), h = Mathf.Max(1f, Screen.height);
        if (safe.width < 1f || safe.height < 1f) safe = new Rect(0f, 0f, w, h);
        areaSegura.anchorMin = new Vector2(safe.xMin / w, safe.yMin / h);
        areaSegura.anchorMax = new Vector2(safe.xMax / w, safe.yMax / h);
        float ajuste = Mathf.Min(areaSegura.rect.width * 0.90f / TamanhoPainel.x,
            areaSegura.rect.height * 0.90f / (TamanhoPainel.y + 2f * SubidaPainel));
        ajuste = Mathf.Clamp(ajuste, 0.01f, 1f);
        painel.localScale = Vector3.one * ajuste;
        painel.anchoredPosition = Vector2.up * (SubidaPainel * ajuste);

        float tempo = Time.unscaledTime - iniciouEm;
        grupo.alpha = Mathf.SmoothStep(0f, 1f, tempo / 0.28f);
        float transicao = Mathf.SmoothStep(0f, 1f, (tempo - 0.85f) / 0.24f);
        titulo.rectTransform.anchoredPosition = Vector2.up * Mathf.Lerp(28f, 126f, transicao);
        mensagem.rectTransform.anchoredPosition = Vector2.up * Mathf.Lerp(-20f, 79f, transicao);
        fundo.color = new Color(0.004f, 0.009f, 0.025f, Mathf.Lerp(0.20f, 0.68f, transicao));
        if (!acoesVisiveis && transicao >= 1f) MostrarAcoes(true);
    }

    void AbrirResultado()
    {
        bool venceu = jogo.Estado == EstadoJogo.Vitoria;
        Color cor = venceu ? CorVitoria : CorDerrota;
        titulo.text = venceu ? "VITÓRIA" : "DERROTA";
        titulo.color = moldura.color = cor;
        mensagem.text = venceu ? "Missão cumprida" : "O Sol colapsou";
        int pontos = Mathf.FloorToInt(jogo.Pontos);
        pontuacao.text = $"{pontos:N0} PONTOS";
        metas.text = $"{jogo.Moedas}/{GameManager.MetaMoedas} MOEDAS   ·   {pontos}/{GameManager.MetaPontos} PTS   ·   " +
            $"{jogo.RaidsSuperadas}/{GameManager.MetaRaids} RAIDS";
        recorde.text = jogo.NovoRecorde ? "NOVO RECORDE!" : $"RECORDE   {jogo.Recorde:N0}";
        recorde.color = jogo.NovoRecorde ? EstiloUI.Ouro : EstiloUI.TextoSuave;
        grupo.alpha = 0f;
        iniciouEm = Time.unscaledTime;
        raiz.gameObject.SetActive(true);
        MostrarAcoes(false);
    }

    void MostrarAcoes(bool mostrar)
    {
        acoesVisiveis = mostrar;
        grupo.interactable = mostrar;
        grupo.blocksRaycasts = mostrar;
        moldura.enabled = mostrar;
        pontuacao.gameObject.SetActive(mostrar);
        metas.gameObject.SetActive(mostrar);
        recorde.gameObject.SetActive(mostrar);
        repetir.gameObject.SetActive(mostrar);
        menuInicial.gameObject.SetActive(mostrar);
        if (mostrar && EventSystem.current != null) repetir.Select();
    }

    Button NovoBotao(string rotulo, float y, Color cor)
    {
        var rect = NovoRect(rotulo, painel, Vector2.up * y, new Vector2(326f, 42f));
        var grafico = rect.gameObject.AddComponent<NeonMenuGraphic>();
        grafico.color = cor;
        grafico.Configurar(new Color(0.029f, 0.059f, 0.097f), 1.6f, 0.20f);
        var botao = rect.gameObject.AddComponent<Button>();
        botao.targetGraphic = grafico;
        var cores = botao.colors;
        cores.normalColor = new Color(0.76f, 0.76f, 0.76f);
        cores.highlightedColor = Color.white;
        cores.selectedColor = Color.white;
        botao.colors = cores;
        MenuButtonLabel.Criar(rect, rotulo, 18, Color.white);
        return botao;
    }

    static Text NovoTexto(string nome, Transform pai, int tamanho, float y, float altura)
    {
        var texto = NovoRect(nome, pai, Vector2.up * y, new Vector2(450f, altura)).gameObject.AddComponent<Text>();
        texto.font = EstiloUI.Fonte;
        texto.fontSize = tamanho;
        texto.fontStyle = FontStyle.Bold;
        texto.alignment = TextAnchor.MiddleCenter;
        texto.color = Color.white;
        texto.raycastTarget = false;
        return texto;
    }

    static RectTransform NovoRect(string nome, Transform pai, Vector2 posicao, Vector2 tamanho)
    {
        var rect = (RectTransform)new GameObject(nome, typeof(RectTransform)).transform;
        rect.SetParent(pai, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = posicao;
        rect.sizeDelta = tamanho;
        return rect;
    }
}
