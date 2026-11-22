using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Faz uma parede acompanhar a curvatura do piso quando o Sol abre o poço gravitacional:
/// cada vértice desce o mesmo tanto que o chão logo abaixo dele. Só é usado em paredes
/// que não dá para passar agachado. É só visual; a colisão continua a mesma.
/// </summary>
public sealed class DeformacaoPeloPoco : MonoBehaviour
{
    const float ArestaMaxima = 0.35f;   // tamanho máximo dos triângulos depois de subdividir
    const float PassoLinha = 0.2f;      // distância entre pontos das linhas luminosas
    const int NiveisMaximos = 4;

    // Malhas subdivididas compartilhadas por todas as paredes iguais.
    static readonly Dictionary<Mesh, Mesh[]> subdivididas = new Dictionary<Mesh, Mesh[]>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReiniciarCache() => subdivididas.Clear();

    sealed class Parte
    {
        public MeshFilter filtro;
        public Mesh repouso, deformada;
        public Vector3[] vertices, trabalho;
    }

    sealed class Linha
    {
        public LineRenderer linha;
        public Vector3[] pontos, trabalho;
    }

    readonly List<Parte> partes = new List<Parte>();
    readonly List<Linha> linhas = new List<Linha>();
    bool preparado, deformado;
    float raio = 1.5f;

    void Start()
    {
        // Raio aproximado no plano para saber quando o poço chega perto.
        Bounds limites = new Bounds(transform.position, Vector3.zero);
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) limites.Encapsulate(r.bounds);
        raio = new Vector2(limites.extents.x, limites.extents.z).magnitude;
    }

    void LateUpdate()
    {
        bool afeta = PocoGravitacional.Ativo && PocoGravitacional.Alcanca(transform.position, raio);
        if (afeta)
        {
            if (!preparado) Preparar();
            Deformar();
            deformado = true;
        }
        else if (deformado)
        {
            Repousar();
            deformado = false;
        }
    }

    void Preparar()
    {
        preparado = true;
        foreach (MeshFilter filtro in GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh original = filtro.sharedMesh;
            if (original == null || !original.isReadable) continue;
            Mesh repouso = Subdividida(original, Niveis(original, filtro.transform.lossyScale));
            var parte = new Parte { filtro = filtro, repouso = repouso, vertices = repouso.vertices };
            parte.trabalho = new Vector3[parte.vertices.Length];
            parte.deformada = Instantiate(repouso);
            parte.deformada.name = repouso.name + " (curvada)";
            parte.deformada.MarkDynamic();
            filtro.sharedMesh = parte.deformada;
            partes.Add(parte);
        }

        foreach (LineRenderer linha in GetComponentsInChildren<LineRenderer>(true))
        {
            int n = linha.positionCount;
            if (n < 2) continue;
            var originais = new Vector3[n];
            linha.GetPositions(originais);
            Matrix4x4 paraMundo = linha.useWorldSpace ? Matrix4x4.identity : linha.transform.localToWorldMatrix;
            var pontos = new List<Vector3>();
            int segmentos = linha.loop ? n : n - 1;
            for (int i = 0; i < segmentos; i++)
            {
                Vector3 a = originais[i], b = originais[(i + 1) % n];
                float comprimento = Vector3.Distance(paraMundo.MultiplyPoint3x4(a), paraMundo.MultiplyPoint3x4(b));
                int passos = Mathf.Clamp(Mathf.CeilToInt(comprimento / PassoLinha), 1, 32);
                for (int s = 0; s < passos; s++) pontos.Add(Vector3.Lerp(a, b, s / (float)passos));
            }
            if (!linha.loop) pontos.Add(originais[n - 1]);
            var item = new Linha { linha = linha, pontos = pontos.ToArray() };
            item.trabalho = new Vector3[item.pontos.Length];
            linha.positionCount = item.pontos.Length;
            linha.SetPositions(item.pontos);
            linhas.Add(item);
        }
    }

    void Deformar()
    {
        for (int p = 0; p < partes.Count; p++)
        {
            Parte parte = partes[p];
            if (parte.filtro == null) continue;
            // Outro código pode ter trocado a malha (ex.: ao retomar da pausa).
            if (parte.filtro.sharedMesh != parte.deformada) parte.filtro.sharedMesh = parte.deformada;
            Transform t = parte.filtro.transform;
            Matrix4x4 paraMundo = t.localToWorldMatrix;
            Matrix4x4 paraLocal = t.worldToLocalMatrix;
            Vector3[] origem = parte.vertices, destino = parte.trabalho;
            for (int i = 0; i < origem.Length; i++)
            {
                Vector3 mundo = paraMundo.MultiplyPoint3x4(origem[i]);
                mundo.y -= PocoGravitacional.Deslocamento(mundo.x, mundo.z);
                destino[i] = paraLocal.MultiplyPoint3x4(mundo);
            }
            parte.deformada.vertices = destino;
            parte.deformada.RecalculateNormals();
            parte.deformada.RecalculateBounds();
        }

        for (int l = 0; l < linhas.Count; l++)
        {
            Linha item = linhas[l];
            if (item.linha == null) continue;
            bool local = !item.linha.useWorldSpace;
            Matrix4x4 paraMundo = local ? item.linha.transform.localToWorldMatrix : Matrix4x4.identity;
            Matrix4x4 paraLocal = local ? item.linha.transform.worldToLocalMatrix : Matrix4x4.identity;
            for (int i = 0; i < item.pontos.Length; i++)
            {
                Vector3 mundo = paraMundo.MultiplyPoint3x4(item.pontos[i]);
                mundo.y -= PocoGravitacional.Deslocamento(mundo.x, mundo.z);
                item.trabalho[i] = paraLocal.MultiplyPoint3x4(mundo);
            }
            item.linha.SetPositions(item.trabalho);
        }
    }

    void Repousar()
    {
        // Volta exatamente à forma original quando o espaço fica plano de novo.
        foreach (Parte parte in partes)
        {
            if (parte.filtro == null) continue;
            parte.deformada.vertices = parte.vertices;
            parte.deformada.RecalculateNormals();
            parte.deformada.RecalculateBounds();
        }
        foreach (Linha item in linhas)
            if (item.linha != null) item.linha.SetPositions(item.pontos);
    }

    void OnDestroy()
    {
        foreach (Parte parte in partes)
            if (parte.deformada != null) Destroy(parte.deformada);
    }

    // ---------- Subdivisão ----------

    static int Niveis(Mesh malha, Vector3 escala)
    {
        Vector3[] v = malha.vertices;
        int[] tri = malha.triangles;
        float maior = 0f;
        for (int i = 0; i < tri.Length; i += 3)
        {
            Vector3 a = Vector3.Scale(v[tri[i]], escala);
            Vector3 b = Vector3.Scale(v[tri[i + 1]], escala);
            Vector3 c = Vector3.Scale(v[tri[i + 2]], escala);
            maior = Mathf.Max(maior, Mathf.Max((a - b).magnitude, Mathf.Max((b - c).magnitude, (c - a).magnitude)));
        }
        int niveis = 0;
        while (maior > ArestaMaxima && niveis < NiveisMaximos) { maior *= 0.5f; niveis++; }
        return niveis;
    }

    struct Vertice
    {
        public Vector3 p, n;
        public Vector2 uv;
        public Vector4 t;
        public static Vertice Meio(Vertice a, Vertice b) => new Vertice
        {
            p = (a.p + b.p) * 0.5f,
            n = ((a.n + b.n) * 0.5f).normalized,
            uv = (a.uv + b.uv) * 0.5f,
            t = (a.t + b.t) * 0.5f
        };
    }

    static Mesh Subdividida(Mesh origem, int niveis)
    {
        if (!subdivididas.TryGetValue(origem, out Mesh[] porNivel) || porNivel == null)
        {
            porNivel = new Mesh[NiveisMaximos + 1];
            subdivididas[origem] = porNivel;
        }
        if (porNivel[niveis] != null) return porNivel[niveis];

        var dados = new DadosOrigem(origem);
        var destino = new DadosDestino();
        var submalhas = new List<List<int>>();
        for (int s = 0; s < origem.subMeshCount; s++)
        {
            var indices = new List<int>();
            int[] tri = origem.GetTriangles(s);
            for (int i = 0; i < tri.Length; i += 3)
                Emitir(destino, indices, dados.Ler(tri[i]), dados.Ler(tri[i + 1]), dados.Ler(tri[i + 2]), niveis);
            submalhas.Add(indices);
        }

        var malha = new Mesh { name = origem.name + " subdividida" };
        if (destino.pos.Count > 65000) malha.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        malha.SetVertices(destino.pos);
        if (dados.temN) malha.SetNormals(destino.nor);
        if (dados.temUv) malha.SetUVs(0, destino.uvs);
        if (dados.temT) malha.SetTangents(destino.tan);
        malha.subMeshCount = submalhas.Count;
        for (int s = 0; s < submalhas.Count; s++) malha.SetTriangles(submalhas[s], s);
        if (!dados.temN) malha.RecalculateNormals();
        malha.RecalculateBounds();
        porNivel[niveis] = malha;
        return malha;
    }

    // Divide cada triângulo em 4 (pelos pontos médios), mantendo a orientação.
    static void Emitir(DadosDestino destino, List<int> indices, Vertice a, Vertice b, Vertice c, int nivel)
    {
        if (nivel == 0)
        {
            destino.Adicionar(indices, a);
            destino.Adicionar(indices, b);
            destino.Adicionar(indices, c);
            return;
        }
        Vertice ab = Vertice.Meio(a, b), bc = Vertice.Meio(b, c), ca = Vertice.Meio(c, a);
        Emitir(destino, indices, a, ab, ca, nivel - 1);
        Emitir(destino, indices, ab, b, bc, nivel - 1);
        Emitir(destino, indices, ca, bc, c, nivel - 1);
        Emitir(destino, indices, ab, bc, ca, nivel - 1);
    }

    sealed class DadosOrigem
    {
        readonly Vector3[] v, n;
        readonly Vector2[] uv;
        readonly Vector4[] t;
        public readonly bool temN, temUv, temT;

        public DadosOrigem(Mesh malha)
        {
            v = malha.vertices; n = malha.normals; uv = malha.uv; t = malha.tangents;
            temN = n.Length == v.Length; temUv = uv.Length == v.Length; temT = t.Length == v.Length;
        }

        public Vertice Ler(int i) => new Vertice
        {
            p = v[i],
            n = temN ? n[i] : Vector3.up,
            uv = temUv ? uv[i] : Vector2.zero,
            t = temT ? t[i] : new Vector4(1f, 0f, 0f, 1f)
        };
    }

    sealed class DadosDestino
    {
        public readonly List<Vector3> pos = new List<Vector3>(), nor = new List<Vector3>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly List<Vector4> tan = new List<Vector4>();

        public void Adicionar(List<int> indices, Vertice x)
        {
            indices.Add(pos.Count);
            pos.Add(x.p); nor.Add(x.n); uvs.Add(x.uv); tan.Add(x.t);
        }
    }
}
