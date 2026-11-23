using UnityEngine;

/// <summary>
/// Planeta ou estrela ao lado da pista para o estilingue gravitacional: passar rente
/// a ele (na faixa da borda daquele lado) dá um impulso de velocidade e pontos extras,
/// a mesma manobra que as sondas espaciais usam. Setas no chão marcam a faixa certa.
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
    Material[] materiais;
    float raio = 2.5f;
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
        int tipo = Random.Range(0, 3);
        Material superficie;
        Color corHalo;
        switch (tipo)
        {
            case 0: // gigante gasoso
                raio = 3f;
                superficie = MaterialPlaneta("Space/jupiter_nasa", new Color(1f, 0.85f, 0.7f));
                corHalo = new Color(1.3f, 0.72f, 0.3f);
                break;
            case 1: // estrela azul
                raio = 2.1f;
                superficie = new Material(Visual.Mat(new Color(0.55f, 0.75f, 1f), true));
                if (superficie.HasProperty("_EmissionColor"))
                    superficie.SetColor("_EmissionColor", new Color(0.55f, 0.75f, 1f) * 3f);
                corHalo = new Color(0.55f, 0.85f, 2f);
                break;
            default: // planeta rochoso com oceanos
                raio = 2.4f;
                superficie = MaterialPlaneta("Space/earth_nasa", Color.white);
                corHalo = new Color(0.3f, 0.75f, 1.5f);
                break;
        }
        cor = new Color(Mathf.Clamp01(corHalo.r), Mathf.Clamp01(corHalo.g), Mathf.Clamp01(corHalo.b));

        float meiaPista = jogador.quantidadeFaixas * jogador.larguraFaixa / 2f;
        transform.position = new Vector3(lado * (meiaPista + 0.8f + raio), 1.1f, z);

        GameObject esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        esfera.name = "CorpoCeleste";
        Destroy(esfera.GetComponent<Collider>());
        esfera.transform.SetParent(transform, false);
        esfera.transform.localScale = Vector3.one * raio * 2f;
        esfera.transform.localRotation = Quaternion.Euler(18f, 0f, -12f);
        esfera.GetComponent<Renderer>().sharedMaterial = superficie;
        corpo = esfera.transform;

        Material matHalo = Halos.Novo(corHalo, 0.52f, 1f, 2.2f, 0f, 0f);
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Atmosfera";
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(transform, false);
        quad.transform.localScale = Vector3.one * raio * 3.4f;
        Renderer r = quad.GetComponent<Renderer>();
        r.sharedMaterial = matHalo;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        halo = quad.transform;

        // Setas no chão da faixa rente, apontando para a frente, logo antes do planeta.
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
        materiais = new[] { superficie, matHalo };
    }

    static Material MaterialPlaneta(string caminho, Color tinta)
    {
        Material m = new Material(Visual.MatMetal(Color.white, 0f));
        m.name = "Superficie do planeta";
        Texture2D mapa = Resources.Load<Texture2D>(caminho);
        m.color = tinta;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tinta);
        if (mapa != null)
        {
            m.mainTexture = mapa;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", mapa);
            // Um pouco de brilho próprio para o lado escuro não sumir no vazio.
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", mapa);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", tinta * 0.35f);
        }
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
        return m;
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
        Camera cam = Camera.main;
        if (halo == null || cam == null) return;
        halo.rotation = cam.transform.rotation;
        halo.localScale = Vector3.one * raio * (3.4f + 1.2f * clarao);
    }

    void OnDestroy()
    {
        if (materiais != null)
            foreach (Material m in materiais) if (m != null) Destroy(m);
    }
}
