using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD compacto no canto superior direito, contido na área segura da tela.</summary>
public sealed class GameplayHud : MonoBehaviour
{
    GameManager jogo;
    Canvas canvas;
    RectTransform raiz, areaSegura, painel, divisor;
    Font fonte;
    readonly Text[] valores = new Text[3];
    readonly Text[] buffs = new Text[3];
    PlayerController jogador;
    Text textoVida, textoEstabilidade;
    RectTransform barraVida, barraEstabilidade;
    static readonly Color Ciano = new Color(0.35f, 0.92f, 1f);
    static readonly Color Verde = new Color(0.45f, 1f, 0.62f);
    static readonly Color Rosa = new Color(1f, 0.52f, 0.87f);

    void Start()
    {
        jogo = GetComponent<GameManager>();
        jogador = Object.FindAnyObjectByType<PlayerController>();
        fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        painel = NovoRect("Pontos e melhorias", areaSegura);
        painel.anchorMin = painel.anchorMax = new Vector2(1f, 1f);
        painel.pivot = new Vector2(1f, 1f);
        Fundo(painel, new Color(0.015f, 0.025f, 0.05f, 0.78f));
        Color[] cores = { Color.white, new Color(1f, 0.85f, 0.2f), new Color(0.74f, 0.84f, 0.92f) };
        int[] tamanhos = { 18, 17, 15 };
        for (int i = 0; i < 3; i++)
        {
            valores[i] = NovoTexto("0", painel, tamanhos[i], cores[i]);
            Posicionar(valores[i].rectTransform, 0f, 1f, 10f, -10f, 7f + i * 22f, 22f);
            buffs[i] = NovoTexto("", painel, 15, Color.white);
        }
        divisor = NovoRect("Separador", painel);
        textoVida = NovoTexto("VIDA  100%", painel, 12, Verde);
        Posicionar(textoVida.rectTransform, 0f, 1f, 10f, -10f, 77f, 16f);
        var fundoVida = NovoRect("Fundo da vida", painel);
        Posicionar(fundoVida, 0f, 1f, 10f, -10f, 96f, 7f);
        Fundo(fundoVida, new Color(0.08f, 0.12f, 0.19f, 1f));
        barraVida = NovaBarra("Vida do Sol", 96f, Verde);

        textoEstabilidade = NovoTexto("ESTABILIDADE  100%", painel, 12, Ciano);
        Posicionar(textoEstabilidade.rectTransform, 0f, 1f, 10f, -10f, 108f, 16f);
        var fundoBarra = NovoRect("Fundo da estabilidade", painel);
        Posicionar(fundoBarra, 0f, 1f, 10f, -10f, 127f, 7f);
        Fundo(fundoBarra, new Color(0.08f, 0.12f, 0.19f, 1f));
        barraEstabilidade = NovoRect("Energia do Sol", painel);
        barraEstabilidade.anchorMin = barraEstabilidade.anchorMax = barraEstabilidade.pivot = new Vector2(0f, 1f);
        barraEstabilidade.anchoredPosition = new Vector2(10f, -127f);
        barraEstabilidade.sizeDelta = new Vector2(260f, 7f);
        Fundo(barraEstabilidade, Ciano);
        Posicionar(divisor, 0f, 1f, 10f, -10f, 140f, 1f);
        Fundo(divisor, new Color(0.3f, 0.55f, 0.8f, 0.55f));
        Atualizar();
    }

    void LateUpdate() { if (canvas != null) Atualizar(); }

    void Atualizar()
    {
        bool mostrar = jogo != null && jogo.Estado != EstadoJogo.Menu;
        painel.gameObject.SetActive(mostrar);
        if (!mostrar) return;
        float w = Mathf.Max(1f, Screen.width), h = Mathf.Max(1f, Screen.height);
        Rect segura = Screen.safeArea;
        float x0 = Mathf.Clamp(segura.xMin, 0f, w), x1 = Mathf.Clamp(segura.xMax, 0f, w);
        float y0 = Mathf.Clamp(segura.yMin, 0f, h), y1 = Mathf.Clamp(segura.yMax, 0f, h);
        if (x1 <= x0 || y1 <= y0) { x0 = y0 = 0f; x1 = w; y1 = h; }
        areaSegura.anchorMin = new Vector2(x0 / w, y0 / h);
        areaSegura.anchorMax = new Vector2(x1 / w, y1 / h);
        areaSegura.offsetMin = areaSegura.offsetMax = Vector2.zero;
        float escala = Mathf.Max(0.01f, canvas.scaleFactor);
        // A margem e a largura usam as mesmas unidades do Canvas em qualquer DPI.
        float alturaDisponivel = (y1 - y0) / escala;
        float larguraDisponivel = (x1 - x0) / escala;
        int quantidadeBuffs = (jogo.TemEscudo ? 1 : 0) + (jogo.TempoIma > 0f ? 1 : 0) + (jogo.TempoDobro > 0f ? 1 : 0);
        float alturaPainel = 145f + (quantidadeBuffs > 0 ? 5f + 20f * quantidadeBuffs : 0f);
        float reducao = Mathf.Min(1f, larguraDisponivel / 328f, alturaDisponivel / (alturaPainel + 40f));
        painel.localScale = Vector3.one * reducao;
        painel.sizeDelta = new Vector2(280f, alturaPainel);
        painel.anchoredPosition = new Vector2(-24f * reducao, -20f * reducao);
        valores[0].text = "PONTOS  " + Mathf.FloorToInt(jogo.Pontos);
        valores[1].text = "MOEDAS  " + jogo.Moedas;
        valores[2].text = "RECORDE  " + jogo.Recorde;
        divisor.gameObject.SetActive(quantidadeBuffs > 0);
        if (jogador == null) jogador = Object.FindAnyObjectByType<PlayerController>();
        float vida = jogador != null ? Mathf.Clamp01(jogador.Vida / PlayerController.VidaMaxima) : 1f;
        textoVida.text = "VIDA  " + Mathf.CeilToInt(vida * 100f) + "%";
        barraVida.sizeDelta = new Vector2(260f * vida, 7f);
        Color corVida = vida > 0.60f ? Verde :
            (vida > 0.30f ? new Color(1f, 0.62f, 0.18f) : new Color(1f, 0.20f, 0.24f));
        if (vida <= 0.30f) corVida *= 0.78f + 0.22f * Mathf.Sin(Time.unscaledTime * 9f);
        barraVida.GetComponent<Image>().color = corVida;
        textoVida.color = corVida;
        float estabilidade = Mathf.Clamp01(jogo.Estabilidade / 100f);
        textoEstabilidade.text = "ESTABILIDADE  " + Mathf.CeilToInt(jogo.Estabilidade) + "%";
        barraEstabilidade.sizeDelta = new Vector2(260f * estabilidade, 8f);
        Color corEnergia = estabilidade > 0.60f ? Ciano :
            (estabilidade > 0.30f ? new Color(1f, 0.55f, 0.16f) : new Color(1f, 0.20f, 0.24f));
        if (estabilidade <= 0.30f) corEnergia *= 0.78f + 0.22f * Mathf.Sin(Time.unscaledTime * 9f);
        barraEstabilidade.GetComponent<Image>().color = corEnergia;
        textoEstabilidade.color = corEnergia;
        float y = 145f;
        AtualizarBuff(0, jogo.TemEscudo, "ESCUDO", Ciano, ref y);
        AtualizarBuff(1, jogo.TempoIma > 0f, "ÍMÃ  " + Mathf.CeilToInt(jogo.TempoIma) + "s", Verde, ref y);
        AtualizarBuff(2, jogo.TempoDobro > 0f, "PONTOS 2X  " + Mathf.CeilToInt(jogo.TempoDobro) + "s", Rosa, ref y);
    }

    void AtualizarBuff(int indice, bool ativo, string texto, Color cor, ref float y)
    {
        buffs[indice].gameObject.SetActive(ativo);
        if (!ativo) return;
        buffs[indice].text = texto;
        buffs[indice].color = cor;
        Posicionar(buffs[indice].rectTransform, 0f, 1f, 10f, -10f, y, 20f);
        y += 20f;
    }

    static RectTransform NovoRect(string nome, Transform pai)
    {
        var objeto = new GameObject(nome, typeof(RectTransform));
        var rect = objeto.GetComponent<RectTransform>();
        rect.SetParent(pai, false);
        return rect;
    }

    static void Posicionar(RectTransform rect, float esquerda, float direita, float margemE, float margemD, float topo, float altura)
    {
        rect.anchorMin = new Vector2(esquerda, 1f);
        rect.anchorMax = new Vector2(direita, 1f);
        rect.offsetMin = new Vector2(margemE, -topo - altura);
        rect.offsetMax = new Vector2(margemD, -topo);
    }

    static Image Fundo(RectTransform rect, Color cor)
    {
        var imagem = rect.gameObject.AddComponent<Image>();
        imagem.color = cor;
        imagem.raycastTarget = false;
        return imagem;
    }

    RectTransform NovaBarra(string nome, float topo, Color cor)
    {
        var barra = NovoRect(nome, painel);
        barra.anchorMin = barra.anchorMax = barra.pivot = new Vector2(0f, 1f);
        barra.anchoredPosition = new Vector2(10f, -topo);
        barra.sizeDelta = new Vector2(260f, 7f);
        Fundo(barra, cor);
        return barra;
    }

    Text NovoTexto(string conteudo, Transform pai, int tamanho, Color cor)
    {
        var texto = NovoRect(conteudo, pai).gameObject.AddComponent<Text>();
        texto.font = fonte;
        texto.fontStyle = FontStyle.Bold;
        texto.fontSize = tamanho;
        texto.text = conteudo;
        texto.color = cor;
        texto.alignment = TextAnchor.MiddleRight;
        texto.resizeTextForBestFit = true;
        texto.resizeTextMinSize = 9;
        texto.resizeTextMaxSize = tamanho;
        texto.horizontalOverflow = HorizontalWrapMode.Overflow;
        texto.verticalOverflow = VerticalWrapMode.Truncate;
        texto.raycastTarget = false;
        var sombra = texto.gameObject.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.9f);
        sombra.effectDistance = new Vector2(1.5f, -1.5f);
        return texto;
    }

    void OnDisable() { if (raiz != null) raiz.gameObject.SetActive(false); }
    void OnEnable() { if (raiz != null) raiz.gameObject.SetActive(true); }
    void OnDestroy() { if (raiz != null) Destroy(raiz.gameObject); }
}
