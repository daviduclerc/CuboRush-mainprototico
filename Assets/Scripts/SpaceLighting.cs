using UnityEngine;

/// <summary>Luz de preenchimento da pista que acompanha a fotosfera.</summary>
public class SpaceLighting : MonoBehaviour
{
    public Transform alvo;
    public PlayerController jogador;

    Light luzSolar;
    static readonly int PosicaoDoSol = Shader.PropertyToID("_TrackSunPosition");
    static readonly int CorDoSol = Shader.PropertyToID("_TrackSunColor");
    static readonly int BrilhoDoSol = Shader.PropertyToID("_TrackSunGlow");

    void Start()
    {
        GameObject objeto = new GameObject("LuzDoSol");
        objeto.transform.SetParent(transform, false);
        luzSolar = objeto.AddComponent<Light>();
        luzSolar.type = LightType.Point;
        luzSolar.range = 7f;
        luzSolar.shadows = LightShadows.None;
        luzSolar.enabled = false;
    }

    void LateUpdate()
    {
        if (jogador == null || luzSolar == null) return;
        Transform sol = jogador.VisualSolar != null ? jogador.VisualSolar : alvo;
        if (sol == null) return;

        bool colapso = GameManager.Instancia?.Estado == EstadoJogo.FimDeJogo;
        Color corSolar = jogador.CorAtual;
        float energia = Mathf.Clamp01(jogador.CorVisualAtual.maxColorComponent);
        Vector3 centro = sol.position;

        // Durante o deslize a fotosfera afunda. Mantém uma luz de preenchimento
        // acima do piso, alinhada ao personagem, para evitar cortes no brilho.
        centro.y = Mathf.Max(0.28f, centro.y);
        luzSolar.transform.position = centro;
        luzSolar.color = corSolar;
        luzSolar.intensity = 2.2f * energia;
        luzSolar.enabled = !colapso;
        Shader.SetGlobalVector(PosicaoDoSol, centro);
        Shader.SetGlobalColor(CorDoSol, corSolar);
        Shader.SetGlobalFloat(BrilhoDoSol, colapso ? 0f : energia);
    }

    void OnDisable()
    {
        if (luzSolar != null) luzSolar.enabled = false;
        Shader.SetGlobalFloat(BrilhoDoSol, 0f);
    }
}
