using UnityEngine;

/// <summary>
/// Limite de visão "gravitacional". Em vez de um corte seco lá na frente, os objetos distantes
/// estão presos pela atração do buraco negro: perto do limite ficam esticados em direção a ele
/// (espaguetificação), finos, encolhidos e puxados para o ponto da tela onde ele está; além do
/// limite foram engolidos e não são desenhados. Conforme o jogador se aproxima, eles se soltam
/// da atração e voltam à forma e ao lugar certos.
/// Só a aparência muda: a lógica e as colisões continuam usando a posição real.
/// Usado pelo ObstacleSpawner (itens da pista) e pelo CampoDeAsteroides (cenário).
/// </summary>
public class AtracaoDoBuracoNegro : MonoBehaviour
{
    [Tooltip("Distância à frente do jogador a partir da qual nada é desenhado (engolido pelo buraco negro).")]
    public float limiteDeVisao = 80f;
    [Tooltip("Mais perto que isso, os objetos já estão livres da atração, com forma e posição normais.")]
    public float distanciaLivre = 42f;
    [Tooltip("Quanto, no limite, os objetos são puxados em direção ao buraco negro (0 = nada, 1 = até a linha dele).")]
    [Range(0f, 1f)] public float puxao = 0.6f;
    [Tooltip("Quanto os objetos esticam em direção ao buraco negro no limite.")]
    [Range(1f, 5f)] public float estiramento = 2.6f;
    [Tooltip("Espessura no limite (1 = não afina).")]
    [Range(0.1f, 1f)] public float afinamento = 0.4f;
    [Tooltip("Giro em espiral (graus) dos asteroides sendo puxados, como num disco de acreção.")]
    public float giroEspiral = 75f;

    /// <summary>Como um objeto aparece neste quadro.</summary>
    public struct Efeito
    {
        /// <summary>Além do limite: não deve ser desenhado.</summary>
        public bool oculto;
        /// <summary>0 = engolido, 1 = livre.</summary>
        public float liberdade;
        /// <summary>Encolhimento geral (0 a 1).</summary>
        public float tamanho;
        /// <summary>Fator ao longo da direção do buraco negro.</summary>
        public float esticar;
        /// <summary>Fator nas outras direções.</summary>
        public float afinar;
        /// <summary>Puxão em direção ao buraco negro (só em x e y).</summary>
        public Vector3 deslocamento;
        /// <summary>Ponto da linha do buraco negro na mesma profundidade do objeto.</summary>
        public Vector3 pontoDoBuraco;
        /// <summary>Giro em espiral, em graus.</summary>
        public float giro;

        public static Efeito Neutro(Vector3 posicao) => new Efeito
        {
            liberdade = 1f, tamanho = 1f, esticar = 1f, afinar = 1f, pontoDoBuraco = posicao
        };
    }

    static AtracaoDoBuracoNegro instancia;
    CameraFollow cameraSeguir;
    BlackHoleBackdrop buraco;
    bool procurouReferencias;

    void Awake() => instancia = this;
    void OnDestroy() { if (instancia == this) instancia = null; }

    public static bool Ativa => instancia != null && instancia.isActiveAndEnabled;

    /// <summary>0 no limite de visão (ou além), 1 a partir da distância livre.</summary>
    public static float Liberdade(float distanciaAFrente)
    {
        if (!Ativa) return 1f;
        AtracaoDoBuracoNegro a = instancia;
        float livre = Mathf.Min(a.distanciaLivre, a.limiteDeVisao - 0.5f);
        float t = Mathf.InverseLerp(a.limiteDeVisao, livre, distanciaAFrente);
        return t * t * (3f - 2f * t);
    }

    public static Efeito Calcular(Vector3 posicao, Vector3 posicaoJogador)
    {
        if (!Ativa) return Efeito.Neutro(posicao);
        AtracaoDoBuracoNegro a = instancia;

        float f = Liberdade(posicao.z - posicaoJogador.z);
        float preso = 1f - f;
        float preso2 = preso * preso;
        Vector3 ponto = a.PontoDoBuraco(posicao.z, posicaoJogador);

        float crescer = Mathf.Clamp01(f / 0.5f);
        return new Efeito
        {
            oculto = f <= 0.001f,
            liberdade = f,
            // Já tem o tamanho normal na metade do caminho; o puxão e o esticamento soltam depois.
            tamanho = crescer * crescer * (3f - 2f * crescer),
            esticar = 1f + (a.estiramento - 1f) * preso2,
            afinar = Mathf.Lerp(1f, a.afinamento, preso2),
            deslocamento = (ponto - posicao) * (a.puxao * preso2),
            pontoDoBuraco = ponto,
            giro = a.giroEspiral * preso2
        };
    }

    /// <summary>
    /// Ponto da linha câmera → buraco negro na profundidade z. O buraco negro é um fundo
    /// "infinitamente" distante, então o que importa é a direção dele vista da câmera.
    /// A conta usa a posição que a câmera terá neste quadro (não depende da ordem dos scripts).
    /// </summary>
    Vector3 PontoDoBuraco(float z, Vector3 posicaoJogador)
    {
        if (!procurouReferencias)
        {
            procurouReferencias = true;
            cameraSeguir = FindAnyObjectByType<CameraFollow>();
            buraco = FindAnyObjectByType<BlackHoleBackdrop>();
        }
        Vector3 camera = cameraSeguir != null ? cameraSeguir.PosicaoDaCorrida(posicaoJogador.z) :
            new Vector3(0f, 4.2f, posicaoJogador.z - 6f);
        float elevacao = buraco != null ? buraco.elevacao : 5f;

        float y = camera.y + Mathf.Tan(elevacao * Mathf.Deg2Rad) * Mathf.Max(0f, z - camera.z);
        return new Vector3(camera.x, y, z);
    }
}
