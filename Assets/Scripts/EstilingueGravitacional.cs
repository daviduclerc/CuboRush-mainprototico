using UnityEngine;

/// <summary>
/// Asteroide ao lado da pista para o estilingue gravitacional. Passar rente
/// a ele dá velocidade e pontos extras. As setas no chão marcam a faixa certa.
/// A avaliação da passagem fica no ObstacleSpawner (TipoItem.Estilingue).
/// </summary>
public sealed class EstilingueGravitacional : MonoBehaviour
{
    public PlayerController jogador;
    /// <summary>-1 = à esquerda da pista, +1 = à direita.</summary>
    public int lado;
    public Color cor;
    public int FaixaRente => lado < 0 ? 0 : jogador.quantidadeFaixas - 1;

    static int avisos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => avisos = 0;

    Transform corpo, halo;
    Mesh malhaRocha;
    Material materialRocha, materialHalo;
    float raio;
    float clarao;
    bool avisado;

    public static GameObject Criar(Transform pai, float z, int lado, PlayerController jogador)
    {
        var raiz = new GameObject("EstilingueGravitacional");
        raiz.transform.SetParent(pai, false);
        var estilingue = raiz.AddComponent<EstilingueGravitacional>();
        estilingue.jogador = jogador;
        estilingue.lado = lado < 0 ? -1 : 1;
        estilingue.Construir(z);
        return raiz;
    }

    void Construir(float z)
    {
        raio = Random.Range(2.1f, 3f);
        cor = new Color(1f, 0.72f, 0.3f);

        float meiaPista = jogador.quantidadeFaixas * jogador.larguraFaixa / 2f;
        transform.position = new Vector3(lado * (meiaPista + 0.8f + raio), 1.1f, z);

        var asteroide = new GameObject("Asteroide do estilingue");
        corpo = asteroide.transform;
        corpo.SetParent(transform, false);
        corpo.localRotation = Random.rotation;
        malhaRocha = MalhasEstelares.CriarRocha(Random.Range(0, 1000000));
        float raioDaMalhaQuadrado = 0.0001f;
        foreach (Vector3 vertice in malhaRocha.vertices)
            raioDaMalhaQuadrado = Mathf.Max(raioDaMalhaQuadrado, vertice.sqrMagnitude);
        corpo.localScale = Vector3.one * (raio / Mathf.Sqrt(raioDaMalhaQuadrado));
        asteroide.AddComponent<MeshFilter>().sharedMesh = malhaRocha;
        Shader shaderRocha = Resources.Load<Shader>("Space/RochaEspacial");
        materialRocha = shaderRocha != null ? new Material(shaderRocha) : new Material(Visual.Mat(Color.gray));
        asteroide.AddComponent<MeshRenderer>().sharedMaterial = materialRocha;

        materialHalo = Halos.Novo(cor * 0.6f, 0.52f, 1f, 2.2f, 0f, 0f);
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Clarão do impulso";
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(transform, false);
        quad.transform.localScale = Vector3.one * raio * 3.4f;
        Renderer r = quad.GetComponent<Renderer>();
        r.sharedMaterial = materialHalo;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        halo = quad.transform;
        quad.SetActive(false);

        // Setas na faixa mais próxima, antes do asteroide.
        Material matSeta = Visual.Mat(cor, true);
        float xFaixa = jogador.PosicaoXDaFaixa(FaixaRente) - transform.position.x;
        for (int i = 0; i < 5; i++)
        {
            float dz = -15f + i * 3f;
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject barra = Visual.Forma(PrimitiveType.Cube, "SetaDoEstilingue", transform,
                    new Vector3(xFaixa + s * 0.26f, -transform.position.y + 0.02f, dz - 0.2f),
                    new Vector3(0.12f, 0.03f, 0.8f), cor, true, raio: 0f);
                barra.transform.localRotation = Quaternion.Euler(0f, -s * 35f, 0f);
                barra.GetComponent<Renderer>().sharedMaterial = matSeta;
            }
        }
    }

    /// <summary>Chamado quando o Sol passa rente: o halo dá um clarão.</summary>
    public void Usar() => clarao = 1f;

    void Update()
    {
        GameManager jogo = GameManager.Instancia;
        bool jogando = jogo != null && jogo.Estado == EstadoJogo.Jogando;
        float dt = jogando ? Time.deltaTime : 0f;
        if (corpo != null) corpo.Rotate(0f, 10f * dt, 0f, Space.Self);
        clarao = Mathf.MoveTowards(clarao, 0f, dt * 1.5f);

        if (jogando && !avisado && jogador != null && transform.position.z - jogador.transform.position.z < 55f)
        {
            avisado = true;
            if (avisos < 3)
                GameplayHud.Avisar("ESTILINGUE GRAVITACIONAL", "passe rente pela " + (lado < 0 ? "esquerda" : "direita") +
                    " para ganhar impulso", cor);
            avisos++;
        }
    }

    void LateUpdate()
    {
        if (halo == null) return;
        halo.gameObject.SetActive(clarao > 0f);
        if (clarao <= 0f) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        halo.rotation = cam.transform.rotation;
        halo.localScale = Vector3.one * raio * (3.4f + 1.2f * clarao);
        if (materialHalo.HasProperty("_Cor")) materialHalo.SetColor("_Cor", cor * (0.6f * clarao));
    }

    void OnDestroy()
    {
        if (malhaRocha != null) Destroy(malhaRocha);
        if (materialRocha != null) Destroy(materialRocha);
        if (materialHalo != null) Destroy(materialHalo);
    }
}
