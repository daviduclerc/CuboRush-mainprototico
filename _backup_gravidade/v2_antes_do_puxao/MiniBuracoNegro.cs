using UnityEngine;

/// <summary>
/// Mini buraco negro numa faixa da pista. Afunda o piso em funil em volta dele e,
/// quando o Sol passa numa faixa vizinha, puxa o Sol para o lado aos poucos: é preciso
/// segurar a direção contrária para não ser arrastado até ele. Tocar o núcleo dá dano
/// como um obstáculo comum (a colisão fica no TrackItem registrado pelo ObstacleSpawner).
/// </summary>
public sealed class MiniBuracoNegro : MonoBehaviour
{
    public PlayerController jogador;

    [Tooltip("Profundidade do funil no piso.")]
    public float profundidade = 0.8f;
    [Tooltip("Começa a puxar quando está a esta distância à frente do Sol.")]
    public float alcancePuxao = 16f;

    static MiniBuracoNegro dono;
    static int avisos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() { dono = null; avisos = 0; }

    Transform disco, anel, halo;
    Material[] materiais;
    float profundidadeAtual;
    bool avisado;

    public static GameObject Criar(Transform pai, Vector3 posicao, PlayerController jogador)
    {
        var raiz = new GameObject("MiniBuracoNegro");
        raiz.transform.SetParent(pai, false);
        raiz.transform.position = posicao;
        var buraco = raiz.AddComponent<MiniBuracoNegro>();
        buraco.jogador = jogador;
        buraco.Construir();
        return raiz;
    }

    void Construir()
    {
        // Núcleo totalmente escuro: esconde a metade de trás do disco, como um horizonte de eventos.
        GameObject nucleo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        nucleo.name = "HorizonteDeEventos";
        Destroy(nucleo.GetComponent<Collider>());
        nucleo.transform.SetParent(transform, false);
        nucleo.transform.localScale = Vector3.one * 0.95f;
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        Material escuro = unlit != null ? new Material(unlit) : new Material(Visual.MatMetal(Color.black, 0f));
        if (escuro.HasProperty("_BaseColor")) escuro.SetColor("_BaseColor", Color.black);
        escuro.color = Color.black;
        nucleo.GetComponent<Renderer>().sharedMaterial = escuro;

        Material matDisco = Halos.Novo(new Color(2.6f, 0.55f, 1.6f), 0.30f, 1f, 1.5f, 1f, 4f);
        Material matAnel = Halos.Novo(new Color(2.4f, 1.6f, 2.8f), 0.60f, 0.78f, 2.2f, 0f, 0f);
        Material matHalo = Halos.Novo(new Color(0.5f, 0.1f, 0.6f), 0f, 1f, 2.4f, 0f, 0f);
        materiais = new[] { escuro, matDisco, matAnel, matHalo };

        disco = Quad("DiscoDeAcrecao", 3.6f, matDisco);
        disco.localRotation = Quaternion.Euler(78f, 0f, 0f);
        anel = Quad("AnelDeFotons", 1.6f, matAnel);
        halo = Quad("Halo", 4.6f, matHalo);
    }

    Transform Quad(string nome, float tamanho, Material material)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = nome;
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(transform, false);
        quad.transform.localScale = Vector3.one * tamanho;
        Renderer r = quad.GetComponent<Renderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return quad.transform;
    }

    void Update()
    {
        if (jogador == null) return;
        GameManager jogo = GameManager.Instancia;
        bool jogando = jogo != null && jogo.Estado == EstadoJogo.Jogando;
        float dt = jogando ? Time.deltaTime : 0f;
        float dz = transform.position.z - jogador.transform.position.z;

        // Funil no piso: abre quando o buraco entra no trecho visível e fecha depois de passar.
        bool visivel = dz > -10f && dz < 80f;
        profundidadeAtual = Mathf.MoveTowards(profundidadeAtual, visivel ? profundidade : 0f, dt * 1.2f);
        if (dono == null || dono == this)
        {
            dono = profundidadeAtual > 0f || visivel ? this : null;
            CampoGravitacional.DefinirBuraco(new Vector2(transform.position.x, transform.position.z), profundidadeAtual);
        }

        if (jogando && !avisado && dz < 55f)
        {
            avisado = true;
            if (avisos < 2)
                GameplayHud.Avisar("MINI BURACO NEGRO!", "segure para o lado oposto para não ser puxado",
                    new Color(0.85f, 0.45f, 1f));
            avisos++;
        }

        // Puxão lateral sobre o Sol nas faixas vizinhas.
        if (jogando && dz > -2f && dz < alcancePuxao)
        {
            float dx = transform.position.x - jogador.PosicaoXDaFaixa(jogador.FaixaAtual);
            float faixas = Mathf.Abs(dx) / jogador.larguraFaixa;
            if (faixas > 0.5f && faixas < 2.5f)
            {
                float entrada = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(alcancePuxao, alcancePuxao - 6f, dz));
                float saida = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-2f, 1f, dz));
                float forca = (faixas < 1.5f ? 1f : 0.45f) * entrada * saida;
                jogador.AplicarAtracaoLateral(dx > 0f ? 1 : -1, forca);
            }
        }

        if (disco != null) disco.Rotate(0f, 0f, 140f * dt, Space.Self);
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        // O anel de fótons e o halo ficam sempre de frente para a câmera.
        if (anel != null) anel.rotation = cam.transform.rotation;
        if (halo != null) halo.rotation = cam.transform.rotation;
    }

    void OnDestroy()
    {
        if (dono == this)
        {
            dono = null;
            CampoGravitacional.DefinirBuraco(Vector2.zero, 0f);
        }
        if (materiais != null)
            foreach (Material m in materiais) if (m != null) Destroy(m);
    }
}

/// <summary>Materiais do shader de halo (anéis e brilhos aditivos).</summary>
public static class Halos
{
    public static Material Novo(Color cor, float interno, float externo, float queda, float espiral, float giro)
    {
        Shader shader = Resources.Load<Shader>("Space/HaloGravitacional");
        Material m = shader != null ? new Material(shader) : new Material(Visual.Mat(cor, true));
        m.name = "Halo gravitacional";
        if (m.HasProperty("_Cor"))
        {
            m.SetColor("_Cor", cor);
            m.SetFloat("_Interno", interno);
            m.SetFloat("_Externo", externo);
            m.SetFloat("_Queda", queda);
            m.SetFloat("_Espiral", espiral);
            m.SetFloat("_Giro", giro);
        }
        return m;
    }
}
