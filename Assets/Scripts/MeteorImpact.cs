using UnityEngine;

/// <summary>Uma queda telegrafada. Alvo, movimento e colisão usam o mesmo ponto de impacto.</summary>
public sealed class MeteorImpact : MonoBehaviour
{
    static readonly Mesh[] Rochas = new Mesh[5];
    static int proximaRocha;
    static Material materialRocha, materialAlvo, materialRastro, materialParticulas;
    MaterialPropertyBlock propriedades;
    Vector3 entrada, posicaoAnterior;
    float inicio, chegada, fimDaBrasa, raio;
    bool emSolo, acertouSol;
    Transform corpo;
    Vector3 eixoDeGiro;
    Quaternion rotacaoInicial;
    Renderer marca;
    TrailRenderer rastro;
    ParticleSystem faiscas, fumaca, impacto;
    public Vector3 Alvo { get; private set; }

    public static MeteorImpact Criar(Transform pai, GameObject visual, Vector3 alvo, Vector3 origem,
        float inicio, float chegada, float tempoBrasa, float raio)
    {
        var raiz = new GameObject("Meteoro da chuva");
        raiz.transform.SetParent(pai, false);
        raiz.transform.position = alvo;
        var meteoro = raiz.AddComponent<MeteorImpact>();
        meteoro.Alvo = alvo;
        meteoro.entrada = meteoro.posicaoAnterior = origem;
        meteoro.inicio = inicio;
        meteoro.chegada = chegada;
        meteoro.fimDaBrasa = chegada + tempoBrasa;
        meteoro.raio = raio;
        meteoro.CriarVisual(visual);
        return meteoro;
    }

    void CriarVisual(GameObject visual)
    {
        propriedades = new MaterialPropertyBlock();
        if (materialAlvo == null) materialAlvo = MaterialDe("Gameplay/MeteorTarget");
        if (materialParticulas == null) materialParticulas = MaterialDe("Gameplay/MeteorParticle");

        GameObject objeto = new GameObject("Corpo rochoso");
        corpo = objeto.transform;
        corpo.SetParent(transform, false);
        corpo.position = entrada;
        eixoDeGiro = new Vector3(0.38f + Mathf.Sin(entrada.x * 0.53f) * 0.32f, 0.82f, -0.33f).normalized;
        rotacaoInicial = Quaternion.AngleAxis(entrada.z * 11f, Vector3.up);
        corpo.localRotation = rotacaoInicial;
        if (visual != null) Instantiate(visual, corpo, false);
        else AdicionarCorpoRochoso(objeto);
        rastro = AdicionarRastro(objeto, 0.86f, 0.43f);
        faiscas = Particulas("Faíscas", corpo, new Color(2.2f, 0.63f, 0.09f, 0.92f), 0.30f, 0.085f, 28, 35);
        fumaca = Particulas("Fumaça espacial", corpo, new Color(0.25f, 0.13f, 0.10f, 0.36f), 0.68f, 0.52f, 15, 24);
        impacto = Particulas("Explosão no impacto", transform, new Color(2.6f, 0.63f, 0.06f, 1f), 0.55f, 0.13f, 0, 44);
        impacto.transform.localPosition = Vector3.up * 0.23f;
        AlongarFaiscas(faiscas, 0.15f);
        AlongarFaiscas(impacto, 0.7f);

        var alvo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        alvo.name = "Alvo de impacto no piso";
        alvo.transform.SetParent(transform, false);
        alvo.transform.localPosition = Vector3.up * 0.039f;
        alvo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        alvo.transform.localScale = Vector3.one * (raio * 2.3f);
        Destroy(alvo.GetComponent<Collider>());
        marca = alvo.GetComponent<Renderer>();
        marca.sharedMaterial = materialAlvo;
        marca.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        AtualizarMarca(0f, 0f);
    }

    internal static TrailRenderer AdicionarRastro(GameObject objeto, float largura, float duracao)
    {
        if (materialRastro == null) materialRastro = MaterialDe("Gameplay/MeteorTrail");
        var rastro = objeto.AddComponent<TrailRenderer>();
        rastro.sharedMaterial = materialRastro;
        rastro.time = duracao;
        rastro.minVertexDistance = 0.09f;
        rastro.startWidth = largura;
        rastro.endWidth = 0.025f;
        rastro.startColor = new Color(1f, 0.63f, 0.21f, 0.94f);
        rastro.endColor = new Color(1f, 0.19f, 0.025f, 0f);
        rastro.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rastro.receiveShadows = false;
        return rastro;
    }

    static Material MaterialDe(string caminho)
    {
        Shader shader = Resources.Load<Shader>(caminho);
        return shader != null ? new Material(shader) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
    }

    internal static void AdicionarCorpoRochoso(GameObject objeto, float calor = 1f)
    {
        int indice = proximaRocha;
        proximaRocha = (proximaRocha + 1) % Rochas.Length;
        if (Rochas[indice] == null) Rochas[indice] = MalhasEstelares.CriarRocha(300 + indice * 17);
        if (materialRocha == null) materialRocha = MaterialDe("Space/RochaEspacial");
        objeto.AddComponent<MeshFilter>().sharedMesh = Rochas[indice];
        var render = objeto.AddComponent<MeshRenderer>();
        render.sharedMaterial = materialRocha;
        var calorDaRocha = new MaterialPropertyBlock();
        calorDaRocha.SetFloat("_CalorMeteoro", calor);
        render.SetPropertyBlock(calorDaRocha);
    }

    static ParticleSystem Particulas(string nome, Transform pai, Color cor, float vida, float tamanho, int taxa, int limite)
    {
        var objeto = new GameObject(nome);
        objeto.transform.SetParent(pai, false);
        var sistema = objeto.AddComponent<ParticleSystem>();
        sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = sistema.main;
        main.playOnAwake = false;
        main.loop = taxa > 0;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(vida * 0.5f, vida);
        main.startSize = new ParticleSystem.MinMaxCurve(tamanho * 0.55f, tamanho);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, taxa > 0 ? 2.7f : 6.2f);
        main.startColor = cor;
        main.maxParticles = limite;
        var forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Hemisphere;
        forma.rotation = new Vector3(-90f, 0f, 0f);
        forma.radius = taxa > 0 ? 0.16f : 0.32f;
        var emissao = sistema.emission;
        emissao.enabled = taxa > 0;
        emissao.rateOverTime = taxa;
        var ciclo = sistema.colorOverLifetime;
        ciclo.enabled = true;
        var gradiente = new Gradient();
        gradiente.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.45f), new GradientAlphaKey(0f, 1f) });
        ciclo.color = gradiente;
        var render = objeto.GetComponent<ParticleSystemRenderer>();
        render.sharedMaterial = materialParticulas;
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        render.receiveShadows = false;
        if (taxa > 0) sistema.Play();
        return sistema;
    }

    static void AlongarFaiscas(ParticleSystem sistema, float gravidade)
    {
        var main = sistema.main;
        main.gravityModifier = gravidade;
        var render = sistema.GetComponent<ParticleSystemRenderer>();
        render.renderMode = ParticleSystemRenderMode.Stretch;
        render.lengthScale = 2.0f;
        render.velocityScale = 0.16f;
    }

    // Retorna falso quando a marca, o calor e as partículas já terminaram.
    public bool Atualizar(float agora, Bounds anterior, Bounds atual, out bool bateuNoPiso, out bool acertou)
    {
        bateuNoPiso = acertou = false;
        if (agora > fimDaBrasa + 0.10f && emSolo) return false;
        float progresso = Mathf.InverseLerp(inicio, chegada, agora);
        Vector3 ponto = Vector3.Lerp(entrada, Alvo + Vector3.up * 0.42f, progresso * (0.36f + 0.64f * progresso));
        ponto.x = Mathf.Lerp(entrada.x, Alvo.x, Mathf.SmoothStep(0f, 1f, progresso / 0.70f));

        if (!emSolo)
        {
            corpo.position = ponto;
            corpo.localRotation = Quaternion.AngleAxis(progresso * 440f, eixoDeGiro) * rotacaoInicial;
            if (!acertouSol && Cruzou(posicaoAnterior, ponto, anterior, atual, raio * 0.76f)) acertou = true;
            posicaoAnterior = ponto;
            if (progresso >= 1f)
            {
                emSolo = bateuNoPiso = true;
                foreach (Renderer render in corpo.GetComponentsInChildren<Renderer>())
                    if (render is MeshRenderer || render is SkinnedMeshRenderer) render.enabled = false;
                rastro.emitting = false;
                faiscas.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                fumaca.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                impacto.Play();
                impacto.Emit(42);
            }
        }

        if (emSolo && agora <= fimDaBrasa && !acertouSol)
        {
            // Calor rente ao piso. O salto por cima do impacto é válido.
            var piso = Alvo + Vector3.up * 0.12f;
            if (Cruzou(piso, piso, anterior, atual, raio)) acertou = true;
        }
        acertouSol |= acertou;
        AtualizarMarca(progresso, emSolo ? Mathf.InverseLerp(chegada, fimDaBrasa + 0.10f, agora) : 0f);
        return true;
    }

    static bool Cruzou(Vector3 origem, Vector3 destino, Bounds anterior, Bounds atual, float raio)
    {
        Vector3 a = origem - anterior.center;
        Vector3 b = destino - atual.center;
        Vector3 percurso = b - a;
        float t = Mathf.Clamp01(-Vector3.Dot(a, percurso) / Mathf.Max(0.0001f, percurso.sqrMagnitude));
        Vector3 maisPerto = a + percurso * t;
        Vector3 extents = Vector3.Lerp(anterior.extents, atual.extents, t);
        Vector3 fora = Vector3.Max(Vector3.zero,
            new Vector3(Mathf.Abs(maisPerto.x), Mathf.Abs(maisPerto.y), Mathf.Abs(maisPerto.z)) - extents);
        return fora.sqrMagnitude <= raio * raio;
    }

    void AtualizarMarca(float progresso, float sumico)
    {
        propriedades.SetFloat("_Progresso", progresso);
        propriedades.SetFloat("_Brasa", emSolo ? 1f : 0f);
        propriedades.SetFloat("_IdadeBrasa", sumico);
        propriedades.SetFloat("_Opacidade", 1f - Mathf.SmoothStep(0f, 1f, sumico));
        marca.SetPropertyBlock(propriedades);
    }

}
