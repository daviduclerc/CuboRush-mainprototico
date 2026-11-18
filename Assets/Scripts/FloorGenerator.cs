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
    public Color corPiso = new Color(0.13f, 0.16f, 0.20f);
    public Color corLinhas = new Color(0.025f, 0.18f, 0.42f);
    public float larguraLinha = 0.035f;

    readonly Queue<Transform> pedacos = new Queue<Transform>();
    float proximoZ;
    static readonly Material[] materiaisLinhaNeon = new Material[6];

    void Start()
    {
        // Começa um pedaço atrás do jogador para a câmera não ver o vazio.
        float zInicial = jogador != null ? jogador.position.z : 0f;
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

        // Cada divisa longitudinal tem uma cor neon; o piso entre elas é liso.
        Color[] coresNeon = {
            new Color(0.02f, 0.85f, 1f),
            new Color(0.12f, 0.32f, 1f),
            new Color(0.62f, 0.12f, 1f),
            new Color(1f, 0.06f, 0.55f),
            new Color(0.04f, 1f, 0.65f),
            new Color(0.55f, 0.85f, 1f)
        };
        for (int i = 0; i <= quantidadeFaixas; i++)
        {
            Color corNeon = coresNeon[i % coresNeon.Length];
            GameObject linhaNeon = Visual.Forma(PrimitiveType.Cube, "LinhaNeonLongitudinal", raiz,
                new Vector3(-larguraTotal / 2f + i * larguraFaixa, 0.008f, comprimentoPedaco / 2f),
                new Vector3(0.060f, 0.012f, comprimentoPedaco), corNeon, true, raio: 0f);
            int indiceCor = i % coresNeon.Length;
            if (materiaisLinhaNeon[indiceCor] == null)
            {
                Material neon = new Material(Visual.Mat(corNeon, true));
                neon.name = "Neon vibrante da pista " + indiceCor;
                neon.EnableKeyword("_EMISSION");
                neon.SetColor("_EmissionColor", corNeon * 3.5f);
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

            // Guarda-corpo de uma pista orbital: placas de liga escura e filete emissivo.
            float bordaX = lado * (larguraTotal / 2f + 0.27f);
            GameObject guarda = Visual.Forma(PrimitiveType.Cube, "GuardaCorpoOrbital", raiz,
                new Vector3(bordaX, 0.30f, comprimentoPedaco / 2f),
                new Vector3(0.12f, 0.58f, comprimentoPedaco), new Color(0.035f, 0.055f, 0.095f), false, raio: 0.035f);
            guarda.GetComponent<Renderer>().sharedMaterial = Visual.MatMetal(new Color(0.035f, 0.055f, 0.095f), 0.82f);

            // Travessas curtas dão escala e profundidade às laterais sem bloquear a pista.
            for (float z = 1f; z < comprimentoPedaco; z += 4f)
            {
                Visual.Forma(PrimitiveType.Cube, "SuporteDoGuardaCorpo", raiz,
                    new Vector3(bordaX, 0.12f, z), new Vector3(0.18f, 0.18f, 0.16f),
                    metal, false, raio: 0.02f).GetComponent<Renderer>().sharedMaterial = Visual.MatMetal(metal, 0.72f);
            }
        }
        return raiz;
    }
}
