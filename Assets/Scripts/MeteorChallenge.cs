using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>Coordena o aviso, as ondas e a devolução da pista ao gerador normal.</summary>
[DefaultExecutionOrder(150)]
public sealed class MeteorChallenge : MonoBehaviour
{
    public MeteorChallengeSettings configuracao;
    public PlayerController jogador;
    public ObstacleSpawner pista;

    enum Fase { Espera, Alerta, Chuva }
    Fase fase;
    GameManager jogo;
    readonly List<MeteorImpact> meteoros = new List<MeteorImpact>();
    readonly List<int> faixasSorteaveis = new List<int>();
    readonly System.Random sorteio = new System.Random();
    float proximoGatilho, fimDaFase, proximaOnda, proximoAlvoZ, ultimoImpacto;
    int faixaSegura;
    Bounds caixaAnterior;
    bool pausado;
    CanvasGroup grupoAviso;
    RectTransform areaSegura, painel;
    Image progresso;
    Text titulo, orientacao;
    AudioSource audioAlerta;
    AudioClip sirene;
    Volume filtroDaChuva;
    VolumeProfile perfil;
    bool configuracaoTemporaria;

    public bool EmEvento => fase != Fase.Espera;

    void Start()
    {
        jogo = GameManager.Instancia;
        if (jogador == null) jogador = Object.FindAnyObjectByType<PlayerController>();
        if (pista == null) pista = Object.FindAnyObjectByType<ObstacleSpawner>();
        if (configuracao == null) configuracao = Resources.Load<MeteorChallengeSettings>("Gameplay/MeteorChallenge");
        if (configuracao == null)
        {
            configuracao = ScriptableObject.CreateInstance<MeteorChallengeSettings>();
            configuracaoTemporaria = true;
        }
        if (jogador == null || pista == null || jogo == null) { enabled = false; return; }
        CriarHud();
        CriarSirene();
        filtroDaChuva = gameObject.AddComponent<Volume>();
        filtroDaChuva.isGlobal = true;
        filtroDaChuva.priority = 15f;
        filtroDaChuva.weight = 0f;
        perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        perfil.Add<ColorAdjustments>().colorFilter.Override(new Color(1f, 0.71f, 0.50f));
        filtroDaChuva.sharedProfile = perfil;
        caixaAnterior = jogador.Limites;
        Agendar();
    }

    void Update()
    {
        if (jogo.Estado == EstadoJogo.Pausado)
        {
            if (!pausado) audioAlerta.Pause();
            pausado = true;
            return;
        }
        if (pausado)
        {
            if (fase == Fase.Alerta) audioAlerta.UnPause();
            caixaAnterior = jogador.Limites;
            pausado = false;
        }
        if (!configuracao.ativado || jogo.Estado != EstadoJogo.Jogando || jogador.quantidadeFaixas < 2)
        {
            if (EmEvento) Encerrar();
            filtroDaChuva.weight = 0f;
            return;
        }

        float agora = jogo.TempoDeJogo;
        if (fase == Fase.Espera && ValorDoGatilho() >= proximoGatilho) IniciarAlerta(agora);
        if (fase == Fase.Alerta && agora >= fimDaFase)
        {
            fase = Fase.Chuva;
            fimDaFase = agora + Mathf.Max(1f, configuracao.duracaoChuva);
            proximaOnda = agora;
            audioAlerta.Stop();
        }
        if (fase == Fase.Chuva) AtualizarChuva(agora);

        float intensidade = fase == Fase.Chuva ? configuracao.intensidadeDoCeu : 0f;
        filtroDaChuva.weight = Mathf.MoveTowards(filtroDaChuva.weight, intensidade, Time.deltaTime * 0.42f);
        caixaAnterior = jogador.Limites;
        AtualizarHud(agora);
    }

    float ValorDoGatilho() => configuracao.gatilho == GatilhoDeMeteoros.Pontos ? jogo.Pontos : jogo.TempoDeJogo;

    void Agendar()
    {
        float minimo = Mathf.Max(1f, configuracao.intervaloAleatorio.x);
        float maximo = Mathf.Max(minimo, configuracao.intervaloAleatorio.y);
        float intervalo = configuracao.gatilho switch
        {
            GatilhoDeMeteoros.Tempo => configuracao.intervaloSegundos,
            GatilhoDeMeteoros.Pontos => configuracao.intervaloPontos,
            _ => Mathf.Lerp(minimo, maximo, (float)sorteio.NextDouble())
        };
        proximoGatilho = ValorDoGatilho() + Mathf.Max(1f, intervalo);
    }

    void IniciarAlerta(float agora)
    {
        fase = Fase.Alerta;
        fimDaFase = agora + Mathf.Max(0.8f, configuracao.avisoGeral);
        faixaSegura = sorteio.Next(jogador.quantidadeFaixas);
        proximoAlvoZ = jogador.transform.position.z;
        ultimoImpacto = agora - 1f;
        pista.EntrarNaChuvaDeMeteoros(faixaSegura);
        audioAlerta.Play();
        CameraFollow.Tremer(0.30f, 0.12f);
    }

    void AtualizarChuva(float agora)
    {
        if (agora < fimDaFase && agora >= proximaOnda)
        {
            CriarOnda(agora);
            proximaOnda = agora + Mathf.Max(1f, configuracao.intervaloOndas);
        }

        Bounds atual = jogador.Limites;
        for (int i = meteoros.Count - 1; i >= 0; i--)
        {
            MeteorImpact meteoro = meteoros[i];
            if (meteoro == null || !meteoro.Atualizar(agora, caixaAnterior, atual, out bool impacto, out bool acertou))
            {
                if (meteoro != null) Destroy(meteoro.gameObject);
                meteoros.RemoveAt(i);
                continue;
            }
            if (impacto && agora - ultimoImpacto > 0.22f)
            {
                float distancia = Mathf.Abs(meteoro.Alvo.z - jogador.transform.position.z);
                if (distancia < 38f)
                {
                    jogo.SomDeImpacto();
                    CameraFollow.Tremer(0.18f, Mathf.Lerp(0.18f, 0.055f, distancia / 38f));
                    ultimoImpacto = agora;
                }
            }
            if (acertou && AplicarDano()) break;
        }
        if (jogo.Estado != EstadoJogo.Jogando || (agora >= fimDaFase && meteoros.Count == 0)) Encerrar();
    }

    bool AplicarDano()
    {
        if (jogador.Invencivel || configuracao.danoVida <= 0f) return false;
        if (jogo.ConsumirEscudo()) jogador.FicarInvencivel(1.2f);
        else if (!jogador.ReceberDano(configuracao.danoVida)) return false;
        CameraFollow.Tremer(0.23f, 0.31f);
        return jogo.Estado != EstadoJogo.Jogando;
    }

    void CriarOnda(float agora)
    {
        faixasSorteaveis.Clear();
        for (int faixa = 0; faixa < jogador.quantidadeFaixas; faixa++)
            if (faixa != faixaSegura) faixasSorteaveis.Add(faixa);

        float aviso = Mathf.Max(1.2f, configuracao.avisoNoPiso);
        float velocidade = Mathf.Max(1f, jogo.Velocidade);
        float zJogador = jogador.transform.position.z;
        float z = Mathf.Max(proximoAlvoZ, zJogador + velocidade * (aviso + 0.24f)
            * Mathf.Lerp(0.97f, 1.14f, (float)sorteio.NextDouble()));
        float atraso = Mathf.Max(aviso, (z - zJogador) / velocidade - 0.14f);
        proximoAlvoZ = z + Mathf.Max(6f, velocidade * 0.50f);
        int quantidade = Mathf.Min(Mathf.Max(1, configuracao.meteorosPorOnda), faixasSorteaveis.Count);
        float raio = Mathf.Min(0.81f, jogador.larguraFaixa * 0.41f);
        for (int i = 0; i < quantidade; i++)
        {
            int indice = sorteio.Next(faixasSorteaveis.Count);
            int faixa = faixasSorteaveis[indice];
            faixasSorteaveis.RemoveAt(indice);
            var alvo = new Vector3(jogador.PosicaoXDaFaixa(faixa), 0f, z);
            var entrada = alvo + new Vector3((float)(sorteio.NextDouble() * 14.0 - 7.0), 14f + i, 24f + i * 4f);
            meteoros.Add(MeteorImpact.Criar(transform, configuracao.visualMeteoro, alvo, entrada,
                agora, agora + atraso, Mathf.Max(0.1f, configuracao.duracaoBrasa), raio));
        }
    }

    void Encerrar()
    {
        fase = Fase.Espera;
        foreach (MeteorImpact meteoro in meteoros)
            if (meteoro != null) Destroy(meteoro.gameObject);
        meteoros.Clear();
        if (audioAlerta != null) audioAlerta.Stop();
        if (grupoAviso != null) grupoAviso.alpha = 0f;
        if (pista != null) pista.SairDaChuvaDeMeteoros();
        if (jogo != null && configuracao != null) Agendar();
    }

    void CriarHud()
    {
        var raiz = new GameObject("HUD do Desafio dos Meteoros", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        raiz.transform.SetParent(transform, false);
        var canvas = raiz.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = raiz.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        areaSegura = Rect("Área segura", raiz.transform, Vector2.zero, Vector2.one);
        painel = Rect("Aviso", areaSegura, new Vector2(0.22f, 0.76f), new Vector2(0.78f, 0.90f));
        var fundo = painel.gameObject.AddComponent<Image>();
        fundo.color = new Color(0.052f, 0.013f, 0.009f, 0.90f);
        fundo.raycastTarget = false;
        grupoAviso = painel.gameObject.AddComponent<CanvasGroup>();
        grupoAviso.interactable = grupoAviso.blocksRaycasts = false;
        grupoAviso.alpha = 0f;
        titulo = CriarTexto("Título", 31, new Vector2(0.035f, 0.37f), new Vector2(0.965f, 0.94f));
        orientacao = CriarTexto("Instrução", 16, new Vector2(0.04f, 0.09f), new Vector2(0.96f, 0.37f));
        orientacao.color = new Color(1f, 0.86f, 0.72f);
        progresso = Rect("Contagem visual", painel, Vector2.zero, new Vector2(1f, 0.04f)).gameObject.AddComponent<Image>();
        progresso.raycastTarget = false;
    }

    Text CriarTexto(string nome, int tamanho, Vector2 minimo, Vector2 maximo)
    {
        var texto = Rect(nome, painel, minimo, maximo).gameObject.AddComponent<Text>();
        texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.fontStyle = FontStyle.Bold;
        texto.fontSize = tamanho;
        texto.resizeTextForBestFit = true;
        texto.resizeTextMinSize = 8;
        texto.resizeTextMaxSize = tamanho;
        texto.alignment = TextAnchor.MiddleCenter;
        texto.raycastTarget = false;
        return texto;
    }

    void AtualizarHud(float agora)
    {
        if (fase == Fase.Espera) { grupoAviso.alpha = 0f; return; }
        UnityEngine.Rect segura = Screen.safeArea;
        Vector2 tela = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        areaSegura.anchorMin = new Vector2(segura.xMin / tela.x, segura.yMin / tela.y);
        areaSegura.anchorMax = new Vector2(segura.xMax / tela.x, segura.yMax / tela.y);
        bool alerta = fase == Fase.Alerta;
        bool estreita = segura.width < segura.height;
        painel.anchorMin = new Vector2(estreita ? 0.06f : 0.28f, alerta ? 0.74f : 0.78f);
        painel.anchorMax = new Vector2(estreita ? 0.94f : 0.72f, 0.88f);
        float pulso = alerta ? 0.5f + Mathf.Sin(agora * 10f) * 0.5f : 1f;
        grupoAviso.alpha = Mathf.Lerp(0.66f, 1f, pulso);
        painel.localScale = Vector3.one * Mathf.Lerp(1f, 1.025f, alerta ? pulso : 0f);
        titulo.text = alerta ? "ALERTA: CHUVA DE METEOROS!" : "DESAFIO DOS METEOROS";
        titulo.color = progresso.color = configuracao.corAviso;
        orientacao.text = alerta ? "Prepare-se para desviar dos alvos no piso" : "Desvie dos alvos marcados";
        float total = alerta ? Mathf.Max(0.8f, configuracao.avisoGeral) : Mathf.Max(1f, configuracao.duracaoChuva);
        progresso.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((fimDaFase - agora) / total), 0.04f);
    }

    static RectTransform Rect(string nome, Transform pai, Vector2 minimo, Vector2 maximo)
    {
        var rect = new GameObject(nome, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(pai, false);
        rect.anchorMin = minimo;
        rect.anchorMax = maximo;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    void CriarSirene()
    {
        audioAlerta = gameObject.AddComponent<AudioSource>();
        audioAlerta.playOnAwake = false;
        audioAlerta.loop = true;
        audioAlerta.volume = 0.28f;
        const int frequencia = 16000;
        var amostras = new float[frequencia];
        float fase = 0f;
        for (int i = 0; i < amostras.Length; i++)
        {
            float t = (float)i / frequencia;
            fase += Mathf.Lerp(520f, 980f, 0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI)) * 2f * Mathf.PI / frequencia;
            amostras[i] = Mathf.Sin(fase) * (0.52f + 0.12f * Mathf.Sin(fase * 2f)) * Mathf.Min(1f, Mathf.Min(t, 1f - t) * 70f);
        }
        sirene = AudioClip.Create("Sirene dos meteoros", amostras.Length, 1, frequencia, false);
        sirene.SetData(amostras, 0);
        audioAlerta.clip = sirene;
    }

    void OnDisable()
    {
        if (EmEvento) Encerrar();
        if (filtroDaChuva != null) filtroDaChuva.weight = 0f;
    }

    void OnDestroy()
    {
        if (sirene != null) Destroy(sirene);
        if (perfil != null) Destroy(perfil);
        if (configuracaoTemporaria && configuracao != null) Destroy(configuracao);
    }
}
