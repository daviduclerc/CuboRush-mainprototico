using UnityEngine;

/// <summary>
/// Recursos visuais compartilhados pela interface (HUD e telas de menu, pausa e fim):
/// fonte, cores e texturas arredondadas geradas por código.
/// </summary>
public static class EstiloUI
{
    public static readonly Color Ciano = new Color(0.35f, 0.92f, 1f);
    public static readonly Color Verde = new Color(0.45f, 1f, 0.62f);
    public static readonly Color Rosa = new Color(1f, 0.32f, 0.78f);
    public static readonly Color Ouro = new Color(1f, 0.80f, 0.25f);
    public static readonly Color Laranja = new Color(1f, 0.55f, 0.16f);
    public static readonly Color Perigo = new Color(1f, 0.22f, 0.28f);
    public static readonly Color TextoSuave = new Color(0.62f, 0.70f, 0.84f);
    public static readonly Color FundoPainel = new Color(0.02f, 0.03f, 0.07f, 0.80f);

    static Font fonte;
    static Texture2D texturaArredondada, texturaContorno, texturaCirculo, texturaGradiente;
    static Sprite spriteArredondado, spriteContorno, spriteCirculo;

    /// <summary>Fonte do sistema com cara de painel de nave; cai para a fonte padrão se faltar.</summary>
    public static Font Fonte
    {
        get
        {
            if (fonte != null) return fonte;
            // Só fontes estáticas: fontes variáveis (como a Bahnschrift) não carregam no Unity.
            string[] preferidas = { "Segoe UI Semibold", "Segoe UI", "Roboto", "Helvetica Neue", "Arial" };
            string[] instaladas = null;
            try { instaladas = Font.GetOSInstalledFontNames(); } catch { }
            if (instaladas != null)
            {
                foreach (string nome in preferidas)
                {
                    if (System.Array.IndexOf(instaladas, nome) < 0) continue;
                    fonte = Font.CreateDynamicFontFromOSFont(nome, 32);
                    if (fonte != null) break;
                }
            }
            if (fonte == null) fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return fonte;
        }
    }

    /// <summary>Retângulo cheio de cantos arredondados (64 px, borda de 9-slice = 20).</summary>
    public static Texture2D TexturaArredondada
    {
        get
        {
            if (texturaArredondada == null) texturaArredondada = CriarArredondada(64, 18f, 0f);
            return texturaArredondada;
        }
    }

    /// <summary>Somente o contorno arredondado, para molduras neon.</summary>
    public static Texture2D TexturaContorno
    {
        get
        {
            if (texturaContorno == null) texturaContorno = CriarArredondada(64, 18f, 2.2f);
            return texturaContorno;
        }
    }

    public static Texture2D TexturaCirculo
    {
        get
        {
            if (texturaCirculo == null) texturaCirculo = CriarArredondada(64, 32f, 0f);
            return texturaCirculo;
        }
    }

    /// <summary>Faixa vertical: transparente em cima, escura embaixo (fundo das telas).</summary>
    public static Texture2D TexturaGradiente
    {
        get
        {
            if (texturaGradiente != null) return texturaGradiente;
            const int altura = 128;
            texturaGradiente = new Texture2D(1, altura, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontUnloadUnusedAsset
            };
            for (int y = 0; y < altura; y++)
            {
                float t = y / (altura - 1f); // 0 = base da tela
                float a = Mathf.Lerp(0.88f, 0.42f, Mathf.SmoothStep(0f, 1f, t));
                texturaGradiente.SetPixel(0, y, new Color(0.008f, 0.006f, 0.025f, a));
            }
            texturaGradiente.Apply(false, true);
            return texturaGradiente;
        }
    }

    public static Sprite SpriteArredondado => spriteArredondado != null ? spriteArredondado :
        (spriteArredondado = CriarSprite(TexturaArredondada, 20f));
    public static Sprite SpriteContorno => spriteContorno != null ? spriteContorno :
        (spriteContorno = CriarSprite(TexturaContorno, 20f));
    public static Sprite SpriteCirculo => spriteCirculo != null ? spriteCirculo :
        (spriteCirculo = CriarSprite(TexturaCirculo, 31f));

    static Sprite CriarSprite(Texture2D tex, float borda)
    {
        Sprite s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(borda, borda, borda, borda));
        s.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return s;
    }

    /// <summary>
    /// Gera um retângulo arredondado branco com bordas suavizadas.
    /// espessura = 0 desenha cheio; maior que 0 desenha só o contorno.
    /// </summary>
    static Texture2D CriarArredondada(int lado, float raio, float espessura)
    {
        Texture2D tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontUnloadUnusedAsset
        };
        Color[] px = new Color[lado * lado];
        float meio = lado * 0.5f;
        float interno = meio - raio;
        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                // Distância com sinal até a borda de uma caixa arredondada.
                float qx = Mathf.Abs(x + 0.5f - meio) - interno;
                float qy = Mathf.Abs(y + 0.5f - meio) - interno;
                float fora = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float dist = fora + Mathf.Min(Mathf.Max(qx, qy), 0f) - raio;
                float a = Mathf.Clamp01(0.5f - dist);
                if (espessura > 0f) a *= Mathf.Clamp01(0.5f + dist + espessura);
                px[y * lado + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return tex;
    }

    /// <summary>Cor de um valor de 0 a 1: saudável, alerta ou perigo (piscando).</summary>
    public static Color CorDoNivel(float nivel, Color saudavel)
    {
        Color cor = nivel > 0.60f ? saudavel : (nivel > 0.30f ? Laranja : Perigo);
        if (nivel <= 0.30f) cor *= 0.78f + 0.22f * Mathf.Sin(Time.unscaledTime * 9f);
        cor.a = 1f;
        return cor;
    }
}
