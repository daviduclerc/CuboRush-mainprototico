using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Anima a morte do Sol antes de liberar a tela de resultado.</summary>
[DefaultExecutionOrder(400)]
public sealed class SunDeathSequence : MonoBehaviour
{
    [Header("Referências opcionais")]
    [SerializeField] PlayerController jogador;
    [SerializeField] Transform nucleoSolar;
    [SerializeField] CameraFollow cameraDaPista;
    [SerializeField] Light luzDirecional;
    [SerializeField] Volume volumeDoBloom;
    [SerializeField] ParticleSystem particulasDeEnergia;

    [Header("Expansão, implosão e supernova")]
    [SerializeField, Min(0.1f)] float segundosDeExpansao = 1f;
    [SerializeField, Min(0.1f)] float segundosDeImplosao = 0.62f;
    [SerializeField, Min(1.9f)] float segundosAposExplosao = 2.2f;
    [SerializeField, Range(1f, 2f)] float escalaMaxima = 1.28f;
    [SerializeField, Min(0f)] float forcaDoImpacto = 1.5f;
    [SerializeField, Min(0.6f)] float tamanhoSupernova = 1.55f;
    [SerializeField] AnimationCurve curvaExpansao = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] AnimationCurve curvaImplosao = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] AnimationCurve curvaEscurecimento = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public bool EmExecucao { get; private set; }
    public float Escurecimento { get; private set; }

    static readonly int PulsoPista = Shader.PropertyToID("_SolarDeathTrackPulse");
    static readonly int PistaEscura = Shader.PropertyToID("_SolarDeathDarkness");
    static readonly int Tint = Shader.PropertyToID("_Tint");
    static readonly int Exposicao = Shader.PropertyToID("_Exposure");
    static readonly Color AzulFrio = new Color(0.36f, 0.52f, 0.82f);
    static readonly Color Laranja = new Color(1f, 0.28f, 0.055f);

    Material ceu, materialParticulas;
    Color ambienteOriginal, corCeu, corDirecional, corDoSol;
    float intensidadeDirecional, exposicaoOriginal, brilhoOriginal;
    bool temCorCeu, temExposicao, explodiu;
    Bloom bloom;
    Light luzSolar;
    Vector3 escalaOriginal, posicaoOriginal, centro;
    Renderer[] partesDoNucleo;
    Material[][] materiaisDoNucleo;
    MaterialPropertyBlock propriedades;
    float inicio, inicioExplosao, proximaEmissao;

    /// <summary>Dispara a sequência de derrota em uma partida ativa.</summary>
    public void TriggerSunDeath()
    {
        GameManager.Instancia?.FimDeJogo();
    }

    internal void Iniciar()
    {
        if (EmExecucao) return;
        if (jogador == null) jogador = FindAnyObjectByType<PlayerController>();
        if (nucleoSolar == null && jogador != null) nucleoSolar = jogador.VisualSolar;
        if (nucleoSolar == null && jogador != null) nucleoSolar = jogador.transform;
        if (nucleoSolar == null) return;

        jogador?.PrepararVisualDaMorte();
        escalaOriginal = nucleoSolar.localScale;
        posicaoOriginal = nucleoSolar.localPosition;
        centro = nucleoSolar.position;
        corDoSol = jogador != null ? jogador.CorAtual : Laranja;
        partesDoNucleo = nucleoSolar.GetComponentsInChildren<Renderer>();
        materiaisDoNucleo = new Material[partesDoNucleo.Length][];
        for (int i = 0; i < partesDoNucleo.Length; i++)
            materiaisDoNucleo[i] = partesDoNucleo[i].sharedMaterials;
        propriedades = new MaterialPropertyBlock();
        PrepararIluminacao();
        PrepararParticulas();

        inicio = Time.unscaledTime;
        proximaEmissao = 0f;
        EmExecucao = true;
    }

    void PrepararIluminacao()
    {
        if (cameraDaPista == null && Camera.main != null)
            cameraDaPista = Camera.main.GetComponent<CameraFollow>();
        if (luzDirecional == null) luzDirecional = RenderSettings.sun;
        if (luzDirecional == null)
            foreach (Light luz in FindObjectsByType<Light>())
                if (luz.type == LightType.Directional)
                {
                    luzDirecional = luz;
                    break;
                }
        if (luzDirecional != null)
        {
            corDirecional = luzDirecional.color;
            intensidadeDirecional = luzDirecional.intensity;
        }

        ambienteOriginal = RenderSettings.ambientLight;
        ceu = RenderSettings.skybox;
        temCorCeu = ceu != null && ceu.HasProperty(Tint);
        temExposicao = ceu != null && ceu.HasProperty(Exposicao);
        if (temCorCeu) corCeu = ceu.GetColor(Tint);
        if (temExposicao) exposicaoOriginal = ceu.GetFloat(Exposicao);

        if (volumeDoBloom == null)
        {
            // O efeito deve alterar o volume de Bloom com maior prioridade na cena.
            float prioridade = float.NegativeInfinity;
            foreach (Volume volume in FindObjectsByType<Volume>())
            {
                if (!volume.isGlobal || volume.weight <= 0f || volume.priority <= prioridade) continue;
                VolumeProfile perfil = volume.HasInstantiatedProfile() ? volume.profile : volume.sharedProfile;
                if (perfil == null || !perfil.Has<Bloom>()) continue;
                volumeDoBloom = volume;
                prioridade = volume.priority;
            }
        }
        if (volumeDoBloom != null && volumeDoBloom.profile.TryGet(out bloom))
            brilhoOriginal = bloom.intensity.value;

        var objeto = new GameObject("Luz da instabilidade solar");
        objeto.transform.SetParent(transform, false);
        objeto.transform.position = centro;
        luzSolar = objeto.AddComponent<Light>();
        luzSolar.type = LightType.Point;
        luzSolar.color = Color.Lerp(corDoSol, Laranja, 0.5f);
        luzSolar.range = 9f;
        luzSolar.intensity = 0f;
    }

    void PrepararParticulas()
    {
        if (particulasDeEnergia != null) return;
        var objeto = new GameObject("Faíscas do colapso solar");
        objeto.transform.SetParent(transform, false);
        objeto.transform.position = centro;
        particulasDeEnergia = objeto.AddComponent<ParticleSystem>();
        var main = particulasDeEnergia.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = true;
        main.maxParticles = 280;
        main.playOnAwake = false;
        var emissao = particulasDeEnergia.emission;
        emissao.enabled = false;
        var size = particulasDeEnergia.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        Shader shader = Resources.Load<Shader>("Sol3D/Labaredas");
        if (shader == null) return;
        materialParticulas = new Material(shader);
        materialParticulas.SetColor(Tint, Color.Lerp(corDoSol, Laranja, 0.35f) * 3.2f);
        objeto.GetComponent<ParticleSystemRenderer>().sharedMaterial = materialParticulas;
    }

    void LateUpdate()
    {
        if (!EmExecucao) return;

        float agora = Time.unscaledTime - inicio;
        float fimDaImplosao = segundosDeExpansao + segundosDeImplosao;
        if (agora < segundosDeExpansao)
        {
            AtualizarNucleo(agora, segundosDeExpansao, false);
            return;
        }
        if (agora < fimDaImplosao)
        {
            AtualizarNucleo(agora, fimDaImplosao, true);
            return;
        }

        if (!explodiu) Explodir();
        float progresso = Mathf.Clamp01((Time.unscaledTime - inicioExplosao) / segundosAposExplosao);
        AtualizarAmbiente(progresso);
        if (progresso >= 1f) EmExecucao = false;
    }

    void AtualizarNucleo(float tempo, float fimDaEtapa, bool implosao)
    {
        float progresso = implosao
            ? Mathf.InverseLerp(segundosDeExpansao, fimDaEtapa, tempo)
            : Mathf.Clamp01(tempo / fimDaEtapa);
        float t = Mathf.Clamp01((implosao ? curvaImplosao : curvaExpansao).Evaluate(progresso));
        float pulso = (1f + Mathf.Sin(tempo * (22f + 22f * tempo))) * 0.5f;
        float escala = implosao ? Mathf.Lerp(escalaMaxima, 0.025f, t) :
            Mathf.Lerp(1f, escalaMaxima, t) + pulso * 0.08f * t;
        nucleoSolar.localScale = escalaOriginal * escala;
        float amplitude = implosao ? Mathf.Lerp(0.13f, 0.03f, progresso) : 0.13f * progresso;
        nucleoSolar.localPosition = posicaoOriginal +
            new Vector3(Mathf.Sin(tempo * 71f), Mathf.Sin(tempo * 59f), 0f) * amplitude;

        float brilho = implosao ? Mathf.Lerp(1.5f, 3.4f, t) : Mathf.Lerp(1f, 1.7f, t);
        Color corNucleo = Color.Lerp(corDoSol, Color.white, implosao ? t * 0.80f : progresso * 0.25f);
        AjustarBrilhoDoNucleo(corNucleo * brilho * (1f + pulso * amplitude));
        luzSolar.intensity = brilho * (1.4f + pulso * 1.8f);
        if (bloom != null) bloom.intensity.value = brilhoOriginal * (1f + progresso * 0.25f + pulso * 0.10f);
        Shader.SetGlobalFloat(PulsoPista, progresso * (0.35f + pulso * 0.18f));

        if (cameraDaPista != null)
            cameraDaPista.AplicarTremor(0.19f, implosao ? Mathf.Lerp(0.52f, 0.88f, t) :
                Mathf.Lerp(0.15f, 0.54f, progresso));
        if (tempo < proximaEmissao) return;
        proximaEmissao = tempo + 0.035f;
        EmitirFaiscas(implosao, Mathf.Min(implosao ? 0.24f : 0.44f, fimDaEtapa - tempo));
    }

    void AjustarBrilhoDoNucleo(Color cor)
    {
        for (int i = 0; i < partesDoNucleo.Length; i++)
        {
            Renderer parte = partesDoNucleo[i];
            if (parte == null) continue;
            Material[] materiais = materiaisDoNucleo[i];
            for (int j = 0; j < materiais.Length; j++)
            {
                Material material = materiais[j];
                if (material == null) continue;
                parte.GetPropertyBlock(propriedades, j);
                if (material.HasProperty(Tint)) propriedades.SetColor(Tint, cor);
                if (material.HasProperty("_GlowColor")) propriedades.SetColor("_GlowColor", cor * 1.35f);
                parte.SetPropertyBlock(propriedades, j);
            }
        }
    }

    void EmitirFaiscas(bool implosao, float tempoDeVida)
    {
        float vida = Mathf.Max(0.025f, tempoDeVida);
        var configuracao = particulasDeEnergia.main;
        Transform espaco = configuracao.simulationSpace switch
        {
            ParticleSystemSimulationSpace.Local => particulasDeEnergia.transform,
            ParticleSystemSimulationSpace.Custom => configuracao.customSimulationSpace,
            _ => null
        };
        for (int i = 0; i < 5; i++)
        {
            Vector3 direcao = Random.onUnitSphere;
            float raio = implosao ? Random.Range(0.85f, 2.4f) : Random.Range(0.35f, 0.72f);
            Vector3 posicao = centro + direcao * raio;
            Vector3 velocidade = implosao ? -direcao * (raio / vida) : direcao * Random.Range(1.4f, 3.8f);
            var faisca = new ParticleSystem.EmitParams
            {
                position = espaco != null ? espaco.InverseTransformPoint(posicao) : posicao,
                velocity = espaco != null ? espaco.InverseTransformVector(velocidade) : velocidade,
                startLifetime = vida,
                startSize = Random.Range(0.055f, 0.15f),
                startColor = Color.Lerp(corDoSol, Color.white, Random.value)
            };
            particulasDeEnergia.Emit(faisca, 1);
        }
    }

    void Explodir()
    {
        explodiu = true;
        inicioExplosao = Time.unscaledTime;
        if (jogador != null) jogador.EsconderVisualDaMorte();
        if (nucleoSolar != transform && (jogador == null || !nucleoSolar.IsChildOf(jogador.transform)))
            nucleoSolar.gameObject.SetActive(false);
        particulasDeEnergia.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        luzSolar.enabled = false;
        Shader.SetGlobalFloat(PulsoPista, 1f);
        SolarDeathExplosion.Criar(centro, corDoSol, tamanhoSupernova);
        GameManager.Instancia?.SomDeImpacto();
        if (cameraDaPista != null) cameraDaPista.AplicarTremor(0.58f, forcaDoImpacto);
    }

    void AtualizarAmbiente(float progresso)
    {
        Escurecimento = Mathf.Clamp01(curvaEscurecimento.Evaluate(progresso));
        float escuro = Escurecimento;
        Shader.SetGlobalFloat(PistaEscura, escuro);
        Shader.SetGlobalFloat(PulsoPista, 1f - escuro);
        RenderSettings.ambientLight = Color.Lerp(ambienteOriginal, AzulFrio * 0.12f, escuro);
        if (temCorCeu) ceu.SetColor(Tint, Color.Lerp(corCeu, AzulFrio, escuro * 0.55f));
        if (temExposicao) ceu.SetFloat(Exposicao, exposicaoOriginal * Mathf.Lerp(1f, 0.38f, escuro));
        if (luzDirecional != null)
        {
            luzDirecional.color = Color.Lerp(corDirecional, AzulFrio, escuro);
            luzDirecional.intensity = intensidadeDirecional * Mathf.Lerp(1f, 0.14f, escuro);
        }
        if (bloom != null) bloom.intensity.value = brilhoOriginal * Mathf.Lerp(1.75f, 0.24f, escuro);
    }

    void OnDestroy()
    {
        Shader.SetGlobalFloat(PulsoPista, 0f);
        Shader.SetGlobalFloat(PistaEscura, 0f);
        if (materialParticulas != null) Destroy(materialParticulas);
        if (luzSolar != null) Destroy(luzSolar.gameObject);
    }
}
