using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vida e estabilidade orbitais à esquerda. Pontos, moedas, recorde, metas
/// e power-ups ficam à direita, dentro da área segura da tela.
/// </summary>
public sealed class GameplayHud : MonoBehaviour
{
    const float Largura = 300f;
    const float Margem = 16f;
    const float AlturaBase = 124f;
    const float LarguraVitais = 384f;
    const float AlturaVitais = 188f;
    const float AlturaDoAlerta = 36f;
    const float AlturaOrbita = 160f;
    const float TopoOrbita = 8f;
    const float EscalaVitais = 1.15f;
    const float PerspectivaVitais = 0.68f;
    static readonly Color VidaVerde = new Color(0.12f, 1f, 0.24f);
    static readonly Color VidaAmarela = new Color(1f, 0.85f, 0.06f);
    static readonly Color VidaVermelha = new Color(1f, 0.10f, 0.14f);

    GameManager jogo;
    Canvas canvas;
    CanvasGroup grupo;
    float aparicao;
    PlayerController jogador;
    RectTransform raiz, areaSegura, painel, painelVitais, linhaChips;
    Image moldura, brilhoTopo, iconeMoeda;
    Text textoPontos, textoMoedas, textoRecorde, textoMetas, rotuloVida, percentualVida, percentualEstabilidade;
    Text rotuloEstabilidade, avisoEstabilidade;
    Material materialVitais;
    readonly Chip[] chips = new Chip[3];

    int moedasAnteriores;
    float pulsoMoeda;
    int nivelAlertaAnterior;
    float inicioAlertaEstabilidade = -10f;

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
        raiz = NovoRect("HUD do jogo", transform);
        canvas = raiz.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        grupo = raiz.gameObject.AddComponent<CanvasGroup>();
        var escala = raiz.gameObject.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1280f, 720f);
        escala.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        escala.matchWidthOrHeight = 0.5f;
        areaSegura = NovoRect("Area segura", raiz);
        CriarVitais();

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

        textoMetas = NovoTexto("", painel, 12, EstiloUI.TextoSuave, TextAnchor.MiddleLeft);
        Posicionar(textoMetas.rectTransform, 82f, 34f);
        linhaChips = NovoRect("Power-ups ativos", painel);
        Posicionar(linhaChips, 122f, 22f);
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

    /// <summary>Opacidade do HUD durante a introdução do menu (0 = oculto, 1 = visível).</summary>
    public void Apresentar(float opacidade)
    {
        if (aparicao == opacidade) return;
        aparicao = opacidade;
        if (grupo != null) Atualizar();
    }

    void AtualizarAviso()
    {
        if (textoAviso == null) return;
        float t = Time.unscaledTime - inicioAviso;
        bool visivel = t >= 0f && t < duracaoAviso && jogo != null &&
            jogo.Estado != EstadoJogo.Menu && !jogo.EmResultado;
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
        grupo.alpha = jogo == null || jogo.EmResultado ? 0f :
            jogo.Estado == EstadoJogo.Menu ? aparicao : 1f;
        bool mostrar = grupo.alpha > 0f;
        painel.gameObject.SetActive(mostrar);
        painelVitais.gameObject.SetActive(mostrar);

        float estabilidade = jogo != null ? Mathf.Clamp(jogo.Estabilidade, 0f, 100f) : 0f;
        int nivelAlerta = mostrar && jogo.Estado != EstadoJogo.Menu && estabilidade > 0f
            ? (estabilidade < 15f ? 3 : estabilidade < 30f ? 2 : estabilidade < 60f ? 1 : 0)
            : 0;
        AtualizarAlertaEstabilidade(nivelAlerta);
        if (!mostrar) return;

        AjustarAreaSegura(out float larguraDisponivel, out float alturaDisponivel);

        int quantidadeBuffs = (jogo.TemEscudo ? 1 : 0) + (jogo.TempoIma > 0f ? 1 : 0) + (jogo.TempoDobro > 0f ? 1 : 0);
        float alturaPainel = AlturaBase + (quantidadeBuffs > 0 ? 32f : 0f);
        float alturaVitais = AlturaVitais + (nivelAlerta > 0 ? AlturaDoAlerta : 0f);
        float alturaDosPaineis = Mathf.Max(alturaPainel, alturaVitais * EscalaVitais);
        float reducao = Mathf.Min(1f, larguraDisponivel / (Largura + LarguraVitais * EscalaVitais + 64f),
            alturaDisponivel / (alturaDosPaineis + 40f));
        painelVitais.localScale = Vector3.one * (reducao * EscalaVitais);
        painelVitais.sizeDelta = new Vector2(LarguraVitais, alturaVitais);
        painelVitais.anchoredPosition = new Vector2(24f, -20f) * reducao;
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
        int pontos = Mathf.FloorToInt(jogo.Pontos);
        textoPontos.text = pontos.ToString("N0").Replace(',', '.');
        textoRecorde.text = "RECORDE  " + jogo.Recorde.ToString("N0").Replace(',', '.');
        if (jogo.Moedas != moedasAnteriores)
        {
            if (jogo.Moedas > moedasAnteriores) pulsoMoeda = 1f;
            moedasAnteriores = jogo.Moedas;
        }
        pulsoMoeda = Mathf.MoveTowards(pulsoMoeda, 0f, Time.unscaledDeltaTime * 4f);
        textoMoedas.text = jogo.Moedas.ToString();
        textoMetas.text = $"META   {Mathf.Min(jogo.Moedas, GameManager.MetaMoedas)}/{GameManager.MetaMoedas} MOEDAS   ·   " +
            $"{Mathf.Min(pontos, GameManager.MetaPontos)}/{GameManager.MetaPontos} PTS\n" +
            $"{Mathf.Min(jogo.RaidsSuperadas, GameManager.MetaRaids)}/{GameManager.MetaRaids} RAIDS DE METEOROS";
        float escalaMoeda = 1f + 0.45f * Mathf.Sin(pulsoMoeda * Mathf.PI);
        iconeMoeda.rectTransform.localScale = Vector3.one * escalaMoeda;
        textoMoedas.color = Color.Lerp(EstiloUI.Ouro, Color.white, pulsoMoeda * 0.8f);
        float larguraMoedas = textoMoedas.preferredWidth;
        iconeMoeda.rectTransform.anchoredPosition = new Vector2(-Margem - larguraMoedas - 14f, -47f);

        AtualizarVitais(estabilidade);

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

    void AtualizarVitais(float estabilidade)
    {
        float vida = jogador != null ? Mathf.Clamp(jogador.Vida, 0f, PlayerController.VidaMaxima) : PlayerController.VidaMaxima;
        float nivel = vida / PlayerController.VidaMaxima;
        Color corVida = nivel > 0.50f ? VidaVerde : nivel > 0.30f ? VidaAmarela : VidaVermelha;
        percentualVida.text = Mathf.CeilToInt(vida * 100f / PlayerController.VidaMaxima) + "%";
        percentualEstabilidade.text = Mathf.CeilToInt(estabilidade) + "%";
        rotuloVida.color = corVida;

        if (materialVitais == null) return;
        materialVitais.SetFloat("_Life", nivel);
        materialVitais.SetColor("_LifeColor", corVida);
        materialVitais.SetFloat("_Fill", estabilidade / 100f);
        materialVitais.SetColor("_SunColor", CorDaEstabilidade(estabilidade));
    }

    void AtualizarAlertaEstabilidade(int nivel)
    {
        avisoEstabilidade.gameObject.SetActive(nivel > 0);
        if (nivel == 0)
        {
            rotuloEstabilidade.color = Color.white;
            nivelAlertaAnterior = 0;
            return;
        }

        float agora = jogo.TempoDeJogo;
        if (nivel > nivelAlertaAnterior && jogo.Estado == EstadoJogo.Jogando &&
            (agora - inicioAlertaEstabilidade >= 4f || nivel == 3))
            inicioAlertaEstabilidade = agora;
        (string titulo, Color cor) = nivel switch
        {
            1 => ("ATENÇÃO: ESTABILIDADE BAIXA", new Color(1f, 0.75f, 0.18f)),
            2 => ("PERIGO: ESTABILIDADE CRÍTICA", new Color(1f, 0.38f, 0.12f)),
            _ => ("URGENTE: COLAPSO IMINENTE", new Color(1f, 0.32f, 0.36f))
        };

        if (nivel != nivelAlertaAnterior)
        {
            avisoEstabilidade.text = titulo + "\nCOLETE MOEDAS DA SUA COR";
            rotuloEstabilidade.color = cor;
            nivelAlertaAnterior = nivel;
        }
        float idade = agora - inicioAlertaEstabilidade;
        if (idade >= 0f && idade < 1.5f) cor.a = 0.65f + 0.35f * Mathf.Abs(Mathf.Cos(idade * 8f));
        avisoEstabilidade.color = cor;
    }

    static Color CorDaEstabilidade(float nivel)
    {
        Color vermelho = new Color(1f, 0.08f, 0.12f);
        Color laranja = new Color(1f, 0.40f, 0.06f);
        Color amarelo = new Color(1f, 0.85f, 0.08f);
        Color ciano = new Color(0.04f, 0.90f, 1f);
        Color azul = new Color(0.12f, 0.40f, 1f);
        if (nivel <= 30f) return Color.Lerp(vermelho, laranja, nivel / 30f);
        if (nivel <= 60f) return Color.Lerp(laranja, amarelo, (nivel - 30f) / 30f);
        if (nivel <= 80f) return Color.Lerp(amarelo, ciano, (nivel - 60f) / 20f);
        return Color.Lerp(ciano, azul, (nivel - 80f) / 20f);
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

    void CriarVitais()
    {
        painelVitais = NovoRect("Painel orbital de vida e estabilidade", areaSegura);
        painelVitais.anchorMin = painelVitais.anchorMax = painelVitais.pivot = new Vector2(0f, 1f);
        painelVitais.sizeDelta = new Vector2(LarguraVitais, AlturaVitais);

        var orbita = NovoRect("Indicadores orbitais", painelVitais);
        orbita.anchorMin = orbita.anchorMax = orbita.pivot = new Vector2(0.5f, 1f);
        orbita.anchoredPosition = Vector2.down * TopoOrbita;
        orbita.sizeDelta = new Vector2(LarguraVitais, AlturaOrbita);

        Shader shader = Resources.Load<Shader>("Gameplay/OrbitalVitals");
        if (shader != null)
        {
            var imagem = orbita.gameObject.AddComponent<RawImage>();
            imagem.raycastTarget = false;
            imagem.texture = Resources.Load<Texture2D>("Sol3D/Fotosfera");
            materialVitais = new Material(shader) { name = "Vida e estabilidade orbitais" };
            materialVitais.SetFloat("_FarScale", PerspectivaVitais);
            imagem.material = materialVitais;
        }

        rotuloVida = NovoTexto("VIDA", painelVitais, 12, VidaVerde, TextAnchor.MiddleCenter);
        Posicionar(rotuloVida.rectTransform, 0f, 16f);
        rotuloVida.gameObject.AddComponent<HudPerspectiveText>().Configurar(orbita, PerspectivaVitais);
        rotuloEstabilidade = NovoTexto("ESTABILIDADE", painelVitais, 12, Color.white, TextAnchor.MiddleCenter);
        Posicionar(rotuloEstabilidade.rectTransform, TopoOrbita + AlturaOrbita, 16f);
        rotuloEstabilidade.gameObject.AddComponent<HudPerspectiveText>().Configurar(orbita, PerspectivaVitais);

        percentualVida = CriarPercentual(orbita, 20f);
        percentualEstabilidade = CriarPercentual(orbita, -20f);

        avisoEstabilidade = NovoTexto("", painelVitais, 13, Color.white, TextAnchor.MiddleCenter);
        avisoEstabilidade.gameObject.name = "Alerta de estabilidade";
        avisoEstabilidade.fontStyle = FontStyle.Bold;
        Posicionar(avisoEstabilidade.rectTransform, AlturaVitais, AlturaDoAlerta);
        avisoEstabilidade.gameObject.AddComponent<HudPerspectiveText>().Configurar(orbita, PerspectivaVitais);
        avisoEstabilidade.gameObject.SetActive(false);
    }

    static Text CriarPercentual(RectTransform orbita, float y)
    {
        Text texto = NovoTexto("100%", orbita, 20, Color.white, TextAnchor.MiddleCenter);
        RectTransform percentual = texto.rectTransform;
        percentual.anchorMin = percentual.anchorMax = percentual.pivot = new Vector2(0.5f, 0.5f);
        percentual.anchoredPosition = Vector2.up * y;
        percentual.sizeDelta = new Vector2(100f, 28f);
        var contorno = texto.gameObject.AddComponent<Outline>();
        contorno.effectColor = Color.black;
        contorno.effectDistance = new Vector2(1.5f, -1.5f);
        texto.gameObject.AddComponent<HudPerspectiveText>().Configurar(orbita, PerspectivaVitais);
        return texto;
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
    void OnDestroy()
    {
        if (materialVitais != null) Destroy(materialVitais);
        if (raiz != null) Destroy(raiz.gameObject);
    }
}













