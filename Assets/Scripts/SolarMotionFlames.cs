using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Plasma nas esquivas, anel que expande e recolhe no pulo e clarão no pouso.</summary>
public sealed class SolarMotionFlames : MonoBehaviour
{
    PlayerController jogador;
    Transform sol;
    Transform origem;
    ParticleSystem chamas, pouso, labaredaTraseira, rastroPulo;
    Transform origemRastroPulo;
    Vector3 centroSolarAnterior;
    float emissaoPulo;
    float afundamentoAnterior;
    float emissaoTraseira;
    float reforcoTraseiro;
    Material material;
    Material materialLateral;
    Material materialTraseiro;
    float ultimoSentidoLateral;
    bool caudaLateralAtiva;
    Transform anelPulo;
    Material materialAnel;
    Mesh malhaAnel;
    float tempoAnel, duracaoAnel, raioAnel;
    bool anelAtivo;
    Vector3 posicaoAnterior;
    bool estavaNoChao;
    float acumuladorLateral;
    float escalaInicial;
    float raioCorpo;
    float volumeInicial = 1f;

    EstadoJogo estadoAnterior = EstadoJogo.Menu;
    // Aleatoriedade própria para o efeito não alterar a geração dos obstáculos.
    readonly System.Random aleatorio = new System.Random(417);

    void Start()
    {
        jogador = GetComponent<PlayerController>();
        sol = jogador != null ? jogador.VisualSolar : null;
        Shader shader = Resources.Load<Shader>("Sol3D/Labaredas");
        if (jogador == null || sol == null || shader == null)
        {
            enabled = false;
            return;
        }
        escalaInicial = Mathf.Max(0.01f, sol.localScale.x);
        // Usa a superfície real: a escala da raiz sozinha não é o raio da esfera.
        raioCorpo = escalaInicial;
        Transform superficie = sol.Find("Fotosfera");
        MeshFilter filtro = superficie != null ? superficie.GetComponent<MeshFilter>() : null;
        if (filtro != null && filtro.sharedMesh != null)
            raioCorpo = filtro.sharedMesh.bounds.extents.z * Mathf.Abs(superficie.localScale.z * sol.localScale.z);
        Vector3 escalaBase = sol.parent != null ? sol.parent.lossyScale : Vector3.one;
        volumeInicial = Mathf.Max(0.001f, Mathf.Abs(escalaBase.x * escalaBase.y * escalaBase.z));
        material = new Material(shader) { name = "Plasma do movimento solar" };
        origem = new GameObject("LabaredasDoSol").transform;
        chamas = CriarSistema("Labaredas", true, 128);
        materialLateral = new Material(material) { name = "Plasma lateral com limite direcional" };
        var renderLateral = chamas.GetComponent<ParticleSystemRenderer>();
        renderLateral.sharedMaterial = materialLateral;
        renderLateral.lengthScale = 1.8f;
        pouso = CriarSistema("ClaraoDoPouso", false, 48);
        labaredaTraseira = CriarSistema("LabaredaTraseiraDeTransformacao", true, 100);
        afundamentoAnterior = jogador.Afundamento;
        origemRastroPulo = new GameObject("ArcoDePlasmaDoPulo").transform;
        origemRastroPulo.position = new Vector3(0f, 0f, transform.position.z);
        rastroPulo = CriarSistema("LabaredaCurvaDoPulo", false, 160);
        rastroPulo.transform.SetParent(origemRastroPulo, false);
        materialTraseiro = new Material(material) { name = "Cauda atras da superficie solar" };
        materialTraseiro.SetFloat("_RearOnly", 1f);
        labaredaTraseira.GetComponent<ParticleSystemRenderer>().sharedMaterial = materialTraseiro;
        rastroPulo.GetComponent<ParticleSystemRenderer>().sharedMaterial = materialTraseiro;
        centroSolarAnterior = sol.position;
        CriarAnelDoPulo();
        posicaoAnterior = transform.position;
        estavaNoChao = jogador.NoChao;
    }

    ParticleSystem CriarSistema(string nome, bool alongado, int limite)
    {
        var objeto = new GameObject(nome);
        objeto.transform.SetParent(origem, false);
        var sistema = objeto.AddComponent<ParticleSystem>();
        sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var principal = sistema.main;
        principal.playOnAwake = false;
        principal.loop = true;
        principal.maxParticles = limite;
        // A origem acompanha apenas o avanço da pista, nunca o giro nem o pulo.
        principal.simulationSpace = ParticleSystemSimulationSpace.Local;
        principal.scalingMode = ParticleSystemScalingMode.Hierarchy;
        principal.gravityModifier = 0f;
        principal.startSpeed = 0f;
        principal.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emissao = sistema.emission;
        emissao.enabled = false;
        var forma = sistema.shape;
        forma.enabled = false;
        var tamanho = sistema.sizeOverLifetime;
        tamanho.enabled = true;
        tamanho.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.18f, 1f), new Keyframe(1f, 0f)));
        var cor = sistema.colorOverLifetime;
        cor.enabled = true;
        var gradiente = new Gradient();
        gradiente.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0.55f, 0.2f), new GradientAlphaKey(0f, 1f) });
        cor.color = gradiente;
        var ruido = sistema.noise;
        ruido.enabled = true;
        ruido.strength = 0.12f;
        ruido.frequency = 2.5f;
        ruido.scrollSpeed = 0.7f;
        var render = sistema.GetComponent<ParticleSystemRenderer>();
        render.sharedMaterial = material;
        render.renderMode = alongado ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        // Rastro alongado, mantendo o aspecto macio da primeira versão.
        render.velocityScale = 0.18f;
        render.lengthScale = 1.55f;
        render.sortMode = ParticleSystemSortMode.Distance;
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = false;
        return sistema;
    }

    void LateUpdate()
    {
        if (origem == null) return;
        var jogo = GameManager.Instancia;
        var estado = jogo != null ? jogo.Estado : EstadoJogo.Menu;
        origem.position = sol.position;
        Vector3 escalaCorpo = sol.parent != null ? sol.parent.lossyScale : Vector3.one;
        float volume = Mathf.Abs(escalaCorpo.x * escalaCorpo.y * escalaCorpo.z);
        // A escala vem do corpo sem rotação; girar não muda o tamanho das chamas.
        float proporcao = Mathf.Max(0.1f, Mathf.Pow(volume / volumeInicial, 1f / 3f));
        origem.localScale = Vector3.one * proporcao;
        origemRastroPulo.position = new Vector3(0f, 0f, transform.position.z);
        material.SetColor("_Tint", jogador.CorVisualAtual * 3.5f);
        materialTraseiro.SetColor("_Tint", jogador.CorVisualAtual * 3.5f);
        materialTraseiro.SetVector("_SourceCenter", sol.position);
        Vector3 raios = new Vector3(Mathf.Abs(escalaCorpo.x), Mathf.Abs(escalaCorpo.y), Mathf.Abs(escalaCorpo.z)) * raioCorpo;
        materialTraseiro.SetVector("_SolarMaskRadii", raios * 0.98f);
        materialLateral.SetVector("_SolarMaskRadii", raios * 0.98f);
        materialTraseiro.SetFloat("_RearClipDistance", raios.z * 0.85f);
        materialLateral.SetColor("_Tint", jogador.CorVisualAtual * 3.5f);
        materialLateral.SetVector("_SourceCenter", sol.position);
        materialLateral.SetFloat("_CutDistance", raioCorpo * proporcao * 0.80f);
        if (estado != EstadoJogo.Jogando)
        {
            if (estado == EstadoJogo.Pausado)
            {
                chamas.Pause();
                pouso.Pause();
                labaredaTraseira.Pause();
                rastroPulo.Pause();
            }
            else
            {
                chamas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                pouso.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                labaredaTraseira.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                rastroPulo.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                OcultarAnel();
            }
            centroSolarAnterior = sol.position;
            emissaoPulo = 0f;
            afundamentoAnterior = jogador.Afundamento;
            emissaoTraseira = reforcoTraseiro = 0f;
            caudaLateralAtiva = false;
            acumuladorLateral = 0f;
            posicaoAnterior = transform.position;
            estavaNoChao = jogador.NoChao;
            estadoAnterior = estado;
            return;
        }
        if (estadoAnterior != EstadoJogo.Jogando)
        {
            chamas.Play();
            pouso.Play();
            labaredaTraseira.Play();
            rastroPulo.Play();
        }
        estadoAnterior = estado;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        float velocidadeX = (transform.position.x - posicaoAnterior.x) / dt;
        Vector3 centro = origem.InverseTransformPoint(sol.position);
        float raio = escalaInicial;
        // Durante a esquiva, a chama lateral original substitui a traseira.
        bool movimentoLateral = Mathf.Abs(velocidadeX) > 0.15f;
        if (movimentoLateral && !caudaLateralAtiva) labaredaTraseira.Clear();
        caudaLateralAtiva = movimentoLateral;
        AtualizarLabaredaTraseira(centro, raio, dt);
        AtualizarRastroPulo(dt, proporcao);
        if (Mathf.Abs(velocidadeX) > 0.1f)
        {
            float sentido = -Mathf.Sign(velocidadeX);
            // Elimina a cauda anterior ao inverter a esquiva para não cruzar o Sol.
            if (ultimoSentidoLateral != 0f && sentido != ultimoSentidoLateral)
            {
                chamas.Clear();
                acumuladorLateral = 0f;
            }
            ultimoSentidoLateral = sentido;
            materialLateral.SetFloat("_LateralSide", sentido);
            acumuladorLateral += Mathf.Min(95f, Mathf.Abs(velocidadeX) * 8f) * dt;
            int quantidade = Mathf.Min(24, Mathf.FloorToInt(acumuladorLateral));
            acumuladorLateral -= quantidade;
            for (int i = 0; i < quantidade; i++)
                Emitir(chamas, centro + new Vector3(sentido * raio * 0.7f, Variar(-0.12f, 0.12f), Variar(-0.07f, 0.07f)),
                    new Vector3(sentido * Mathf.Lerp(2.5f, 6f, Mathf.Clamp01(Mathf.Abs(velocidadeX) / 8f)), Variar(-0.25f, 0.45f), Variar(-0.5f, 0f)), 0.32f, Mathf.Min(0.56f, escalaInicial * 6.5f / 6f));
        }
        else acumuladorLateral = 0f;

        if (estavaNoChao && !jogador.NoChao)
        {
            tempoAnel = 0f;
            duracaoAnel = Mathf.Max(0.2f, jogador.TempoNoAr * 0.95f);
            raioAnel = raio;
            anelAtivo = anelPulo != null;
            if (anelAtivo) anelPulo.gameObject.SetActive(true);
        }
        AtualizarAnel(centro, dt);
        if (!estavaNoChao && jogador.NoChao)
        {
            // Pulso radial curto acima do chão, para não atravessar a pista.
            for (int i = 0; i < 26; i++)
            {
                float angulo = i * Mathf.PI * 2f / 26f;
                Vector3 direcao = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));
                Emitir(pouso, origem.InverseTransformPoint(new Vector3(sol.position.x, 0.12f, sol.position.z)) + direcao * 0.15f,
                    direcao * Variar(1.4f, 2.6f) + Vector3.up * 0.15f, 0.25f, 0.26f);
            }
        }
        estavaNoChao = jogador.NoChao;
        posicaoAnterior = transform.position;
    }

    void AtualizarRastroPulo(float dt, float proporcao)
    {
        bool transformando = reforcoTraseiro > 0f || jogador.Deslizando;
        if (!jogador.NoChao || !estavaNoChao || transformando)
        {
            // Amostra posições reais para que a chama desenhe a subida e a queda,
            // sem mover as partículas antigas para cima junto com o personagem.
            emissaoPulo += dt * 90f;
            int quantidade = Mathf.Min(32, Mathf.FloorToInt(emissaoPulo));
            emissaoPulo -= quantidade;
            for (int i = 0; i < quantidade; i++)
            {
                float t = (i + 1f) / Mathf.Max(1, quantidade);
                Vector3 ponto = Vector3.Lerp(centroSolarAnterior, sol.position, t);
                ponto += new Vector3(Variar(-0.06f, 0.06f), 0.02f, -raioCorpo * Mathf.Abs(sol.parent.lossyScale.z) * 1.05f);
                float tamanhoRastro = (jogador.NoChao ? 0.50f : 0.60f) * proporcao;
                ponto.y = Mathf.Max(ponto.y, tamanhoRastro * 0.5f + 0.035f);
                float comprimentoMaximo = escalaInicial * proporcao * (jogador.NoChao ? 9f : 10.5f);
                float duracaoRastro = Mathf.Clamp(comprimentoMaximo / Mathf.Max(1f, GameManager.Instancia.Velocidade + 0.45f), 0.05f, 0.58f);
                Emitir(rastroPulo, origemRastroPulo.InverseTransformPoint(ponto),
                    new Vector3(Variar(-0.12f, 0.12f), Variar(-0.05f, 0.15f), -GameManager.Instancia.Velocidade - 0.45f),
                    tamanhoRastro, duracaoRastro);
            }
        }
        else emissaoPulo = 0f;
        centroSolarAnterior = sol.position;
    }

    void AtualizarLabaredaTraseira(Vector3 centro, float raio, float dt)
    {
        float afundamentoAtual = jogador.Afundamento;
        float variacao = Mathf.Abs(afundamentoAtual - afundamentoAnterior) / dt;
        afundamentoAnterior = afundamentoAtual;
        // Tanto afundar quanto emergir lançam plasma para trás da corrida.
        if (variacao > 0.04f) reforcoTraseiro = 0.18f;
        else reforcoTraseiro = Mathf.Max(0f, reforcoTraseiro - dt);
        // O aumento vale para a chama constante (intensidade zero).
        // Os tamanhos do arco de pulo e dos efeitos de contração são independentes.
        float intensidade = Mathf.Clamp01(reforcoTraseiro / 0.18f);
        intensidade = Mathf.Max(intensidade, jogador.Deslizando ? 1f : (!jogador.NoChao ? 0.75f : 0f));
        if (caudaLateralAtiva || !jogador.NoChao) { emissaoTraseira = 0f; return; }
        emissaoTraseira += dt * Mathf.Lerp(90f, 120f, intensidade);
        int quantidade = Mathf.Min(24, Mathf.FloorToInt(emissaoTraseira));
        emissaoTraseira -= quantidade;
        for (int i = 0; i < quantidade; i++)
        {
            Vector3 baseChama = centro + new Vector3(Variar(-0.08f, 0.08f),
                Variar(0.02f, 0.08f), -raioCorpo * 0.96f);
            Vector3 velocidade = new Vector3(Variar(-0.12f, 0.12f),
                Variar(0f, 0.16f), -Mathf.Lerp(5.472f, 5.8f, intensidade));
            Emitir(labaredaTraseira, baseChama, velocidade,
                Mathf.Lerp(0.72f, 0.60f, intensidade), Mathf.Lerp(0.36f, 0.48f, intensidade));
        }
        // A origem e as partículas usam a escala atual do Sol, sem herdar o giro.
    }

    void CriarAnelDoPulo()
    {
        Shader shader = Resources.Load<Shader>("Sol3D/AnelDoPulo");
        if (shader == null) return;
        materialAnel = new Material(shader) { name = "Cinturao solar do pulo" };
        anelPulo = new GameObject("AnelDoPulo").transform;
        anelPulo.SetParent(origem, false);
        // Inclinação constante: o cinturão acompanha o centro, sem capotar com o Sol.
        anelPulo.localRotation = Quaternion.Euler(-22f, 0f, -8f);
        const int segmentos = 128;
        var vertices = new Vector3[(segmentos + 1) * 2];
        var uv = new Vector2[vertices.Length];
        var triangulos = new int[segmentos * 6];
        for (int i = 0; i <= segmentos; i++)
        {
            float volta = i / (float)segmentos;
            float angulo = volta * Mathf.PI * 2f;
            Vector3 direcao = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));
            vertices[i * 2] = direcao * 0.66f;
            vertices[i * 2 + 1] = direcao;
            uv[i * 2] = new Vector2(0f, volta);
            uv[i * 2 + 1] = new Vector2(1f, volta);
            if (i == segmentos) continue;
            int v = i * 2, t = i * 6;
            triangulos[t] = v;
            triangulos[t + 1] = v + 2;
            triangulos[t + 2] = v + 1;
            triangulos[t + 3] = v + 1;
            triangulos[t + 4] = v + 2;
            triangulos[t + 5] = v + 3;
        }
        malhaAnel = new Mesh { name = "Cinturao solar" };
        malhaAnel.vertices = vertices;
        malhaAnel.uv = uv;
        malhaAnel.triangles = triangulos;
        malhaAnel.RecalculateBounds();
        anelPulo.gameObject.AddComponent<MeshFilter>().sharedMesh = malhaAnel;
        var render = anelPulo.gameObject.AddComponent<MeshRenderer>();
        render.sharedMaterial = materialAnel;
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = false;
        anelPulo.gameObject.SetActive(false);
    }

    void AtualizarAnel(Vector3 centro, float dt)
    {
        if (!anelAtivo) return;
        tempoAnel += dt;
        // Uma queda antecipada recolhe o anel rapidamente, em vez de cortá-lo.
        if (jogador.NoChao) tempoAnel = Mathf.Max(tempoAnel, duracaoAnel * 0.80f);
        float t = Mathf.Clamp01(tempoAnel / duracaoAnel);
        float expansao = t < 0.42f ? Mathf.SmoothStep(0f, 1f, t / 0.42f)
            : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.42f) / 0.58f);
        anelPulo.localPosition = centro;
        anelPulo.localScale = Vector3.one * raioAnel * Mathf.Lerp(0.04f, 2.4f, expansao);
        // Branco frio com leve tom ciano: continua contrastando com todas as cores do Sol.
        materialAnel.SetColor("_Tint", new Color(0.42f, 0.92f, 1f) * 3.5f);
        materialAnel.SetFloat("_Opacity", Mathf.SmoothStep(0f, 1f, expansao * 3f));
        if (t >= 1f) OcultarAnel();
    }

    void OcultarAnel()
    {
        anelAtivo = false;
        if (anelPulo != null) anelPulo.gameObject.SetActive(false);
    }

    void Emitir(ParticleSystem sistema, Vector3 posicao, Vector3 velocidade, float tamanho, float vida)
    {
        var particula = new ParticleSystem.EmitParams
        {
            position = posicao,
            velocity = velocidade,
            startSize = tamanho * Variar(0.65f, 1.25f),
            startLifetime = vida * Variar(0.8f, 1.15f),
            startColor = Color.white
        };
        sistema.Emit(particula, 1);
    }

    float Variar(float minimo, float maximo) => Mathf.Lerp(minimo, maximo, (float)aleatorio.NextDouble());

    void OnDisable()
    {
        OcultarAnel();
        if (chamas != null) chamas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (pouso != null) pouso.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (labaredaTraseira != null) labaredaTraseira.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (rastroPulo != null) rastroPulo.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        estadoAnterior = EstadoJogo.Menu;
    }

    void OnDestroy()
    {
        if (origem != null) Destroy(origem.gameObject);
        if (origemRastroPulo != null) Destroy(origemRastroPulo.gameObject);
        if (material != null) Destroy(material);
        if (materialLateral != null) Destroy(materialLateral);
        if (materialTraseiro != null) Destroy(materialTraseiro);
        if (materialAnel != null) Destroy(materialAnel);
        if (malhaAnel != null) Destroy(malhaAnel);
    }
}








