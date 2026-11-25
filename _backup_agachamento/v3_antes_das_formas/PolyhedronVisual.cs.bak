using System.Collections.Generic;
using UnityEngine;

/// <summary>Prismas sólidos facetados com malhas compartilhadas.</summary>
public static class PolyhedronVisual
{
    static readonly Dictionary<Color, Material> materiaisVisiveis = new Dictionary<Color, Material>();

    static readonly Dictionary<int, Mesh> malhas = new Dictionary<int, Mesh>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReiniciarCacheDeSessao()
    {
        // Enter Play Mode sem Domain Reload mantém dicionários, mas as malhas
        // geradas da sessão anterior podem já ter sido destruídas pela Unity.
        malhas.Clear();
        materiaisVisiveis.Clear();
    }

    public static GameObject Criar(string nome, Transform pai, Vector3 posicao,
        Vector3 tamanho, int lados, Color cor)
    {
        var objeto = new GameObject(nome);
        objeto.transform.SetParent(pai, false);
        objeto.transform.localPosition = posicao;
        objeto.transform.localScale = tamanho;
        objeto.AddComponent<MeshFilter>().sharedMesh = Malha(lados);
        if (!materiaisVisiveis.TryGetValue(cor, out Material material) || material == null)
        {
            Color baseVisivel = Color.Lerp(cor, Color.white, 0.15f);
            material = new Material(Visual.MatMetal(baseVisivel, 0.38f));
            material.name = "Liga visivel dos obstaculos";
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", baseVisivel * 0.6f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.55f);
            materiaisVisiveis[cor] = material;
        }
        bool meteoro = nome.Contains("Meteoro");
        bool ceramica = false;
        objeto.AddComponent<MeshRenderer>().sharedMaterial = meteoro ? material : TechObstacleMaterials.Obter(ceramica);
        if (!meteoro) CriarBordas(objeto.transform, lados, ceramica);
        objeto.AddComponent<PolyhedronHitShape>().Inicializar(lados);
        return objeto;
    }

    public static void RestaurarCorpo(GameObject objeto)
    {
        if (objeto == null || !objeto.name.StartsWith("Poliedro")) return;
        var corpo = objeto.GetComponent<MeshRenderer>();
        if (corpo == null) return;
        // O corpo opaco e os aros continuam renderizando após retomar a partida.
        var filtro = objeto.GetComponent<MeshFilter>();
        var forma = objeto.GetComponent<PolyhedronHitShape>();
        if (filtro != null && filtro.sharedMesh == null && forma != null && forma.QuantidadeLados >= 3)
            filtro.sharedMesh = Malha(forma.QuantidadeLados);
        corpo.enabled = true;
        corpo.forceRenderingOff = false;
        corpo.sharedMaterial = TechObstacleMaterials.Obter(false);
    }

    static Mesh Malha(int lados)
    {
        if (malhas.TryGetValue(lados, out Mesh existente) && existente != null) return existente;
        var contorno = new Vector3[lados];
        var minimo = Vector3.one * float.PositiveInfinity;
        var maximo = Vector3.one * float.NegativeInfinity;
        for (int i = 0; i < lados; i++)
        {
            float angulo = (90f + i * 360f / lados) * Mathf.Deg2Rad;
            contorno[i] = new Vector3(Mathf.Cos(angulo), Mathf.Sin(angulo), 0f);
            minimo = Vector3.Min(minimo, contorno[i]);
            maximo = Vector3.Max(maximo, contorno[i]);
        }
        for (int i = 0; i < lados; i++)
            contorno[i] = new Vector3((contorno[i].x - minimo.x) / (maximo.x - minimo.x) - 0.5f,
                (contorno[i].y - minimo.y) / (maximo.y - minimo.y) - 0.5f, 0f);
        var vertices = new List<Vector3>();
        var triangulos = new List<int>();
        // Duplica vértices por triângulo para preservar as arestas planas.
        for (int i = 0; i < lados; i++)
        {
            Vector3 a = contorno[i], b = contorno[(i + 1) % lados];
            Vector3 frente = Vector3.back * 0.5f, tras = Vector3.forward * 0.5f;
            Triangulo(vertices, triangulos, frente, b + frente, a + frente);
            Triangulo(vertices, triangulos, tras, a + tras, b + tras);
            Triangulo(vertices, triangulos, a + frente, b + frente, b + tras);
            Triangulo(vertices, triangulos, a + frente, b + tras, a + tras);
        }
        var malha = new Mesh { name = "Prisma solido " + lados };
        malha.SetVertices(vertices);
        malha.SetTriangles(triangulos, 0);
        var uv = new List<Vector2>(vertices.Count);
        for (int t = 0; t < vertices.Count; t += 3)
        {
            Vector3 normal = Vector3.Cross(vertices[t + 1] - vertices[t], vertices[t + 2] - vertices[t]).normalized;
            for (int j = 0; j < 3; j++)
            {
                Vector3 p = vertices[t + j];
                if (Mathf.Abs(normal.z) > 0.5f) uv.Add(new Vector2(p.x + 0.5f, p.y + 0.5f));
                else if (Mathf.Abs(normal.x) > Mathf.Abs(normal.y)) uv.Add(new Vector2(p.z + 0.5f, p.y + 0.5f));
                else uv.Add(new Vector2(p.x + 0.5f, p.z + 0.5f));
            }
        }
        malha.SetUVs(0, uv);
        malha.RecalculateNormals();
        malha.RecalculateTangents();
        malha.RecalculateBounds();
        malhas[lados] = malha;
        return malha;
    }

    static void CriarBordas(Transform pai, int lados, bool ceramica)
    {
        var pontos = new Vector3[lados];
        Vector2 min = Vector2.one * float.PositiveInfinity;
        Vector2 max = Vector2.one * float.NegativeInfinity;
        for (int i = 0; i < lados; i++)
        {
            float a = (90f + i * 360f / lados) * Mathf.Deg2Rad;
            pontos[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            min = Vector2.Min(min, pontos[i]); max = Vector2.Max(max, pontos[i]);
        }
        for (int face = 0; face < 2; face++)
        {
            var linha = new GameObject("ArestaLuminosaDoRevestimento").AddComponent<LineRenderer>();
            linha.transform.SetParent(pai, false);
            linha.useWorldSpace = false;
            linha.loop = true;
            linha.positionCount = lados;
            linha.widthMultiplier = 0.018f;
            linha.sharedMaterial = TechObstacleMaterials.Borda(ceramica);
            linha.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            linha.receiveShadows = false;
            for (int i = 0; i < lados; i++)
            {
                Vector3 p = pontos[i];
                p.x = ((p.x - min.x) / (max.x - min.x) - 0.5f) * 0.94f;
                p.y = ((p.y - min.y) / (max.y - min.y) - 0.5f) * 0.94f;
                p.z = face == 0 ? -0.502f : 0.502f;
                linha.SetPosition(i, p);
            }
        }
    }

    static void Triangulo(List<Vector3> vertices, List<int> indices, Vector3 a, Vector3 b, Vector3 c)
    {
        int inicio = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        indices.Add(inicio); indices.Add(inicio + 1); indices.Add(inicio + 2);
    }
}
