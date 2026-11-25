import sys, os, shutil

raiz = sys.argv[1]
scripts = os.path.join(raiz, "Assets", "Scripts")
backup = os.path.join(raiz, "_backup_agachamento", "v4_antes_dos_muros")
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


editar("ContornoObstaculo.cs", [
    ("""    /// <summary>Hexágono com a base inteira reta, rente ao chão: paredes de cor.</summary>
    HexagonoBaseReta
}""", """    /// <summary>Hexágono com a base inteira reta, rente ao chão: paredes que só dá para desviar.</summary>
    HexagonoBaseReta,
    /// <summary>Pentágono com a base inteira reta, rente ao chão: obstáculo móvel.</summary>
    PentagonoBaseReta
}""", 1),
    ("""            default:
                return Regular(lados);""", """            case FormaObstaculo.PentagonoBaseReta:
                return new[]
                {
                    new Vector3(0f, 0.5f, 0f), new Vector3(-0.5f, 0.05f, 0f),
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.05f, 0f)
                };
            default:
                return Regular(lados);""", 1),
])

editar("PolyhedronVisual.cs", [
    ("""    static Mesh Malha(FormaObstaculo forma, int lados)
    {""", """    /// <summary>Malha compartilhada do prisma (contorno de -0,5 a 0,5 puxado em Z).</summary>
    public static Mesh MalhaPrisma(FormaObstaculo forma, int lados) => Malha(forma, lados);

    static Mesh Malha(FormaObstaculo forma, int lados)
    {""", 1),
])

editar("ObstacleSpawner.cs", [
    ("""        FormaObstaculo forma = tipo == Obst.Barra ? FormaObstaculo.Quadrado : FormaObstaculo.Regular;
""", """        // Barra: quadrado (passa por baixo). Alto: base inteira no chão (só desviando).
        FormaObstaculo forma = tipo == Obst.Barra ? FormaObstaculo.Quadrado
            : (tipo == Obst.Alto ? FormaObstaculo.HexagonoBaseReta : FormaObstaculo.Regular);
""", 1),
    ("""            tamanho, 5, corMovel), TipoItem.Obstaculo, tamanho);
""", """            tamanho, 5, corMovel, FormaObstaculo.PentagonoBaseReta), TipoItem.Obstaculo, tamanho);
""", 1),
    ("""    /// <summary>
    /// Barreira luminosa em todas as faixas. Só passa quem estiver da mesma cor.
""", """    /// <summary>
    /// Muro de tijolos em todas as faixas. Só passa quem estiver da mesma cor.
""", 1),
    ("""        Vector3 tamanho = new Vector3(jogador.larguraFaixa, 3.2f, 0.3f);
""", """        Vector3 tamanho = new Vector3(jogador.larguraFaixa, 3.2f, 0.45f);
""", 1),
])

for nome in ("ContornoObstaculo.cs", "PolyhedronVisual.cs", "ObstacleSpawner.cs", "ColorPortalVisual.cs"):
    shutil.copy2(os.path.join(scripts, nome), os.path.join(backup, nome + ".bak"))
for caminho, saida in pendentes:
    open(caminho, "wb").write(saida)
    print("ok", os.path.basename(caminho))
