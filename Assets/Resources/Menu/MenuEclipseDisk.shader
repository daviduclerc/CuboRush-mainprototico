Shader "CuboRush/UI/MenuEclipseDisk"
{
    Properties
    {
        [PerRendererData] _MainTex ("Arte do menu", 2D) = "white" {}
        _Clock ("Tempo do menu", Float) = 0
        _Distorcao ("Lente na abertura", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Clock, _Distorcao;
            #define AMOSTRAR_ECLIPSE(uv) tex2D(_MainTex, uv).rgb
            #include "../Space/EclipseAccretion.hlsl"
            #include "../Space/RuidoEstelar.hlsl"

            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varying { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varying vert(Input v)
            {
                Varying o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half3 Estrelas(float2 ceu, float densidade, float2 deriva, float sementes, float pixel)
            {
                float2 grade = (ceu + deriva * _Clock) * densidade;
                float2 celula = floor(grade) + sementes;
                float brilho = EstHash12(celula + 21.7);
                float2 centro = 0.25 + 0.50 * float2(EstHash12(celula), EstHash12(celula + 47.3));
                float2 ponto = (frac(grade) - centro) / densidade;
                float distancia = length(ponto);
                float raio = lerp(0.0007, 0.0022, brilho);
                float nucleo = 1 - smoothstep(0, raio + pixel, distancia);
                float halo = 1 - smoothstep(raio, raio * 2.1 + pixel, distancia);
                float cintilar = 0.60 + 0.40 * sin(_Clock * lerp(1.35, 4.6, brilho) + centro.x * 22.0);
                half3 cor = lerp(half3(0.44, 0.66, 1), half3(1, 0.87, 0.72), centro.y);
                return cor * step(0.78, brilho) * cintilar * (nucleo * 0.95 + halo * 0.10);
            }

            half3 EstrelaCadente(float2 ceu, float pixel)
            {
                float periodo = (_Clock + 5.4) / 7.6;
                float vida = frac(periodo) * 7.6 / 1.15;
                if (vida > 1) return 0;

                float2 rumo = normalize(float2(-1, -0.32));
                float faixa = fmod(floor(periodo), 2);
                float2 origem = lerp(float2(1.48, 0.94), float2(1.65, 0.27), faixa);
                float2 cabeca = origem + rumo * (0.60 * vida);
                float2 delta = ceu - cabeca;
                float rastro = dot(delta, -rumo);
                float afastamento = abs(dot(delta, float2(-rumo.y, rumo.x)));
                float comprimento = 0.14;
                float largura = lerp(0.0025 + pixel, 0.0008 + pixel, saturate(rastro / comprimento));
                float faixaBrilhante = smoothstep(-0.012, 0.002, rastro) * (1 - smoothstep(0.0, comprimento, rastro));
                faixaBrilhante *= 1 - smoothstep(0.0, largura, afastamento);
                float luz = 1 - smoothstep(0.0, 0.010 + pixel, length(delta));
                float aparicao = smoothstep(0.0, 0.18, vida) * (1 - smoothstep(0.63, 1.0, vida));
                return half3(0.50, 0.78, 1) * aparicao * (faixaBrilhante * 0.77 + luz * 1.2);
            }

            half3 NitidezDoTitulo(float2 uv, half3 corDaArte)
            {
                float2 dx = ddx(uv);
                float2 dy = ddy(uv);
                float2 margem = smoothstep(float2(0.025, 0.72), float2(0.043, 0.74), uv)
                    * (1 - smoothstep(float2(0.345, 0.883), float2(0.367, 0.900), uv));
                float mascara = margem.x * margem.y;
                if (mascara <= 0) return 0;

                float2 passo = max(_MainTex_TexelSize.xy, (abs(dx) + abs(dy)) * 0.70);
                float2 x = float2(passo.x, 0);
                float2 y = float2(0, passo.y);
                half3 media = (tex2Dgrad(_MainTex, uv - x, dx, dy).rgb + tex2Dgrad(_MainTex, uv + x, dx, dy).rgb
                    + tex2Dgrad(_MainTex, uv - y, dx, dy).rgb + tex2Dgrad(_MainTex, uv + y, dx, dy).rgb) * 0.25;
                half3 detalhe = clamp(corDaArte - media, -0.11, 0.24);
                return (saturate(corDaArte + detalhe * 0.55) - corDaArte) * mascara;
            }

            half3 AnimarAnelDoLogo(float2 uv, half3 corDaArte)
            {
                // Centro, inclinação e tamanho medidos no traço laranja da arte.
                float2 ponto = (uv - float2(0.3101, 0.7709)) * float2(1672.0, 941.0);
                if (abs(ponto.x) > 108.0 || abs(ponto.y) > 76.0) return 0;

                float2 eixo = float2(0.8645, 0.5026);
                float2 orbita = float2(dot(ponto, eixo), dot(ponto, float2(-eixo.y, eixo.x))) / float2(86.7, 34.6);
                float raio = length(orbita);
                float faixa = 1 - smoothstep(0.05, 0.27, abs(raio - 1.0));
                float brasa = smoothstep(0.06, 0.52, corDaArte.r - corDaArte.b);
                float foraDasLetras = max(smoothstep(0.339, 0.349, uv.x),
                    max(1 - smoothstep(0.737, 0.746, uv.y), smoothstep(0.788, 0.797, uv.y)));
                float mascara = faixa * brasa * foraDasLetras;
                if (mascara <= 0) return 0;

                float2 direcao = orbita / max(raio, 0.001);
                float fase = _Clock * 1.85;
                float2 frente = float2(cos(fase), sin(fase));
                float2 cauda = float2(cos(fase - 0.42), sin(fase - 0.42));
                float nucleo = pow(saturate(dot(direcao, frente)), 18.0);
                float rastro = pow(saturate(dot(direcao, cauda)), 5.0);
                return mascara * (half3(1.0, 0.66, 0.23) * nucleo * 0.64
                    + half3(0.86, 0.26, 0.035) * rastro * 0.42);
            }

            half3 EspacoAnimado(float2 uv, half3 corDaArte)
            {
                float2 ceu = uv * EclipseAspecto;
                float pixel = max(fwidth(ceu.x), fwidth(ceu.y)) * 0.70;
                float raioBuraco = length(ceu - EclipseCentro * EclipseAspecto);
                float luzDaArte = max(corDaArte.r, max(corDaArte.g, corDaArte.b));
                float calor = smoothstep(0.035, 0.18, corDaArte.r - corDaArte.b);
                float mascara = smoothstep(0.27, 0.38, raioBuraco) * smoothstep(0.23, 0.43, uv.x)
                    * (1 - smoothstep(0.16, 0.56, luzDaArte)) * (1 - calor);
                if (mascara <= 0) return 0;

                half3 estrelas = Estrelas(ceu, 37, float2(0.005, -0.002), 18.3, pixel) * 0.55;
                estrelas += Estrelas(ceu, 16, float2(0.010, -0.004), 82.7, pixel);
                return mascara * (estrelas + EstrelaCadente(ceu, pixel));
            }

            half4 frag(Varying i) : SV_Target
            {
                float2 uv = i.uv;
                if (_Distorcao > 0)
                {
                    float2 rel = (uv - EclipseCentro) * EclipseAspecto;
                    float distancia = length(rel);
                    float onda = sin(distancia * 29 - _Clock * 12);
                    float lente = smoothstep(0.10, 0.25, distancia) * (1 - smoothstep(0.38, 0.72, distancia));
                    uv = saturate(uv + rel / max(0.03, distancia) / EclipseAspecto * onda * lente * _Distorcao * 0.050);
                }
                half4 base = tex2D(_MainTex, uv);
                half3 cor = EclipsePlasma(uv, base.rgb, _Clock);
                cor += NitidezDoTitulo(uv, base.rgb);
                cor += AnimarAnelDoLogo(uv, base.rgb);
                cor += EspacoAnimado(uv, base.rgb);
                return half4(cor * i.color.rgb, base.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
