using UnityEngine;

/// <summary>Revestimentos compartilhados de liga escovada e cerâmica, com painéis e emissores.</summary>
public static class TechObstacleMaterials
{
    static readonly Material[] materiais = new Material[2];
    static readonly Material[] bordas = new Material[2];

    public static Material Obter(bool ceramica)
    {
        int indice = ceramica ? 1 : 0;
        if (materiais[indice] != null) return materiais[indice];
        const int lado = 256;
        var pixels = new Color[lado * lado];
        var brilho = new Color[pixels.Length];
        var alturas = new float[pixels.Length];
        Color baseCor = ceramica ? new Color(0.76f, 0.80f, 0.85f) : new Color(0.20f, 0.24f, 0.30f);
        for (int y = 0; y < lado; y++)
        for (int x = 0; x < lado; x++)
        {
            float u = (x + 0.5f) / lado, v = (y + 0.5f) / lado;
            float margem = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            bool junta = margem < 0.035f || Mathf.Abs(v - (0.61f + (u - 0.5f) * 0.13f)) < 0.009f;
            bool emissor = (Mathf.Abs(u - 0.075f) < 0.008f || Mathf.Abs(u - 0.925f) < 0.008f) && v > 0.28f && v < 0.52f;
            emissor |= u > 0.36f && u < 0.64f && Mathf.Abs(v - 0.925f) < 0.008f;
            float ruido = Mathf.PerlinNoise(u * 170f + 3f, v * 30f + 11f);
            float escovado = Mathf.Sin(v * 1300f) * 0.012f + (ruido - 0.5f) * 0.035f;
            Color cor = baseCor + Color.white * (ceramica ? (ruido - 0.5f) * 0.014f : escovado);
            float altura = 0.5f;
            if (margem > 0.035f && margem < 0.047f) { cor *= 1.15f; altura = 0.6f; }
            if (junta) { cor = new Color(0.025f, 0.034f, 0.05f); altura = 0.15f; }
            // Rebites discretos nos quatro cantos da placa.
            float du = Mathf.Min(Mathf.Abs(u - 0.12f), Mathf.Abs(u - 0.88f));
            float dv = Mathf.Min(Mathf.Abs(v - 0.12f), Mathf.Abs(v - 0.88f));
            float dist = Mathf.Sqrt(du * du + dv * dv);
            if (dist < 0.018f)
            {
                cor = dist > 0.012f ? new Color(0.4f, 0.46f, 0.54f) : new Color(0.055f, 0.07f, 0.09f);
                altura = dist > 0.012f ? 0.7f : 0.3f;
            }
            if (emissor) { cor = Color.white; altura = 0.4f; }
            int p = y * lado + x;
            pixels[p] = cor;
            brilho[p] = emissor ? Color.white : Color.white * 0.24f;
            alturas[p] = altura;
        }
        var normais = new Color[pixels.Length];
        for (int y = 0; y < lado; y++)
        for (int x = 0; x < lado; x++)
        {
            float dx = alturas[y * lado + Mathf.Min(lado - 1, x + 1)] - alturas[y * lado + Mathf.Max(0, x - 1)];
            float dy = alturas[Mathf.Min(lado - 1, y + 1) * lado + x] - alturas[Mathf.Max(0, y - 1) * lado + x];
            Vector3 n = new Vector3(-dx * 1.5f, -dy * 1.5f, 1f).normalized;
            normais[y * lado + x] = new Color(1f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, n.x * 0.5f + 0.5f);
        }
                Shader shaderCorpo = Shader.Find("Universal Render Pipeline/Lit");
        if (shaderCorpo == null) shaderCorpo = Shader.Find("Standard");
        Material material = new Material(shaderCorpo);
        material.color = Color.white;
        material.SetFloat("_Metallic", ceramica ? 0.10f : 0.55f);
        material.SetOverrideTag("RenderType", "Opaque");
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_AlphaClip", 0f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        material.SetInt("_ZWrite", 1);
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        material.name = ceramica ? "Ceramica tecnologica clara" : "Liga de nave grafite escovada";
        Texture2D albedo = Textura(pixels, lado, false, "Paineis de revestimento");
        material.mainTexture = albedo;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
        material.SetTexture("_BumpMap", Textura(normais, lado, true, "Relevo de juntas e rebites"));
        material.SetFloat("_BumpScale", 0.65f);
        material.EnableKeyword("_NORMALMAP");
        material.SetFloat("_Smoothness", ceramica ? 0.48f : 0.68f);
        material.SetTexture("_EmissionMap", Textura(brilho, lado, false, "Emissores dos paineis"));
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", ceramica ? new Color(0.08f, 0.35f, 1f) * 2f : new Color(0.02f, 0.85f, 1f) * 1.6f);
        materiais[indice] = material;
        return material;
    }

    static Texture2D Textura(Color[] pixels, int lado, bool linear, string nome)
    {
        var textura = new Texture2D(lado, lado, TextureFormat.RGBA32, true, linear) { name = nome };
        textura.wrapMode = TextureWrapMode.Clamp;
        textura.filterMode = FilterMode.Trilinear;
        textura.SetPixels(pixels);
        textura.Apply(true, true);
        return textura;
    }

    public static Material Borda(bool ceramica)
    {
        int i = ceramica ? 1 : 0;
        if (bordas[i] == null)
        {
            bordas[i] = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            bordas[i].color = ceramica ? new Color(0.08f, 0.35f, 1f) * 3.4f : new Color(0.03f, 0.85f, 1f) * 3.2f;
        }
        return bordas[i];
    }
}
