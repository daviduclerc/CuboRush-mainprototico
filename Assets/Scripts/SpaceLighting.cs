using UnityEngine;

/// <summary>Luzes de preenchimento da pista que acompanham o jogador.</summary>
public class SpaceLighting : MonoBehaviour
{
    public Transform alvo;
    public PlayerController jogador;

    Light luzQuente;
    Light luzFria;



    void Start()
    {
        luzQuente = CriarLuz("LuzDeAcrecao", new Color(1f, 0.64f, 0.39f), 4.0f, 28f);
        luzFria = CriarLuz("LuzDeEstrelas", new Color(0.34f, 0.62f, 1f), 2.4f, 20f);

        AtualizarCorDoSol();
        AtualizarPosicao();
    }

    Light CriarLuz(string nome, Color cor, float intensidade, float alcance)
    {
        GameObject objeto = new GameObject(nome);
        Light luz = objeto.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = cor;
        luz.intensity = intensidade;
        luz.range = alcance;
        luz.shadows = LightShadows.None;
        return luz;
    }

    void LateUpdate()
    {
        AtualizarCorDoSol();
        if (alvo != null) AtualizarPosicao();
    }

    void AtualizarCorDoSol()
    {
        if (jogador == null) return;

        Color corSolar = jogador.CorVisualAtual;
        float energia = GameManager.Instancia != null ? GameManager.Instancia.Estabilidade / 100f : 1f;
        luzQuente.color = corSolar;
        luzFria.color = corSolar;
        luzQuente.intensity = 5f * Mathf.SmoothStep(0.08f, 1f, energia);
        luzFria.intensity = 0f;
    }

    void AtualizarPosicao()
    {
        // A luz principal nasce junto ao Sol; a segunda espalha a mesma cor pela pista.
        luzQuente.transform.position = alvo.position + new Vector3(0f, 0.25f, 0.15f);
        luzFria.transform.position = alvo.position + new Vector3(-2f, 2f, 3f);
    }
}
