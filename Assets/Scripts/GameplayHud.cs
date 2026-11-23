using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD em cartão no canto superior direito: pontos em destaque, moedas, recorde,
/// barras de vida e estabilidade e etiquetas dos power-ups ativos.
/// A moldura acompanha a cor atual do Sol. Tudo fica dentro da área segura da tela.
/// </summary>
public sealed class GameplayHud : MonoBehaviour
{
    const float Largura = 300f;
    const float Margem = 16f;
    const float AlturaBase = 150f;

    GameManager jogo;
    PlayerController jogador;
    Canvas canvas;
    RectTransform raiz, areaSegura, painel, linhaChips;
    Image moldura, brilhoTopo, iconeMoeda;
    Text textoPontos, textoMoedas, textoRecorde;
    Barra barraVida, barraEstabilidade;
    readonly Chip[] chips = new Chip[3];

    int moedasAnteriores;
    float pulsoMoeda;

    // Aviso no alto da tela (ondas, estilingue, atração...).
    static GameplayHud instancia;
    RectTransform rectAviso;
    Text textoAviso;
    float inicioAviso = -10f, duracaoAviso;
    Color corAviso = Color.white;

    /// <summary>
    /// Mostra um aviso grande no alto da tela por alguns segundos.
    /// "detalhe" (opcional) aparece menor, numa segunda linha.
    /// </summary>
    public static void Avisar(string texto, string detalhe, Color cor, float duracao = 2.2f)
    {
        if (instancia == null || instancia.textoAviso == null) return;
        instancia.textoAviso.text = string.IsNullOrEmpty(detalhe)
            ? texto
            : texto + "\n<size=17>" + detalhe.ToUpperInvariant() + "</size>";
        instancia.corAviso = cor;
        instancia.inicioAviso = Time.unscaledTime;
        instancia.duracaoAviso = duracao;
    }

    sealed class Barra
    {
        public Text rotulo, valor;
        public RectTransform trilho, preenchimento;
        public Image imagem;
        public float exibido = 1f;
    }

    sealed class Chip
    {
        public RectTransform rect;
        public Image fundo, contorno;
        public Text texto;
    }

    void Start()
    {
        instancia = this;
        jogo = GetComponent<GameManager>();
        jogador = Object.FindAnyObjectByType<PlayerController>();

        raiz = NovoRect("HUD superior direito", transform);
        canvas = raiz.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var escala = raiz.gameObject.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1280f, 720f);
        escala.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        escala.matchWidthOrHeight = 0.5f;
        areaSegura = NovoRect("Area segura", raiz);

        painel = NovoRect("Cartao do HUD", areaSegura);
        painel.anchorMin = painel.anchorMax = painel.pivot = new Vector2(1f, 1f);
        Imagem(painel, EstiloUI.SpriteArredondado, EstiloUI.FundoPainel, 1.5f);

        // Aura magenta por fora da moldura, como o contorno duplo do HUD da arte de referência.
        var rectAura = NovoRect("Aura neon", painel);
        Esticar(rectAura);
        rectAura.offsetMin = new Vector2(-4f, -4f);
        rectAura.offsetMax = new Vector2(4f, 4f);
        Imagem(rectAura, EstiloUI.SpriteContorno, new Color(EstiloUI.Rosa.r, EstiloUI.Rosa.g, EstiloUI.Rosa.b, 0.4f), 1.5f);

        // Moldura neon e um filete de luz no topo, ambos tingidos pela cor do Sol.
        var rectMoldura = NovoRect("Moldura", painel);
        Esticar(rectMoldura);
        moldura = Imagem(rectMoldura, EstiloUI.SpriteContorno, EstiloUI.Ciano, 1.5f);
        var rectBrilho = NovoRect("Filete", painel);
        rectBrilho.anchorMin = new Vector2(0.5f, 1f);
        rectBrilho.anchorMax = new Vector2(0.5f, 1f);
        rectBrilho.pivot = new Vector2(0.5f, 0.5f);
        rectBrilho.sizeDelta = new Vector2(120f, 3f);
        rectBrilho.anchoredPosition = new Vector2(0f, -1f);
        brilhoTopo = Imagem(rectBrilho, EstiloUI.SpriteArredondado, EstiloUI.Ciano, 8f);

        // Linha 1: rótulo dos pontos e recorde.
        Text rotuloPontos = NovoTexto("PONTOS", painel, 12, EstiloUI.TextoSuave, TextAnchor.MiddleLeft);
        Posicionar(rotuloPontos.rectTransform, 12f, 16f);
        textoRecorde = NovoTexto("RECORDE 0", painel, 12, EstiloUI.TextoSuave, TextAnchor.MiddleRight);
        Posicionar(textoRecorde.rectTransform, 12f, 16f);

        // Linha 2: pontos grandes à esquerda, moedas à direita.
        textoPontos = NovoTexto("0", painel, 36, Color.white, TextAnchor.MiddleLeft);
        Posicionar(textoPontos.rectTransform, 27f, 40f);
        textoMoedas = NovoTexto("0", painel, 22, EstiloUI.Ouro, TextAnchor.MiddleRight);
        Posicionar(textoMoedas.rectTransform, 33f, 28f);
        var rectMoeda = NovoRect("Icone da moeda", painel);
        rectMoeda.anchorMin = rectMoeda.anchorMax = new Vector2(1f, 1f);
        rectMoeda.pivot = new Vector2(0.5f, 0.5f);
        rectMoeda.sizeDelta = new Vector2(14f, 14f);
        iconeMoeda = Imagem(rectMoeda, EstiloUI.SpriteCirculo, EstiloUI.Ouro, 1f);
        var brilhoMoeda = NovoRect("Reflexo", rectMoeda);
        brilhoMoeda.anchorMin = brilhoMoeda.anchorMax = new Vector2(0.38f, 0.64f);
        brilhoMoeda.sizeDelta = new Vector2(5f, 5f);
        Imagem(brilhoMoeda, EstiloUI.SpriteCirculo, new Color(1f, 1f, 0.9f, 0.85f), 1f);

        var separador = NovoRect("Separador", painel);
        Posicionar(separador, 72f, 1f);
        Imagem(separador, null, new Color(1f, 1f, 1f, 0.07f), 1f);

        barraVida = NovaBarra("VIDA", 82f, EstiloUI.Verde);
        barraEstabilidade = NovaBarra("ESTABILIDADE", 114f, EstiloUI.Ciano);

        linhaChips = NovoRect("Power-ups ativos", painel);
        Posicionar(linhaChips, 148f, 22f);
        for (int i = 0; i < chips.Length; i++) chips[i] = NovoChip(linhaChips);

        if (jogo != null) moedasAnteriores = jogo.Moedas;

        rectAviso = NovoRect("Aviso", areaSegura);
        rectAviso.anchorMin = rectAviso.anchorMax = new Vector2(0.5f, 1f);
        rectAviso.pivot = new Vector2(0.5f, 0.5f);
        rectAviso.sizeDelta = new Vector2(900f, 80f);
        rectAviso.anchoredPosition = new Vector2(0f, -118f);
        textoAviso = NovoTexto("", rectAviso, 30, Color.white, TextAnchor.MiddleCenter);
        textoAviso.supportRichText = true;
        textoAviso.lineSpacing = 0.9f;
        Esticar(textoAviso.rectTransform);
        var contorno = textoAviso.gameObject.AddComponent<Outline>();
        contorno.effectColor = new Color(0f, 0f, 0f, 0.65f);
        contorno.effectDistance = new Vector2(1.5f, -1.5f);
        textoAviso.gameObject.SetActive(false);

        Atualizar();
    }

    void LateUpdate()
    {
        if (canvas == null) return;
        Atualizar();
        AtualizarAviso();
    }

    void AtualizarAviso()
    {
        if (textoAviso == null) return;
        float t = Time.unscaledTime - inicioAviso;
        bool visivel = t >= 0f && t < duracaoAviso && jogo != null && jogo.Estado != EstadoJogo.Menu;
        if (textoAviso.gameObject.activeSelf != visivel) textoAviso.gameObject.SetActive(visivel);
        if (!visivel) return;
        // Entra crescendo rápido, fica parado e some no fim.
        float entrada = Mathf.Clamp01(t / 0.18f);
        float saida = Mathf.Clamp01((duracaoAviso - t) / 0.45f);
        rectAviso.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, Mathf.SmoothStep(0f, 1f, entrada));
        textoAviso.color = new Color(corAviso.r, corAviso.g, corAviso.b, Mathf.Min(entrada, saida));
    }

    void Atualizar()
    {
        bool mostrar = jogo != null && jogo.Estado != EstadoJogo.Menu;
        painel.gameObject.SetActive(mostrar);
        if (!mostrar) return;

        AjustarAreaSegura(out float larguraDisponivel, out float alturaDisponivel);

        int quantidadeBuffs = (jogo.TemEscudo ? 1 : 0) + (jogo.TempoIma > 0f ? 1 : 0) + (jogo.TempoDobro > 0f ? 1 : 0);
        float alturaPainel = AlturaBase + (quantidadeBuffs > 0 ? 32f : 0f);
        float reducao = Mathf.Min(1f, larguraDisponivel / (Largura + 48f), alturaDisponivel / (alturaPainel + 40f));
        painel.localScale = Vector3.one * reducao;
        painel.sizeDelta = new Vector2(Largura, alturaPainel);
        painel.anchoredPosition = new Vector2(-24f * reducao, -20f * reducao);

        // Cor do Sol na moldura.
        if (jogador == null) jogador = Object.FindAnyObjectByType<PlayerController>();
        // Usa a cor pura (sem o brilho do decaimento) e pulsa no mesmo batimento do Sol.
        Color corSol = jogador != null ? jogador.CorAtual : EstiloUI.Ciano;
        float batida = jogador != null ? jogador.BatimentoPerigo : 0f;
        moldura.color = new Color(corSol.r, corSol.g, corSol.b, Mathf.Clamp01(0.42f + 0.5f * batida));
        brilhoTopo.color = new Color(corSol.r, corSol.g, corSol.b, 0.95f);

        // Pontos, recorde e moedas (com um pulo rápido a cada moeda nova).
        textoPontos.text = Mathf.FloorToInt(jogo.Pontos).ToString("N0").Replace(',', '.');
        textoRecorde.text = "RECORDE  " + jogo.Recorde.ToString("N0").Replace(',', '.');
        if (jogo.Moedas != moedasAnteriores)
        {
            if (jogo.Moedas > moedasAnteriores) pulsoMoeda = 1f;
            moedasAnteriores = jogo.Moedas;
        }
        pulsoMoeda = Mathf.MoveTowards(pulsoMoeda, 0f, Time.unscaledDeltaTime * 4f);
        textoMoedas.text = jogo.Moedas.ToString();
        float escalaMoeda = 1f + 0.45f * Mathf.Sin(pulsoMoeda * Mathf.PI);
        iconeMoeda.rectTransform.localScale = Vector3.one * escalaMoeda;
        textoMoedas.color = Color.Lerp(EstiloUI.Ouro, Color.white, pulsoMoeda * 0.8f);
        float larguraMoedas = textoMoedas.preferredWidth;
        iconeMoeda.rectTransform.anchoredPosition = new Vector2(-Margem - larguraMoedas - 14f, -47f);

        // Barras.
        float vida = jogador != null ? Mathf.Clamp01(jogador.Vida / PlayerController.VidaMaxima) : 1f;
        AtualizarBarra(barraVida, vida, EstiloUI.Verde);
        AtualizarBarra(barraEstabilidade, Mathf.Clamp01(jogo.Estabilidade / 100f), EstiloUI.Ciano);

        // Etiquetas dos power-ups, lado a lado.
        float x = 0f;
        int indice = 0;
        if (jogo.TemEscudo) x = MostrarChip(indice++, "ESCUDO", EstiloUI.Ciano, x);
        if (jogo.TempoIma > 0f) x = MostrarChip(indice++, "ÍMÃ GRAV.  " + Mathf.CeilToInt(jogo.TempoIma) + "s", EstiloUI.Verde, x);
        if (jogo.TempoDobro > 0f) x = MostrarChip(indice++, "2X  " + Mathf.CeilToInt(jogo.TempoDobro) + "s", EstiloUI.Rosa, x);
        for (; indice < chips.Length; indice++) chips[indice].rect.gameObject.SetActive(false);
        linhaChips.gameObject.SetActive(quantidadeBuffs > 0);
    }

    void AjustarAreaSegura(out float larguraDisponivel, out float alturaDisponivel)
    {
        float w = Mathf.Max(1f, Screen.width), h = Mathf.Max(1f, Screen.height);
        Rect segura = Screen.safeArea;
        float x0 = Mathf.Clamp(segura.xMin, 0f, w), x1 = Mathf.Clamp(segura.xMax, 0f, w);
        float y0 = Mathf.Clamp(segura.yMin, 0f, h), y1 = Mathf.Clamp(segura.yMax, 0f, h);
        if (x1 <= x0 || y1 <= y0) { x0 = y0 = 0f; x1 = w; y1 = h; }
        areaSegura.anchorMin = new Vector2(x0 / w, y0 / h);
        areaSegura.anchorMax = new Vector2(x1 / w, y1 / h);
        areaSegura.offsetMin = areaSegura.offsetMax = Vector2.zero;
        float escala = Mathf.Max(0.01f, canvas.scaleFactor);
        alturaDisponivel = (y1 - y0) / escala;
        larguraDisponivel = (x1 - x0) / escala;
    }

    void AtualizarBarra(Barra barra, float nivel, Color saudavel)
    {
        // A barra desliza até o valor real em vez de pular.
        barra.exibido = Mathf.Lerp(barra.exibido, nivel, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        Color cor = EstiloUI.CorDoNivel(nivel, saudavel);
        float total = Largura - 2f * Margem;
        float largura = total * barra.exibido;
        barra.preenchimento.gameObject.SetActive(largura > 1f);
        barra.preenchimento.sizeDelta = new Vector2(Mathf.Max(8f, largura), 8f);
        barra.imagem.color = cor;
        barra.rotulo.color = Color.Lerp(EstiloUI.TextoSuave, cor, 0.45f);
        barra.valor.color = cor;
        barra.valor.text = Mathf.CeilToInt(nivel * 100f) + "%";
    }

    float MostrarChip(int indice, string texto, Color cor, float x)
    {
        Chip chip = chips[indice];
        chip.rect.gameObject.SetActive(true);
        chip.texto.text = texto;
        chip.texto.color = cor;
        chip.fundo.color = new Color(cor.r, cor.g, cor.b, 0.16f);
        chip.contorno.color = new Color(cor.r, cor.g, cor.b, 0.85f);
        float largura = chip.texto.preferredWidth + 22f;
        chip.rect.anchoredPosition = new Vector2(x, 0f);
        chip.rect.sizeDelta = new Vector2(largura, 22f);
        return x + largura + 6f;
    }

    // ---------- Construção ----------

    Barra NovaBarra(string nome, float topo, Color cor)
    {
        var barra = new Barra();
        barra.rotulo = NovoTexto(nome, painel, 12, EstiloUI.TextoSuave, TextAnchor.MiddleLeft);
        Posicionar(barra.rotulo.rectTransform, topo, 16f);
        barra.valor = NovoTexto("100%", painel, 13, cor, TextAnchor.MiddleRight);
        Posicionar(barra.valor.rectTransform, topo, 16f);

        barra.trilho = NovoRect("Trilho " + nome, painel);
        Posicionar(barra.trilho, topo + 19f, 8f);
        Imagem(barra.trilho, EstiloUI.SpriteArredondado, new Color(1f, 1f, 1f, 0.08f), 5f);

        barra.preenchimento = NovoRect("Nivel " + nome, barra.trilho);
        barra.preenchimento.anchorMin = barra.preenchimento.anchorMax = barra.preenchimento.pivot = new Vector2(0f, 0.5f);
        barra.preenchimento.anchoredPosition = Vector2.zero;
        barra.imagem = Imagem(barra.preenchimento, EstiloUI.SpriteArredondado, cor, 5f);
        return barra;
    }

    Chip NovoChip(Transform pai)
    {
        var chip = new Chip();
        chip.rect = NovoRect("Power-up", pai);
        chip.rect.anchorMin = chip.rect.anchorMax = chip.rect.pivot = new Vector2(0f, 1f);
        chip.fundo = Imagem(chip.rect, EstiloUI.SpriteArredondado, Color.clear, 1.8f);
        var rectContorno = NovoRect("Contorno", chip.rect);
        Esticar(rectContorno);
        chip.contorno = Imagem(rectContorno, EstiloUI.SpriteContorno, Color.white, 1.8f);
        chip.texto = NovoTexto("", chip.rect, 12, Color.white, TextAnchor.MiddleCenter);
        Esticar(chip.texto.rectTransform);
        chip.rect.gameObject.SetActive(false);
        return chip;
    }

    static RectTransform NovoRect(string nome, Transform pai)
    {
        var objeto = new GameObject(nome, typeof(RectTransform));
        var rect = objeto.GetComponent<RectTransform>();
        rect.SetParent(pai, false);
        return rect;
    }

    static void Esticar(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    /// <summary>Faixa horizontal dentro do cartão, respeitando a margem interna.</summary>
    static void Posicionar(RectTransform rect, float topo, float altura)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(Margem, -topo - altura);
        rect.offsetMax = new Vector2(-Margem, -topo);
    }

    static Image Imagem(RectTransform rect, Sprite sprite, Color cor, float multiplicador)
    {
        var imagem = rect.gameObject.AddComponent<Image>();
        imagem.sprite = sprite;
        if (sprite != null)
        {
            imagem.type = Image.Type.Sliced;
            imagem.pixelsPerUnitMultiplier = multiplicador;
        }
        imagem.color = cor;
        imagem.raycastTarget = false;
        return imagem;
    }

    static Text NovoTexto(string conteudo, Transform pai, int tamanho, Color cor, TextAnchor alinhamento)
    {
        var texto = NovoRect(conteudo, pai).gameObject.AddComponent<Text>();
        texto.font = EstiloUI.Fonte;
        texto.fontSize = tamanho;
        texto.text = conteudo;
        texto.color = cor;
        texto.alignment = alinhamento;
        texto.horizontalOverflow = HorizontalWrapMode.Overflow;
        texto.verticalOverflow = VerticalWrapMode.Overflow;
        texto.raycastTarget = false;
        var sombra = texto.gameObject.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.75f);
        sombra.effectDistance = new Vector2(1f, -1.5f);
        return texto;
    }

    void OnDisable() { if (raiz != null) raiz.gameObject.SetActive(false); }
    void OnEnable() { if (raiz != null) raiz.gameObject.SetActive(true); }
    void OnDestroy() { if (raiz != null) Destroy(raiz.gameObject); }
}
