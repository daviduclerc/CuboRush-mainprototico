using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Estrelas discretas e estrelas cadentes ocasionais, fora da área da pista.</summary>
public class StarField : MonoBehaviour
{
    public Transform jogador;
    public int quantidade = 130;
    public float profundidade = 110f;
    public float largura = 70f;

    Transform[] estrelas;
    Vector2[] posicoesCeu;
    Material materialEstrelas;
    float[] tamanhos;
    readonly System.Random aleatorio = new System.Random(73519);
    Camera cameraJogo;
    sealed class RastroCadente
    {
        public LineRenderer linha;
        public Vector3 inicio, fim;
        public float idade, duracao;
        public bool ativo;
    }
    readonly RastroCadente[] cadentes = new RastroCadente[6];
    Material materialCadente;
    float proximaChuva, intervaloEmissao;
    int restantesChuva;
    bool chuvaParaDireita;
    float Sorteio(float min, float max) => Mathf.Lerp(min, max, (float)aleatorio.NextDouble());

    void Start()
    {
        cameraJogo = Camera.main;
        estrelas = new Transform[quantidade];
        tamanhos = new float[quantidade];
        posicoesCeu = new Vector2[quantidade];
        materialEstrelas = new Material(Resources.Load<Shader>("Space/StarDot"));
        for (int i = 0; i < quantidade; i++)
        {
            // Mantém todos os pontos no campo de visão, com tamanho legível em pixels.
            float x = Sorteio(0.02f, 0.98f);
            float y = Sorteio(0.62f, 0.98f);
            if (i % 4 == 0) { x = i % 8 == 0 ? Sorteio(0.02f, 0.14f) : Sorteio(0.86f, 0.98f); y = Sorteio(0.22f, 0.9f); }
            if (x > 0.30f && x < 0.70f && y < 0.83f) y = Sorteio(0.86f, 0.98f);
            posicoesCeu[i] = new Vector2(x, y);
            tamanhos[i] = i % 11 == 0 ? Sorteio(3.8f, 5.2f) : Sorteio(2.1f, 3.5f);
            GameObject estrela = GameObject.CreatePrimitive(PrimitiveType.Quad);
            estrela.name = "EstrelaDeFundo";
            Destroy(estrela.GetComponent<Collider>());
            estrela.transform.SetParent(transform, false);
            estrela.GetComponent<Renderer>().sharedMaterial = materialEstrelas;
            estrelas[i] = estrela.transform;
        }
        CriarCadente();
        proximaChuva = Sorteio(8f, 12f);
    }

    void CriarCadente()
    {
        Shader shader = Resources.Load<Shader>("Space/ShootingStar");
        if (shader == null) return;
        materialCadente = new Material(shader);
        for (int i = 0; i < cadentes.Length; i++)
        {
            var objeto = new GameObject("EstrelaDaChuva" + i);
            objeto.transform.SetParent(transform, false);
            var linha = objeto.AddComponent<LineRenderer>();
            linha.sharedMaterial = materialCadente;
            linha.useWorldSpace = true;
            linha.positionCount = 12;
            linha.widthMultiplier = 0.18f;
            linha.widthCurve = AnimationCurve.Linear(0f, 0.03f, 1f, 1f);
            linha.shadowCastingMode = ShadowCastingMode.Off;
            linha.receiveShadows = false;
            linha.enabled = false;
            cadentes[i] = new RastroCadente { linha = linha };
        }
    }
    void LateUpdate()
    {
        if (jogador == null) return;
        if (cameraJogo == null) { cameraJogo = Camera.main; if (cameraJogo == null) return; }
        float unidadesPixel = 2f * 90f * Mathf.Tan(cameraJogo.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1f, cameraJogo.pixelHeight);
        for (int i = 0; i < estrelas.Length; i++)
        {
            Vector2 uv = posicoesCeu[i];
            estrelas[i].position = cameraJogo.ViewportToWorldPoint(new Vector3(uv.x, uv.y, 90f));
            estrelas[i].rotation = cameraJogo.transform.rotation;
            estrelas[i].localScale = Vector3.one * tamanhos[i] * unidadesPixel * (0.94f + 0.06f * Mathf.Sin(Time.time * 1.4f + i));
        }
        GameManager jogo = GameManager.Instancia;
        bool jogando = jogo != null && jogo.Estado == EstadoJogo.Jogando;
        if (!jogando)
        {
            for (int i = 0; i < cadentes.Length; i++)
                if (cadentes[i] != null) cadentes[i].linha.enabled = false;
            return;
        }
        AtualizarChuva();
    }

    void AtualizarChuva()
    {
        if (materialCadente == null) return;
        float dt = Time.deltaTime;
        if (restantesChuva == 0)
        {
            proximaChuva -= dt;
            if (proximaChuva <= 0f)
            {
                restantesChuva = Mathf.FloorToInt(Sorteio(7f, 10f));
                chuvaParaDireita = Sorteio(0f, 1f) < 0.5f;
                intervaloEmissao = 0f;
            }
        }
        if (restantesChuva > 0)
        {
            intervaloEmissao -= dt;
            if (intervaloEmissao <= 0f)
            {
                for (int i = 0; i < cadentes.Length; i++)
                {
                    if (cadentes[i].ativo) continue;
                    IniciarCadente(cadentes[i]);
                    restantesChuva--;
                    intervaloEmissao = Sorteio(0.5f, 0.8f);
                    if (restantesChuva == 0) proximaChuva = Sorteio(24f, 36f);
                    break;
                }
            }
        }
        for (int i = 0; i < cadentes.Length; i++)
        {
            RastroCadente rastro = cadentes[i];
            if (!rastro.ativo) continue;
            rastro.idade += dt;
            float t = Mathf.Clamp01(rastro.idade / rastro.duracao);
            float alpha = Mathf.SmoothStep(0f, 1f, t / 0.12f) * (1f - Mathf.SmoothStep(0.68f, 1f, t));
            rastro.linha.enabled = true;
            rastro.linha.startColor = new Color(0.35f, 0.65f, 1f, 0f);
            rastro.linha.endColor = new Color(0.85f, 0.94f, 1f, alpha);
            for (int p = 0; p < rastro.linha.positionCount; p++)
            {
                float atraso = (1f - p / (float)(rastro.linha.positionCount - 1)) * 0.22f;
                Vector3 ponto = Vector3.Lerp(rastro.inicio, rastro.fim, Mathf.Clamp01(t - atraso));
                rastro.linha.SetPosition(p, cameraJogo.transform.TransformPoint(ponto));
            }
            if (t >= 1f) { rastro.ativo = false; rastro.linha.enabled = false; }
        }
    }

    void IniciarCadente(RastroCadente rastro)
    {
        // Uma mesma chuva tem direção coerente, com variações leves de origem e velocidade.
        float x = chuvaParaDireita ? Sorteio(0.04f, 0.63f) : Sorteio(0.37f, 0.96f);
        float y = Sorteio(0.86f, 0.98f);
        float fimX = Mathf.Clamp(x + (chuvaParaDireita ? 0.19f : -0.19f), 0.02f, 0.98f);
        rastro.inicio = cameraJogo.transform.InverseTransformPoint(cameraJogo.ViewportToWorldPoint(new Vector3(x, y, 90f)));
        rastro.fim = cameraJogo.transform.InverseTransformPoint(cameraJogo.ViewportToWorldPoint(new Vector3(fimX, y - 0.10f, 90f)));
        rastro.idade = 0f;
        rastro.duracao = Sorteio(1.1f, 1.6f);
        rastro.linha.widthMultiplier = Sorteio(0.14f, 0.22f);
        rastro.ativo = true;
    }
    void Posicionar(Transform estrela, float z)
    {
        float x = Sorteio(9f, largura) * (Sorteio(0f, 1f) < 0.5f ? -1f : 1f);
        float y = Sorteio(-15f, 42f);
        if (Sorteio(0f, 1f) < 0.25f)
        {
            x = Sorteio(-11f, 11f);
            y = Sorteio(18f, 42f);
        }
        estrela.position = new Vector3(x, y, z);
    }

    void OnDestroy()
    {
        if (materialCadente != null) Destroy(materialCadente);
        if (materialEstrelas != null) Destroy(materialEstrelas);
    }
}
