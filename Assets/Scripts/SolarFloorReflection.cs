using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Reflexo luminoso do Sol projetado no metal, com intensidade ligada à estabilidade.</summary>
public sealed class SolarFloorReflection : MonoBehaviour
{
    public PlayerController jogador;
    Transform reflexo;
    Material material;
    Renderer render;
    float energiaSuavizada = 1f;

    void Start()
    {
        GameObject plano = GameObject.CreatePrimitive(PrimitiveType.Quad);
        plano.name = "ReflexoSolarNoMetal";
        Destroy(plano.GetComponent<Collider>());
        reflexo = plano.transform;
        reflexo.SetParent(transform, false);
        reflexo.rotation = Quaternion.Euler(90f, 0f, 0f);
        material = new Material(Resources.Load<Shader>("Sol3D/FloorReflection"));
        material.mainTexture = Resources.Load<Texture2D>("Sol3D/Fotosfera");
        render = plano.GetComponent<Renderer>();
        render.sharedMaterial = material;
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = false;
    }

    void LateUpdate()
    {
        if (jogador == null || reflexo == null) return;
        Transform sol = jogador.VisualSolar != null ? jogador.VisualSolar : jogador.transform;
        GameManager jogo = GameManager.Instancia;
        float energia = jogo != null ? Mathf.Clamp01(jogo.Estabilidade / 100f) : 1f;
        energiaSuavizada = Mathf.Lerp(energiaSuavizada, energia, 1f - Mathf.Exp(-5f * Time.deltaTime));
        bool morto = jogo != null && jogo.Estado == EstadoJogo.FimDeJogo;
        render.enabled = !morto;
        reflexo.position = new Vector3(sol.position.x, 0.045f, sol.position.z - 1.15f);
        float tamanho = 1f;
        reflexo.localScale = new Vector3(2.5f * tamanho, 5.0f * tamanho, 1f);
        float proximidade = 1f / (1f + Mathf.Max(0f, sol.position.y - 0.45f) * 0.85f);
        Color cor = jogador.CorVisualAtual;
        cor.a = Mathf.SmoothStep(0.02f, 0.95f, energiaSuavizada) * proximidade;
        // O brilho é um plano reto: some enquanto o piso está curvado pelo poço gravitacional.
        cor.a *= 1f - PocoGravitacional.Intensidade;
        material.SetColor("_Tint", cor);
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
