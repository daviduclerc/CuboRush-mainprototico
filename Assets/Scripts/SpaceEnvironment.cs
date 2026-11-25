using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Cenário distante do jogo: planetas texturizados, luas, nebulosas, galáxias
/// e pequenos campos de asteroides. Tudo acompanha a câmera como pano de fundo.
/// </summary>
public class SpaceEnvironment : MonoBehaviour
{
    public Transform jogador;

    readonly List<Transform> planetas = new List<Transform>();
    readonly List<Transform> galpaoAsteroides = new List<Transform>();
    static readonly Dictionary<string, Texture2D> mapas = new Dictionary<string, Texture2D>();

    // Céu profundo com estrelas e nebulosas roxas, azuis e magenta, mais densas
    // em volta do buraco negro (estilo da arte de referência).
    static Texture2D ceuEmCache;

    public static Texture2D CriarCeuRealista()
    {
        // O céu é gerado uma vez e reaproveitado a cada reinício da partida.
        if (ceuEmCache != null) return ceuEmCache;

        // Resolução alta + filtro bilinear: cada estrela vira um ponto redondo e nítido
        // em vez dos quadradinhos que apareciam com a textura pequena ampliada.
        const int largura = 4096, altura = 2048;
        Texture2D tex = new Texture2D(largura, altura, TextureFormat.RGBA32, false);
        tex.name = "CeuEspacialProfundo";
        tex.wrapModeU = TextureWrapMode.Repeat;
        tex.wrapModeV = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
        Color32[] pixels = new Color32[largura * altura];
        Color32 vazio = new Color32(0, 1, 2, 255);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = vazio;

        // Estrelas pequenas e espaçadas; o contraste vem do buraco negro e da pista.
        // Cores pela temperatura: azuladas, brancas, amareladas e algumas alaranjadas.
        Color[] temperaturas =
        {
            new Color(0.70f, 0.80f, 1f), new Color(0.82f, 0.88f, 1f), new Color(1f, 0.97f, 0.92f),
            new Color(1f, 0.90f, 0.74f), new Color(1f, 0.76f, 0.55f)
        };
        System.Random sorteio = new System.Random(24681);
        float Sorteio(float min, float max) => min + (max - min) * (float)sorteio.NextDouble();

        for (int i = 0; i < 3200; i++)
        {
            float x = Sorteio(0f, largura), y = Sorteio(0f, altura);
            float chance = Sorteio(0f, 1f);
            Color cor = temperaturas[(int)Mathf.Min(temperaturas.Length - 1, Mathf.Pow(Sorteio(0f, 1f), 1.6f) * temperaturas.Length)];
            if (i % 3 == 0) cor = temperaturas[1];
            if (chance < 0.012f)
            {
                // Poucas estrelas brilhantes, com um halo discreto.
                Pincelar(pixels, largura, altura, x, y, Sorteio(2.6f, 3.6f), cor * 0.10f);
                Pincelar(pixels, largura, altura, x, y, Sorteio(0.85f, 1.05f), cor * Sorteio(0.85f, 1f));
            }
            else if (chance < 0.12f)
            {
                Pincelar(pixels, largura, altura, x, y, Sorteio(0.7f, 0.9f), cor * Sorteio(0.45f, 0.75f));
            }
            else
            {
                float brilho = Mathf.Lerp(0.12f, 0.42f, Sorteio(0f, 1f) * Sorteio(0f, 1f));
                Pincelar(pixels, largura, altura, x, y, Sorteio(0.5f, 0.68f), cor * brilho);
            }
        }

        PintarNebulosas(pixels, largura, altura);

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        ceuEmCache = tex;
        return tex;
    }

    /// <summary>
    /// Soma nebulosas ao céu panorâmico. O ruído é calculado numa grade menor, pela direção
    /// 3D de cada pixel (sem emenda e sem esticar nos polos), e ampliado com filtro bilinear.
    /// </summary>
    static void PintarNebulosas(Color32[] pixels, int largura, int altura)
    {
        const int lb = 768, ab = 384;
        var nebulosa = new Color[lb * ab];
        // O buraco negro fica à frente (+Z), logo acima do horizonte.
        Vector3 frente = new Vector3(0f, 0.08f, 1f).normalized;
        Color roxa = new Color(0.50f, 0.13f, 0.78f);
        Color azul = new Color(0.07f, 0.20f, 0.80f);
        Color magenta = new Color(0.80f, 0.14f, 0.45f);
        Color laranja = new Color(0.95f, 0.42f, 0.12f);

        for (int y = 0; y < ab; y++)
        {
            float latitude = (1f - (y + 0.5f) / ab) * Mathf.PI;
            float sl = Mathf.Sin(latitude), cl = Mathf.Cos(latitude);
            for (int x = 0; x < lb; x++)
            {
                // Mesmo mapeamento do Skybox/Panoramic: u = 0,5 - longitude / 2π.
                float longitude = (0.5f - (x + 0.5f) / lb) * 2f * Mathf.PI;
                Vector3 d = new Vector3(sl * Mathf.Cos(longitude), cl, sl * Mathf.Sin(longitude));

                float n1 = Fbm(d * 2.2f + new Vector3(11f, 3f, 7f));
                float n2 = Fbm(d * 4.6f + new Vector3(2f, 19f, 5f));
                float proximo = Mathf.Max(0f, Vector3.Dot(d, frente));
                float nuvem = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.30f, 0.80f, n1));
                // Faixas escuras de poeira cortando a nebulosa.
                nuvem *= Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.65f, n2)));
                float forca = 1.3f * nuvem * (0.35f + 0.9f * proximo * proximo);
                // Abaixo do horizonte a nebulosa some aos poucos (a pista ocupa essa parte).
                forca *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.55f, 0.05f, d.y));

                Color cor = Color.Lerp(azul, roxa, Mathf.Clamp01(n2 * 1.8f - 0.35f));
                cor = Color.Lerp(cor, magenta, Mathf.Clamp01((n1 - 0.62f) * 4f) * 0.7f);
                // Brilho quente do disco escapando em volta do buraco negro.
                cor = Color.Lerp(cor, laranja, Mathf.Pow(proximo, 24f) * 0.6f);
                nebulosa[y * lb + x] = cor * forca;
            }
        }

        for (int y = 0; y < altura; y++)
        {
            float fy = (y + 0.5f) / altura * ab - 0.5f;
            int y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, ab - 1), y1 = Mathf.Min(y0 + 1, ab - 1);
            float ty = Mathf.Clamp01(fy - y0);
            for (int x = 0; x < largura; x++)
            {
                float fx = (x + 0.5f) / largura * lb - 0.5f;
                int x0 = Mathf.FloorToInt(fx);
                float tx = fx - x0;
                int xa = (x0 % lb + lb) % lb, xb = (xa + 1) % lb;
                Color c = Color.Lerp(
                    Color.Lerp(nebulosa[y0 * lb + xa], nebulosa[y0 * lb + xb], tx),
                    Color.Lerp(nebulosa[y1 * lb + xa], nebulosa[y1 * lb + xb], tx), ty);
                int i = y * largura + x;
                Color32 atual = pixels[i];
                atual.r = (byte)Mathf.Min(255f, atual.r + c.r * 255f);
                atual.g = (byte)Mathf.Min(255f, atual.g + c.g * 255f);
                atual.b = (byte)Mathf.Min(255f, atual.b + c.b * 255f);
                pixels[i] = atual;
            }
        }
    }

    /// <summary>Ruído fractal 3D (soma de ruídos de Perlin nos três planos).</summary>
    static float Fbm(Vector3 p)
    {
        float v = 0f, a = 0.5f;
        for (int k = 0; k < 4; k++)
        {
            v += a * (Mathf.PerlinNoise(p.x, p.y) + Mathf.PerlinNoise(p.y + 31.7f, p.z + 12.3f) +
                      Mathf.PerlinNoise(p.z + 57.1f, p.x + 91.9f)) / 3f;
            p = p * 2.03f + new Vector3(1.7f, 9.2f, 4.1f);
            a *= 0.5f;
        }
        // A média de vários Perlin fica concentrada perto de 0,47; espalha para 0..1.
        return Mathf.Clamp01(0.5f + (v - 0.465f) * 3.5f);
    }

    /// <summary>Soma um ponto de luz gaussiano na textura do céu (com volta horizontal).</summary>
    static void Pincelar(Color32[] pixels, int largura, int altura, float cx, float cy, float sigma, Color cor)
    {
        int raio = Mathf.CeilToInt(sigma * 3f);
        float inverso = 1f / (2f * sigma * sigma);
        int x0 = Mathf.FloorToInt(cx), y0 = Mathf.FloorToInt(cy);
        for (int dy = -raio; dy <= raio; dy++)
        {
            int y = y0 + dy;
            if (y < 0 || y >= altura) continue;
            float ddy = y + 0.5f - cy;
            for (int dx = -raio; dx <= raio; dx++)
            {
                float ddx = x0 + dx + 0.5f - cx;
                float k = Mathf.Exp(-(ddx * ddx + ddy * ddy) * inverso);
                if (k < 0.01f) continue;
                int x = ((x0 + dx) % largura + largura) % largura;
                int indice = y * largura + x;
                Color32 atual = pixels[indice];
                atual.r = (byte)Mathf.Min(255f, atual.r + cor.r * k * 255f);
                atual.g = (byte)Mathf.Min(255f, atual.g + cor.g * k * 255f);
                atual.b = (byte)Mathf.Min(255f, atual.b + cor.b * k * 255f);
                pixels[indice] = atual;
            }
        }
    }

    void Update()
    {
        if (jogador != null) AtualizarOrigem();
        for (int i = 0; i < planetas.Count; i++)
            planetas[i].Rotate(Vector3.up, (i % 2 == 0 ? 1f : -0.55f) * Time.deltaTime, Space.Self);
        for (int i = 0; i < galpaoAsteroides.Count; i++)
            galpaoAsteroides[i].Rotate(new Vector3(0.3f, 1f, 0.2f), (i % 2 == 0 ? 0.8f : -0.6f) * Time.deltaTime, Space.Self);
    }

    void AtualizarOrigem()
    {
        if (jogador == null) return;
        transform.position = new Vector3(jogador.position.x * 0.08f, 0f, jogador.position.z);
    }

    void CriarPlaneta(string nome, string rotulo, float raio, Vector3 posicao, string tipo, bool nuvens)
    {
        GameObject planeta = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        planeta.name = nome;
        planeta.transform.SetParent(transform, false);
        planeta.transform.localPosition = posicao;
        planeta.transform.localScale = Vector3.one * (raio * 2f);
        Collider colisor = planeta.GetComponent<Collider>();
        if (colisor != null) Destroy(colisor);

        Material superficie = NovoMaterialLit();
        Texture2D mapa = ObterMapa(tipo);
        superficie.mainTexture = mapa;
        if (superficie.HasProperty("_BaseMap")) superficie.SetTexture("_BaseMap", mapa);
        if (superficie.HasProperty("_Metallic")) superficie.SetFloat("_Metallic", 0.04f);
        if (superficie.HasProperty("_Smoothness")) superficie.SetFloat("_Smoothness", tipo == "gas" ? 0.36f : 0.18f);
        planeta.GetComponent<Renderer>().sharedMaterial = superficie;
        planetas.Add(planeta.transform);

        CriarAtmosfera(planeta.transform, raio, tipo == "ocean" ? new Color(0.20f, 0.52f, 1f) :
            (tipo == "ice" ? new Color(0.48f, 0.75f, 1f) : new Color(1f, 0.40f, 0.18f)));
        if (nuvens) CriarNuvens(planeta.transform, raio);

        // Os mapas equiretangulares da NASA fazem o relevo do planeta, sem
        // nebulosas opacas em placas planas sobrepondo o céu.

        planeta.transform.Rotate(new Vector3(18f, 32f, -12f), 27f, Space.Self);
        planeta.name = rotulo;
    }

    Material NovoMaterialLit()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        return new Material(shader);
    }

    void CriarAtmosfera(Transform planeta, float raio, Color cor)
    {
        GameObject casca = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        casca.name = "Halo atmosferico";
        casca.transform.SetParent(planeta, false);
        casca.transform.localPosition = Vector3.zero;
        casca.transform.localScale = Vector3.one * 1.045f;
        Collider c = casca.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        Material mat = new Material(shader);
        ConfigurarTransparencia(mat, true);
        mat.color = new Color(cor.r, cor.g, cor.b, 0.34f);
        casca.GetComponent<Renderer>().sharedMaterial = mat;
    }

    void CriarNuvens(Transform planeta, float raio)
    {
        GameObject camada = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        camada.name = "Camada de nuvens";
        camada.transform.SetParent(planeta, false);
        camada.transform.localPosition = Vector3.zero;
        camada.transform.localScale = Vector3.one * 1.009f;
        Collider c = camada.GetComponent<Collider>();
        if (c != null) Destroy(c);
        Material mat = NovoMaterialLit();
        Texture2D nuvem = CriarMapaNuvens();
        mat.mainTexture = nuvem;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", nuvem);
        mat.color = new Color(0.72f, 0.82f, 1f, 0.35f);
        ConfigurarTransparencia(mat, false);
        camada.GetComponent<Renderer>().sharedMaterial = mat;
    }

    Texture2D CriarMapaNuvens()
    {
        const int w = 256, h = 128;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a = Mathf.PerlinNoise(x * 0.035f, y * 0.055f) * 0.7f + Mathf.PerlinNoise(x * 0.09f, y * 0.13f) * 0.3f;
                float alpha = Mathf.SmoothStep(0.58f, 0.78f, a) * 0.48f;
                px[y * w + x] = new Color(0.75f + a * 0.2f, 0.83f + a * 0.16f, 1f, alpha);
            }
        tex.SetPixels(px); tex.Apply(true, true); return tex;
    }

    void CriarAneis(Transform planeta)
    {
        GameObject anel = new GameObject("Aneis de gelo e poeira");
        anel.transform.SetParent(planeta, false);
        anel.transform.localRotation = Quaternion.Euler(73f, 0f, 13f);
        MeshFilter mf = anel.AddComponent<MeshFilter>();
        mf.sharedMesh = MalhaAnel();
        MeshRenderer mr = anel.AddComponent<MeshRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Material mat = new Material(shader);
        ConfigurarTransparencia(mat, false);
        Texture2D mapa = MapaAneis();
        mat.mainTexture = mapa;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mapa);
        mr.sharedMaterial = mat;
    }

    Mesh MalhaAnel()
    {
        const int radial = 96, angular = 128;
        Vector3[] v = new Vector3[(radial + 1) * (angular + 1)];
        Vector2[] uv = new Vector2[v.Length];
        int[] tri = new int[radial * angular * 6];
        int k = 0, t = 0;
        for (int r = 0; r <= radial; r++)
        {
            float u = r / (float)radial;
            float radius = Mathf.Lerp(0.67f, 1.18f, u);
            for (int a = 0; a <= angular; a++)
            {
                float angle = a * Mathf.PI * 2f / angular;
                v[k] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                uv[k] = new Vector2(u, a / (float)angular);
                if (r < radial && a < angular)
                {
                    int n = k + angular + 1;
                    tri[t++] = k; tri[t++] = k + 1; tri[t++] = n;
                    tri[t++] = k + 1; tri[t++] = n + 1; tri[t++] = n;
                }
                k++;
            }
        }
        Mesh mesh = new Mesh { name = "Disco de aneis" };
        mesh.vertices = v; mesh.uv = uv; mesh.triangles = tri;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    Texture2D MapaAneis()
    {
        const int w = 256, h = 8;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[w * h];
        for (int x = 0; x < w; x++)
        {
            float u = x / (float)(w - 1);
            float band = 0.42f + 0.22f * Mathf.Sin(u * 118f) + 0.12f * Mathf.Sin(u * 347f);
            float gap = (u > 0.43f && u < 0.49f) || (u > 0.73f && u < 0.755f) ? 0.10f : 1f;
            Color c = Color.Lerp(new Color(0.24f, 0.19f, 0.14f), new Color(0.76f, 0.66f, 0.52f), band);
            c.a = gap * (0.52f + band * 0.42f);
            for (int y = 0; y < h; y++) px[y * w + x] = c;
        }
        tex.SetPixels(px); tex.Apply(true, true); return tex;
    }

    void ConfigurarTransparencia(Material mat, bool somenteCasca)
    {
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", somenteCasca ? (float)CullMode.Front : (float)CullMode.Off);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
    }

    Texture2D ObterMapa(string tipo)
    {
        if (mapas.TryGetValue(tipo, out Texture2D existente)) return existente;
        string recurso = tipo == "ocean" ? "Space/earth_nasa" : (tipo == "gas" ? "Space/jupiter_nasa" : null);
        if (!string.IsNullOrEmpty(recurso))
        {
            Texture2D mapaNASA = Resources.Load<Texture2D>(recurso);
            if (mapaNASA != null)
            {
                mapas[tipo] = mapaNASA;
                return mapaNASA;
            }
        }
        const int w = 512, h = 256;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        Color[] px = new Color[w * h];
        float seed = tipo == "ocean" ? 11f : tipo == "gas" ? 27f : tipo == "ice" ? 43f : 61f;
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)h;
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w;
                float n = Mathf.PerlinNoise(u * 7f + seed, v * 5f + seed) * 0.56f +
                          Mathf.PerlinNoise(u * 19f + seed, v * 13f + seed) * 0.29f +
                          Mathf.PerlinNoise(u * 43f + seed, v * 31f + seed) * 0.15f;
                Color c;
                if (tipo == "gas")
                {
                    float bands = Mathf.Sin(v * 112f + Mathf.PerlinNoise(u * 3f, v * 7f) * 4f);
                    float t = Mathf.InverseLerp(-0.8f, 0.8f, bands + (n - 0.5f) * 0.7f);
                    c = Color.Lerp(new Color(0.30f, 0.16f, 0.10f), new Color(0.82f, 0.61f, 0.36f), t);
                    float storm = Mathf.Exp(-((u - 0.70f) * (u - 0.70f) * 90f + (v - 0.59f) * (v - 0.59f) * 600f));
                    c = Color.Lerp(c, new Color(0.72f, 0.31f, 0.16f), storm * 0.9f);
                }
                else if (tipo == "ocean")
                {
                    float land = Mathf.SmoothStep(0.48f, 0.58f, n);
                    c = Color.Lerp(new Color(0.018f, 0.085f, 0.25f), new Color(0.16f, 0.30f, 0.13f), land);
                    c = Color.Lerp(c, new Color(0.44f, 0.33f, 0.17f), Mathf.Clamp01((n - 0.68f) * 3f));
                    float polar = Mathf.SmoothStep(0.88f, 0.99f, Mathf.Abs(v - 0.5f) * 2f);
                    c = Color.Lerp(c, new Color(0.72f, 0.82f, 0.9f), polar * 0.85f);
                    float cloud = Mathf.PerlinNoise(u * 24f + 9f, v * 20f + 2f);
                    c = Color.Lerp(c, new Color(0.72f, 0.80f, 0.87f), Mathf.SmoothStep(0.67f, 0.78f, cloud) * 0.52f);
                }
                else if (tipo == "ice")
                {
                    float ice = Mathf.SmoothStep(0.40f, 0.67f, n);
                    c = Color.Lerp(new Color(0.035f, 0.12f, 0.24f), new Color(0.66f, 0.79f, 0.88f), ice);
                    c *= 0.82f + n * 0.28f;
                }
                else
                {
                    c = Color.Lerp(new Color(0.13f, 0.035f, 0.018f), new Color(0.68f, 0.29f, 0.11f), n);
                    float canyon = Mathf.PerlinNoise(u * 36f + 5f, v * 8f + 9f);
                    c = Color.Lerp(c, new Color(0.22f, 0.12f, 0.08f), Mathf.SmoothStep(0.62f, 0.78f, canyon) * 0.55f);
                }
                px[y * w + x] = c;
            }
        }
        tex.SetPixels(px); tex.Apply(true, true);
        mapas[tipo] = tex;
        return tex;
    }

    void CriarNebulosa(string nome, Vector3 posicao, Vector2 escala, int seed, Color cor)
    {
        Texture2D tex = new Texture2D(384, 192, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[384 * 192];
        for (int y = 0; y < 192; y++)
            for (int x = 0; x < 384; x++)
            {
                float u = x / 383f, v = y / 191f;
                float nx = (u - 0.5f) * 2f, ny = (v - 0.5f) * 2f;
                float radius = Mathf.Sqrt(nx * nx * 0.52f + ny * ny);
                float n = Mathf.PerlinNoise(u * 5f + seed, v * 6f + seed) * 0.58f +
                          Mathf.PerlinNoise(u * 15f + seed, v * 18f + seed) * 0.42f;
                float filamento = Mathf.SmoothStep(0.48f, 0.79f, n) * Mathf.SmoothStep(1.1f, 0.18f, radius);
                float a = filamento * 0.42f;
                Color c = Color.Lerp(cor * 0.45f, new Color(0.45f, 0.68f, 1f), n);
                px[y * 384 + x] = new Color(c.r, c.g, c.b, a);
            }
        tex.SetPixels(px); tex.Apply(true, true);
        GameObject plano = GameObject.CreatePrimitive(PrimitiveType.Quad);
        plano.name = nome;
        plano.transform.SetParent(transform, false);
        plano.transform.localPosition = posicao;
        plano.transform.localScale = new Vector3(escala.x, escala.y, 1f);
        Collider col = plano.GetComponent<Collider>(); if (col != null) Destroy(col);
        Material mat = NovoMaterialUnlit();
        DefinirTexturaUnlit(mat, tex);
        plano.GetComponent<Renderer>().sharedMaterial = mat;
    }

    void CriarGalaxia(string nome, Vector3 posicao, Vector2 escala, int seed)
    {
        const int w = 384, h = 192;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x / (float)(w - 1) - 0.5f) * 2f;
                float v = (y / (float)(h - 1) - 0.5f) * 2f;
                float r = Mathf.Sqrt(u * u + v * v * 2.8f);
                float angle = Mathf.Atan2(v * 1.7f, u);
                float arms = Mathf.Pow(Mathf.Clamp01(0.5f + 0.5f * Mathf.Cos(angle * 2.4f + r * 17f)), 5f);
                float noise = Mathf.PerlinNoise(x * 0.035f + seed, y * 0.035f + seed);
                float core = Mathf.Exp(-r * 7f);
                float alpha = Mathf.Clamp01(core * 0.85f + arms * Mathf.Exp(-r * 1.8f) * noise * 0.65f);
                Color c = Color.Lerp(new Color(0.14f, 0.23f, 0.62f), new Color(0.82f, 0.54f, 0.35f), core);
                px[y * w + x] = new Color(c.r, c.g, c.b, alpha * Mathf.SmoothStep(1.15f, 0.08f, r));
            }
        tex.SetPixels(px); tex.Apply(true, true);
        GameObject plano = GameObject.CreatePrimitive(PrimitiveType.Quad);
        plano.name = nome;
        plano.transform.SetParent(transform, false);
        plano.transform.localPosition = posicao;
        plano.transform.localScale = new Vector3(escala.x, escala.y, 1f);
        Collider col = plano.GetComponent<Collider>(); if (col != null) Destroy(col);
        Material mat = NovoMaterialUnlit();
        DefinirTexturaUnlit(mat, tex);
        plano.GetComponent<Renderer>().sharedMaterial = mat;
    }

    Material NovoMaterialUnlit()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        Material mat = new Material(shader);
        ConfigurarTransparencia(mat, false);
        return mat;
    }

    void DefinirTexturaUnlit(Material mat, Texture2D tex)
    {
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        mat.color = Color.white;
    }

    void CriarCampoDeAsteroides(Vector3 centro, int quantidade, float raioX, float raioZ, int seed)
    {
        Transform grupo = new GameObject("Campo de asteroides").transform;
        grupo.SetParent(transform, false);
        grupo.localPosition = centro;
        galpaoAsteroides.Add(grupo);
        Random.State estado = Random.state;
        Random.InitState(seed);
        for (int i = 0; i < quantidade; i++)
        {
            GameObject pedra = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pedra.name = "Asteroide";
            pedra.transform.SetParent(grupo, false);
            pedra.transform.localPosition = new Vector3(Random.Range(-raioX, raioX), Random.Range(-5f, 7f), Random.Range(-raioZ, raioZ));
            pedra.transform.localRotation = Random.rotation;
            pedra.transform.localScale = new Vector3(Random.Range(0.35f, 1.2f), Random.Range(0.3f, 0.9f), Random.Range(0.4f, 1.1f));
            Collider col = pedra.GetComponent<Collider>(); if (col != null) Destroy(col);
            pedra.GetComponent<Renderer>().sharedMaterial = Visual.MatMetal(new Color(0.18f, 0.17f, 0.16f), 0.32f);
        }
        Random.state = estado;
    }
}
