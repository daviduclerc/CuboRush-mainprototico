using UnityEngine;

/// <summary>Uma queda telegrafada. Alvo, movimento e colisão usam o mesmo ponto de impacto.</summary>
public sealed class MeteorImpact : MonoBehaviour
{
    static Mesh malhaRochosa;
    static Material materialRocha, materialAlvo, materialRastro, materialParticulas;
    MaterialPropertyBlock propriedades;
    Vector3 entrada, posicaoAnterior;
    float inicio, chegada, fimDaBrasa, raio;
    bool emSolo, acertouSol;
    Transform corpo;
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
        if (materialRocha == null) materialRocha = MaterialDe("Gameplay/MeteorRock");
        if (materialAlvo == null) materialAlvo = MaterialDe("Gameplay/MeteorTarget");
        if (materialRastro == null) materialRastro = MaterialDe("Space/ShootingStar");
        if (materialParticulas == null) materialParticulas = MaterialDe("Gameplay/MeteorParticle");
        if (malhaRochosa == null) malhaRochosa = CriarRocha();

        GameObject objeto = new GameObject("Corpo rochoso");
        corpo = objeto.transform;
        corpo.SetParent(transform, false);
        corpo.position = entrada;
        if (visual != null) Instantiate(visual, corpo, false);
        else
        {
            objeto.AddComponent<MeshFilter>().sharedMesh = malhaRochosa;
            objeto.AddComponent<MeshRenderer>().sharedMaterial = materialRocha;
        }
        rastro = objeto.AddComponent<TrailRenderer>();
        rastro.sharedMaterial = materialRastro;
        rastro.time = 0.32f;
        rastro.minVertexDistance = 0.12f;
        rastro.startWidth = 0.64f;
        rastro.endWidth = 0.04f;
        rastro.startColor = new Color(1f, 0.60f, 0.16f, 0.9f);
        rastro.endColor = new Color(1f, 0.13f, 0.018f, 0f);
        rastro.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rastro.receiveShadows = false;
        faiscas = Particulas("Faíscas", corpo, new Color(2f, 0.48f, 0.06f, 0.9f), 0.23f, 0.12f, 22, 33);
        fumaca = Particulas("Fumaça espacial", corpo, new Color(0.24f, 0.10f, 0.07f, 0.42f), 0.60f, 0.43f, 13, 22);
        impacto = Particulas("Explosão no impacto", transform, new Color(2f, 0.44f, 0.025f, 1f), 0.44f, 0.17f, 0, 44);
        impacto.transform.localPosition = Vector3.up * 0.23f;

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

    static Material MaterialDe(string caminho)
    {
        Shader shader = Resources.Load<Shader>(caminho);
        return shader != null ? new Material(shader) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
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
            corpo.localRotation = Quaternion.Euler(progresso * 570f, progresso * 870f, progresso * 290f);
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
        propriedades.SetFloat("_Opacidade", 1f - Mathf.SmoothStep(0f, 1f, sumico));
        marca.SetPropertyBlock(propriedades);
    }

    static Mesh CriarRocha()
    {
        const float g = 1.618034f;
        Vector3[] ico =
        {
            new Vector3(-1,g,0), new Vector3(1,g,0), new Vector3(-1,-g,0), new Vector3(1,-g,0),
            new Vector3(0,-1,g), new Vector3(0,1,g), new Vector3(0,-1,-g), new Vector3(0,1,-g),
            new Vector3(g,0,-1), new Vector3(g,0,1), new Vector3(-g,0,-1), new Vector3(-g,0,1)
        };
        int[] faces =
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
            1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
            4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };
        var vertices = new Vector3[faces.Length * 4];
        var uv = new Vector2[vertices.Length];
        var indices = new int[vertices.Length];
        int n = 0;
        for (int i = 0; i < faces.Length; i += 3)
        {
            Vector3 a = ico[faces[i]], b = ico[faces[i + 1]], c = ico[faces[i + 2]];
            Vector3 ab = (a + b) * 0.5f, bc = (b + c) * 0.5f, ca = (c + a) * 0.5f;
            Face(a, ab, ca); Face(ab, b, bc); Face(ca, bc, c); Face(ab, bc, ca);
        }
        var mesh = new Mesh { name = "Rocha facetada com fissuras" };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = indices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;

        void Face(Vector3 a, Vector3 b, Vector3 c)
        {
            vertices[n] = Irregular(a); uv[n] = Vector2.zero; indices[n] = n; n++;
            vertices[n] = Irregular(b); uv[n] = Vector2.right; indices[n] = n; n++;
            vertices[n] = Irregular(c); uv[n] = Vector2.up; indices[n] = n; n++;
        }
    }

    static Vector3 Irregular(Vector3 ponto)
    {
        Vector3 p = ponto.normalized;
        return Vector3.Scale(p, new Vector3(0.57f, 0.62f, 0.55f)) *
            (0.98f + Mathf.Sin(Vector3.Dot(p, new Vector3(8f, 19f, 12f))) * 0.13f);
    }
}
