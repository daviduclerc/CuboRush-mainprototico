using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Explosão visível de plasma quando o Sol é destruído.</summary>
public sealed class SolarDeathExplosion : MonoBehaviour
{
    ParticleSystem plasma;
    Material materialPlasma;
    Light clarão;
    LineRenderer ondaChoque;
    Material materialOnda;
    float idade;
    float duracao = 1.9f;
    float escala;
    static readonly Color CorExplosao = new Color(1f, 0.42f, 0.085f);

    public static void Criar(Vector3 centro, float tamanho)
    {
        var objeto = new GameObject("ExplosaoSolar");
        objeto.transform.position = centro;
        objeto.AddComponent<SolarDeathExplosion>().Iniciar(Mathf.Max(0.6f, tamanho));
    }

    void Iniciar(float tamanho)
    {
        escala = tamanho;
        CriarParticulas();
        CriarOndaDeChoque();

        clarão = gameObject.AddComponent<Light>();
        clarão.type = LightType.Point;
        clarão.color = new Color(1f, 0.58f, 0.2f);
        clarão.intensity = 12f * escala;
        clarão.range = 9f * escala;
        clarão.shadows = LightShadows.None;
    }

    void CriarParticulas()
    {
        plasma = gameObject.AddComponent<ParticleSystem>();
        plasma.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = plasma.main;
        main.playOnAwake = false;
        main.loop = false;
        main.useUnscaledTime = true;
        main.duration = 1.45f;
        main.maxParticles = 260;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.gravityModifier = -0.04f;
        main.startSpeed = 0f;

        var emissao = plasma.emission;
        emissao.enabled = false;
        var lifetime = plasma.sizeOverLifetime;
        lifetime.enabled = true;
        lifetime.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.16f, 1.15f), new Keyframe(1f, 0f)));
        var fade = plasma.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.Lerp(CorExplosao, Color.white, 0.25f), 0.16f),
                new GradientColorKey(CorExplosao, 0.52f),
                new GradientColorKey(new Color(0.38f, 0.10f, 0.035f), 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.94f, 0.48f),
                new GradientAlphaKey(0f, 1f)
            });
        fade.color = gradient;

        Shader shader = Resources.Load<Shader>("Sol3D/Labaredas");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader != null)
        {
            materialPlasma = new Material(shader) { name = "Plasma brilhante da explosao solar" };
            if (materialPlasma.HasProperty("_Tint")) materialPlasma.SetColor("_Tint", CorExplosao * 2.5f);
            if (materialPlasma.HasProperty("_BaseColor")) materialPlasma.SetColor("_BaseColor", CorExplosao * 1.8f);
            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = materialPlasma;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.58f;
            renderer.lengthScale = 2.5f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // Um anel de plasma largo e rápido deixa a ruptura legível mesmo à distância.
        plasma.Play();
        const int quantidade = 210;
        for (int i = 0; i < quantidade; i++)
        {
            Vector3 direcao = Random.onUnitSphere;
            float velocidade = Random.Range(3.8f, 9.2f) * escala;
            float vida = Random.Range(0.72f, 1.65f);
            var particula = new ParticleSystem.EmitParams
            {
                position = transform.position + direcao * Random.Range(0f, 0.18f) * escala,
                velocity = direcao * velocidade + Vector3.up * Random.Range(-0.6f, 1.1f),
                startSize = Random.Range(0.22f, 0.62f) * escala,
                startLifetime = vida,
                startColor = Color.white
            };
            plasma.Emit(particula, 1);
        }
    }

    void CriarOndaDeChoque()
    {
        var anel = new GameObject("OndaDeChoqueSolar");
        anel.transform.SetParent(transform, false);
        Camera camera = Camera.main;
        if (camera != null) anel.transform.rotation = camera.transform.rotation;
        ondaChoque = anel.AddComponent<LineRenderer>();
        ondaChoque.useWorldSpace = false;
        ondaChoque.loop = true;
        ondaChoque.positionCount = 96;
        ondaChoque.startWidth = 0.34f * escala;
        ondaChoque.endWidth = 0.05f * escala;
        ondaChoque.numCapVertices = 4;
        ondaChoque.shadowCastingMode = ShadowCastingMode.Off;
        ondaChoque.receiveShadows = false;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        materialOnda = new Material(shader) { name = "Plasma da onda de choque" };
        materialOnda.color = Color.white;
        if (materialOnda.HasProperty("_BaseColor")) materialOnda.SetColor("_BaseColor", Color.white);
        ondaChoque.material = materialOnda;
        AtualizarOnda(0f);
    }

    void AtualizarOnda(float progresso)
    {
        if (ondaChoque == null) return;
        ondaChoque.enabled = progresso < 1f;
        if (!ondaChoque.enabled) return;
        float raio = Mathf.Lerp(0.25f, 5.8f * escala, Mathf.SmoothStep(0f, 1f, progresso));
        for (int i = 0; i < ondaChoque.positionCount; i++)
        {
            float angulo = i * Mathf.PI * 2f / ondaChoque.positionCount;
            ondaChoque.SetPosition(i, new Vector3(Mathf.Cos(angulo) * raio, Mathf.Sin(angulo) * raio, 0f));
        }
        float alpha = Mathf.Clamp01(1f - progresso);
        Color cor = Color.Lerp(CorExplosao, Color.white, 0.42f);
        cor.a = alpha;
        ondaChoque.startColor = cor;
        ondaChoque.endColor = new Color(CorExplosao.r, CorExplosao.g, CorExplosao.b, alpha * 0.55f);
        float largura = Mathf.Lerp(0.46f, 0.025f, progresso) * escala;
        ondaChoque.startWidth = largura;
        ondaChoque.endWidth = largura * 0.28f;
        if (materialOnda != null)
        {
            if (materialOnda.HasProperty("_BaseColor")) materialOnda.SetColor("_BaseColor", cor);
            else materialOnda.color = cor;
        }
    }

    void Update()
    {
        idade += Time.unscaledDeltaTime;
        float progresso = Mathf.Clamp01(idade / 1.25f);
        AtualizarOnda(progresso);
        if (clarão != null)
        {
            clarão.intensity = Mathf.Lerp(12f * escala, 0f, Mathf.Clamp01(idade / 0.64f));
            clarão.range = Mathf.Lerp(9f * escala, 0f, Mathf.Clamp01(idade / 1.1f));
        }
        if (idade >= duracao) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (materialPlasma != null) Destroy(materialPlasma);
        if (materialOnda != null) Destroy(materialOnda);
    }
}
