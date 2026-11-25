import sys, os, shutil

raiz = sys.argv[1]
assets = os.path.join(raiz, "Assets")
scripts = os.path.join(assets, "Scripts")
backup = os.path.join(raiz, "_backup_agachamento")
os.makedirs(os.path.join(backup, "v1_afundar_no_chao"), exist_ok=True)

pendentes = []


def ler(caminho):
    dados = open(caminho, "rb").read()
    return dados.startswith(b"\xef\xbb\xbf"), dados.decode("utf-8-sig").replace("\r\n", "\n")


def editar(rel, trocas, base=None):
    caminho = os.path.join(assets, rel)
    bom, texto = ler(base or caminho)
    for antigo, novo, vezes in trocas:
        achados = texto.count(antigo)
        if achados != vezes:
            raise SystemExit(f"{rel}: esperava {vezes}x, achei {achados}x de:\n{antigo}")
        texto = texto.replace(antigo, novo)
    saida = texto.replace("\n", "\r\n").encode("utf-8")
    if bom:
        saida = b"\xef\xbb\xbf" + saida
    pendentes.append((caminho, saida))


# ---------------- PlayerController ----------------
editar("Scripts/PlayerController.cs", [
    ("""    [Tooltip("Quanto do diâmetro do Sol afunda na pista ao abaixar (0 = nada, 1 = inteiro).")]
    [Range(0f, 1f)] public float fracaoAfundada = 0.5f;
""",
     """
    [Header("Poço gravitacional (ao abaixar)")]
    [Tooltip("Profundidade do funil embaixo do Sol, em unidades de mundo.")]
    public float profundidadePoco = 0.75f;
    [Tooltip("Raio do fundo do funil: maior = poço mais largo e suave.")]
    public float nucleoPoco = 1.6f;
    [Tooltip("Até que distância do Sol o espaço se curva.")]
    public float alcancePoco = 5f;
    [Tooltip("Altura da onda que percorre o espaço quando o Sol afunda e quando sai.")]
    public float amplitudeOndaPoco = 0.18f;
""", 1),
    ("""    /// <summary>0 = Sol sobre a pista, 1 = afundado no espaço-tempo durante o deslize.</summary>
""",
     """    /// <summary>0 = espaço plano, 1 = poço gravitacional totalmente aberto durante o deslize.</summary>
""", 1),
    ("""        if (rodaCores != null) { rodaCores.Cancelar(); Destroy(rodaCores); rodaCores = null; }
""",
     """        if (rodaCores != null) { rodaCores.Cancelar(); Destroy(rodaCores); rodaCores = null; }
        PocoGravitacional.Reiniciar();
""", 1),
    ("""        // Ao abaixar, o Sol mantém o tamanho e afunda na pista, como se a sua massa
        // curvasse o espaço-tempo. O próprio piso esconde a parte que fica por baixo.
        // As deformações dos eixos ficam reservadas para o preparo e o impulso do pulo.
        float afundamento = Afundamento;
""",
     """        // Ao abaixar, o Sol pesa sobre a pista: o próprio chão afunda em funil ao redor
        // dele, como nos diagramas de buraco negro, e as paredes próximas acompanham.
        // As deformações dos eixos ficam reservadas para o preparo e o impulso do pulo.
        PocoGravitacional.Atualizar(new Vector2(p.x, p.z), Afundamento, profundidadePoco, nucleoPoco,
            alcancePoco, quantidadeFaixas * larguraFaixa / 2f, amplitudeOndaPoco, Time.deltaTime);
        float afundamento = PocoGravitacional.Deslocamento(p.x, p.z);
""", 1),
    ("""            // Ao deslizar, desce uma fração do diâmetro para dentro da pista.
            float raioAtual = raioSolarBase * deformacaoSolar.localScale.y;
            p.y = altura + raioAtual - deformacaoSolar.localPosition.y
                - afundamento * fracaoAfundada * 2f * raioAtual;
""",
     """            // Ao deslizar, desce junto com o fundo do poço e continua apoiado no piso curvado.
            float raioAtual = raioSolarBase * deformacaoSolar.localScale.y;
            p.y = altura + raioAtual - deformacaoSolar.localPosition.y - afundamento;
""", 1),
    ("""            p.y = altura + alturaVisual / 2f - afundamento * fracaoAfundada * alturaVisual;
""",
     """            p.y = altura + alturaVisual / 2f - afundamento;
""", 1),
])

# ---------------- SolarMotionFlames (volta ao original, só renomeando a propriedade) ----------------
editar("Scripts/SolarMotionFlames.cs", [
    ("""    float proporcaoAbaixadoAnterior = 1f;
""", """    float afundamentoAnterior;
""", 1),
    ("proporcaoAbaixadoAnterior = jogador.ProporcaoAbaixado;", "afundamentoAnterior = jogador.Afundamento;", 2),
    ("""        float proporcaoAtual = jogador.ProporcaoAbaixado;
        float variacao = Mathf.Abs(proporcaoAtual - proporcaoAbaixadoAnterior) / dt;
        proporcaoAbaixadoAnterior = proporcaoAtual;
        // Tanto a contração quanto a expansão lançam plasma para trás da corrida.
""",
     """        float afundamentoAtual = jogador.Afundamento;
        float variacao = Mathf.Abs(afundamentoAtual - afundamentoAnterior) / dt;
        afundamentoAnterior = afundamentoAtual;
        // Tanto afundar quanto emergir lançam plasma para trás da corrida.
""", 1),
], base=os.path.join(backup, "SolarMotionFlames.cs.bak"))

# ---------------- SolarFloorReflection ----------------
editar("Scripts/SolarFloorReflection.cs", [
    ("""        // Afundado, o Sol encosta na pista e a luz se espalha um pouco ao redor.
        float tamanho = 1f + jogador.Afundamento * 0.25f;
""",
     """        float tamanho = 1f;
""", 1),
    ("""        cor.a = Mathf.SmoothStep(0.02f, 0.95f, energiaSuavizada) * proximidade;
""",
     """        cor.a = Mathf.SmoothStep(0.02f, 0.95f, energiaSuavizada) * proximidade;
        // O brilho é um plano reto: some enquanto o piso está curvado pelo poço gravitacional.
        cor.a *= 1f - PocoGravitacional.Intensidade;
""", 1),
])

# ---------------- ObstacleSpawner ----------------
editar("Scripts/ObstacleSpawner.cs", [
    ("""        GameObject obj = PolyhedronVisual.Criar("Poliedro" + tipo, transform,
            new Vector3(x, centroY, z), tamanho, faces, cor);
        Registrar(obj, TipoItem.Obstaculo, tamanho);
""",
     """        GameObject obj = PolyhedronVisual.Criar("Poliedro" + tipo, transform,
            new Vector3(x, centroY, z), tamanho, faces, cor);
        // Paredes que não dá para passar agachado se curvam junto com o piso no poço gravitacional.
        if (tipo != Obst.Barra) obj.AddComponent<DeformacaoPeloPoco>();
        Registrar(obj, TipoItem.Obstaculo, tamanho);
""", 1),
    ("""        item.velocidadeX = Mathf.Lerp(3f, 6f, d) * (Random.value < 0.5f ? -1f : 1f);    }
""",
     """        item.velocidadeX = Mathf.Lerp(3f, 6f, d) * (Random.value < 0.5f ? -1f : 1f);
        item.gameObject.AddComponent<DeformacaoPeloPoco>();
    }
""", 1),
    ("""                TipoItem.Obstaculo, tamanho);
            item.corId = cor;
""",
     """                TipoItem.Obstaculo, tamanho);
            item.corId = cor;
            item.gameObject.AddComponent<DeformacaoPeloPoco>();
""", 1),
])

# ---------------- FloorGenerator ----------------
editar("Scripts/FloorGenerator.cs", [
    ("""    readonly Queue<Transform> pedacos = new Queue<Transform>();
    float proximoZ;
    static readonly Material[] materiaisLinhaNeon = new Material[1];
""",
     """    readonly Queue<Transform> pedacos = new Queue<Transform>();
    float proximoZ;
    static readonly Material[] materiaisLinhaNeon = new Material[1];
    // Malhas subdivididas: o piso e as linhas precisam de vértices para se curvar no poço gravitacional.
    static Mesh malhaPiso, malhaLinha;
    static Vector3 tamanhoMalhaPiso;
    const float PassoDaGradeX = 0.2f;
    const float PassoDaGradeZ = 0.25f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReiniciarCache()
    {
        malhaPiso = null;
        malhaLinha = null;
        materiaisLinhaNeon[0] = null;
    }
""", 1),
    ("""        // Liga metálica lisa com guias luminosas longitudinais.
        piso.GetComponent<Renderer>().sharedMaterial = Visual.MatPisoLiso(corPiso);
""",
     """        // Liga metálica lisa com guias luminosas longitudinais.
        piso.GetComponent<Renderer>().sharedMaterial = Visual.MatPisoLiso(corPiso);
        piso.GetComponent<MeshFilter>().sharedMesh = MalhaPiso(new Vector3(larguraTotal, 0.2f, comprimentoPedaco));
""", 1),
    ("""            int indiceCor = i % coresNeon.Length;
            if (materiaisLinhaNeon[indiceCor] == null)
            {
                Material neon = new Material(Visual.Mat(corNeon, true));
                neon.name = "Neon vibrante da pista " + indiceCor;
                neon.EnableKeyword("_EMISSION");
                neon.SetColor("_EmissionColor", corNeon * 3.5f);
                materiaisLinhaNeon[indiceCor] = neon;
            }
""",
     """            linhaNeon.GetComponent<MeshFilter>().sharedMesh = MalhaLinha(Mathf.CeilToInt(comprimentoPedaco / PassoDaGradeZ));
            int indiceCor = i % coresNeon.Length;
            if (materiaisLinhaNeon[indiceCor] == null)
            {
                // Shader próprio para a linha acompanhar a curvatura do piso.
                Shader shaderNeon = Resources.Load<Shader>("Space/NeonDaPista");
                Material neon = shaderNeon != null ? new Material(shaderNeon) : new Material(Visual.Mat(corNeon, true));
                neon.name = "Neon vibrante da pista " + indiceCor;
                if (shaderNeon != null) neon.SetColor("_Cor", corNeon * 3.5f);
                else
                {
                    neon.EnableKeyword("_EMISSION");
                    neon.SetColor("_EmissionColor", corNeon * 3.5f);
                }
                materiaisLinhaNeon[indiceCor] = neon;
            }
""", 1),
    ("""        return raiz;
    }
}
""",
     """        return raiz;
    }

    // ---------- Malhas subdivididas ----------

    /// <summary>Laje do piso com o topo em grade fina (as bordas laterais ficam presas).</summary>
    static Mesh MalhaPiso(Vector3 tamanho)
    {
        if (malhaPiso != null && tamanhoMalhaPiso == tamanho) return malhaPiso;
        int colunas = Mathf.Max(1, Mathf.CeilToInt(tamanho.x / PassoDaGradeX));
        int linhas = Mathf.Max(1, Mathf.CeilToInt(tamanho.z / PassoDaGradeZ));
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var t = new List<int>();
        Vector3 h = tamanho * 0.5f;
        Vector3 x = new Vector3(tamanho.x, 0f, 0f), y = new Vector3(0f, tamanho.y, 0f), z = new Vector3(0f, 0f, tamanho.z);
        Face(v, n, t, new Vector3(-h.x, h.y, -h.z), x, z, colunas, linhas, Vector3.up);
        Face(v, n, t, new Vector3(-h.x, -h.y, -h.z), x, z, 1, 1, Vector3.down);
        Face(v, n, t, new Vector3(-h.x, -h.y, -h.z), z, y, 1, 1, Vector3.left);
        Face(v, n, t, new Vector3(h.x, -h.y, -h.z), z, y, 1, 1, Vector3.right);
        Face(v, n, t, new Vector3(-h.x, -h.y, -h.z), x, y, colunas, 1, Vector3.back);
        Face(v, n, t, new Vector3(-h.x, -h.y, h.z), x, y, colunas, 1, Vector3.forward);
        malhaPiso = CriarMalha("Piso curvavel do espaco-tempo", v, n, t);
        tamanhoMalhaPiso = tamanho;
        return malhaPiso;
    }

    /// <summary>Cubo de lado 1 dividido ao longo de Z (a escala do objeto dá o tamanho).</summary>
    static Mesh MalhaLinha(int segmentos)
    {
        if (malhaLinha != null) return malhaLinha;
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
        malhaLinha = CriarMalha("Linha neon curvavel", v, n, t);
        return malhaLinha;
    }

    /// <summary>Grade de quadrados de "origem" até origem + eixoU + eixoV, virada para "normal".</summary>
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

    static Mesh CriarMalha(string nome, List<Vector3> v, List<Vector3> n, List<int> t)
    {
        var malha = new Mesh { name = nome };
        if (v.Count > 65000) malha.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        malha.SetVertices(v);
        malha.SetNormals(n);
        malha.SetTriangles(t, 0);
        malha.RecalculateBounds();
        // Margem nos limites: o shader afunda o piso abaixo da laje original.
        Bounds b = malha.bounds;
        b.Expand(new Vector3(0f, 3f, 0f));
        malha.bounds = b;
        return malha;
    }
}
""", 1),
])

# Grava só depois de todas as trocas conferirem.
for caminho, saida in pendentes:
    destino_backup = os.path.join(backup, "v1_afundar_no_chao", os.path.basename(caminho) + ".bak")
    shutil.copy2(caminho, destino_backup)
    open(caminho, "wb").write(saida)
    print("ok", os.path.relpath(caminho, raiz))
