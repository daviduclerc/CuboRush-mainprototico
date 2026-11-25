using UnityEngine;

/// <summary>
/// Mini buraco negro numa faixa da pista. Afunda o piso em funil em volta dele e:
///  - quando o Sol está a 1 ou 2 faixas de distância, arrasta o Sol na hora uma faixa
///    na direção dele (uma vez por buraco, para sempre dar tempo de desviar depois);
///  - na faixa do buraco não dá para pular nem deslizar: ele suga o Sol para baixo e a
///    colisão (TrackItem registrado pelo ObstacleSpawner) vai do chão até bem acima do pulo.
/// </summary>
public sealed class MiniBuracoNegro : MonoBehaviour
{
    public PlayerController jogador;

    [Tooltip("Profundidade do funil no piso.")]
    public float profundidade = 0.8f;
    [Tooltip("Distância mínima à frente do Sol em que o puxão acontece (cresce com a velocidade).")]
    public float alcancePuxao = 15f;
    /// <summary>Puxa o Sol se ele estiver a até esta quantidade de faixas de distância.</summary>
    public const int AlcanceEmFaixas = 2;
    /// <summary>Faixa em que o buraco está.</summary>
    public int faixa;

    static MiniBuracoNegro dono;
    static int avisos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() { dono = null; avisos = 0; }

    Transform disco, anel, halo;
    Material[] materiais;
    float profundidadeAtual;
    bool avisado;
    bool puxou;
    float tranco;
    LineRenderer feixe;
    float vidaFeixe;

    public static GameObject Criar(Transform pai, Vector3 posicao, int faixa, PlayerController jogador)
    {
        var raiz = new GameObject("MiniBuracoNegro");
        raiz.transform.SetParent(pai, false);
        raiz.transform.position = posicao;
        var buraco = raiz.AddComponent<MiniBuracoNegro>();
        buraco.jogador = jogador;
        buraco.faixa = faixa;
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
        // Logo depois do puxão ele afunda mais por um instante.
        bool visivel = dz > -10f && dz < 80f;
        tranco = Mathf.MoveTowards(tranco, 0f, dt * 2.5f);
        float alvo = visivel ? profundidade * (1f + 0.6f * tranco) : 0f;
        profundidadeAtual = Mathf.MoveTowards(profundidadeAtual, alvo, dt * (tranco > 0f ? 6f : 1.2f));
        if (dono == null || dono == this)
        {
            dono = profundidadeAtual > 0f || visivel ? this : null;
            CampoGravitacional.DefinirBuraco(new Vector2(transform.position.x, transform.position.z), profundidadeAtual);
        }

        if (jogando && !avisado && dz < 60f)
        {
            avisado = true;
            if (avisos < 2)
                GameplayHud.Avisar("MINI BURACO NEGRO!", "ele puxa quem estiver a até 2 faixas — desvie logo depois do puxão",
                    new Color(0.85f, 0.45f, 1f), 2.6f);
            avisos++;
        }

        if (jogando)
        {
            int distancia = faixa - jogador.FaixaAtual;

            // Puxão: a primeira vez que o Sol estiver a 1 ou 2 faixas dentro do alcance,
            // é arrastado na hora uma faixa na direção do buraco. Só acontece uma vez por buraco,
            // para sempre sobrar a chance de desviar depois.
            float alcance = Mathf.Max(alcancePuxao, jogo.Velocidade * 0.8f);
            if (!puxou && dz > 1f && dz < alcance && distancia != 0 && Mathf.Abs(distancia) <= AlcanceEmFaixas)
            {
                puxou = true;
                jogador.PuxarUmaFaixa(distancia);
                tranco = 1f;
                MostrarFeixe();
                jogo.SomGravitacional();
            }

            // Na faixa do buraco não adianta pular nem deslizar: ele suga o Sol para baixo
            // e a colisão dele vai do chão até bem acima do pulo.
            if (distancia == 0 && dz > -0.6f && dz < 4.5f) jogador.SugarParaBaixo();
        }

        AtualizarFeixe(dt);
        if (disco != null) disco.Rotate(0f, 0f, (140f + 500f * tranco) * dt, Space.Self);
    }

    // ---------- Feixe do puxão ----------

    /// <summary>Um filete de luz liga o Sol ao buraco por um instante, mostrando o puxão.</summary>
    void MostrarFeixe()
    {
        if (feixe == null)
        {
            var objeto = new GameObject("FeixeDoPuxao");
            objeto.transform.SetParent(transform, false);
            feixe = objeto.AddComponent<LineRenderer>();
            feixe.useWorldSpace = true;
            feixe.positionCount = 12;
            feixe.sharedMaterial = Visual.Mat(new Color(0.85f, 0.4f, 1f), true);
            feixe.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            feixe.receiveShadows = false;
            feixe.numCapVertices = 4;
        }
        vidaFeixe = 1f;
        feixe.enabled = true;
    }

    void AtualizarFeixe(float dt)
    {
        if (feixe == null || !feixe.enabled) return;
        vidaFeixe -= dt * 2.4f;
        if (vidaFeixe <= 0f) { feixe.enabled = false; return; }
        Vector3 de = jogador.Limites.center;
        Vector3 ate = transform.position;
        // Curva levemente para baixo, como algo escorrendo para dentro do funil.
        for (int i = 0; i < feixe.positionCount; i++)
        {
            float t = i / (feixe.positionCount - 1f);
            Vector3 p = Vector3.Lerp(de, ate, t);
            p.y -= Mathf.Sin(t * Mathf.PI) * 0.35f;
            p += Random.insideUnitSphere * 0.05f * vidaFeixe;
            feixe.SetPosition(i, p);
        }
        feixe.widthMultiplier = 0.12f * vidaFeixe;
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
