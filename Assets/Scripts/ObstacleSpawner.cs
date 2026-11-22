using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cria obstáculos, moedas e power-ups na frente do jogador, apaga o que ficou
/// para trás e verifica as colisões com o cubo.
///
/// Tipos de obstáculo:
///  - Baixo  (rosa):    pule por cima ou desvie.
///  - Alto   (vermelho): só desviando.
///  - Barra  (roxo):    deslize por baixo ou desvie.
///  - Móvel  (branco):  anda de um lado para o outro da pista.
///  - Parede de cor:    barreira luminosa; só atravessa com o cubo da mesma cor.
///  - Projétil:         disparado pelas naves inimigas (ver Enemy).
/// Sempre existe um caminho possível em cada fileira.
/// </summary>
public class ObstacleSpawner : MonoBehaviour
{
    public PlayerController jogador;

    [Header("Geração")]
    [Tooltip("Até que distância à frente os objetos são criados.")]
    public float distanciaVisao = 130f;
    [Tooltip("Trecho livre no começo da partida.")]
    public float inicioLivre = 45f;
    [Tooltip("Tempo (s) entre fileiras no começo e na dificuldade máxima.")]
    public float tempoReacaoInicial = 1.5f;
    public float tempoReacaoFinal = 0.95f;

    [Header("Moedas e power-ups")]
    [Range(0f, 1f)] public float chanceMoedas = 0.65f;
    public float distanciaMinPowerUp = 180f;
    public float distanciaMaxPowerUp = 320f;
    public float raioIma = 8f;

    [Header("Paredes de cor")]
    [Tooltip("Chance de uma fileira ser uma parede de cor.")]
    [Range(0f, 1f)] public float chanceParedeDeCor = 0.14f;

    [Header("Inimigos")]
    [Tooltip("Segundos de jogo até o primeiro inimigo aparecer.")]
    public float primeiroInimigo = 15f;
    [Tooltip("Intervalo (s) entre o surgimento de um inimigo e o do próximo.")]
    public float intervaloInimigoMin = 16f;
    public float intervaloInimigoMax = 26f;
    [Tooltip("Velocidade do projétil vindo contra o jogador.")]
    public float velocidadeProjetil = 8f;

    Enemy inimigoAtual;
    int inimigosCriados;
    float proximoInimigo;
    float zInicial;

    enum Obst { Nenhum, Baixo, Alto, Barra }

    static readonly Color corBaixo = new Color(1f, 0.2f, 0.7f);
    static readonly Color corAlto = new Color(0.9f, 0.15f, 0.2f);
    static readonly Color corBarra = new Color(0.55f, 0.3f, 1f);
    static readonly Color corMovel = new Color(0.9f, 0.9f, 0.95f);
    static readonly Color corEscudo = new Color(0.3f, 0.8f, 1f);
    static readonly Color corIma = new Color(0.3f, 1f, 0.45f);
    static readonly Color corDobro = new Color(1f, 0.5f, 0.9f);

    static readonly string[] caminhosArtefatos =
    {
        "Gameplay/ArtifactFire", "Gameplay/ArtifactEarth", "Gameplay/ArtifactWater",
        "Gameplay/ArtifactAir", "Gameplay/ArtifactCosmos"
    };
    static Texture2D[] imagensArtefatos;
    static Material[] materiaisArtefatos;

    readonly List<TrackItem> itens = new List<TrackItem>();
    float proximoZ;
    EstadoJogo estadoAnterior = EstadoJogo.Menu;
    float proximoPowerUpZ;

    int Faixas => jogador.quantidadeFaixas;
    float MeiaPista => Faixas * jogador.larguraFaixa / 2f;

    void Start()
    {
        float z = jogador.transform.position.z;
        zInicial = z;
        proximoInimigo = primeiroInimigo;
        proximoZ = z + inicioLivre;
        proximoPowerUpZ = z + Random.Range(120f, 200f);
    }

    void Update()
    {
        GameManager jogo = GameManager.Instancia;
        if (jogo == null || jogador == null) return;

        bool retomando = estadoAnterior == EstadoJogo.Pausado && jogo.Estado == EstadoJogo.Jogando;
        estadoAnterior = jogo.Estado;
        if (retomando)
        {
            for (int i = 0; i < itens.Count; i++)
                if (itens[i] != null) PolyhedronVisual.RestaurarCorpo(itens[i].gameObject);
        }
        // A pausa não gera, remove nem move obstáculos; o primeiro quadro sem
        // deltaTime também aguarda antes de atualizar colisões e projéteis.
        if (jogo.Estado == EstadoJogo.Pausado || (jogo.Estado == EstadoJogo.Jogando && Time.deltaTime <= 0f)) return;
        float zJogador = jogador.transform.position.z;

        while (proximoZ < zJogador + distanciaVisao)
            proximoZ += CriarFileira(proximoZ, jogo);

        // De tempos em tempos chega uma nave inimiga (uma de cada vez).
        if (jogo.Estado == EstadoJogo.Jogando && inimigoAtual == null &&
            jogo.TempoDeJogo >= proximoInimigo)
        {
            // O primeiro é sempre o Atirador (mais simples); depois, qualquer tipo.
            TipoInimigo tipo = inimigosCriados == 0
                ? TipoInimigo.Atirador
                : (TipoInimigo)Random.Range(0, 4);
            inimigosCriados++;

            inimigoAtual = new GameObject("Inimigo" + tipo).AddComponent<Enemy>();
            inimigoAtual.Configurar(jogador, this, tipo);
            proximoInimigo = jogo.TempoDeJogo + Random.Range(intervaloInimigoMin, intervaloInimigoMax);
        }

        AtualizarItens(jogo, zJogador);
    }

    // ---------- Atualização e colisão ----------

    void AtualizarItens(GameManager jogo, float zJogador)
    {
        bool jogando = jogo.Estado == EstadoJogo.Jogando;
        Bounds caixaJogador = jogador.Limites;

        for (int i = itens.Count - 1; i >= 0; i--)
        {
            TrackItem item = itens[i];
            if (item == null) { itens.RemoveAt(i); continue; }

            Transform t = item.transform;

            // Ficou para trás: apaga.
            if (t.position.z < zJogador - 15f)
            {
                Remover(i);
                continue;
            }

            if (item.girar) t.Rotate(0f, 180f * Time.deltaTime, 0f, Space.World);

            if (!jogando) continue;

            // Obstáculo móvel: vai e volta entre as bordas da pista.
            if (item.velocidadeX != 0f)
            {
                Vector3 p = t.position;
                p.x += item.velocidadeX * Time.deltaTime;
                float limite = MeiaPista - item.tamanho.x / 2f;
                if (Mathf.Abs(p.x) > limite)
                {
                    p.x = Mathf.Sign(p.x) * limite;
                    item.velocidadeX = -item.velocidadeX;
                }
                t.position = p;
            }

            if (item.meteoroCaindo)
            {
                Vector3 p = t.position;
                p.y = Mathf.MoveTowards(p.y, item.alturaAlvoMeteoro,
                    item.velocidadeQuedaMeteoro * Time.deltaTime);
                t.position = p;
            }

            // Projétil: desce até a altura do cubo e voa contra o jogador.
            // Se bater em um obstáculo no caminho, some (dá para se proteger atrás deles).
            if (item.projetil)
            {
                Vector3 p = t.position;
                p.z += item.velocidadeZ * Time.deltaTime;
                p.y = Mathf.MoveTowards(p.y, item.alturaVoo, 10f * Time.deltaTime);

                float limiteX = MeiaPista - 0.5f;

                // Onda: balança em torno da faixa em que foi lançado.
                if (item.ondaAmplitude > 0f)
                {
                    item.fase += item.ondaFrequencia * Time.deltaTime;
                    p.x = Mathf.Clamp(item.xBase + Mathf.Sin(item.fase) * item.ondaAmplitude, -limiteX, limiteX);
                }

                // Teleguiado: persegue o cubo de lado, mas para de corrigir
                // quando chega perto, para dar chance de desviar.
                if (item.perseguicao > 0f && p.z - zJogador > 6f)
                    p.x = Mathf.MoveTowards(p.x, caixaJogador.center.x, item.perseguicao * Time.deltaTime);

                t.position = p;

                if (BateuEmObstaculo(item))
                {
                    Remover(i);
                    continue;
                }
            }

            // Ímã: puxa as moedas próximas.
            if (item.tipo == TipoItem.Moeda && item.corId == jogador.CorId && jogo.TempoIma > 0f &&
                Vector3.Distance(t.position, caixaJogador.center) < raioIma)
            {
                t.position = Vector3.MoveTowards(t.position, caixaJogador.center,
                    (jogo.Velocidade + 18f) * Time.deltaTime);
            }

            if (!item.Intersects(caixaJogador)) continue;

            switch (item.tipo)
            {
                case TipoItem.Moeda:
                {
                    bool corCorreta = item.corId == jogador.CorId;
                    AtomCoinVisual visualMoeda = item.GetComponentInChildren<AtomCoinVisual>();
                    if (visualMoeda != null) visualMoeda.Coletar(jogador.VisualSolar != null ? jogador.VisualSolar : jogador.transform);
                    jogo.ColetarMoeda(corCorreta);
                    Remover(i);
                    if (jogo.Estado == EstadoJogo.FimDeJogo) return;
                    break;
                }
                case TipoItem.Escudo: jogo.AtivarEscudo(); Remover(i); break;
                case TipoItem.Ima: jogo.AtivarIma(); Remover(i); break;
                case TipoItem.Dobro: jogo.AtivarDobro(); Remover(i); break;

                case TipoItem.Obstaculo:
                    // Portais coloridos só deixam atravessar sem dano com a cor correspondente.
                    if (item.corId >= 0)
                    {
                        if (item.corId == jogador.CorId)
                        {
                            if (!item.atravessado)
                            {
                                item.atravessado = true;
                                ColorPortalVisual portal = item.GetComponent<ColorPortalVisual>();
                                if (portal != null) portal.Atravessar(true);
                                jogo.BonusDeCor();
                            }
                            break;
                        }

                        // A cor errada causa dano uma vez; as piscadas impedem dano repetido ao atravessar.
                        if (!item.atravessado)
                        {
                            item.atravessado = true;
                            ColorPortalVisual portal = item.GetComponent<ColorPortalVisual>();
                            if (portal != null) portal.Atravessar(false);
                            if (!jogador.Invencivel && jogador.ReceberDano(25f))
                            {
                                CameraFollow.Tremer(0.24f, 0.32f);
                                if (jogo.Estado == EstadoJogo.FimDeJogo) return;
                            }
                        }
                        break;
                    }

                    if (jogador.Invencivel) break;
                    if (jogo.ConsumirEscudo())
                    {
                        // O escudo quebra o obstáculo e dá um instante de proteção.
                        Remover(i);
                        jogador.FicarInvencivel(1.2f);
                        CameraFollow.Tremer(0.2f, 0.25f);
                    }
                    else
                    {
                        if (jogador.ReceberDano(25f))
                        {
                            CameraFollow.Tremer(0.24f, 0.32f);
                            if (jogo.Estado == EstadoJogo.FimDeJogo) return;
                        }
                    }
                    break;
            }
        }
    }

    bool BateuEmObstaculo(TrackItem projetil)
    {
        Bounds caixa = projetil.Limites;
        for (int i = 0; i < itens.Count; i++)
        {
            TrackItem outro = itens[i];
            if (outro == null || outro == projetil || outro.projetil) continue;
            if (outro.tipo == TipoItem.Obstaculo && outro.Intersects(caixa)) return true;
        }
        return false;
    }

    void Remover(int indice)
    {
        if (itens[indice] != null) Destroy(itens[indice].gameObject);
        itens.RemoveAt(indice);
    }

    // ---------- Geração ----------

    /// <summary>Cria uma fileira em Z e devolve a distância até a próxima.</summary>
    float CriarFileira(float z, GameManager jogo)
    {
        float d = jogo.Dificuldade;
        float vel = jogo.Velocidade;
        float espaco = vel * Mathf.Lerp(tempoReacaoInicial, tempoReacaoFinal, d) * Random.Range(1f, 1.35f);

        if (z > zInicial + 120f && Random.value < chanceParedeDeCor)
        {
            // Fileira especial: parede de cor ocupando a pista inteira.
            CriarParedeDeCor(z);
            espaco *= 1.15f;
        }
        else if (d > 0.25f && Random.value < 0.12f)
        {
            // Fileira especial: um obstáculo móvel sozinho.
            CriarMovel(z, d);
            espaco *= 1.15f;
        }
        else
        {
            Obst[] fila = MontarFila(d);

            for (int f = 0; f < Faixas; f++)
                if (fila[f] != Obst.Nenhum) CriarObstaculo(fila[f], f, z);

            // Moedas mostrando um caminho seguro por esta fileira.
            if (Random.value < chanceMoedas)
            {
                List<int> seguras = new List<int>();
                for (int f = 0; f < Faixas; f++)
                    if (fila[f] != Obst.Alto) seguras.Add(f);

                int faixa = seguras[Random.Range(0, seguras.Count)];
                CriarMoedas(faixa, z, fila[faixa], vel);
            }
        }

        // No meio do caminho até a próxima fileira: power-up ou mais moedas.
        float zMeio = z + espaco / 2f;
        if (zMeio >= proximoPowerUpZ)
        {
            CriarPowerUp(Random.Range(0, Faixas), zMeio);
            proximoPowerUpZ = zMeio + Random.Range(distanciaMinPowerUp, distanciaMaxPowerUp);
        }
        else if (espaco > 14f && Random.value < 0.3f)
        {
            CriarMoedas(Random.Range(0, Faixas), zMeio, Obst.Nenhum, vel);
        }

        return espaco;
    }

    /// <summary>Decide o que vai em cada faixa, sempre deixando passagem.</summary>
    Obst[] MontarFila(float d)
    {
        Obst[] fila = new Obst[Faixas];

        // Parede inteira que obriga a pular ou a deslizar.
        if (d > 0.15f && Random.value < 0.15f)
        {
            Obst tipo = Random.value < 0.5f ? Obst.Baixo : Obst.Barra;
            for (int f = 0; f < Faixas; f++) fila[f] = tipo;
            return fila;
        }

        // Bloqueia algumas faixas; pelo menos uma fica totalmente livre.
        int maximo = Mathf.Min(Faixas - 1, Mathf.RoundToInt(Mathf.Lerp(2f, 4f, d)));
        int quantidade = Random.Range(1, maximo + 1);

        List<int> livres = new List<int>();
        for (int f = 0; f < Faixas; f++) livres.Add(f);

        for (int i = 0; i < quantidade; i++)
        {
            int sorteio = Random.Range(0, livres.Count);
            int faixa = livres[sorteio];
            livres.RemoveAt(sorteio);

            float r = Random.value;
            fila[faixa] = r < 0.35f ? Obst.Baixo : (r < 0.75f ? Obst.Alto : Obst.Barra);
        }
        return fila;
    }

    void CriarObstaculo(Obst tipo, int faixa, float z)
    {
        float x = jogador.PosicaoXDaFaixa(faixa);
        float largura = jogador.larguraFaixa;
        Vector3 tamanho;
        float centroY;
        Color cor;

        switch (tipo)
        {
            case Obst.Baixo:
                tamanho = new Vector3(largura * 0.9f, 0.7f, 1f);
                centroY = 0.35f;
                cor = corBaixo;
                break;
            case Obst.Barra:
                // Painel suspenso: vão de 0,65 embaixo para passar deslizando.
                tamanho = new Vector3(largura, 2.2f, 0.6f);
                centroY = 0.65f + 1.1f;
                cor = corBarra;
                break;
            default:
                tamanho = new Vector3(largura * 0.9f, 2.6f, 1f);
                centroY = 1.3f;
                cor = corAlto;
                break;
        }

        int faces = tipo == Obst.Baixo ? 3 : (tipo == Obst.Barra ? 4 : 6);
        GameObject obj = PolyhedronVisual.Criar("Poliedro" + tipo, transform,
            new Vector3(x, centroY, z), tamanho, faces, cor);
        // Paredes que não dá para passar agachado se curvam junto com o piso no poço gravitacional.
        if (tipo != Obst.Barra) obj.AddComponent<DeformacaoPeloPoco>();
        Registrar(obj, TipoItem.Obstaculo, tamanho);
    }

    void CriarArtefatoNoObstaculo(Transform pai, Obst tipo, Vector3 tamanho)
    {
        GarantirArtefatos();
        int indice = tipo == Obst.Baixo ? 0 : (tipo == Obst.Barra ? 2 : 1);
        if (imagensArtefatos[indice] == null || materiaisArtefatos[indice] == null) return;

        float largura = Mathf.Min(tamanho.x * 0.70f, 1.12f);
        float altura = Mathf.Min(tamanho.y * 0.70f, 1.50f);
        GameObject imagem = GameObject.CreatePrimitive(PrimitiveType.Quad);
        imagem.name = "NucleoVisualDoArtefato";
        imagem.transform.SetParent(pai, false);
        imagem.transform.localPosition = new Vector3(0f, 0f, -0.39f);
        imagem.transform.localScale = new Vector3(largura, altura, 1f);
        Collider colisor = imagem.GetComponent<Collider>();
        if (colisor != null) Destroy(colisor);
        imagem.GetComponent<Renderer>().sharedMaterial = materiaisArtefatos[indice];
    }

    static void GarantirArtefatos()
    {
        if (imagensArtefatos != null) return;
        imagensArtefatos = new Texture2D[caminhosArtefatos.Length];
        materiaisArtefatos = new Material[caminhosArtefatos.Length];
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        for (int i = 0; i < caminhosArtefatos.Length; i++)
        {
            imagensArtefatos[i] = Resources.Load<Texture2D>(caminhosArtefatos[i]);
            if (imagensArtefatos[i] == null || shader == null) continue;
            Material mat = new Material(shader);
            mat.mainTexture = imagensArtefatos[i];
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", imagensArtefatos[i]);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            mat.color = Color.white;
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            materiaisArtefatos[i] = mat;
        }
    }

    void CriarMovel(float z, float d)
    {
        Vector3 tamanho = new Vector3(1.8f, 2.2f, 0.9f);
        float limite = MeiaPista - tamanho.x / 2f;

        TrackItem item = Registrar(PolyhedronVisual.Criar("PoliedroMovel", transform,
            new Vector3(Random.Range(-limite, limite), tamanho.y / 2f, z),
            tamanho, 5, corMovel), TipoItem.Obstaculo, tamanho);
        item.velocidadeX = Mathf.Lerp(3f, 6f, d) * (Random.value < 0.5f ? -1f : 1f);
        item.gameObject.AddComponent<DeformacaoPeloPoco>();
    }

    /// <summary>
    /// Barreira luminosa em todas as faixas. Só passa quem estiver da mesma cor.
    /// Pode vir de uma cor só (troque a cor do Sol) ou com uma cor por faixa
    /// (troque para a cor primária/inversa certa ou vá para a faixa certa).
    /// </summary>
    void CriarParedeDeCor(float z)
    {
        int quantidadeCores = PlayerController.Cores.Length;
        bool corUnica = Random.value < 0.6f;
        int corBase = Random.Range(0, quantidadeCores);
        Vector3 tamanho = new Vector3(jogador.larguraFaixa, 3.2f, 0.3f);

        for (int f = 0; f < Faixas; f++)
        {
            int cor = corUnica ? corBase : Random.Range(0, quantidadeCores);

            TrackItem item = Registrar(ColorPortalVisual.Criar(transform,
                new Vector3(jogador.PosicaoXDaFaixa(f), tamanho.y / 2f, z), tamanho, cor),
                TipoItem.Obstaculo, tamanho);
            item.corId = cor;
            item.gameObject.AddComponent<DeformacaoPeloPoco>();
        }
    }

    /// <summary>Projétil disparado por um inimigo a partir de "origem".</summary>
    public void CriarProjetil(Vector3 origem, TipoProjetil tipo = TipoProjetil.Normal)
    {
        GameObject obj;
        Vector3 tamanho;
        float velocidade = velocidadeProjetil;
        float alturaVoo = 0.5f;

        switch (tipo)
        {
            case TipoProjetil.Alto:
                // Lâmina alta: do vão de 0,65 para cima. Só passa deslizando.
                tamanho = new Vector3(0.9f, 1.9f, 0.35f);
                alturaVoo = 0.65f + tamanho.y / 2f;
                obj = Visual.Forma(PrimitiveType.Cube, "ProjetilAlto", transform,
                    origem, tamanho, new Color(0.65f, 0.3f, 1f), true);
                break;

            case TipoProjetil.Rapido:
                tamanho = Vector3.one * 0.42f;
                velocidade *= 1.7f;
                obj = Visual.Forma(PrimitiveType.Sphere, "ProjetilRapido", transform,
                    origem, tamanho, new Color(1f, 0.9f, 0.2f), true);
                // Rastro esticado para passar a ideia de velocidade.
                Visual.Forma(PrimitiveType.Cube, "Rastro", obj.transform,
                    new Vector3(0f, 0f, 1.6f), new Vector3(0.35f, 0.35f, 2.4f), new Color(1f, 0.9f, 0.2f), true, raio: 0f);
                break;

            case TipoProjetil.Onda:
                tamanho = Vector3.one * 0.55f;
                obj = Visual.Forma(PrimitiveType.Sphere, "ProjetilOnda", transform,
                    origem, tamanho, new Color(1f, 0.3f, 0.8f), true);
                break;

            case TipoProjetil.Teleguiado:
                tamanho = Vector3.one * 0.6f;
                velocidade *= 0.8f;
                obj = Visual.Forma(PrimitiveType.Sphere, "ProjetilTeleguiado", transform,
                    origem, tamanho, new Color(0.5f, 1f, 0.95f), true);
                // Anel em volta para diferenciar dos outros.
                Visual.Forma(PrimitiveType.Cube, "Anel", obj.transform,
                    Vector3.zero, new Vector3(1.7f, 0.15f, 1.7f), new Color(0.5f, 1f, 0.95f), true);
                break;

            default:
                tamanho = Vector3.one * 0.55f;
                obj = Visual.Forma(PrimitiveType.Sphere, "Projetil", transform,
                    origem, tamanho, new Color(1f, 0.1f, 0.15f), true);
                break;
        }

        TrackItem item = Registrar(obj, TipoItem.Obstaculo, tamanho);
        item.projetil = true;
        item.velocidadeZ = -velocidade;
        item.alturaVoo = alturaVoo;
        item.xBase = origem.x;

        if (tipo == TipoProjetil.Onda)
        {
            item.ondaAmplitude = jogador.larguraFaixa;
            item.ondaFrequencia = 3.2f;
        }
        else if (tipo == TipoProjetil.Teleguiado)
        {
            item.perseguicao = 2.5f;
            item.girar = true;
        }
    }

    /// <summary>
    /// A nave Kamikaze, ao mergulhar, passa a se comportar como um projétil:
    /// desce até a pista, voa contra o jogador e explode se bater em obstáculo.
    /// </summary>
    public void AdotarKamikaze(GameObject nave, Vector3 tamanho)
    {
        TrackItem item = Registrar(nave, TipoItem.Obstaculo, tamanho);
        item.projetil = true;
        item.velocidadeZ = -velocidadeProjetil * 1.5f;
        item.alturaVoo = tamanho.y / 2f + 0.1f;
        item.perseguicao = 1.5f;
    }

    void CriarMoedas(int faixa, float z, Obst sobre, float vel)
    {
        const int quantidade = 5;
        int corDaFileira = Random.Range(0, PlayerController.Cores.Length);
        float x = jogador.PosicaoXDaFaixa(faixa);

        // Sobre obstáculo baixo as moedas formam o arco do pulo.
        bool arco = sobre == Obst.Baixo;
        float passo = arco ? vel * jogador.TempoNoAr / 6f : 1.6f;

        for (int i = 0; i < quantidade; i++)
        {
            float dz = (i - (quantidade - 1) / 2f) * passo;
            float y;

            if (arco)
            {
                float t = dz / vel;
                float subida = Mathf.Max(0f, jogador.AlturaMaximaPulo - 0.5f * jogador.gravidade * t * t);
                y = 0.5f + subida;
            }
            else if (sobre == Obst.Barra) y = 0.3f;  // rente ao chão, por baixo da barra
            else y = 0.7f;

            int corId = corDaFileira;
            Color cor = PlayerController.Cores[corId];
            GameObject moeda = AtomCoinVisual.Criar(transform, new Vector3(x, y, z + dz), corId);
            TrackItem item = Registrar(moeda, TipoItem.Moeda, Vector3.one * 0.9f);
            item.corId = corId;
            item.girar = false; // O átomo anima apenas o visual, preservando a colisão.
        }
    }

    void CriarPowerUp(int faixa, float z)
    {
        TipoItem tipo;
        Color cor;
        switch (Random.Range(0, 3))
        {
            case 0: tipo = TipoItem.Escudo; cor = corEscudo; break;
            case 1: tipo = TipoItem.Ima; cor = corIma; break;
            default: tipo = TipoItem.Dobro; cor = corDobro; break;
        }

        GameObject obj = Visual.Forma(PrimitiveType.Sphere, "PowerUp" + tipo, transform,
            new Vector3(jogador.PosicaoXDaFaixa(faixa), 0.9f, z), Vector3.one * 0.9f, cor, true);

        // Anel em volta, para diferenciar bem das moedas.
        Visual.Forma(PrimitiveType.Cube, "Anel", obj.transform,
            Vector3.zero, new Vector3(1.5f, 0.12f, 1.5f), cor, true);

        TrackItem item = Registrar(obj, tipo, Vector3.one * 1.2f);
        item.girar = true;
    }

    /// <summary>Gera um meteoro que cai numa faixa, com espaço para desviar.</summary>
    public void CriarMeteoro(float z)
    {
        List<int> livres = new List<int>();
        for (int faixa = 0; faixa < Faixas; faixa++)
        {
            float x = jogador.PosicaoXDaFaixa(faixa);
            bool ocupado = false;
            for (int i = 0; i < itens.Count; i++)
            {
                TrackItem outro = itens[i];
                if (outro == null || outro.tipo != TipoItem.Obstaculo || outro.projetil) continue;
                if (Mathf.Abs(outro.transform.position.x - x) < jogador.larguraFaixa * 0.7f &&
                    Mathf.Abs(outro.transform.position.z - z) < 7f)
                { ocupado = true; break; }
            }
            if (!ocupado) livres.Add(faixa);
        }
        if (livres.Count == 0) return;

        int faixaEscolhida = livres[Random.Range(0, livres.Count)];
        Color brasa = new Color(1f, 0.24f, 0.055f);
        GameObject meteoro = PolyhedronVisual.Criar("MeteoroEmQueda", transform,
            new Vector3(jogador.PosicaoXDaFaixa(faixaEscolhida), 6.5f, z),
            new Vector3(0.92f, 0.78f, 0.92f), 5, brasa);
        meteoro.transform.localRotation = Random.rotation;
        Visual.Forma(PrimitiveType.Sphere, "BrasaDoMeteoro", meteoro.transform,
            new Vector3(0f, 0.45f, 0.02f), Vector3.one * 0.42f,
            new Color(1f, 0.78f, 0.18f), true);
        TrackItem item = Registrar(meteoro, TipoItem.Obstaculo, new Vector3(0.82f, 0.82f, 0.82f));
        item.meteoroCaindo = true;
        item.velocidadeQuedaMeteoro = Random.Range(7f, 9f);
    }

    TrackItem Registrar(GameObject obj, TipoItem tipo, Vector3 tamanho)
    {
        TrackPlanarReflection.IncluirObjeto(obj);
        TrackItem item = obj.AddComponent<TrackItem>();
        item.tipo = tipo;
        item.formaPoliedro = obj.GetComponent<PolyhedronHitShape>();
        item.tamanho = tamanho;
        itens.Add(item);
        return item;
    }
}
