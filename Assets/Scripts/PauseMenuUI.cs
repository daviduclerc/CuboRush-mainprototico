using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Sobreposição da pausa. Usa a mesma entrada e a mesma moldura do menu inicial.</summary>
[DisallowMultipleComponent]
public sealed class PauseMenuUI : MonoBehaviour
{
    static readonly Vector2 TamanhoPainel = new Vector2(420f, 252f);
    static readonly Color Ciano = new Color(0.27f, 0.84f, 1f);
    static readonly Color Dourado = new Color(1f, 0.69f, 0.24f);
    GameManager jogo;
    RectTransform raiz, areaSegura, painel;
    Button retomar, menuInicial;
    Font fonte;

    void Start()
    {
        jogo = GetComponent<GameManager>();
        fonte = EstiloUI.Fonte;
        raiz = NovoRect("Menu de pausa", transform);
        Canvas canvas = raiz.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        raiz.gameObject.AddComponent<GraphicRaycaster>();
        var escala = raiz.gameObject.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1280f, 720f);
        escala.matchWidthOrHeight = 0.5f;

        var fundo = NovoRect("Fundo da pausa", raiz);
        Preencher(fundo);
        fundo.gameObject.AddComponent<Image>().color = new Color(0.009f, 0.014f, 0.027f, 0.55f);
        areaSegura = NovoRect("Área segura", raiz);
        Preencher(areaSegura);
        painel = NovoRect("Painel da pausa", areaSegura);
        painel.sizeDelta = TamanhoPainel;
        var moldura = painel.gameObject.AddComponent<NeonMenuGraphic>();
        moldura.color = new Color(0.21f, 0.49f, 0.72f);
        moldura.Configurar(new Color(0.022f, 0.033f, 0.062f, 0.94f), 1.6f, 0.075f);
        moldura.raycastTarget = false;

        Text titulo = CriarTexto("PAUSADO", painel, 33);
        titulo.rectTransform.anchoredPosition = new Vector2(0f, 76f);
        titulo.rectTransform.sizeDelta = new Vector2(350f, 52f);
        retomar = CriarBotao("RETOMAR", 1f, Dourado, new Color(0.25f, 0.13f, 0.045f, 0.98f));
        menuInicial = CriarBotao("MENU INICIAL", -67f, Ciano, new Color(0.037f, 0.073f, 0.112f, 0.98f));
        retomar.onClick.AddListener(jogo.RetomarPartida);
        menuInicial.onClick.AddListener(jogo.VoltarAoMenu);
        retomar.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = menuInicial, selectOnDown = menuInicial };
        menuInicial.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = retomar, selectOnDown = retomar };
        raiz.gameObject.SetActive(false);
        AtualizarVisibilidade();
    }

    void Update()
    {
        if (raiz == null || !raiz.gameObject.activeSelf || jogo.Estado != EstadoJogo.Pausado) return;
        Keyboard teclado = Keyboard.current;
        if (teclado == null) return;
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null) retomar.Select();
        if (!teclado.spaceKey.wasPressedThisFrame) return;
        GameObject selecionado = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selecionado == null || selecionado == retomar.gameObject) jogo.RetomarPartida();
    }

    void LateUpdate()
    {
        if (raiz == null) return;
        AtualizarVisibilidade();
        if (!raiz.gameObject.activeSelf) return;

        Rect segura = Screen.safeArea;
        float largura = Mathf.Max(1f, Screen.width), altura = Mathf.Max(1f, Screen.height);
        if (segura.width < 1f || segura.height < 1f) segura = new Rect(0f, 0f, largura, altura);
        areaSegura.anchorMin = new Vector2(segura.xMin / largura, segura.yMin / altura);
        areaSegura.anchorMax = new Vector2(segura.xMax / largura, segura.yMax / altura);
        float ajuste = Mathf.Min(areaSegura.rect.width * 0.90f / TamanhoPainel.x,
            areaSegura.rect.height * 0.90f / TamanhoPainel.y);
        painel.localScale = Vector3.one * Mathf.Clamp(ajuste, 0.01f, 1f);
    }

    void AtualizarVisibilidade()
    {
        bool mostrar = jogo.Estado == EstadoJogo.Pausado;
        if (raiz.gameObject.activeSelf == mostrar) return;
        if (!mostrar) LimparSelecao();
        raiz.gameObject.SetActive(mostrar);
        if (mostrar) retomar.Select();
    }

    Button CriarBotao(string nome, float y, Color cor, Color fundo)
    {
        RectTransform rect = NovoRect(nome, painel);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(326f, 54f);
        var moldura = rect.gameObject.AddComponent<NeonMenuGraphic>();
        moldura.Configurar(fundo, 2f, 0.24f);
        var botao = rect.gameObject.AddComponent<Button>();
        botao.targetGraphic = moldura;
        botao.colors = new ColorBlock
        {
            normalColor = Color.Lerp(cor, Color.black, 0.20f),
            highlightedColor = Color.Lerp(cor, Color.white, 0.50f),
            pressedColor = Color.white,
            selectedColor = Color.Lerp(cor, Color.white, 0.22f),
            disabledColor = Color.gray,
            colorMultiplier = 1f,
            fadeDuration = 0.12f
        };
        MenuButtonLabel.Criar(rect, nome, 22, new Color(0.93f, 0.97f, 1f));
        return botao;
    }

    Text CriarTexto(string nome, Transform pai, int tamanho)
    {
        var texto = NovoRect(nome, pai).gameObject.AddComponent<Text>();
        texto.text = nome;
        texto.font = fonte;
        texto.fontSize = tamanho;
        texto.fontStyle = FontStyle.Bold;
        texto.alignment = TextAnchor.MiddleCenter;
        texto.color = new Color(0.93f, 0.97f, 1f);
        texto.raycastTarget = false;
        return texto;
    }

    void LimparSelecao()
    {
        var eventos = EventSystem.current;
        if (eventos == null) return;
        GameObject selecionado = eventos.currentSelectedGameObject;
        if (selecionado == retomar.gameObject || selecionado == menuInicial.gameObject)
            eventos.SetSelectedGameObject(null);
    }

    static RectTransform NovoRect(string nome, Transform pai)
    {
        var rect = new GameObject(nome, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(pai, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    static void Preencher(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    void OnDisable()
    {
        if (raiz == null) return;
        LimparSelecao();
        raiz.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (raiz != null) Destroy(raiz.gameObject);
    }
}
