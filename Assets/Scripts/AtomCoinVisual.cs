using UnityEngine;
using UnityEngine.Rendering;

public sealed class AtomCoinVisual : MonoBehaviour
{
    static readonly Material[] materiais = new Material[4];
    static readonly Material[] faiscas = new Material[4];
    Transform destino;
    Vector3 origemColeta;
    float idadeColeta;
    float fase;
    int corId;
    bool coletando;
    float elevacaoVisual;
    const float TamanhoMoeda = 1.16f;

    public static GameObject Criar(Transform pai, Vector3 posicao, int cor)
    {
        GameObject raiz = new GameObject("MoedaAtomo" + cor);
        raiz.transform.SetParent(pai, false);
        raiz.transform.localPosition = posicao;
        GameObject icone = GameObject.CreatePrimitive(PrimitiveType.Quad);
        icone.name = "AtomoFlutuante";
        Destroy(icone.GetComponent<Collider>());
        icone.transform.SetParent(raiz.transform, false);
        icone.transform.localScale = Vector3.one * TamanhoMoeda;
        if (materiais[cor] == null)
        {
            Material mat = new Material(Resources.Load<Shader>("Gameplay/AtomCoin"));
            mat.mainTexture = Resources.Load<Texture2D>("Gameplay/AtomCoin" + cor);
            materiais[cor] = mat;
        }
        Renderer renderer = icone.GetComponent<Renderer>();
        renderer.sharedMaterial = materiais[cor];
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        AtomCoinVisual efeito = icone.AddComponent<AtomCoinVisual>();
        efeito.corId = cor;
        efeito.fase = posicao.z * 0.35f;
        efeito.elevacaoVisual = Mathf.Max(0f, 0.67f - posicao.y);
        return raiz;
    }

    public void Coletar(Transform alvo)
    {
        if (coletando) return;
        coletando = true;
        destino = alvo;
        origemColeta = transform.position;
        transform.SetParent(null, true);
    }

    void Update()
    {
        if (coletando)
        {
            idadeColeta += Time.deltaTime;
            float t = Mathf.Clamp01(idadeColeta / 0.20f);
            Vector3 alvo = destino != null ? destino.position : origemColeta;
            transform.position = Vector3.Lerp(origemColeta, alvo, t * t);
            transform.localScale = Vector3.one * (TamanhoMoeda * (1f - t));
            if (t >= 1f) { EmitirFaiscas(alvo); Destroy(gameObject); }
            return;
        }
        if (GameManager.Instancia == null || GameManager.Instancia.Estado != EstadoJogo.Jogando) return;
        fase += Time.deltaTime;
        transform.localPosition = new Vector3(0f, elevacaoVisual + Mathf.Sin(fase * 2.3f) * 0.055f, 0f);
        // Mantém a face voltada à câmera, com um balanço lento sem ficar de perfil.
        Quaternion frente = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;
        transform.rotation = frente * Quaternion.Euler(0f, Mathf.Sin(fase * 0.9f) * 24f, Mathf.Sin(fase * 1.1f) * 4f);
    }

    void EmitirFaiscas(Vector3 centro)
    {
        var objeto = new GameObject("EnergiaDoAtomoColetado");
        objeto.transform.position = centro;
        var sistema = objeto.AddComponent<ParticleSystem>();
        sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = sistema.main;
        main.loop = false;
        main.startLifetime = 0.28f;
        main.startSize = 0.07f;
        main.startSpeed = 0f;
        main.maxParticles = 12;
        var emissao = sistema.emission; emissao.enabled = false;
        if (faiscas[corId] == null)
        {
            faiscas[corId] = new Material(Resources.Load<Shader>("Sol3D/Labaredas"));
            faiscas[corId].SetColor("_Tint", PlayerController.Cores[corId] * 2f);
        }
        sistema.GetComponent<ParticleSystemRenderer>().sharedMaterial = faiscas[corId];
        var tamanho = sistema.sizeOverLifetime;
        tamanho.enabled = true;
        tamanho.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI * 2f / 10f;
            sistema.Emit(new ParticleSystem.EmitParams {
                velocity = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 1.3f,
                startColor = Color.white
            }, 1);
        }
        sistema.Play();
        Destroy(objeto, 0.6f);
    }
}
