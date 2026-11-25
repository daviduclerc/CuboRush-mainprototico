using System.Collections.Generic;
using UnityEngine;

/// <summary>Prismas facetados de vidro transparente com contorno neon, com malhas compartilhadas.</summary>
public static class PolyhedronVisual
{
    static readonly Dictionary<Color, Material> materiaisVisiveis = new Dictionary<Color, Material>();
    static readonly Dictionary<Color, Material> materiaisVidro = new Dictionary<Color, Material>();
    static readonly Dictionary<Color, Material> materiaisNeon = new Dictionary<Color, Material>();
    const float LarguraNeon = 0.035f;

    static readonly Dictionary<int, Mesh> malhas = new Dictionary<int, Mesh>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReiniciarCacheDeSessao()
    {
        // Enter Play Mode sem Domain Reload mantém dicionários, mas as malhas
        // geradas da sessão anterior podem já ter sido destruídas pela Unity.
        malhas.Clear();
        materiaisVisiveis.Clear();
        materiaisVidro.Clear();
        materiaisNeon.Clear();
    }

    public static GameObject Criar(string nome, Transform pai, Vector3 posicao,
        Vector3 tamanho, int lados, Color cor, FormaObstaculo forma = FormaObstaculo.Regular)
    {
        var objeto = new GameObject(nome);
        objeto.transform.SetParent(pai, false);
        objeto.transform.localPosition = posicao;
        objeto.transform.localScale = tamanho;
        objeto.AddComponent<MeshFilter>().sharedMesh = Malha(forma, lados);
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
        var corpo = objeto.AddComponent<MeshRenderer>();
        corpo.sharedMaterial = meteoro ? material : MaterialVidro(cor);
        if (!meteoro)
        {
            // Vidro não projeta sombra; o contorno neon na cor do obstáculo dá a leitura da forma.
            corpo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            CriarBordas(objeto.transform, ContornoObstaculo.Obter(forma, lados), cor);
        }
        var colisao = objeto.AddComponent<PolyhedronHitShape>();
        colisao.Inicializar(lados, forma);
        colisao.CorVisual = cor;
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
            filtro.sharedMesh = Malha(forma.Forma, forma.QuantidadeLados);
        corpo.enabled = true;
        corpo.forceRenderingOff = false;
        corpo.sharedMaterial = forma != null ? MaterialVidro(forma.CorVisual) : TechObstacleMaterials.Obter(false);
    }

    /// <summary>Vidro transparente tingido com a cor do obstáculo.</summary>
    static Material MaterialVidro(Color cor)
    {
        if (materiaisVidro.TryGetValue(cor, out Material pronto) && pronto != null) return pronto;
        ReflexoDoAmbiente.Garantir();
        Shader vidro = Resources.Load<Shader>("Space/VidroNeon");
        if (vidro == null) return TechObstacleMaterials.Obter(false);
        var material = new Material(vidro) { name = "Vidro neon dos obstaculos" };
        material.SetColor("_Cor", Color.Lerp(cor, Color.white, 0.1f));
        material.SetFloat("_Opacidade", 0.12f);
        materiaisVidro[cor] = material;
        return material;
    }

    /// <summary>Neon das arestas, bem mais claro que o vidro para destacar o contorno.</summary>
    static Material MaterialNeon(Color cor)
    {
        if (materiaisNeon.TryGetValue(cor, out Material pronto) && pronto != null) return pronto;
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null) return TechObstacleMaterials.Borda(false);
        var material = new Material(unlit) { name = "Neon das arestas dos obstaculos" };
        Color brilho = Color.Lerp(cor, Color.white, 0.15f) * 4f;
        material.color = brilho;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", brilho);
        materiaisNeon[cor] = material;
        return material;
    }

    /// <summary>Malha compartilhada do prisma (contorno de -0,5 a 0,5 puxado em Z).</summary>
    public static Mesh MalhaPrisma(FormaObstaculo forma, int lados) => Malha(forma, lados);

    static Mesh Malha(FormaObstaculo forma, int lados)
    {
        int chave = ContornoObstaculo.Chave(forma, lados);
        if (malhas.TryGetValue(chave, out Mesh existente) && existente != null) return existente;
        Vector3[] contorno = ContornoObstaculo.Obter(forma, lados);
        int quantidade = contorno.Length;
        var vertices = new List<Vector3>();
        var triangulos = new List<int>();
        // Duplica vértices por triângulo para preservar as arestas planas.
        for (int i = 0; i < quantidade; i++)
        {
            Vector3 a = contorno[i], b = contorno[(i + 1) % quantidade];
            Vector3 frente = Vector3.back * 0.5f, tras = Vector3.forward * 0.5f;
            Triangulo(vertices, triangulos, frente, b + frente, a + frente);
            Triangulo(vertices, triangulos, tras, a + tras, b + tras);
            Triangulo(vertices, triangulos, a + frente, b + frente, b + tras);
            Triangulo(vertices, triangulos, a + frente, b + tras, a + tras);
        }
        var malha = new Mesh { name = "Prisma solido " + forma + " " + quantidade };
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
        malhas[chave] = malha;
        return malha;
    }

    /// <summary>Neon em todas as arestas do prisma: contorno da frente, de trás e as de profundidade.</summary>
    static void CriarBordas(Transform pai, Vector3[] contorno, Color cor)
    {
        int lados = contorno.Length;
        Material neon = MaterialNeon(cor);
        for (int face = 0; face < 2; face++)
        {
            LineRenderer linha = NovaAresta(pai, neon, lados, true);
            for (int i = 0; i < lados; i++)
                linha.SetPosition(i, NaAresta(contorno[i], face == 0 ? -0.5f : 0.5f));
        }
        for (int i = 0; i < lados; i++)
        {
            LineRenderer linha = NovaAresta(pai, neon, 2, false);
            linha.SetPosition(0, NaAresta(contorno[i], -0.5f));
            linha.SetPosition(1, NaAresta(contorno[i], 0.5f));
        }
    }

    static LineRenderer NovaAresta(Transform pai, Material neon, int pontos, bool fechada)
    {
        var linha = new GameObject("ArestaNeonDoVidro").AddComponent<LineRenderer>();
        linha.transform.SetParent(pai, false);
        linha.useWorldSpace = false;
        linha.loop = fechada;
        linha.positionCount = pontos;
        linha.widthMultiplier = LarguraNeon;
        linha.numCornerVertices = 2;
        linha.sharedMaterial = neon;
        linha.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linha.receiveShadows = false;
        return linha;
    }

    // Levemente para dentro, para o neon ficar sobre a aresta sem vazar da forma.
    static Vector3 NaAresta(Vector3 p, float z) => new Vector3(p.x * 0.985f, p.y * 0.985f, z * 0.985f);

    static void Triangulo(List<Vector3> vertices, List<int> indices, Vector3 a, Vector3 b, Vector3 c)
    {
        int inicio = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        indices.Add(inicio); indices.Add(inicio + 1); indices.Add(inicio + 2);
    }
}
