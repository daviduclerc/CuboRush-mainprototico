using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dobra, vértice a vértice, as malhas compridas que não usam os shaders da pista
/// (vigas, guarda-corpos, naves inimigas e as faixas de aviso delas) para acompanharem
/// a curvatura do horizonte causada pela atração do buraco negro.
/// O piso e as linhas neon já se curvam na GPU e por isso ficam de fora.
/// Objetos registrados no ObstacleSpawner (TrackItem) são movidos inteiros por ele.
/// </summary>
public sealed class CurvaturaDaMalha : MonoBehaviour
{
    sealed class Parte
    {
        public MeshFilter filtro;
        public Mesh original, deformada;
        public Vector3[] repouso, trabalho;
        public Bounds limites;
        public bool curvada;
    }

    static readonly Dictionary<int, Mesh> cubosSubdivididos = new Dictionary<int, Mesh>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReiniciarCache() => cubosSubdivididos.Clear();

    readonly List<Parte> partes = new List<Parte>();
    bool preparado;
    bool movidoPeloGerador;

    void LateUpdate()
    {
        // Quando vira item da pista (Kamikaze adotada), o gerador move o objeto inteiro.
        if (!movidoPeloGerador && GetComponent<TrackItem>() != null)
        {
            movidoPeloGerador = true;
            Restaurar();
        }
        if (movidoPeloGerador) return;

        if (CampoGravitacional.Curvatura <= 0f)
        {
            if (preparado) Restaurar();
            return;
        }
        if (!preparado) Preparar();

        float inicio = CampoGravitacional.ZReferencia + CampoGravitacional.InicioCurva;
        for (int i = 0; i < partes.Count; i++)
        {
            Parte parte = partes[i];
            if (parte.filtro == null) continue;
            Matrix4x4 paraMundo = parte.filtro.transform.localToWorldMatrix;

            // Pedaço inteiro antes do começo da curva: fica reto (e só restaura uma vez).
            if (ZMaximo(parte.limites, paraMundo) < inicio)
            {
                if (parte.curvada) { parte.deformada.vertices = parte.repouso; parte.curvada = false; }
                continue;
            }

            Matrix4x4 paraLocal = parte.filtro.transform.worldToLocalMatrix;
            Vector3[] repouso = parte.repouso, trabalho = parte.trabalho;
            for (int v = 0; v < repouso.Length; v++)
            {
                Vector3 mundo = paraMundo.MultiplyPoint3x4(repouso[v]);
                mundo.y -= CampoGravitacional.Queda(mundo.z);
                trabalho[v] = paraLocal.MultiplyPoint3x4(mundo);
            }
            parte.deformada.vertices = trabalho;
            parte.curvada = true;
        }
    }

    void Preparar()
    {
        preparado = true;
        foreach (MeshFilter filtro in GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh original = filtro.sharedMesh;
            if (original == null || !original.isReadable) continue;
            Renderer render = filtro.GetComponent<Renderer>();
            if (render != null && JaCurvaNaGpu(render.sharedMaterial)) continue;

            var parte = new Parte { filtro = filtro, original = original, limites = original.bounds };
            parte.repouso = original.vertices;
            parte.trabalho = new Vector3[parte.repouso.Length];
            parte.deformada = Instantiate(original);
            parte.deformada.name = original.name + " (curvada pela atracao)";
            parte.deformada.MarkDynamic();
            // A malha pode descer bastante; limites folgados evitam que ela suma da câmera.
            Bounds b = parte.deformada.bounds;
            b.Expand(new Vector3(0f, 60f / Mathf.Max(0.01f, Mathf.Abs(filtro.transform.lossyScale.y)), 0f));
            parte.deformada.bounds = b;
            filtro.sharedMesh = parte.deformada;
            partes.Add(parte);
        }
    }

    void Restaurar()
    {
        for (int i = 0; i < partes.Count; i++)
        {
            Parte parte = partes[i];
            if (parte.filtro != null) parte.filtro.sharedMesh = parte.original;
            if (parte.deformada != null) Destroy(parte.deformada);
        }
        partes.Clear();
        preparado = false;
    }

    static bool JaCurvaNaGpu(Material material)
    {
        if (material == null || material.shader == null) return false;
        string nome = material.shader.name;
        return nome == "CuboRush/NeonDaPista" || nome == "CuboRush/ReflectiveTrack";
    }

    static float ZMaximo(Bounds limites, Matrix4x4 paraMundo)
    {
        Vector3 c = limites.center, e = limites.extents;
        float maximo = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 canto = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
            maximo = Mathf.Max(maximo, paraMundo.MultiplyPoint3x4(canto).z);
        }
        return maximo;
    }

    void OnDestroy() => Restaurar();

    /// <summary>
    /// Cubo de lado 1 dividido ao longo de Z (a escala do objeto dá o tamanho).
    /// Peças compridas precisam de vértices ao longo do comprimento para conseguir se curvar.
    /// </summary>
    public static Mesh CuboSubdividido(int segmentos)
    {
        segmentos = Mathf.Max(1, segmentos);
        if (cubosSubdivididos.TryGetValue(segmentos, out Mesh pronto) && pronto != null) return pronto;
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var t = new List<int>();
        Vector3 x = Vector3.right, y = Vector3.up, z = Vector3.forward;
        Face(v, n, t, new Vector3(-0.5f, 0.5f, -0.5f), x, z, 1, segmentos, Vector3.up);
        Face(v, n, t, new Vector3(-0.5f, -0.5f, -0.5f), x, z, 1, segmentos, Vector3.down);
        Face(v, n, t, new Vector3(-0.5f, -0.5f, -0.5f), z, y, segmentos, 1, Vector3.left);
        Face(v, n, t, new Vector3(0.5f, -0.5f, -0.5f), z, y, segmentos, 1, Vector3.right);
        Face(v, n, t, new Vector3(-0.5f, -0.5f, -0.5f), x, y, 1, 1, Vector3.back);
        Face(v, n, t, new Vector3(-0.5f, -0.5f, 0.5f), x, y, 1, 1, Vector3.forward);
        var malha = new Mesh { name = "Cubo subdividido " + segmentos };
        malha.SetVertices(v);
        malha.SetNormals(n);
        malha.SetTriangles(t, 0);
        malha.RecalculateBounds();
        cubosSubdivididos[segmentos] = malha;
        return malha;
    }

    static void Face(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 origem,
        Vector3 eixoU, Vector3 eixoV, int divisoesU, int divisoesV, Vector3 normal)
    {
        int inicio = v.Count;
        for (int j = 0; j <= divisoesV; j++)
            for (int i = 0; i <= divisoesU; i++)
            {
                v.Add(origem + eixoU * (i / (float)divisoesU) + eixoV * (j / (float)divisoesV));
                n.Add(normal);
            }
        bool inverter = Vector3.Dot(Vector3.Cross(eixoV, eixoU), normal) < 0f;
        for (int j = 0; j < divisoesV; j++)
            for (int i = 0; i < divisoesU; i++)
            {
                int a = inicio + j * (divisoesU + 1) + i, b = a + 1, c = a + divisoesU + 1, d = c + 1;
                if (!inverter) { t.Add(a); t.Add(c); t.Add(b); t.Add(b); t.Add(c); t.Add(d); }
                else { t.Add(a); t.Add(b); t.Add(c); t.Add(b); t.Add(d); t.Add(c); }
            }
    }
}
