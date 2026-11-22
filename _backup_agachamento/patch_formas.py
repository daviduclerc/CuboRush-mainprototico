import sys, os, shutil

raiz = sys.argv[1]
scripts = os.path.join(raiz, "Assets", "Scripts")
backup = os.path.join(raiz, "_backup_agachamento", "v3_antes_das_formas")
os.makedirs(backup, exist_ok=True)
pendentes = []


def editar(nome, trocas):
    caminho = os.path.join(scripts, nome)
    dados = open(caminho, "rb").read()
    bom = dados.startswith(b"\xef\xbb\xbf")
    texto = dados.decode("utf-8-sig").replace("\r\n", "\n")
    for antigo, novo, vezes in trocas:
        achados = texto.count(antigo)
        if achados != vezes:
            raise SystemExit(f"{nome}: esperava {vezes}x, achei {achados}x de:\n{antigo}")
        texto = texto.replace(antigo, novo)
    saida = texto.replace("\n", "\r\n").encode("utf-8")
    pendentes.append((caminho, (b"\xef\xbb\xbf" if bom else b"") + saida))


editar("ColorPortalVisual.cs", [
    ("""        Vector3[] pontos = Contorno();
""", """        // Base inteira reta, rente ao chão: fica claro que não dá para passar por baixo.
        Vector3[] pontos = ContornoObstaculo.Obter(FormaObstaculo.HexagonoBaseReta, 6);
""", 1),
    ("""        objeto.AddComponent<PolyhedronHitShape>().Inicializar(6);
""", """        objeto.AddComponent<PolyhedronHitShape>().Inicializar(6, FormaObstaculo.HexagonoBaseReta);
""", 1),
    ("""    static Vector3[] Contorno()
    {
        var pontos = new Vector3[6];
        Vector2 minimo = Vector2.one * float.PositiveInfinity;
        Vector2 maximo = Vector2.one * float.NegativeInfinity;
        for (int i = 0; i < 6; i++)
        {
            float a = (90f + i * 60f) * Mathf.Deg2Rad;
            pontos[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            minimo = Vector2.Min(minimo, pontos[i]); maximo = Vector2.Max(maximo, pontos[i]);
        }
        for (int i = 0; i < 6; i++)
            pontos[i] = new Vector3((pontos[i].x - minimo.x) / (maximo.x - minimo.x) - 0.5f,
                (pontos[i].y - minimo.y) / (maximo.y - minimo.y) - 0.5f, 0f);
        return pontos;
    }

""", "", 1),
])

editar("ObstacleSpawner.cs", [
    ("""            case Obst.Barra:
                // Painel suspenso: vão de 0,65 embaixo para passar deslizando.
                tamanho = new Vector3(largura, 2.2f, 0.6f);
                centroY = 0.65f + 1.1f;
""", """            case Obst.Barra:
                // Quadrado suspenso: vão de 0,65 embaixo para passar deslizando.
                // A base reta e o vão aberto deixam claro que dá para passar por baixo.
                tamanho = new Vector3(largura, largura, 0.6f);
                centroY = 0.65f + largura / 2f;
""", 1),
    ("""        GameObject obj = PolyhedronVisual.Criar("Poliedro" + tipo, transform,
            new Vector3(x, centroY, z), tamanho, faces, cor);
""", """        FormaObstaculo forma = tipo == Obst.Barra ? FormaObstaculo.Quadrado : FormaObstaculo.Regular;
        GameObject obj = PolyhedronVisual.Criar("Poliedro" + tipo, transform,
            new Vector3(x, centroY, z), tamanho, faces, cor, forma);
""", 1),
])

for nome in ("PolyhedronVisual.cs", "PolyhedronHitShape.cs", "ColorPortalVisual.cs", "ObstacleSpawner.cs"):
    shutil.copy2(os.path.join(scripts, nome), os.path.join(backup, nome + ".bak"))
for caminho, saida in pendentes:
    open(caminho, "wb").write(saida)
    print("ok", os.path.basename(caminho))
