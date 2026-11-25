import sys, os, shutil

raiz = sys.argv[1]
scripts = os.path.join(raiz, "Assets", "Scripts")
backup = os.path.join(raiz, "_backup_agachamento", "v5_antes_do_espelho_e_vidro")
os.makedirs(backup, exist_ok=True)
pendentes = []


def editar(nome, trocas):
    caminho = os.path.join(scripts, nome)
    dados = open(caminho, "rb").read()
    bom = dados.startswith(b"\xef\xbb\xbf")
    crlf = b"\r\n" in dados
    texto = dados.decode("utf-8-sig").replace("\r\n", "\n")
    for antigo, novo, vezes in trocas:
        achados = texto.count(antigo)
        if achados != vezes:
            raise SystemExit(f"{nome}: esperava {vezes}x, achei {achados}x de:\n{antigo}")
        texto = texto.replace(antigo, novo)
    if crlf:
        texto = texto.replace("\n", "\r\n")
    pendentes.append((caminho, (b"\xef\xbb\xbf" if bom else b"") + texto.encode("utf-8")))


editar("PolyhedronVisual.cs", [
    ("""/// <summary>Prismas sólidos facetados com malhas compartilhadas.</summary>
public static class PolyhedronVisual
{
    static readonly Dictionary<Color, Material> materiaisVisiveis = new Dictionary<Color, Material>();
""", """/// <summary>Prismas facetados de vidro transparente com contorno neon, com malhas compartilhadas.</summary>
public static class PolyhedronVisual
{
    static readonly Dictionary<Color, Material> materiaisVisiveis = new Dictionary<Color, Material>();
    static readonly Dictionary<Color, Material> materiaisVidro = new Dictionary<Color, Material>();
    static readonly Dictionary<Color, Material> materiaisNeon = new Dictionary<Color, Material>();
    const float LarguraNeon = 0.035f;
""", 1),
    ("""        malhas.Clear();
        materiaisVisiveis.Clear();
""", """        malhas.Clear();
        materiaisVisiveis.Clear();
        materiaisVidro.Clear();
        materiaisNeon.Clear();
""", 1),
    ("""        bool meteoro = nome.Contains("Meteoro");
        bool ceramica = false;
        objeto.AddComponent<MeshRenderer>().sharedMaterial = meteoro ? material : TechObstacleMaterials.Obter(ceramica);
        if (!meteoro) CriarBordas(objeto.transform, ContornoObstaculo.Obter(forma, lados), ceramica);
        objeto.AddComponent<PolyhedronHitShape>().Inicializar(lados, forma);
        return objeto;
""", """        bool meteoro = nome.Contains("Meteoro");
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
""", 1),
    ("""        corpo.sharedMaterial = TechObstacleMaterials.Obter(false);
    }
""", """        corpo.sharedMaterial = forma != null ? MaterialVidro(forma.CorVisual) : TechObstacleMaterials.Obter(false);
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
""", 1),
    ("""    static void CriarBordas(Transform pai, Vector3[] contorno, bool ceramica)
    {
        int lados = contorno.Length;
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
                Vector3 p = contorno[i];
                p.x *= 0.94f;
                p.y *= 0.94f;
                p.z = face == 0 ? -0.502f : 0.502f;
                linha.SetPosition(i, p);
            }
        }
    }
""", """    /// <summary>Neon em todas as arestas do prisma: contorno da frente, de trás e as de profundidade.</summary>
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
""", 1),
])

editar("PolyhedronHitShape.cs", [
    ("""    public FormaObstaculo Forma { get; private set; }
""", """    public FormaObstaculo Forma { get; private set; }
    /// <summary>Cor do vidro e do neon (usada para refazer o material ao retomar da pausa).</summary>
    public Color CorVisual { get; set; } = Color.white;
""", 1),
])

editar("FloorGenerator.cs", [
    ("""        float zInicial = jogador != null ? jogador.position.z : 0f;
""", """        float zInicial = jogador != null ? jogador.position.z : 0f;
        // Espelhos e vidros refletem as linhas neon na posição certa das faixas.
        ReflexoDoAmbiente.DefinirPista(quantidadeFaixas * larguraFaixa / 2f, larguraFaixa);
""", 1),
])

for nome in ("PolyhedronVisual.cs", "PolyhedronHitShape.cs", "FloorGenerator.cs", "ColorPortalVisual.cs"):
    shutil.copy2(os.path.join(scripts, nome), os.path.join(backup, nome + ".bak"))
for caminho, saida in pendentes:
    open(caminho, "wb").write(saida)
    print("ok", os.path.basename(caminho))
