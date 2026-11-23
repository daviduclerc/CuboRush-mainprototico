using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cenário da arte de referência: asteroides escuros com contraluz laranja do buraco negro
/// e cristais luminosos flutuando dos dois lados da pista. Tudo é só decoração (sem colisão)
/// e é reciclado para a frente conforme o jogador avança.
/// Lá na frente eles estão presos pela atração do buraco negro (AtracaoDoBuracoNegro):
/// são puxados em espiral em direção a ele, esticados e engolidos além do limite de visão.
/// </summary>
public class CampoDeAsteroides : MonoBehaviour
{
    public Transform jogador;
    [Tooltip("Metade da largura da pista; nada é criado dentro dela.")]
    public float meiaLarguraPista = 5f;
    public int quantidade = 80;
    [Range(0f, 1f)] public float proporcaoCristais = 0.22f;
    [Tooltip("Distância extra a partir da borda da pista (mínima e máxima).")]
    public Vector2 distanciaLateral = new Vector2(4.5f, 42f);
    public Vector2 altura = new Vector2(-9f, 16f);
    [Tooltip("Até onde à frente os corpos existem. Passando do limite de visão, só ficam escondidos.")]
    public float profundidade = 95f;
    public Vector2 escala = new Vector2(0.45f, 3.2f);

    // Mesmas cores dos cristais da referência: vermelho, roxo, laranja e rosa.
    static readonly Color[] coresBorda =
    {
        new Color(1.4f, 0.22f, 0.32f), new Color(0.85f, 0.38f, 1.5f),
        new Color(1.5f, 0.72f, 0.18f), new Color(1.5f, 0.32f, 0.9f)
    };
    static readonly Color[] coresInternas =
    {
        new Color(0.55f, 0.03f, 0.1f), new Color(0.26f, 0.05f, 0.5f),
        new Color(0.6f, 0.2f, 0.02f), new Color(0.5f, 0.04f, 0.32f)
    };

    sealed class Corpo
    {
        /// <summary>Não gira: recebe o puxão e o esticamento na direção do buraco negro.</summary>
        public Transform pivo;
        /// <summary>A malha, que gira devagar dentro do pivô.</summary>
        public Transform t;
        public Renderer render;
        public Vector3 posicaoBase;
        public Vector3 escalaBase;
        /// <summary>Rotação da malha no mundo, independente da orientação do pivô.</summary>
        public Quaternion rotacao;
        public Vector3 eixoGiro;
        public float velGiro;
        public bool cristal;
    }

    readonly List<Corpo> corpos = new List<Corpo>();
    readonly List<Mesh> malhasCriadas = new List<Mesh>();
    readonly List<Material> materiaisCriados = new List<Material>();

    void Start()
    {
        var rochas = new Mesh[6];
        for (int i = 0; i < rochas.Length; i++) malhasCriadas.Add(rochas[i] = MalhasEstelares.CriarRocha(300 + i * 17));
        var cristais = new Mesh[4];
        for (int i = 0; i < cristais.Length; i++) malhasCriadas.Add(cristais[i] = MalhasEstelares.CriarCristal(900 + i * 41));

        Material matRocha = NovoMaterial("Space/RochaEspacial");
        var matCristais = new Material[coresBorda.Length];
        for (int i = 0; i < matCristais.Length; i++)
        {
            matCristais[i] = NovoMaterial("Space/CristalEspacial");
            if (matCristais[i] == null) continue;
            matCristais[i].SetColor("_Cor", coresBorda[i]);
            matCristais[i].SetColor("_CorInterna", coresInternas[i]);
        }
        if (matRocha == null)
        {
            Debug.LogWarning("CampoDeAsteroides: shader Space/RochaEspacial não encontrado.");
            return;
        }

        float zBase = jogador != null ? jogador.position.z : 0f;
        for (int i = 0; i < quantidade; i++)
        {
            bool cristal = Random.value < proporcaoCristais && matCristais[0] != null;
            var pivo = new GameObject(cristal ? "CristalFlutuante" : "Asteroide").transform;
            pivo.SetParent(transform, false);
            var go = new GameObject("Malha");
            go.transform.SetParent(pivo, false);
            go.AddComponent<MeshFilter>().sharedMesh = cristal
                ? cristais[Random.Range(0, cristais.Length)]
                : rochas[Random.Range(0, rochas.Length)];
            var render = go.AddComponent<MeshRenderer>();
            render.sharedMaterial = cristal ? matCristais[Random.Range(0, matCristais.Length)] : matRocha;
            render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            render.receiveShadows = false;

            var corpo = new Corpo
            {
                pivo = pivo,
                t = go.transform,
                render = render,
                eixoGiro = Random.onUnitSphere,
                // Giro lento: girando rápido, as facetas pegavam luz e piscavam.
                velGiro = Random.Range(3f, cristal ? 9f : 12f),
                cristal = cristal
            };
            corpo.rotacao = Random.rotation;
            Posicionar(corpo, zBase + Random.Range(-10f, profundidade));
            corpos.Add(corpo);
        }
    }

    Material NovoMaterial(string caminho)
    {
        Shader shader = Resources.Load<Shader>(caminho);
        if (shader == null) return null;
        var m = new Material(shader);
        materiaisCriados.Add(m);
        return m;
    }

    void Posicionar(Corpo c, float z)
    {
        float lado = Random.value < 0.5f ? -1f : 1f;
        // A maioria fica perto da pista; poucos, bem longe.
        float extra = Mathf.Lerp(distanciaLateral.x, distanciaLateral.y, Random.value * Random.value);
        float x = lado * (meiaLarguraPista + extra);
        float y = Random.Range(altura.x, altura.y);
        // Perto da pista ficam mais baixos, para não tapar a visão da câmera.
        if (extra < 10f) y = Mathf.Min(y, Random.Range(-6f, 3f));
        c.posicaoBase = new Vector3(x, y, z);
        float s = Mathf.Lerp(escala.x, escala.y, Random.value * Random.value) * (1f + extra / distanciaLateral.y);
        if (c.cristal) s *= 1.4f;
        c.escalaBase = c.cristal ? new Vector3(s * 0.8f, s * 1.3f, s * 0.8f) : Vector3.one * s;
        c.t.localScale = c.escalaBase;
        Atualizar(c, jogador != null ? jogador.position : Vector3.zero);
    }

    /// <summary>Sem a atração ligada: os corpos reciclados crescem aos poucos em vez de surgirem de uma vez.</summary>
    float Surgimento(float distanciaAFrente)
    {
        float t = Mathf.InverseLerp(profundidade + 15f, profundidade - 45f, distanciaAFrente);
        return t * t * (3f - 2f * t);
    }

    void Atualizar(Corpo c, Vector3 posJogador)
    {
        AtracaoDoBuracoNegro.Efeito e = AtracaoDoBuracoNegro.Calcular(c.posicaoBase, posJogador);
        float tamanho = AtracaoDoBuracoNegro.Ativa ? e.tamanho : Surgimento(c.posicaoBase.z - posJogador.z);

        // Puxado em direção ao buraco negro e girando em espiral em volta dele, como num disco de acreção.
        Vector3 p = c.posicaoBase + e.deslocamento;
        p = e.pontoDoBuraco + Quaternion.AngleAxis(e.giro, Vector3.forward) * (p - e.pontoDoBuraco);
        c.pivo.position = p;

        // Esticado na direção do buraco negro (espaguetificação) e mais fino nas outras.
        Vector3 paraBuraco = e.pontoDoBuraco - p;
        if (paraBuraco.sqrMagnitude > 0.0001f)
            c.pivo.rotation = Quaternion.LookRotation(paraBuraco, Vector3.forward);
        c.pivo.localScale = new Vector3(e.afinar, e.afinar, e.esticar) * Mathf.Max(tamanho, 0.01f);
        // A rocha mantém o próprio giro: virar o pivô não muda a orientação dela.
        c.t.rotation = c.rotacao;
        c.render.forceRenderingOff = e.oculto || tamanho < 0.01f;
    }

    void Update()
    {
        Vector3 posJogador = jogador != null ? jogador.position : Vector3.zero;
        float zRef = posJogador.z;
        float dt = Time.deltaTime;
        foreach (var c in corpos)
        {
            if (c.t == null) continue;
            c.rotacao = Quaternion.AngleAxis(c.velGiro * dt, c.eixoGiro) * c.rotacao;
            if (c.posicaoBase.z < zRef - 15f)
                Posicionar(c, zRef + profundidade + Random.Range(0f, 30f));
            else if (c.posicaoBase.z > zRef + profundidade + 60f)
                Posicionar(c, zRef + Random.Range(0f, profundidade));
            else
                Atualizar(c, posJogador);
        }
    }

    void OnDestroy()
    {
        foreach (var m in malhasCriadas) if (m != null) Destroy(m);
        foreach (var m in materiaisCriados) if (m != null) Destroy(m);
    }
}
