using UnityEngine;

public enum GatilhoDeMeteoros { Aleatorio, Tempo, Pontos }

[CreateAssetMenu(menuName = "Cubo Rush/Desafio dos Meteoros")]
public sealed class MeteorChallengeSettings : ScriptableObject
{
    [Header("Gatilho")]
    public bool ativado = true;
    public GatilhoDeMeteoros gatilho = GatilhoDeMeteoros.Aleatorio;
    public Vector2 intervaloAleatorio = new Vector2(18f, 24f);
    [Min(1f)] public float intervaloSegundos = 35f;
    [Min(1)] public int intervaloPontos = 650;

    [Header("Evento")]
    [Min(0.8f)] public float avisoGeral = 2.4f;
    [Min(1f)] public float duracaoChuva = 12f;
    [Min(1f)] public float intervaloOndas = 1.4f;
    [Min(1.2f)] public float avisoNoPiso = 1.65f;
    [Min(0.1f)] public float duracaoBrasa = 0.75f;
    [Range(1, 4)] public int meteorosPorOnda = 3;
    [Min(0f)] public float danoVida = 25f;
    [Range(0f, 0.65f)] public float intensidadeDoCeu = 0.28f;

    [Header("Apresentação")]
    public Color corAviso = new Color(1f, 0.29f, 0.08f);
    [Tooltip("Opcional. Prefab apenas do visual rochoso, centralizado, com diâmetro aproximado de 1,2.")]
    public GameObject visualMeteoro;
}
