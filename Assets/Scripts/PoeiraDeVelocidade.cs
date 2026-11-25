using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Poeira estelar que surge à frente da câmera, num anel em volta da pista,
/// e fica para trás em riscos de luz. Quanto maior a velocidade, mais poeira e
/// mais longos os riscos. O centro da tela fica livre para não atrapalhar a leitura
/// dos obstáculos.
/// </summary>
[RequireComponent(typeof(Camera))]
public sealed class PoeiraDeVelocidade : MonoBehaviour
{
    [Tooltip("Partículas por segundo na velocidade inicial e na máxima.")]
    public Vector2 taxa = new Vector2(30f, 140f);
    [Tooltip("Raio interno e externo do anel de poeira em volta do eixo da câmera.")]
    public Vector2 raioAnel = new Vector2(7f, 16f);
    [Tooltip("Distância à frente da câmera onde a poeira aparece.")]
    public Vector2 distancia = new Vector2(35f, 70f);

    ParticleSystem sistema;
    Material material;
    float acumulado;

    static readonly Color[] Cores =
    {
        new Color(0.78f, 0.88f, 1f), new Color(0.78f, 0.88f, 1f), new Color(0.95f, 0.95f, 1f),
        new Color(1f, 0.38f, 0.82f), new Color(1f, 0.72f, 0.42f)
    };

    void Start()
    {
        Shader shader = Resources.Load<Shader>("Space/PoeiraVelocidade");
        if (shader == null) { enabled = false; return; }
        material = new Material(shader) { name = "Poeira de velocidade" };

        var objeto = new GameObject("PoeiraDeVelocidade");
        objeto.transform.SetParent(transform, false);
        sistema = objeto.AddComponent<ParticleSystem>();
        sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var principal = sistema.main;
        principal.loop = true;
        principal.playOnAwake = false;
        principal.simulationSpace = ParticleSystemSimulationSpace.World;
        principal.startSpeed = 0f;
        principal.startLifetime = 4f;
        principal.startSize = 0.1f;
        principal.maxParticles = 1200;

        var emissao = sistema.emission;
        emissao.enabled = false; // emitimos manualmente para controlar onde nasce cada grão
        var forma = sistema.shape;
        forma.enabled = false;

        // Surge suave e some suave no fim da vida.
        var corNoTempo = sistema.colorOverLifetime;
        corNoTempo.enabled = true;
        var gradiente = new Gradient();
        gradiente.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
        corNoTempo.color = new ParticleSystem.MinMaxGradient(gradiente);

        var render = objeto.GetComponent<ParticleSystemRenderer>();
        render.renderMode = ParticleSystemRenderMode.Stretch;
        render.velocityScale = 0f;
        render.cameraVelocityScale = 0.12f;
        render.lengthScale = 2f;
        render.sharedMaterial = material;
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = false;
        render.lightProbeUsage = LightProbeUsage.Off;
        render.reflectionProbeUsage = ReflectionProbeUsage.Off;

        sistema.Play();
    }

    void Update()
    {
        if (sistema == null) return;
        GameManager jogo = GameManager.Instancia;
        if (jogo == null || jogo.Estado != EstadoJogo.Jogando || Time.deltaTime <= 0f) return;

        float velocidade = Mathf.Max(5f, jogo.Velocidade);
        acumulado += Mathf.Lerp(taxa.x, taxa.y, jogo.Dificuldade) * Time.deltaTime;
        Vector3 origem = transform.position;
        while (acumulado >= 1f)
        {
            acumulado -= 1f;
            float angulo = Random.value * Mathf.PI * 2f;
            float raio = Mathf.Lerp(raioAnel.x, raioAnel.y, Mathf.Sqrt(Random.value));
            float frente = Random.Range(distancia.x, distancia.y);
            // Anel achatado na vertical: mais poeira dos lados do que em cima e embaixo.
            Vector3 posicao = origem + new Vector3(Mathf.Cos(angulo) * raio * 1.35f,
                Mathf.Sin(angulo) * raio * 0.75f, frente);

            Color cor = Cores[Random.Range(0, Cores.Length)];
            cor.a = Random.Range(0.35f, 0.9f);
            var parametros = new ParticleSystem.EmitParams
            {
                position = posicao,
                velocity = Vector3.zero,
                startSize = Random.Range(0.08f, 0.2f),
                // Vive o tempo de a câmera passar por ela, com folga.
                startLifetime = (frente + 6f) / velocidade,
                startColor = cor,
                applyShapeToPosition = false
            };
            sistema.Emit(parametros, 1);
        }
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
