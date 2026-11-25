using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gera o chão automaticamente conforme o jogador avança.
/// O piso é sempre o mesmo: um conjunto fixo de pedaços que, ao ficarem
/// para trás do jogador, são reposicionados lá na frente (pista infinita
/// sem criar/destruir objetos o tempo todo).
/// </summary>
public class FloorGenerator : MonoBehaviour
{
    [Tooltip("Quem o chão acompanha (o cubo).")]
    public Transform jogador;

    [Header("Pista")]
    public int quantidadeFaixas = 5;
    public float larguraFaixa = 2f;

    [Header("Pedaços de chão")]
    [Tooltip("Comprimento de cada pedaço.")]
    public float comprimentoPedaco = 20f;
    [Tooltip("Quantos pedaços existem ao mesmo tempo.")]
    public int quantidadePedacos = 9;

    [Header("Visual")]
    // Vidro escuro azul-marinho (os veios roxos vêm do shader ReflectiveTrack).
    public Color corPiso = new Color(0.03f, 0.035f, 0.085f);
    public Color corLinhas = new Color(0.025f, 0.18f, 0.42f);
    public float larguraLinha = 0.035f;

    readonly Queue<Transform> pedacos = new Queue<Transform>();
    float proximoZ;
    static readonly Material[] materiaisLinhaNeon = new Material[2];
    // Malhas subdivididas: o piso e as linhas precisam de vértices para se curvar no poço gravitacional.
    static Mesh malhaPiso, malhaLinha;
    static Vector3 tamanhoMalhaPiso;
    const float PassoDaGradeX = 0.2f;
    const float PassoDaGradeZ = 0.25f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReiniciarCache()
    {
        malhaPiso = null;
        malhaLinha = null;
        materiaisLinhaNeon[0] = null;
        materiaisLinhaNeon[1] = null;
    }

    void Start()
    {
        // Começa um pedaço atrás do jogador para a câmera não ver o vazio.
        float zInicial = jogador != null ? jogador.position.z : 0f;
        // Espelhos e vidros refletem as linhas neon na posição certa das faixas.
        ReflexoDoAmbiente.DefinirPista(quantidadeFaixas * larguraFaixa / 2f, larguraFaixa);
        proximoZ = zInicial - comprimentoPedaco;

        for (int i = 0; i < quantidadePedacos; i++)
            ColocarNaFrente(CriarPedaco());
    }

    void Update()
    {
        if (jogador == null) return;

        // Enquanto o pedaço mais antigo estiver totalmente para trás, manda ele para a frente.
        while (pedacos.Count > 0 &&
               jogador.position.z - pedacos.Peek().position.z > comprimentoPedaco * 2f)
        {
            ColocarNaFrente(pedacos.Dequeue());
        }
    }

    void ColocarNaFrente(Transform pedaco)
    {
        // O pivô do pedaço fica no início dele (borda de trás).
        pedaco.position = new Vector3(0f, 0f, proximoZ);
        proximoZ += comprimentoPedaco;
        pedacos.Enqueue(pedaco);
    }

    Transform CriarPedaco()
    {
        float larguraTotal = quantidadeFaixas * larguraFaixa;

        Transform raiz = new GameObject("PedacoDeChao").transform;
        raiz.SetParent(transform, false);

        // Piso (topo na altura Y = 0).
        GameObject piso = Visual.Forma(PrimitiveType.Cube, "Piso", raiz,
            new Vector3(0f, -0.1f, comprimentoPedaco / 2f),
            new Vector3(larguraTotal, 0.2f, comprimentoPedaco), corPiso, false, true, 0.03f);

        // Liga metálica lisa com guias luminosas longitudinais.
        piso.GetComponent<Renderer>().sharedMaterial = Visual.MatPisoLiso(corPiso);
        piso.GetComponent<MeshFilter>().sharedMesh = MalhaPiso(new Vector3(larguraTotal, 0.2f, comprimentoPedaco));

        // Divisas das faixas em ciano; as duas bordas externas da pista em violeta (arte de referência).
        Color[] coresNeon = { new Color(0.30f, 0.85f, 1f), new Color(0.62f, 0.32f, 1f) };
        for (int i = 0; i <= quantidadeFaixas; i++)
        {
            int indiceCor = (i == 0 || i == quantidadeFaixas) ? 1 : 0;
            Color corNeon = coresNeon[indiceCor];
            GameObject linhaNeon = Visual.Forma(PrimitiveType.Cube, "LinhaNeonLongitudinal", raiz,
                new Vector3(-larguraTotal / 2f + i * larguraFaixa, 0.008f, comprimentoPedaco / 2f),
                new Vector3(0.060f, 0.012f, comprimentoPedaco), corNeon, true, raio: 0f);
            linhaNeon.GetComponent<MeshFilter>().sharedMesh = MalhaLinha(Mathf.CeilToInt(comprimentoPedaco / PassoDaGradeZ));
            if (materiaisLinhaNeon[indiceCor] == null)
            {
                // Shader próprio para a linha acompanhar a curvatura do piso.
                Shader shaderNeon = Resources.Load<Shader>("Space/NeonDaPista");
                Material neon = shaderNeon != null ? new Material(shaderNeon) : new Material(Visual.Mat(corNeon, true));
                neon.name = "Neon vibrante da pista " + indiceCor;
                if (shaderNeon != null) neon.SetColor("_Cor", corNeon * 3.5f);
                else
                {
                    neon.EnableKeyword("_EMISSION");
                    neon.SetColor("_EmissionColor", corNeon * 3.5f);
                }
                materiaisLinhaNeon[indiceCor] = neon;
            }
            linhaNeon.GetComponent<Renderer>().sharedMaterial = materiaisLinhaNeon[indiceCor];
        }

        // Reforços escuros e travessas finas dão leitura de pista construída.
        Color metal = new Color(0.055f, 0.075f, 0.13f);
        for (int lado = -1; lado <= 1; lado += 2)
        {
            GameObject viga = Visual.Forma(PrimitiveType.Cube, "VigaLateral", raiz,
                new Vector3(lado * (larguraTotal / 2f + 0.13f), -0.015f, comprimentoPedaco / 2f),
                new Vector3(0.22f, 0.16f, comprimentoPedaco), metal, false, raio: 0.025f);
            viga.GetComponent<Renderer>().sharedMaterial = Visual.MatMetal(metal, 0.65f);
            // Malha dividida ao longo do comprimento para a viga se curvar com a atração do buraco negro.
            viga.GetComponent<MeshFilter>().sharedMesh = CurvaturaDaMalha.CuboSubdividido(40);
            viga.transform.localScale = new Vector3(0.22f, 0.16f, comprimentoPedaco);

            // Guarda-corpo de uma pista orbital: placas de liga escura e filete emissivo.
            float bordaX = lado * (larguraTotal / 2f + 0.27f);
            GameObject guarda = Visual.Forma(PrimitiveType.Cube, "GuardaCorpoOrbital", raiz,
                new Vector3(bordaX, 0.30f, comprimentoPedaco / 2f),
                new Vector3(0.12f, 0.58f, comprimentoPedaco), new Color(0.035f, 0.055f, 0.095f), false, raio: 0.035f);
            guarda.GetComponent<Renderer>().sharedMaterial = Visual.MatMetal(new Color(0.035f, 0.055f, 0.095f), 0.82f);
            guarda.GetComponent<MeshFilter>().sharedMesh = CurvaturaDaMalha.CuboSubdividido(40);
            guarda.transform.localScale = new Vector3(0.12f, 0.58f, comprimentoPedaco);

            // Travessas curtas dão escala e profundidade às laterais sem bloquear a pista.
            for (float z = 1f; z < comprimentoPedaco; z += 4f)
            {
                Visual.Forma(PrimitiveType.Cube, "SuporteDoGuardaCorpo", raiz,
                    new Vector3(bordaX, 0.12f, z), new Vector3(0.18f, 0.18f, 0.16f),
                    metal, false, raio: 0.02f).GetComponent<Renderer>().sharedMaterial = Visual.MatMetal(metal, 0.72f);
            }
        }
        // Vigas, guarda-corpos e suportes descem junto com o piso quando a pista se curva lá na frente.
        raiz.gameObject.AddComponent<CurvaturaDaMalha>();
        return raiz;
    }

    // ---------- Malhas subdivididas ----------

    /// <summary>Laje do piso com o topo em grade fina (as bordas laterais ficam presas).</summary>
    static Mesh MalhaPiso(Vector3 tamanho)
    {
        if (malhaPiso != null && tamanhoMalhaPiso == tamanho) return malhaPiso;
        int colunas = Mathf.Max(1, Mathf.CeilToInt(tamanho.x / PassoDaGradeX));
        int linhas = Mathf.Max(1, Mathf.CeilToInt(tamanho.z / PassoDaGradeZ));
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var t = new List<int>();
        Vector3 h = tamanho * 0.5f;
        Vector3 x = new Vector3(tamanho.x, 0f, 0f), y = new Vector3(0f, tamanho.y, 0f), z = new Vector3(0f, 0f, tamanho.z);
        Face(v, n, t, new Vector3(-h.x, h.y, -h.z), x, z, colunas, linhas, Vector3.up);
        Face(v, n, t, new Vector3(-h.x, -h.y, -h.z), x, z, 1, 1, Vector3.down);
        Face(v, n, t, new Vector3(-h.x, -h.y, -h.z), z, y, 1, 1, Vector3.left);
        Face(v, n, t, new Vector3(h.x, -h.y, -h.z), z, y, 1, 1, Vector3.right);
        Face(v, n, t, new Vector3(-h.x, -h.y, -h.z), x, y, colunas, 1, Vector3.back);
        Face(v, n, t, new Vector3(-h.x, -h.y, h.z), x, y, colunas, 1, Vector3.forward);
        malhaPiso = CriarMalha("Piso curvavel do espaco-tempo", v, n, t);
        tamanhoMalhaPiso = tamanho;
        return malhaPiso;
    }

    /// <summary>Cubo de lado 1 dividido ao longo de Z (a escala do objeto dá o tamanho).</summary>
    static Mesh MalhaLinha(int segmentos)
    {
        if (malhaLinha != null) return malhaLinha;
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var t = new List<int>();
        Vector3 x = Vector3.right, y = Vector3.up, z = Vector3.forward;
        Face(v, n, t, new Vector3(-0.5f, 0.5f, -0.5f), x, z, 1, segmentos, Vector3.up);
        Face(v, n, t, new Vector3(-0.5f, -0.5f, -0.5f), x, z, 1, segmentos, Vector3.down);
        Face(v, n, t, new Vector3(-0.5f, -0.5f, -0.5f), z, y, segmentos, 1, Vector3.left);
        Face(v, n, t, new Vector3(0.5f, -0.5f, -0.5f), z, y, segmentos, 1, Vector3.right);
        Face(v, n, t, new Vector3(-0.5f, -0.5f, -0.5f), x, y, 1, 1, Vector3.back);
        Face(v, n, t, new Vector3(-0.5f, -0.5f, 0.5f), x, y, 1, 1, Vector3.forward);
        malhaLinha = CriarMalha("Linha neon curvavel", v, n, t);
        return malhaLinha;
    }

    /// <summary>Grade de quadrados de "origem" até origem + eixoU + eixoV, virada para "normal".</summary>
    static void Face(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 origem,
        Vector3 eixoU, Vector3 eixoV, int divisoesU, int divisoesV, Vector3 normal)
    {
        int inicio = v.Count;
        for (int j = 0; j <= divisoesV; j++)
            for (int i = 0; i <= divisoesU; i++)
            {
                v.Add(origem + eixoU * (i / (float)divisoesU) + eixoV * (j / (float)divisoesV));
                n.Add(normal);
            }
        bool inverter = Vector3.Dot(Vector3.Cross(eixoV, eixoU), normal) < 0f;
        for (int j = 0; j < divisoesV; j++)
            for (int i = 0; i < divisoesU; i++)
            {
                int a = inicio + j * (divisoesU + 1) + i, b = a + 1, c = a + divisoesU + 1, d = c + 1;
                if (!inverter) { t.Add(a); t.Add(c); t.Add(b); t.Add(b); t.Add(c); t.Add(d); }
                else { t.Add(a); t.Add(b); t.Add(c); t.Add(b); t.Add(d); t.Add(c); }
            }
    }

    static Mesh CriarMalha(string nome, List<Vector3> v, List<Vector3> n, List<int> t)
    {
        var malha = new Mesh { name = nome };
        if (v.Count > 65000) malha.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        malha.SetVertices(v);
        malha.SetNormals(n);
        malha.SetTriangles(t, 0);
        malha.RecalculateBounds();
        // Margem nos limites: o shader afunda o piso abaixo da laje original.
        Bounds b = malha.bounds;
        b.Expand(new Vector3(0f, 3f, 0f));
        malha.bounds = b;
        return malha;
    }
}

