using UnityEngine;

/// <summary>
/// Envia para os shaders de espelho e vidro (Resources/Space/ReflexoDoAmbiente.hlsl)
/// o céu panorâmico e o desenho da pista que eles refletem.
/// </summary>
public static class ReflexoDoAmbiente
{
    static bool ceuPronto;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() => ceuPronto = false;

    public static void Garantir()
    {
        if (ceuPronto) return;
        Material ceu = RenderSettings.skybox;
        Texture textura = ceu != null && ceu.HasProperty("_MainTex") ? ceu.GetTexture("_MainTex") : null;
        // Sem céu ainda: a exposição global fica 0 e o reflexo mostra só pista e Sol.
        if (textura == null) return;
        Shader.SetGlobalTexture("_CeuPanorama", textura);
        Shader.SetGlobalFloat("_CeuExposicao", ceu.HasProperty("_Exposure") ? ceu.GetFloat("_Exposure") : 1f);
        ceuPronto = true;
    }

    /// <summary>Posição das linhas neon refletidas (bordas das faixas).</summary>
    public static void DefinirPista(float meiaLargura, float larguraFaixa)
    {
        Shader.SetGlobalVector("_PistaFaixas", new Vector4(meiaLargura, larguraFaixa, 0f, 0f));
    }
}
