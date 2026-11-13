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
    public Color corPiso = new Color(0.018f, 0.026f, 0.055f);
    public Color corLinhas = new Color(0.025f, 0.18f, 0.42f);
    public float larguraLinha = 0.035f;

    readonly Queue<Transform> pedacos = new Queue<Transform>();
    float proximoZ;

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

        // Grade neon: um quadrado por faixa, repetido ao longo do pedaço.
        piso.GetComponent<Renderer>().sharedMaterial = Visual.MatPiso(corPiso, corLinhas,
            new Vector2(quantidadeFaixas, comprimentoPedaco / larguraFaixa));

        // Guias metálicas sutis mantêm as faixas visíveis sem parecer uma grade neon.
        for (int i = 0; i <= quantidadeFaixas; i++)
        {
            GameObject guia = Visual.Forma(PrimitiveType.Cube, "GuiaMetalica", raiz,
                new Vector3(-larguraTotal / 2f + i * larguraFaixa, 0.01f, comprimentoPedaco / 2f),
                new Vector3(larguraLinha, 0.025f, comprimentoPedaco), corLinhas, false, raio: 0f);
            guia.GetComponent<Renderer>().sharedMaterial = Visual.Mat(corLinhas, true);
        }

        // Trilhos de energia âmbar atravessam a faixa central, como reflexo da supernova.
        Color corEnergia = new Color(1f, 0.20f, 0.035f);
        for (int lado = -1; lado <= 1; lado += 2)
        {
            Visual.Forma(PrimitiveType.Cube, "TrilhoEnergeticoCentral", raiz,
                new Vector3(lado * 0.34f, 0.018f, comprimentoPedaco / 2f),
                new Vector3(0.035f, 0.025f, comprimentoPedaco), corEnergia, true, raio: 0f);
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

            GameObject filete = Visual.Forma(PrimitiveType.Cube, "FileteDeLuzLateral", raiz,
                new Vector3(bordaX - lado * 0.075f, 0.20f, comprimentoPedaco / 2f),
                new Vector3(0.05f, 0.045f, comprimentoPedaco),
                lado < 0 ? new Color(0.05f, 0.48f, 1f) : new Color(0.88f, 0.08f, 1f), true, raio: 0f);

            // Travessas curtas dão escala e profundidade às laterais sem bloquear a pista.
            for (float z = 1f; z < comprimentoPedaco; z += 4f)
            {
                Visual.Forma(PrimitiveType.Cube, "SuporteDoGuardaCorpo", raiz,
                    new Vector3(bordaX, 0.12f, z), new Vector3(0.18f, 0.18f, 0.16f),
                    metal, false, raio: 0.02f).GetComponent<Renderer>().sharedMaterial = Visual.MatMetal(metal, 0.72f);
            }
        }
        for (float z = 2f; z < comprimentoPedaco; z += 4f)
        {
            GameObject junta = Visual.Forma(PrimitiveType.Cube, "JuntaEstrutural", raiz,
                new Vector3(0f, 0.012f, z), new Vector3(larguraTotal, 0.025f, 0.035f),
                new Color(0.075f, 0.12f, 0.22f), false, raio: 0f);
            junta.GetComponent<Renderer>().sharedMaterial = Visual.MatMetal(new Color(0.075f, 0.12f, 0.22f), 0.45f);
        }

        return raiz;
    }
}
