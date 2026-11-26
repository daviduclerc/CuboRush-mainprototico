using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cria obstáculos, moedas e power-ups na frente do jogador, apaga o que ficou
/// para trás e verifica as colisões com o cubo.
///
/// Tipos de obstáculo:
///  - Pirâmide: pule por cima ou desvie.
///  - Barreira suspensa: deslize por baixo ou desvie.
///  - Parede de cor:    barreira luminosa; só atravessa com o cubo da mesma cor.
///  - Projétil:         disparado pelas naves inimigas (ver Enemy).
/// Sempre existe um caminho possível em cada fileira.
/// </summary>
[DefaultExecutionOrder(100)]
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

    [System.Flags]
    enum Obst { Nenhum = 0, Baixo = 1, Barra = 2 }

    static readonly Color corEscudo = new Color(0.3f, 0.8f, 1f);
    static readonly Color corIma = new Color(0.3f, 1f, 0.45f);
    static readonly Color corDobro = new Color(1f, 0.5f, 0.9f);

    readonly List<TrackItem> itens = new List<TrackItem>();
    float proximoZ;
    EstadoJogo estadoAnterior = EstadoJogo.Menu;
    float proximoPowerUpZ;
    Bounds caixaAnterior;
    int passoPadrao;
    int faixaRota;
    bool comecaComPulo;
    bool emChuvaDeMeteoros;
    int faixaSeguraDaChuva;
    float proximaFileiraDeMoedasDaChuva;

    int Faixas => jogador.quantidadeFaixas;
    float MeiaPista => Faixas * jogador.larguraFaixa / 2f;

    void Start()
    {
        float z = jogador.transform.position.z;
        zInicial = z;
        faixaRota = Faixas / 2;
        caixaAnterior = jogador.Limites;
        proximoInimigo = primeiroInimigo;
        proximoZ = z + inicioLivre;
        proximoPowerUpZ = z + Random.Range(120f, 200f);
    }

    void Update()
    {
        GameManager jogo = GameManager.Instancia;
        if (jogo == null || jogador == null) return;

        bool vinhaJogando = estadoAnterior == EstadoJogo.Jogando;
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

        if (emChuvaDeMeteoros)
        {
            float horizonte = zJogador + Mathf.Min(distanciaVisao, jogo.Velocidade * 2.6f + 8f);
            while (proximaFileiraDeMoedasDaChuva < horizonte)
            {
                if (Random.value < chanceMoedas)
                    CriarMoedas(faixaSeguraDaChuva, proximaFileiraDeMoedasDaChuva, Obst.Nenhum, jogo.Velocidade);
                proximaFileiraDeMoedasDaChuva += Mathf.Max(16f, jogo.Velocidade * 1.25f);
            }
        }
        else
        {
            while (proximoZ < zJogador + distanciaVisao)
                proximoZ += CriarFileira(proximoZ, jogo);
        }

        // De tempos em tempos chega uma nave inimiga (uma de cada vez).
        if (!emChuvaDeMeteoros && jogo.Estado == EstadoJogo.Jogando && inimigoAtual == null &&
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

        Bounds caixa = jogador.Limites;
        AtualizarItens(jogo, zJogador, vinhaJogando ? caixaAnterior : caixa, caixa);
        caixaAnterior = caixa;
    }

    // ---------- Atualização e colisão ----------

    void AtualizarItens(GameManager jogo, float zJogador, Bounds anterior, Bounds caixaJogador)
    {
        bool jogando = jogo.Estado == EstadoJogo.Jogando;

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

            if (item.obstaculoSolar != null)
            {
                if (item.atravessado) continue;
                Bounds limites = item.Limites;
                if (caixaJogador.min.z < limites.min.z - 8f && caixaJogador.max.z < limites.min.z) continue;
                if (!item.obstaculoSolar.Intersecta(anterior, caixaJogador))
                {
                    RegistrarTravessia(item, limites, anterior, caixaJogador, jogo);
                    continue;
                }
                item.atravessado = true;
                item.obstaculoSolar.Sinalizar(false);
            }
            else if (!item.Intersects(caixaJogador)) continue;

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
                            if (item.obstaculoSolar != null) jogo.SomDeImpacto();
                        }
                    }
                    break;
            }
        }
    }

    static void RegistrarTravessia(TrackItem item, Bounds limites, Bounds anterior, Bounds atual, GameManager jogo)
    {
        if (atual.min.z <= limites.max.z) return;
        item.atravessado = true;
        if (!item.obstaculoSolar.SomDeTravessia || anterior.min.z > limites.max.z) return;

        float fracao = Mathf.InverseLerp(anterior.min.z, atual.min.z, limites.max.z);
        float x = Mathf.Lerp(anterior.center.x, atual.center.x, fracao);
        if (x < limites.min.x || x > limites.max.x) return;
        item.obstaculoSolar.Sinalizar(true);
        jogo.SomDeTravessia();
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
        float manobra = Mathf.Max(jogador.TempoNoAr + PlayerController.TempoPreparoPulo, jogador.duracaoDeslize);
        float intervalo = Mathf.Max(Mathf.Lerp(tempoReacaoInicial, tempoReacaoFinal, d), manobra + 0.26f);
        float espaco = jogo.VelocidadePrevista(z - jogador.transform.position.z + 20f)
            * intervalo * Random.Range(1f, 1.16f) + 2f;

        if (passoPadrao == 0 && z > zInicial + 120f && Random.value < chanceParedeDeCor)
        {
            CriarParedeDeCor(z);
            espaco *= 1.15f;
        }
        else
        {
            Obst[] fila = MontarFila(d);
            for (int f = 0; f < Faixas; f++)
                if (fila[f] != Obst.Nenhum) CriarObstaculos(fila[f], f, z);

            // As moedas sugerem a rota, incluindo a faixa vizinha quando a anterior está bloqueada.
            if (Random.value < chanceMoedas)
                CriarMoedas(faixaRota, z, fila[faixaRota], vel);
        }

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

    // Cada padrão alterna os dois gestos e fecha com uma troca para uma faixa adjacente.
    Obst[] MontarFila(float dificuldade)
    {
        var fila = new Obst[Faixas];
        int faixaAnterior = faixaRota;
        if (passoPadrao == 0) comecaComPulo = Random.value < 0.5f;

        if (passoPadrao == 2)
        {
            faixaRota = FaixaVizinha(faixaAnterior);
            fila[faixaAnterior] = Obst.Baixo | Obst.Barra;
        }
        else
        {
            bool pular = passoPadrao == 0 ? comecaComPulo : !comecaComPulo;
            fila[faixaRota] = pular ? Obst.Baixo : Obst.Barra;
        }

        int alternativa = Faixas > 1 ? FaixaVizinha(faixaRota) : faixaRota;
        for (int f = 0; f < Faixas; f++)
        {
            if (f == faixaRota || f == alternativa || fila[f] != Obst.Nenhum) continue;
            if (Random.value < Mathf.Lerp(0.26f, 0.80f, dificuldade))
                fila[f] = Random.value < 0.5f ? Obst.Baixo : Obst.Barra;
        }

        passoPadrao = (passoPadrao + 1) % (Faixas > 1 ? 3 : 2);
        return fila;
    }

    int FaixaVizinha(int faixa)
    {
        if (faixa == 0) return 1;
        if (faixa == Faixas - 1) return faixa - 1;
        return faixa + (Random.value < 0.5f ? -1 : 1);
    }

    void CriarObstaculos(Obst tipo, int faixa, float z)
    {
        var posicao = new Vector3(jogador.PosicaoXDaFaixa(faixa), 0f, z);
        // No bloqueio duplo, só a barreira leva as marcas de piso. Ambos mantêm colisões próprias.
        if ((tipo & Obst.Baixo) != 0)
        {
            var piramide = ObstaculoSolar.Criar(transform, posicao, jogador.larguraFaixa, true, tipo == Obst.Baixo);
            Registrar(piramide.gameObject, TipoItem.Obstaculo, piramide.Limites.size).obstaculoSolar = piramide;
        }
        if ((tipo & Obst.Barra) != 0)
        {
            var barreira = ObstaculoSolar.Criar(transform, posicao, jogador.larguraFaixa, false, tipo == Obst.Barra);
            Registrar(barreira.gameObject, TipoItem.Obstaculo, barreira.Limites.size).obstaculoSolar = barreira;
        }
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
        }
    }

    /// <summary>Projétil disparado por um inimigo a partir de "origem".</summary>
    public void CriarProjetil(Vector3 origem, TipoProjetil tipo = TipoProjetil.Normal)
    {
        if (emChuvaDeMeteoros) return;
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
        if (emChuvaDeMeteoros) { Destroy(nave); return; }
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

    public void EntrarNaChuvaDeMeteoros(int faixaSegura)
    {
        emChuvaDeMeteoros = true;
        faixaSeguraDaChuva = faixaSegura;
        proximaFileiraDeMoedasDaChuva = Mathf.Max(proximoZ, jogador.transform.position.z + 18f);
        for (int i = itens.Count - 1; i >= 0; i--)
        {
            if (itens[i] == null || itens[i].tipo != TipoItem.Obstaculo) continue;
            itens[i].gameObject.SetActive(false);
            Remover(i);
        }
        if (inimigoAtual != null)
        {
            inimigoAtual.gameObject.SetActive(false);
            Destroy(inimigoAtual.gameObject);
            inimigoAtual = null;
        }
    }

    public void SairDaChuvaDeMeteoros()
    {
        if (!emChuvaDeMeteoros) return;
        emChuvaDeMeteoros = false;
        GameManager jogo = GameManager.Instancia;
        if (jogo == null || jogador == null) return;
        float margem = Mathf.Min(distanciaVisao - 1f, jogo.VelocidadePrevista(45f) * 1.4f + 5f);
        proximoZ = jogador.transform.position.z + Mathf.Max(4f, margem);
        proximoInimigo = Mathf.Max(proximoInimigo, jogo.TempoDeJogo + 4f);
    }

    /// <summary>Gera um meteoro que cai numa faixa, com espaço para desviar.</summary>
    public void CriarMeteoro(float z)
    {
        if (emChuvaDeMeteoros) return;
        // Não ocupa a faixa livre de uma sequência perto da sua fileira.
        foreach (TrackItem obstaculo in itens)
            if (obstaculo != null && obstaculo.obstaculoSolar != null && Mathf.Abs(obstaculo.transform.position.z - z) < 7f) return;

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

