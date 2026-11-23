using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gera malhas low poly procedurais: asteroides e aglomerados de cristal.
/// Todas as malhas usam vértices não compartilhados para ter sombreamento facetado.
/// </summary>
public static class MalhasEstelares
{
    // ------------------------------------------------------------------
    // Construtor de malha
    // ------------------------------------------------------------------
    class Construtor
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<int> triangulos = new List<int>();

        public void Triangulo(Vector3 a, Vector3 b, Vector3 c, Vector3 paraFora)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, paraFora) < 0f)
            {
                Vector3 t = b; b = c; c = t;
            }
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            triangulos.Add(i); triangulos.Add(i + 1); triangulos.Add(i + 2);
        }

        public Mesh Gerar(string nome)
        {
            var m = new Mesh { name = nome };
            if (vertices.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(vertices);
            m.SetTriangles(triangulos, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }

    static float Ruido3D(Vector3 p)
    {
        float xy = Mathf.PerlinNoise(p.x, p.y);
        float yz = Mathf.PerlinNoise(p.y + 31.7f, p.z + 12.3f);
        float zx = Mathf.PerlinNoise(p.z + 57.1f, p.x + 91.9f);
        return (xy + yz + zx) / 3f;
    }

    // ------------------------------------------------------------------
    // Asteroide
    // ------------------------------------------------------------------
    public static Mesh CriarRocha(int semente, int subdivisoes = 2)
    {
        var rnd = new System.Random(semente);
        float R() => (float)rnd.NextDouble();

        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var v = new[]
        {
            new Vector3(-1,  t,  0), new Vector3( 1,  t,  0), new Vector3(-1, -t,  0), new Vector3( 1, -t,  0),
            new Vector3( 0, -1,  t), new Vector3( 0,  1,  t), new Vector3( 0, -1, -t), new Vector3( 0,  1, -t),
            new Vector3( t,  0, -1), new Vector3( t,  0,  1), new Vector3(-t,  0, -1), new Vector3(-t,  0,  1),
        };
        int[] f =
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
            1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
            4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };

        var tris = new List<Vector3[]>();
        for (int i = 0; i < f.Length; i += 3)
            tris.Add(new[] { v[f[i]].normalized, v[f[i + 1]].normalized, v[f[i + 2]].normalized });

        for (int s = 0; s < subdivisoes; s++)
        {
            var novos = new List<Vector3[]>(tris.Count * 4);
            foreach (var tri in tris)
            {
                Vector3 a = tri[0], b = tri[1], c = tri[2];
                Vector3 ab = (a + b).normalized, bc = (b + c).normalized, ca = (c + a).normalized;
                novos.Add(new[] { a, ab, ca });
                novos.Add(new[] { b, bc, ab });
                novos.Add(new[] { c, ca, bc });
                novos.Add(new[] { ab, bc, ca });
            }
            tris = novos;
        }

        Vector3 deslocRuido = new Vector3(R() * 100f, R() * 100f, R() * 100f);
        Vector3 achatamento = new Vector3(0.7f + R() * 0.6f, 0.55f + R() * 0.5f, 0.7f + R() * 0.6f);
        float amplitude = 0.25f + R() * 0.2f;

        Vector3 Deformar(Vector3 p)
        {
            float n1 = Ruido3D(p * 1.6f + deslocRuido);
            float n2 = Ruido3D(p * 4.2f + deslocRuido * 1.7f);
            float raio = 1f + (n1 - 0.5f) * 2f * amplitude + (n2 - 0.5f) * amplitude * 0.5f;
            return Vector3.Scale(p * raio, achatamento) * 0.5f;
        }

        var c2 = new Construtor();
        foreach (var tri in tris)
        {
            Vector3 a = Deformar(tri[0]), b = Deformar(tri[1]), c = Deformar(tri[2]);
            c2.Triangulo(a, b, c, (a + b + c) / 3f);
        }
        return c2.Gerar("Asteroide_" + semente);
    }

    // ------------------------------------------------------------------
    // Aglomerado de cristais (cabe num cubo de -0.5 a 0.5)
    // ------------------------------------------------------------------
    public static Mesh CriarCristal(int semente, int lascas = 6)
    {
        var rnd = new System.Random(semente);
        float R() => (float)rnd.NextDouble();

        var c = new Construtor();
        const int lados = 6;

        for (int k = 0; k < lascas; k++)
        {
            bool central = k == 0;
            float altura = central ? 1.0f : 0.35f + R() * 0.45f;
            float raio = central ? 0.2f : 0.07f + R() * 0.08f;
            float ponta = altura * (0.25f + R() * 0.15f);

            float angBase = R() * Mathf.PI * 2f;
            float distBase = central ? 0f : 0.12f + R() * 0.15f;
            Vector3 baseC = new Vector3(Mathf.Cos(angBase) * distBase, 0f, Mathf.Sin(angBase) * distBase);

            float inclinacao = central ? R() * 8f : 20f + R() * 30f;
            Vector3 dirIncl = new Vector3(Mathf.Cos(angBase), 0f, Mathf.Sin(angBase));
            // Gira o "para cima" na direção de dirIncl (lascas abrem para fora)
            Vector3 eixo = Quaternion.AngleAxis(inclinacao, Vector3.Cross(Vector3.up, dirIncl).normalized) * Vector3.up;
            if (central) eixo = Quaternion.Euler(R() * 8f - 4f, 0f, R() * 8f - 4f) * Vector3.up;
            eixo.Normalize();

            Vector3 a = Vector3.Cross(eixo, Vector3.forward);
            if (a.sqrMagnitude < 0.001f) a = Vector3.Cross(eixo, Vector3.right);
            a.Normalize();
            Vector3 b = Vector3.Cross(eixo, a).normalized;

            float giro = R() * Mathf.PI * 2f;
            var anelBaixo = new Vector3[lados];
            var anelAlto = new Vector3[lados];
            for (int i = 0; i < lados; i++)
            {
                float th = giro + i * Mathf.PI * 2f / lados;
                float irregular = 0.85f + R() * 0.3f;
                Vector3 off = (a * Mathf.Cos(th) + b * Mathf.Sin(th)) * raio * irregular;
                anelBaixo[i] = baseC + off - eixo * 0.15f;
                anelAlto[i] = baseC + off * 0.92f + eixo * altura;
            }
            Vector3 topo = baseC + eixo * (altura + ponta);

            for (int i = 0; i < lados; i++)
            {
                int j = (i + 1) % lados;
                Vector3 meio = (anelBaixo[i] + anelBaixo[j] + anelAlto[i] + anelAlto[j]) * 0.25f;
                Vector3 fora = meio - (baseC + eixo * Vector3.Dot(meio - baseC, eixo));
                c.Triangulo(anelBaixo[i], anelBaixo[j], anelAlto[i], fora);
                c.Triangulo(anelBaixo[j], anelAlto[j], anelAlto[i], fora);

                Vector3 meioPonta = (anelAlto[i] + anelAlto[j] + topo) / 3f;
                Vector3 foraPonta = meioPonta - (baseC + eixo * Vector3.Dot(meioPonta - baseC, eixo)) + eixo * 0.05f;
                c.Triangulo(anelAlto[i], anelAlto[j], topo, foraPonta);
            }
        }

        var malha = c.Gerar("Cristal_" + semente);

        // Normaliza para caber no cubo unitário centrado na origem
        Bounds bb = malha.bounds;
        Vector3 tam = bb.size;
        var verts = new List<Vector3>();
        malha.GetVertices(verts);
        for (int i = 0; i < verts.Count; i++)
        {
            Vector3 p = verts[i] - bb.center;
            verts[i] = new Vector3(p.x / Mathf.Max(tam.x, 0.001f), p.y / Mathf.Max(tam.y, 0.001f), p.z / Mathf.Max(tam.z, 0.001f));
        }
        malha.SetVertices(verts);
        malha.RecalculateNormals();
        malha.RecalculateBounds();
        return malha;
    }
}
