using System.Collections.Generic;
using UnityEngine;

/// <summary>Modelo neon e volumes de colisão dos obstáculos fixos da pista.</summary>
public sealed class ObstaculoSolar : MonoBehaviour
{
    public const float AlturaPiramide = 0.72f;
    public const float VaoDaBarreira = 0.79f;
    public const float ProfundidadePiramide = 0.95f;
    public const float ProfundidadeBarreira = 0.48f;
    static readonly Color Laranja = new Color(1f, 0.36f, 0.045f);
    static readonly Color Vermelho = new Color(1f, 0.095f, 0.045f);
    static Mesh malhaPiramide;
    static Material materialFaiscas;

    readonly List<BoxCollider> volumes = new List<BoxCollider>(3);
    Renderer[] emissores;
    ParticleSystem faiscas;
    MaterialPropertyBlock propriedades;
    Color cor;
    Bounds envelope;
    float pulsoAte;
    bool passagemLimpa;
    public bool SomDeTravessia { get; private set; }
    public Bounds Limites => new Bounds(transform.position + envelope.center, envelope.size);

    public static ObstaculoSolar Criar(Transform pai, Vector3 posicao, float larguraFaixa, bool piramide, bool singelo)
    {
        var raiz = new GameObject(piramide ? "Pirâmide de salto" : "Barreira suspensa");
        raiz.transform.SetParent(pai, false);
        raiz.transform.localPosition = posicao;
        var obstaculo = raiz.AddComponent<ObstaculoSolar>();
        obstaculo.cor = piramide ? Laranja : Vermelho;
        obstaculo.SomDeTravessia = singelo;
        if (piramide) obstaculo.MontarPiramide(larguraFaixa * 0.85f);
        else obstaculo.MontarBarreira(larguraFaixa * 0.87f);
        if (singelo || !piramide) obstaculo.MontarFaixasDeAviso(larguraFaixa);
        obstaculo.CriarFaiscas(piramide ? AlturaPiramide : VaoDaBarreira);
        obstaculo.propriedades = new MaterialPropertyBlock();
        return obstaculo;
    }

    void MontarPiramide(float largura)
    {
        float y = AlturaPiramide, z = ProfundidadePiramide;
        if (malhaPiramide == null) malhaPiramide = CriarMalhaPiramide();
        var corpo = new GameObject("Liga metálica");
        corpo.transform.SetParent(transform, false);
        corpo.transform.localScale = new Vector3(largura, y, z);
        corpo.AddComponent<MeshFilter>().sharedMesh = malhaPiramide;
        corpo.AddComponent<MeshRenderer>().sharedMaterial = TechObstacleMaterials.Obter(false);

        Vector3[] basePiramide = Cantos(largura, z);
        var brilho = new List<Renderer>(8);
        for (int i = 0; i < basePiramide.Length; i++)
        {
            brilho.Add(Trilho("Aresta iluminada", basePiramide[i], Vector3.up * y, 0.038f));
            brilho.Add(Trilho("Base iluminada", basePiramide[i], basePiramide[(i + 1) % 4], 0.038f));
        }
        emissores = brilho.ToArray();

        // Cada faixa cabe dentro da pirâmide na parte mais estreita da faixa.
        float[] alturas = { 0f, 0.24f, 0.44f, 0.66f };
        float[] proporcoes = { 0.66f, 0.38f, 0.08f };
        for (int i = 0; i < proporcoes.Length; i++)
            AdicionarVolume((alturas[i] + alturas[i + 1]) * 0.5f,
                new Vector3(largura * proporcoes[i], alturas[i + 1] - alturas[i], z * proporcoes[i]));
    }

    void MontarBarreira(float largura)
    {
        const float topo = 2.33f;
        float altura = topo - VaoDaBarreira;
        float centro = VaoDaBarreira + altura * 0.5f;
        var chassi = Visual.Forma(PrimitiveType.Cube, "Painel suspenso", transform, Vector3.up * centro,
            new Vector3(largura, altura, ProfundidadeBarreira), cor, raio: 0f);
        chassi.GetComponent<Renderer>().sharedMaterial = TechObstacleMaterials.Obter(false);

        float frente = -ProfundidadeBarreira * 0.5f - 0.024f;
        float metade = largura * 0.5f;
        var a = new Vector3(-metade, VaoDaBarreira + 0.025f, frente);
        var b = new Vector3(metade, a.y, frente);
        emissores = new[] {
            Trilho("Feixe de limite inferior", a, b, 0.05f),
            Trilho("Luz da lateral esquerda", a, a + Vector3.up * (altura - 0.07f), 0.032f),
            Trilho("Luz da lateral direita", b, b + Vector3.up * (altura - 0.07f), 0.032f)
        };
        AdicionarVolume(centro, new Vector3(largura - 0.07f, altura, ProfundidadeBarreira - 0.05f));
    }

    void MontarFaixasDeAviso(float largura)
    {
        for (int i = -1; i <= 1; i++)
        {
            var aviso = Visual.Forma(PrimitiveType.Cube, "Marca de alerta no piso", transform,
                new Vector3(i * largura * 0.22f, 0.024f, -0.94f), new Vector3(0.12f, 0.01f, 0.38f),
                cor * 0.75f, brilhante: true, raio: 0f);
            aviso.transform.localRotation = Quaternion.Euler(0f, 32f, 0f);
        }
    }

    Renderer Trilho(string nome, Vector3 a, Vector3 b, float espessura)
    {
        var aresta = Visual.Forma(PrimitiveType.Cube, nome, transform, (a + b) * 0.5f,
            new Vector3(espessura, espessura, (a - b).magnitude), cor, brilhante: true, raio: 0f);
        aresta.transform.localRotation = Quaternion.LookRotation(b - a,
            Mathf.Abs((b - a).normalized.y) > 0.99f ? Vector3.forward : Vector3.up);
        return aresta.GetComponent<Renderer>();
    }

    void AdicionarVolume(float altura, Vector3 tamanho)
    {
        BoxCollider caixa = gameObject.AddComponent<BoxCollider>();
        caixa.center = Vector3.up * altura;
        caixa.size = tamanho;
        caixa.isTrigger = true;
        Bounds local = new Bounds(caixa.center, caixa.size);
        if (volumes.Count == 0) envelope = local;
        else envelope.Encapsulate(local);
        volumes.Add(caixa);
    }

    void CriarFaiscas(float altura)
    {
        var efeito = new GameObject("Faíscas de energia");
        efeito.transform.SetParent(transform, false);
        efeito.transform.localPosition = new Vector3(0f, altura, -0.42f);
        faiscas = efeito.AddComponent<ParticleSystem>();
        faiscas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var principal = faiscas.main;
        principal.playOnAwake = false;
        principal.loop = false;
        principal.duration = 0.35f;
        principal.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.29f);
        principal.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 2.1f);
        principal.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.095f);
        principal.maxParticles = 18;
        var emissao = faiscas.emission;
        emissao.enabled = false;
        var forma = faiscas.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = 0.12f;
        var render = efeito.GetComponent<ParticleSystemRenderer>();
        Shader shader = Resources.Load<Shader>("Space/ShootingStar");
        if (materialFaiscas == null && shader != null) materialFaiscas = new Material(shader);
        if (materialFaiscas != null) render.sharedMaterial = materialFaiscas;
        render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        render.receiveShadows = false;
    }

    public void Sinalizar(bool passou)
    {
        passagemLimpa = passou;
        pulsoAte = Time.time + 0.33f;
        if (!faiscas.isPlaying) faiscas.Play();
        faiscas.Emit(new ParticleSystem.EmitParams { startColor = passou ? new Color(1f, 0.77f, 0.3f) : Vermelho },
            passou ? 7 : 16);
    }

    void LateUpdate()
    {
        if (emissores == null) return;
        float pulso = Mathf.Clamp01((pulsoAte - Time.time) / 0.33f);
        float brilho = 1.5f + Mathf.Sin(Time.time * 5f + transform.position.z * 0.2f) * 0.35f;
        Color tom = Color.Lerp(cor, passagemLimpa ? Color.yellow : Color.red, pulso);
        propriedades.SetColor("_EmissionColor", tom * (brilho + pulso * 2f));
        foreach (Renderer emissor in emissores) emissor.SetPropertyBlock(propriedades);
    }

    public bool Intersecta(Bounds anterior, Bounds atual)
    {
        foreach (BoxCollider caixa in volumes)
        {
            var alvo = new Bounds(transform.position + caixa.center, caixa.size);
            if (TrackItem.CruzouCaixa(anterior, atual, alvo)) return true;
        }
        return false;
    }

    static Vector3[] Cantos(float largura, float profundidade)
    {
        float x = largura * 0.5f, z = profundidade * 0.5f;
        return new[] { new Vector3(-x, 0f, -z), new Vector3(x, 0f, -z),
            new Vector3(x, 0f, z), new Vector3(-x, 0f, z) };
    }

    static Mesh CriarMalhaPiramide()
    {
        var cantos = Cantos(1f, 1f);
        var vertices = new Vector3[12];
        var uv = new Vector2[12];
        var indices = new int[12];
        for (int i = 0; i < 4; i++)
        {
            int vertice = i * 3;
            vertices[vertice] = cantos[i];
            vertices[vertice + 1] = Vector3.up;
            vertices[vertice + 2] = cantos[(i + 1) % 4];
            uv[vertice + 1] = new Vector2(0.5f, 1f);
            uv[vertice + 2] = Vector2.right;
            for (int j = 0; j < 3; j++) indices[vertice + j] = vertice + j;
        }
        var mesh = new Mesh { name = "Pirâmide de liga estelar" };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = indices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
