using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Muro de cor: parede sólida pintada inteira de uma cor, com acabamento de espelho
/// colorido (reflete o céu, a pista e o Sol), ocupando a faixa inteira e rente ao chão.
/// Só passa o Sol da mesma cor: quando ele chega perto com a cor certa, o muro afunda no
/// piso e abre a passagem; com a cor errada ele continua de pé e pisca ao ser atravessado.
/// </summary>
public sealed class ColorPortalVisual : MonoBehaviour
{
    const float VelocidadeAbertura = 8f;     // 1 / segundos para abrir ou fechar
    const float AntecedenciaAbertura = 0.3f; // segundos antes do contato em que começa a abrir

    static readonly Material[] espelhos = new Material[4];
    static readonly Material[] neons = new Material[4];
    static PlayerController jogadorEmCache;
    static readonly int idClarao = Shader.PropertyToID("_Clarao");
    static readonly int idEmissao = Shader.PropertyToID("_EmissionColor");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReiniciarCache()
    {
        for (int i = 0; i < espelhos.Length; i++) espelhos[i] = neons[i] = null;
        jogadorEmCache = null;
    }

    Transform muro;
    Renderer corpo;
    MaterialPropertyBlock propriedades;
    Color cor;
    int corId;
    float pulso, falha, abertura;
    bool usandoEspelho;
    bool atravessadoComCorCerta;

    public static GameObject Criar(Transform pai, Vector3 posicao, Vector3 tamanho, int corId)
    {
        var objeto = new GameObject("MuroDeCor" + corId);
        objeto.transform.SetParent(pai, false);
        objeto.transform.localPosition = posicao;
        objeto.transform.localScale = tamanho;
        Color cor = PlayerController.Cores[corId];
        GarantirMateriais(corId, cor);

        // Tudo que se vê fica num pivô filho, que afunda no chão ao abrir.
        Transform pivo = new GameObject("Muro").transform;
        pivo.SetParent(objeto.transform, false);

        var corpoObjeto = new GameObject("EspelhoDoMuro");
        corpoObjeto.transform.SetParent(pivo, false);
        corpoObjeto.AddComponent<MeshFilter>().sharedMesh = PolyhedronVisual.MalhaPrisma(FormaObstaculo.Quadrado, 4);
        var render = corpoObjeto.AddComponent<MeshRenderer>();
        render.sharedMaterial = espelhos[corId];

        // Faixas luminosas no topo e nas laterais: cada faixa do muro se destaca da vizinha.
        Vector3 espessuraTopo = new Vector3(1.0f, 0.07f / tamanho.y, 1.12f);
        Vector3 espessuraLado = new Vector3(0.05f / tamanho.x, 1.0f, 1.12f);
        Emissor(pivo, "CristaDoMuro", new Vector3(0f, 0.5f, 0f), espessuraTopo, corId);
        Emissor(pivo, "ArestaEsquerdaDoMuro", new Vector3(-0.5f, 0f, 0f), espessuraLado, corId);
        Emissor(pivo, "ArestaDireitaDoMuro", new Vector3(0.5f, 0f, 0f), espessuraLado, corId);

        var efeito = objeto.AddComponent<ColorPortalVisual>();
        efeito.muro = pivo;
        efeito.corpo = render;
        efeito.cor = cor;
        efeito.corId = corId;
        efeito.propriedades = new MaterialPropertyBlock();
        efeito.usandoEspelho = espelhos[corId].HasProperty(idClarao);
        objeto.AddComponent<PolyhedronHitShape>().Inicializar(4, FormaObstaculo.Quadrado);
        return objeto;
    }

    static void Emissor(Transform pai, string nome, Vector3 posicao, Vector3 escala, int corId)
    {
        GameObject faixa = Visual.Forma(PrimitiveType.Cube, nome, pai, posicao, escala, Color.white, true, raio: 0f);
        Renderer r = faixa.GetComponent<Renderer>();
        r.sharedMaterial = neons[corId];
        r.shadowCastingMode = ShadowCastingMode.Off;
    }

    public void Atravessar(bool corCorreta)
    {
        pulso = 1f;
        falha = corCorreta ? 0f : 1f;
        if (corCorreta) atravessadoComCorCerta = true;
    }

    void Update()
    {
        // Abre quando o Sol da mesma cor está para chegar; fecha se ele trocar de cor antes.
        PlayerController jogador = Jogador();
        GameManager jogo = GameManager.Instancia;
        bool abrir = atravessadoComCorCerta;
        if (!abrir && jogador != null && jogador.CorId == corId)
        {
            float velocidade = jogo != null ? jogo.Velocidade : 10f;
            float distancia = transform.position.z - jogador.transform.position.z;
            abrir = distancia > -1f && distancia < Mathf.Max(2.5f, velocidade * AntecedenciaAbertura);
        }
        float dt = Time.deltaTime;
        abertura = Mathf.MoveTowards(abertura, abrir ? 1f : 0f, VelocidadeAbertura * dt);
        float descida = Mathf.SmoothStep(0f, 1f, abertura);
        // Afunda um pouco além da própria altura para sumir inteiro dentro do piso.
        muro.localPosition = new Vector3(0f, -1.05f * descida, 0f);

        pulso = Mathf.MoveTowards(pulso, 0f, dt * 3f);
        // Cor errada: o espelho pisca em branco quente ao ser atravessado.
        propriedades.SetFloat(idClarao, pulso * falha);
        if (!usandoEspelho) propriedades.SetColor(idEmissao, Color.Lerp(cor * 0.6f, Color.white * 4f, pulso * falha));
        corpo.SetPropertyBlock(propriedades);
    }

    static PlayerController Jogador()
    {
        if (jogadorEmCache == null) jogadorEmCache = Object.FindAnyObjectByType<PlayerController>();
        return jogadorEmCache;
    }

    // ---------- Materiais ----------

    static void GarantirMateriais(int corId, Color cor)
    {
        if (espelhos[corId] != null && neons[corId] != null) return;
        ReflexoDoAmbiente.Garantir();

        Shader espelho = Resources.Load<Shader>("Space/MuroEspelho");
        Material material;
        if (espelho != null)
        {
            material = new Material(espelho);
            material.SetColor("_Cor", cor);
            material.SetFloat("_Brilho", 0.4f);
        }
        else
        {
            // Reserva: metal liso e polido pintado da cor do muro.
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(lit != null ? lit : Shader.Find("Standard"));
            material.color = cor;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 1f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.95f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", cor * 0.6f);
        }
        material.name = "Muro espelhado " + corId;
        espelhos[corId] = material;

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        var neon = new Material(unlit != null ? unlit : material.shader) { name = "Crista luminosa do muro " + corId };
        neon.color = cor * 3f;
        if (neon.HasProperty("_BaseColor")) neon.SetColor("_BaseColor", cor * 3f);
        neons[corId] = neon;
    }
}
