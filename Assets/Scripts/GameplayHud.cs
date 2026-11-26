using UnityEngine;
using UnityEngine.UI;

/// <summary>Vida e estabilidade a esquerda; pontos e buffs a direita, dentro da area segura.</summary>
public sealed class GameplayHud : MonoBehaviour
{
    GameManager jogo;
    Canvas canvas;
    CanvasGroup grupo;
    float aparicao;
    RectTransform raiz, areaSegura, painel, painelDados, divisor, fundoBuffs;
    Font fonte;
    readonly Text[] valores = new Text[3];
    readonly Text[] buffs = new Text[3];
    PlayerController jogador;
    Text textoVida, textoEstabilidade;
    Text porcentagemVida;
    Text porcentagemEstabilidade;
    Material materialSolEstabilidade;
    Text avisoEstabilidade;
    int nivelAlertaAnterior;
    float tempoUltimoAlerta = -10f;
    const float EscalaDistanteHud = 0.68f;
    static readonly Color Ciano = new Color(0.35f, 0.92f, 1f);
    static readonly Color Verde = new Color(0.45f, 1f, 0.62f);
    static readonly Color Rosa = new Color(1f, 0.52f, 0.87f);

    void Start()
    {
        jogo = GetComponent<GameManager>();
        jogador = Object.FindAnyObjectByType<PlayerController>();
        fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        painel = NovoRect("Vida e estabilidade", areaSegura);
        painel.anchorMin = painel.anchorMax = new Vector2(0f, 1f);
        painel.pivot = new Vector2(0f, 1f);
        painelDados = NovoRect("Pontos moedas recorde e buffs", areaSegura);
        painelDados.anchorMin = painelDados.anchorMax = painelDados.pivot = new Vector2(1f, 1f);
        // Os indicadores orbitais ficam soltos; os dados mantem o fundo a direita.
        var fundoSuperior = NovoRect("Fundo dos pontos", painelDados);
        Posicionar(fundoSuperior, 0f, 1f, 0f, 0f, 0f, 72f);
        Fundo(fundoSuperior, new Color(0.015f, 0.025f, 0.05f, 0.78f));
        fundoBuffs = NovoRect("Fundo dos buffs", painelDados);
        Fundo(fundoBuffs, new Color(0.015f, 0.025f, 0.05f, 0.78f));
        Color[] cores = { Color.white, new Color(1f, 0.85f, 0.2f), new Color(0.74f, 0.84f, 0.92f) };
        int[] tamanhos = { 18, 17, 15 };
        for (int i = 0; i < 3; i++)
        {
            valores[i] = NovoTexto("0", painelDados, tamanhos[i], cores[i]);
            Posicionar(valores[i].rectTransform, 0f, 1f, 10f, -10f, 7f + i * 22f, 22f);
            buffs[i] = NovoTexto("", painelDados, 15, Color.white);
        }
        divisor = NovoRect("Separador", painelDados);
        textoVida = NovoTexto("VIDA", painel, 12, Verde);
        textoVida.alignment = TextAnchor.MiddleCenter;
        Posicionar(textoVida.rectTransform, 0f, 1f, 10f, -10f, 0f, 16f);
        textoEstabilidade = NovoTexto("ESTABILIDADE", painel, 12, Color.white);
        textoEstabilidade.alignment = TextAnchor.MiddleCenter;
        Posicionar(textoEstabilidade.rectTransform, 0f, 1f, 10f, -10f, 168f, 16f);
        var sol = NovoRect("Vida e estabilidade orbital", painel);
        sol.anchorMin = sol.anchorMax = sol.pivot = new Vector2(0.5f, 1f);
        sol.anchoredPosition = new Vector2(0f, -8f);
        sol.sizeDelta = new Vector2(384f, 160f);
        var imagemSolar = sol.gameObject.AddComponent<RawImage>();
        imagemSolar.raycastTarget = false;
        imagemSolar.texture = Resources.Load<Texture2D>("Sol3D/Fotosfera");
        Shader shaderSolar = Resources.Load<Shader>("Gameplay/OrbitalVitals");
        if (shaderSolar != null)
        {
            materialSolEstabilidade = new Material(shaderSolar) { name = "Indicador orbital de vida e estabilidade" };
            materialSolEstabilidade.SetFloat("_FarScale", EscalaDistanteHud);
            materialSolEstabilidade.SetTexture("_MainTex", imagemSolar.texture);
            imagemSolar.material = materialSolEstabilidade;
        }
        foreach (Text titulo in new[] { textoVida, textoEstabilidade })
            titulo.gameObject.AddComponent<HudPerspectiveText>().Configurar(sol, EscalaDistanteHud);
        porcentagemVida = CriarPercentual("100%", sol, 20f);
        porcentagemEstabilidade = CriarPercentual("100%", sol, -20f);
        avisoEstabilidade = NovoTexto("", painel, 13, Color.white);
        avisoEstabilidade.alignment = TextAnchor.MiddleCenter;
        Posicionar(avisoEstabilidade.rectTransform, 0f, 1f, 10f, -10f, 188f, 32f);
        avisoEstabilidade.gameObject.AddComponent<HudPerspectiveText>().Configurar(sol, EscalaDistanteHud);
        avisoEstabilidade.gameObject.SetActive(false);
        Posicionar(divisor, 0f, 1f, 10f, -10f, 267f, 1f);
        Fundo(divisor, new Color(0.3f, 0.55f, 0.8f, 0.55f));
        Atualizar();
    }

    void LateUpdate() { if (canvas != null) Atualizar(); }

    public void Apresentar(float opacidade)
    {
        if (aparicao == opacidade) return;
        aparicao = opacidade;
        if (grupo != null) Atualizar();
    }

    void Atualizar()
    {
        grupo.alpha = jogo != null && jogo.Estado != EstadoJogo.Menu ? 1f : aparicao;
        bool mostrar = grupo.alpha > 0f;
        painel.gameObject.SetActive(mostrar);
        painelDados.gameObject.SetActive(mostrar);
        if (!mostrar)
        {
            nivelAlertaAnterior = 0;
            tempoUltimoAlerta = -10f;
            return;
        }
        float estabilidade = Mathf.Clamp01(jogo.Estabilidade / 100f);
        int nivelAlerta = jogo.Estado == EstadoJogo.FimDeJogo || estabilidade <= 0f ? 0 :
            (estabilidade < 0.15f ? 3 : estabilidade < 0.30f ? 2 : estabilidade < 0.60f ? 1 : 0);
        const float inicioBuffs = 88f;
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
        float alturaPainel = nivelAlerta > 0 ? 224f : 188f;
        float alturaDados = quantidadeBuffs > 0 ? inicioBuffs + 20f * quantidadeBuffs + 5f : 78f;
        float margemX = Mathf.Min(24f, larguraDisponivel * 0.05f);
        float margemY = Mathf.Min(20f, alturaDisponivel * 0.05f);
        float intervalo = Mathf.Min(16f, larguraDisponivel * 0.025f);
        // Reserva espaco para ambos os cantos, inclusive em telas estreitas.
        float fatorLargura = Mathf.Clamp01((larguraDisponivel - margemX * 2f - intervalo) / (384f * 1.15f + 280f));
        float reducao = Mathf.Max(0.0001f, Mathf.Min(1.15f * fatorLargura,
            (alturaDisponivel - margemY * 2f) / alturaPainel));
        float escalaDados = Mathf.Max(0.0001f, Mathf.Min(fatorLargura,
            (alturaDisponivel - margemY * 2f) / alturaDados));
        painel.localScale = Vector3.one * reducao;
        painel.sizeDelta = new Vector2(384f, alturaPainel);
        painel.anchoredPosition = new Vector2(margemX, -margemY);
        painelDados.localScale = Vector3.one * escalaDados;
        painelDados.sizeDelta = new Vector2(280f, alturaDados);
        painelDados.anchoredPosition = new Vector2(-margemX, -margemY);
        fundoBuffs.gameObject.SetActive(quantidadeBuffs > 0);
        Posicionar(fundoBuffs, 0f, 1f, 0f, 0f, inicioBuffs - 4f, Mathf.Max(0f, alturaDados - inicioBuffs + 4f));
        Posicionar(divisor, 0f, 1f, 10f, -10f, inicioBuffs - 7f, 1f);
        valores[0].text = "PONTOS  " + Mathf.FloorToInt(jogo.Pontos);
        valores[1].text = "MOEDAS  " + jogo.Moedas;
        valores[2].text = "RECORDE  " + jogo.Recorde;
        divisor.gameObject.SetActive(quantidadeBuffs > 0);
        if (jogador == null) jogador = Object.FindAnyObjectByType<PlayerController>();
        float vida = jogador != null ? Mathf.Clamp01(jogador.Vida / PlayerController.VidaMaxima) : 1f;
        Color corVida = CorDaVida(vida);
        porcentagemVida.text = Mathf.CeilToInt(jogador != null ? Mathf.Clamp(jogador.Vida, 0f, PlayerController.VidaMaxima) * 100f / PlayerController.VidaMaxima : 100f) + "%";
        textoVida.color = corVida;
        porcentagemEstabilidade.text = Mathf.CeilToInt(Mathf.Clamp(jogo.Estabilidade, 0f, 100f)) + "%";
        if (materialSolEstabilidade != null)
        {
            materialSolEstabilidade.SetFloat("_Life", vida);
            materialSolEstabilidade.SetFloat("_Fill", estabilidade);
            materialSolEstabilidade.SetColor("_LifeColor", corVida);
            materialSolEstabilidade.SetColor("_SunColor", CorDaEstabilidade(estabilidade));
        }
        AtualizarAlertaEstabilidade(nivelAlerta);
        float y = inicioBuffs;
        AtualizarBuff(0, jogo.TemEscudo, "ESCUDO", Ciano, ref y);
        AtualizarBuff(1, jogo.TempoIma > 0f, "ÍMÃ  " + Mathf.CeilToInt(jogo.TempoIma) + "s", Verde, ref y);
        AtualizarBuff(2, jogo.TempoDobro > 0f, "PONTOS 2X  " + Mathf.CeilToInt(jogo.TempoDobro) + "s", Rosa, ref y);
    }

    void AtualizarAlertaEstabilidade(int nivel)
    {
        if (nivel > nivelAlertaAnterior && jogo.Estado == EstadoJogo.Jogando &&
            (Time.time - tempoUltimoAlerta >= 4f || nivel >= 3))
            tempoUltimoAlerta = Time.time;
        nivelAlertaAnterior = nivel;
        avisoEstabilidade.gameObject.SetActive(nivel > 0);
        textoEstabilidade.color = Color.white;
        if (nivel == 0) return;
        string titulo = nivel == 1 ? "ATENÇÃO: ESTABILIDADE BAIXA" :
            nivel == 2 ? "PERIGO: ESTABILIDADE CRÍTICA" : "URGENTE: COLAPSO IMINENTE";
        avisoEstabilidade.text = titulo + "\nCOLETE MOEDAS DA SUA COR";
        Color cor = nivel == 1 ? new Color(1f, 0.75f, 0.18f) :
            nivel == 2 ? new Color(1f, 0.38f, 0.12f) : new Color(1f, 0.32f, 0.36f);
        textoEstabilidade.color = cor;
        float idade = Time.time - tempoUltimoAlerta;
        // Pulsa brevemente na entrada da faixa; depois o aviso permanece legivel.
        cor.a = idade >= 0f && idade < 1.5f ? 0.65f + 0.35f * Mathf.Abs(Mathf.Cos(idade * 8f)) : 1f;
        avisoEstabilidade.color = cor;
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

    static Color CorDaVida(float vida)
    {
        return vida > 0.50f ? new Color(0.12f, 1f, 0.24f) :
            vida > 0.30f ? new Color(1f, 0.85f, 0.06f) : new Color(1f, 0.10f, 0.14f);
    }

    static Color CorDaEstabilidade(float valor)
    {
        Color vermelho = new Color(1f, 0.08f, 0.12f);
        Color laranja = new Color(1f, 0.40f, 0.06f);
        Color amarelo = new Color(1f, 0.85f, 0.08f);
        Color ciano = new Color(0.04f, 0.9f, 1f);
        Color azul = new Color(0.12f, 0.40f, 1f);
        if (valor <= 0.30f) return Color.Lerp(vermelho, laranja, Mathf.Clamp01(valor / 0.30f));
        if (valor <= 0.60f) return Color.Lerp(laranja, amarelo, (valor - 0.30f) / 0.30f);
        if (valor <= 0.80f) return Color.Lerp(amarelo, ciano, (valor - 0.60f) / 0.20f);
        return Color.Lerp(ciano, azul, Mathf.Clamp01((valor - 0.80f) / 0.20f));
    }

    Text CriarPercentual(string valor, Transform pai, float y)
    {
        Text texto = NovoTexto(valor, pai, 20, Color.white);
        var rect = texto.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(100f, 28f);
        texto.alignment = TextAnchor.MiddleCenter;
        var contorno = texto.gameObject.AddComponent<Outline>();
        contorno.effectColor = Color.black;
        contorno.effectDistance = new Vector2(1.5f, -1.5f);
        // Aplica depois da sombra e do contorno para deformar o conjunto inteiro.
        texto.gameObject.AddComponent<HudPerspectiveText>().Configurar((RectTransform)pai, EscalaDistanteHud);
        return texto;
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
    void OnDestroy()
    {
        if (raiz != null) Destroy(raiz.gameObject);
        if (materialSolEstabilidade != null) Destroy(materialSolEstabilidade);
    }
}













