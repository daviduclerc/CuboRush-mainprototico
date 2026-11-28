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
    Color corExplosao;

    public static void Criar(Vector3 centro, Color cor, float tamanho)
    {
        var objeto = new GameObject("ExplosaoSolar");
        objeto.transform.position = centro;
        objeto.AddComponent<SolarDeathExplosion>().Iniciar(cor, Mathf.Max(0.6f, tamanho));
    }

    void Iniciar(Color cor, float tamanho)
    {
        escala = tamanho;
        // Mantém a cor que o jogador escolheu; o núcleo branco preserva o impacto.
        corExplosao = cor;
        CriarParticulas();
        CriarOndaDeChoque();

        clarão = gameObject.AddComponent<Light>();
        clarão.type = LightType.Point;
        clarão.color = Color.Lerp(Color.white, corExplosao, 0.72f);
        clarão.intensity = 24f * escala;
        clarão.range = 16f * escala;
        clarão.shadows = LightShadows.None;
    }

    void CriarParticulas()
    {
        plasma = gameObject.AddComponent<ParticleSystem>();
        var main = plasma.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1.45f;
        main.maxParticles = 260;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.gravityModifier = -0.04f;
        main.startSpeed = 0f;

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
                new GradientColorKey(Color.Lerp(corExplosao, Color.white, 0.25f), 0.16f),
                new GradientColorKey(corExplosao, 0.52f),
                new GradientColorKey(Color.Lerp(corExplosao, Color.black, 0.62f), 1f)
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
            if (materialPlasma.HasProperty("_Tint")) materialPlasma.SetColor("_Tint", corExplosao * 3.6f);
            if (materialPlasma.HasProperty("_BaseColor")) materialPlasma.SetColor("_BaseColor", corExplosao * 2.5f);
            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = materialPlasma;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.58f;
            renderer.lengthScale = 2.5f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // Um anel de plasma largo e rápido deixa a ruptura legível mesmo à distância.
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
        plasma.Play();
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
        float raio = Mathf.Lerp(0.25f, 5.8f * escala, Mathf.SmoothStep(0f, 1f, progresso));
        for (int i = 0; i < ondaChoque.positionCount; i++)
        {
            float angulo = i * Mathf.PI * 2f / ondaChoque.positionCount;
            ondaChoque.SetPosition(i, new Vector3(Mathf.Cos(angulo) * raio, Mathf.Sin(angulo) * raio, 0f));
        }
        float alpha = Mathf.Clamp01(1f - progresso);
        Color cor = Color.Lerp(corExplosao, Color.white, 0.42f);
        cor.a = alpha;
        ondaChoque.startColor = cor;
        ondaChoque.endColor = new Color(corExplosao.r, corExplosao.g, corExplosao.b, alpha * 0.55f);
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
            clarão.intensity = Mathf.Lerp(24f * escala, 0f, Mathf.Clamp01(idade / 0.8f));
            clarão.range = Mathf.Lerp(16f * escala, 0f, Mathf.Clamp01(idade / 1.3f));
        }
        if (idade >= duracao) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (materialPlasma != null) Destroy(materialPlasma);
        if (materialOnda != null) Destroy(materialOnda);
    }
}
