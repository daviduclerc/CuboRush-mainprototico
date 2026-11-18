using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Seleção radial: apenas soltar sobre uma cor confirma a troca.</summary>
public sealed class ColorWheelUI : MonoBehaviour
{
    const float RaioExterno = 156f, RaioInterno = 64f;
    static readonly string[] Nomes = { "VERMELHO", "VERDE", "AZUL", "AMARELO" };
    PlayerController jogador;
    Canvas canvas;
    RectTransform raiz, segura, roda;
    readonly ColorWheelGraphic[] setores = new ColorWheelGraphic[4];
    readonly Text[] numeros = new Text[4];
    Text titulo, instrucao;
    Font fonte;
    readonly Sprite[] mascaras = new Sprite[4];
    bool aberta, cursorAnterior;
    CursorLockMode travaAnterior;
    int selecionada = -1;
    int larguraAoAbrir, alturaAoAbrir;

    public void Inicializar(PlayerController dono)
    {
        if (raiz != null) return;
        jogador = dono;
        fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        // Canvas próprio fora da hierarquia que gira e achata o personagem.
        raiz = Rect("Roda de cores", null);
        canvas = raiz.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = raiz.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        segura = Rect("Area segura", raiz);
        roda = Rect("Seletor", segura);
        roda.anchorMin = roda.anchorMax = new Vector2(0.5f, 0.47f);
        roda.sizeDelta = new Vector2(360f, 400f);
        mascaras[0] = CriarMascara(false, false);
        mascaras[1] = CriarMascara(false, true);
        mascaras[2] = CriarMascara(true, false);
        mascaras[3] = CriarMascara(true, true);
        for (int i = 0; i < 4; i++)
        {
            var rect = Rect(Nomes[i], roda);
            rect.sizeDelta = Vector2.one * RaioExterno * 2f;
            rect.localRotation = Quaternion.Euler(0f, 0f, -90f * i);
            setores[i] = new ColorWheelGraphic(rect, mascaras[0], mascaras[1], PlayerController.Cores[i], false);
            float angulo = (90f - 90f * i) * Mathf.Deg2Rad;
            Vector2 pos = new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo)) * 110f;
            numeros[i] = Texto((i + 1).ToString(), roda, pos + Vector2.up * 11f, new Vector2(84f, 33f), 28, Color.white);
            Texto(Nomes[i], roda, pos - Vector2.up * 15f, new Vector2(92f, 22f), 14, Color.white);
        }
        var centro = Rect("Centro", roda);
        centro.sizeDelta = Vector2.one * RaioInterno * 2f;
        new ColorWheelGraphic(centro, mascaras[2], mascaras[3], new Color(0.28f, 0.38f, 0.48f), true);
        titulo = Texto("ESCOLHA", roda, new Vector2(0f, 14f), new Vector2(118f, 26f), 17, Color.white);
        instrucao = Texto("Aponte para\numa cor", roda, new Vector2(0f, -20f), new Vector2(113f, 38f), 12, Color.white);
        var rodape = Rect("Ajuda", roda);
        rodape.anchoredPosition = new Vector2(0f, -188f);
        rodape.sizeDelta = new Vector2(360f, 40f);
        var fundo = rodape.gameObject.AddComponent<Image>();
        fundo.color = new Color(0.015f, 0.025f, 0.045f, 0.9f);
        fundo.raycastTarget = false;
        Texto("Segure o botão direito • Aponte • Solte\nCentro cancela  |  Atalhos: 1–4", rodape, Vector2.zero, new Vector2(350f, 38f), 13, Color.white);
        raiz.gameObject.SetActive(false);
    }

    public void ProcessarEntrada()
    {
        if (jogador == null || raiz == null) return;
        var jogo = GameManager.Instancia;
        var mouse = Mouse.current;
        var teclado = Keyboard.current;
        if (jogo == null || jogo.Estado != EstadoJogo.Jogando || mouse == null || !Application.isFocused
            || (teclado != null && (teclado.escapeKey.wasPressedThisFrame || teclado.pKey.wasPressedThisFrame)))
        { Cancelar(); return; }
        if (mouse.rightButton.wasPressedThisFrame && !aberta)
        {
            aberta = true;
            selecionada = -1;
            cursorAnterior = Cursor.visible;
            travaAnterior = Cursor.lockState;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            larguraAoAbrir = Screen.width;
            alturaAoAbrir = Screen.height;
            raiz.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            AjustarLayout();
        }
        if (!aberta) return;
        // Redimensionar cancela a seleção para evitar confirmar a cor sob um layout que mudou.
        if (Screen.width != larguraAoAbrir || Screen.height != alturaAoAbrir) { Cancelar(); return; }
        Vector2 mousePos = mouse.position.ReadValue();
        selecionada = -1;
        if (mousePos.x >= 0f && mousePos.x < Screen.width && mousePos.y >= 0f && mousePos.y < Screen.height
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(roda, mousePos, null, out Vector2 ponto))
            selecionada = CorNoPonto(ponto);
        AtualizarDestaque();
        if (mouse.rightButton.wasReleasedThisFrame)
        {
            int confirmar = selecionada;
            Cancelar();
            if (confirmar >= 0) jogador.DefinirCor(confirmar);
        }
        else if (!mouse.rightButton.isPressed) Cancelar();
    }

    // Mesmo sentido e setores usados no desenho; o centro e o exterior não selecionam.
    public static int CorNoPonto(Vector2 ponto)
    {
        float distancia = ponto.magnitude;
        if (distancia < RaioInterno || distancia > RaioExterno) return -1;
        float graus = Mathf.Repeat(Mathf.Atan2(ponto.x, ponto.y) * Mathf.Rad2Deg + 45f, 360f);
        return Mathf.FloorToInt(graus / 90f) % 4;
    }

    void AjustarLayout()
    {
        float w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
        Rect area = Screen.safeArea;
        if (area.width <= 0f || area.height <= 0f) area = new Rect(0f, 0f, w, h);
        segura.anchorMin = new Vector2(Mathf.Clamp01(area.xMin / w), Mathf.Clamp01(area.yMin / h));
        segura.anchorMax = new Vector2(Mathf.Clamp01(area.xMax / w), Mathf.Clamp01(area.yMax / h));
        segura.offsetMin = segura.offsetMax = Vector2.zero;
        float escala = Mathf.Max(0.01f, canvas.scaleFactor);
        float tamanho = Mathf.Min(1f, area.width / escala / 400f, area.height / escala / 480f) * 0.90f;
        roda.localScale = Vector3.one * tamanho;
    }

    void AtualizarDestaque()
    {
        for (int i = 0; i < 4; i++)
        {
            setores[i].Destacar(i == selecionada);
            numeros[i].color = Color.white;
        }
        titulo.text = selecionada >= 0 ? Nomes[selecionada] : "ESCOLHA";
        titulo.color = selecionada >= 0 ? PlayerController.Cores[selecionada] : Color.white;
        instrucao.text = selecionada >= 0 ? "Solte para\nselecionar" : "Aponte para\numa cor";
    }

    void LateUpdate()
    {
        if (aberta && (GameManager.Instancia == null || GameManager.Instancia.Estado != EstadoJogo.Jogando)) Cancelar();
    }

    public void Cancelar()
    {
        if (!aberta) return;
        aberta = false;
        selecionada = -1;
        if (raiz != null) raiz.gameObject.SetActive(false);
        Cursor.lockState = travaAnterior;
        Cursor.visible = cursorAnterior;
    }

    void OnApplicationFocus(bool foco) { if (!foco) Cancelar(); }
    void OnDisable() { Cancelar(); }
    void OnDestroy()
    {
        Cancelar();
        if (raiz != null) Destroy(raiz.gameObject);
        foreach (Sprite mascara in mascaras)
        {
            if (mascara == null) continue;
            Destroy(mascara.texture);
            Destroy(mascara);
        }
    }

    // Máscaras compartilhadas por seis Images nativas; sem componente de malha personalizado.
    static Sprite CriarMascara(bool disco, bool borda)
    {
        const int lado = 384;
        const float meio = lado * 0.5f;
        var pixels = new Color32[lado * lado];
        for (int y = 0; y < lado; y++)
            for (int x = 0; x < lado; x++)
            {
                float nx = (x + 0.5f - meio) / meio;
                float ny = (y + 0.5f - meio) / meio;
                float raio = Mathf.Sqrt(nx * nx + ny * ny);
                float distancia = (1f - raio) * meio;
                if (!disco)
                {
                    distancia = Mathf.Min(distancia, (raio - RaioInterno / RaioExterno) * meio);
                    float angulo = Mathf.Abs(Mathf.Atan2(nx, ny));
                    float limite = 43.5f * Mathf.Deg2Rad;
                    // Ângulos atrás do setor permanecem fora, sem reaparecer por periodicidade.
                    float ladoAngular = angulo < limite
                        ? raio * Mathf.Sin(limite - angulo) * meio : -raio * (angulo - limite) * meio;
                    distancia = Mathf.Min(distancia, ladoAngular);
                }
                float alpha = Mathf.Clamp01(distancia + 0.5f);
                if (borda) alpha *= 1f - Mathf.Clamp01(distancia - (disco ? 5f : 3f));
                pixels[y * lado + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        var textura = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
        textura.name = disco ? "Disco da roda" : "Setor da roda";
        textura.wrapMode = TextureWrapMode.Clamp;
        textura.filterMode = FilterMode.Bilinear;
        textura.SetPixels32(pixels);
        textura.Apply(false, true);
        return Sprite.Create(textura, new UnityEngine.Rect(0f, 0f, lado, lado), Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect);
    }

    static RectTransform Rect(string nome, Transform pai)
    {
        var rect = new GameObject(nome, typeof(RectTransform)).GetComponent<RectTransform>();
        if (pai != null) rect.SetParent(pai, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        return rect;
    }

    Text Texto(string valor, Transform pai, Vector2 pos, Vector2 tamanho, int fonteTamanho, Color cor)
    {
        var rect = Rect(valor, pai);
        rect.anchoredPosition = pos;
        rect.sizeDelta = tamanho;
        var texto = rect.gameObject.AddComponent<Text>();
        texto.font = fonte;
        texto.fontStyle = FontStyle.Bold;
        texto.fontSize = fonteTamanho;
        texto.color = cor;
        texto.text = valor;
        texto.alignment = TextAnchor.MiddleCenter;
        texto.raycastTarget = false;
        texto.resizeTextForBestFit = true;
        texto.resizeTextMinSize = 9;
        texto.resizeTextMaxSize = fonteTamanho;
        var contorno = rect.gameObject.AddComponent<Outline>();
        contorno.effectColor = new Color(0.005f, 0.01f, 0.025f, 1f);
        contorno.effectDistance = new Vector2(1.3f, -1.3f);
        return texto;
    }
}

/// <summary>Fundo e contorno feitos com Image, usando a renderização padrão do Canvas.</summary>
public sealed class ColorWheelGraphic
{
    readonly Image fundo, contorno;
    readonly Color cor;
    readonly bool disco;
    bool destaque;

    public ColorWheelGraphic(RectTransform rect, Sprite preenchimento, Sprite borda, Color corSetor, bool central)
    {
        cor = corSetor;
        disco = central;
        fundo = rect.gameObject.AddComponent<Image>();
        fundo.sprite = preenchimento;
        fundo.type = Image.Type.Simple;
        fundo.raycastTarget = false;
        var objetoBorda = new GameObject("Contorno", typeof(RectTransform));
        var rectBorda = objetoBorda.GetComponent<RectTransform>();
        rectBorda.SetParent(rect, false);
        rectBorda.anchorMin = Vector2.zero;
        rectBorda.anchorMax = Vector2.one;
        rectBorda.offsetMin = rectBorda.offsetMax = Vector2.zero;
        contorno = objetoBorda.AddComponent<Image>();
        contorno.sprite = borda;
        contorno.type = Image.Type.Simple;
        contorno.raycastTarget = false;
        AplicarCores();
    }

    public void Destacar(bool valor)
    {
        if (destaque == valor) return;
        destaque = valor;
        AplicarCores();
    }

    void AplicarCores()
    {
        Color preenchimento = disco ? new Color(0.018f, 0.028f, 0.05f)
            : (destaque ? Color.Lerp(cor, Color.white, 0.12f) : cor * 0.72f);
        preenchimento.a = 1f;
        fundo.color = preenchimento;
        contorno.color = destaque ? Color.white : cor;
    }
}
