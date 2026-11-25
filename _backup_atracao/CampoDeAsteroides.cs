using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cenário da arte de referência: asteroides escuros com contraluz laranja do buraco negro
/// e cristais luminosos flutuando dos dois lados da pista. Tudo é só decoração (sem colisão)
/// e é reciclado para a frente conforme o jogador avança.
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
    public float profundidade = 170f;
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
        public Transform t;
        public Vector3 escalaBase;
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
            var go = new GameObject(cristal ? "CristalFlutuante" : "Asteroide");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = cristal
                ? cristais[Random.Range(0, cristais.Length)]
                : rochas[Random.Range(0, rochas.Length)];
            var render = go.AddComponent<MeshRenderer>();
            render.sharedMaterial = cristal ? matCristais[Random.Range(0, matCristais.Length)] : matRocha;
            render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            render.receiveShadows = false;

            var corpo = new Corpo
            {
                t = go.transform,
                eixoGiro = Random.onUnitSphere,
                // Giro lento: girando rápido, as facetas pegavam luz e piscavam.
                velGiro = Random.Range(3f, cristal ? 9f : 12f),
                cristal = cristal
            };
            corpo.t.rotation = Random.rotation;
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
        c.t.position = new Vector3(x, y, z);
        float s = Mathf.Lerp(escala.x, escala.y, Random.value * Random.value) * (1f + extra / distanciaLateral.y);
        if (c.cristal) s *= 1.4f;
        c.escalaBase = c.cristal ? new Vector3(s * 0.8f, s * 1.3f, s * 0.8f) : Vector3.one * s;
        c.t.localScale = c.escalaBase * Surgimento(z - (jogador != null ? jogador.position.z : 0f));
    }

    /// <summary>
    /// Os corpos reciclados nascem lá na frente; em vez de aparecerem de uma vez (um "flash"),
    /// crescem aos poucos enquanto se aproximam.
    /// </summary>
    float Surgimento(float distanciaAFrente)
    {
        float t = Mathf.InverseLerp(profundidade + 15f, profundidade - 45f, distanciaAFrente);
        return t * t * (3f - 2f * t);
    }

    void Update()
    {
        float zRef = jogador != null ? jogador.position.z : 0f;
        float dt = Time.deltaTime;
        foreach (var c in corpos)
        {
            if (c.t == null) continue;
            c.t.Rotate(c.eixoGiro, c.velGiro * dt, Space.World);
            c.t.localScale = c.escalaBase * Surgimento(c.t.position.z - zRef);
            if (c.t.position.z < zRef - 15f)
                Posicionar(c, zRef + profundidade + Random.Range(0f, 30f));
            else if (c.t.position.z > zRef + profundidade + 60f)
                Posicionar(c, zRef + Random.Range(0f, profundidade));
        }
    }

    void OnDestroy()
    {
        foreach (var m in malhasCriadas) if (m != null) Destroy(m);
        foreach (var m in materiaisCriados) if (m != null) Destroy(m);
    }
}
