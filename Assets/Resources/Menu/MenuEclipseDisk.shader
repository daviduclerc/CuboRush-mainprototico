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
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Clock, _Distorcao;
            static const float2 Centro = float2(0.69, 0.494);
            static const float2 Aspecto = float2(1.7777778, 1.0);

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

            // Fluxo tangencial pelo disco inclinado e contorno curvado pela lente.
            float2 AmostraEmOrbita(float2 arco, float2 disco, float pesoDisco, float angulo)
            {
                float s, c;
                sincos(angulo, s, c);
                float2 arcoGirado = float2(c * arco.x - s * arco.y, s * arco.x + c * arco.y);
                float2 discoGirado = float2(c * disco.x - s * disco.y, s * disco.x + c * disco.y);
                discoGirado.y *= 0.31;
                float2 faixa = float2(0.960 * discoGirado.x - 0.280 * discoGirado.y,
                    0.280 * discoGirado.x + 0.960 * discoGirado.y);
                return Centro + lerp(arcoGirado, faixa, pesoDisco) / Aspecto;
            }

            half4 frag(Varying i) : SV_Target
            {
                float2 uv = i.uv;
                if (_Distorcao > 0)
                {
                    float2 rel = (uv - Centro) * Aspecto;
                    float distancia = length(rel);
                    float onda = sin(distancia * 29 - _Clock * 12);
                    float lente = smoothstep(0.10, 0.25, distancia) * (1 - smoothstep(0.38, 0.72, distancia));
                    uv = saturate(uv + rel / max(0.03, distancia) / Aspecto * onda * lente * _Distorcao * 0.050);
                }
                half4 base = tex2D(_MainTex, uv);
                float2 arco = (uv - Centro) * Aspecto;
                float2 disco = float2(dot(arco, float2(0.960, 0.280)), dot(arco, float2(-0.280, 0.960)));
                float pesoDisco = 1 - smoothstep(0.12, 0.21, abs(disco.y));
                disco.y /= 0.31;
                float raio = length(arco);
                float calor = smoothstep(0.072, 0.33, base.r - base.b) * smoothstep(0.09, 0.42, base.r);
                float mascara = calor * smoothstep(0.17, 0.23, raio)
                    * (1 - smoothstep(0.44, 0.78, raio)) * smoothstep(0.35, 0.52, uv.x);

                // As fases estão defasadas em meio ciclo para que a troca de amostra seja contínua.
                float faseA = frac(_Clock * 0.50);
                float faseB = frac(faseA + 0.5);
                float mistura = smoothstep(0, 1, 1 - abs(2 * faseA - 1));
                float2 uvA = AmostraEmOrbita(arco, disco, pesoDisco, (0.5 - faseA) * 0.29);
                float2 uvB = AmostraEmOrbita(arco, disco, pesoDisco, (0.5 - faseB) * 0.29);
                half3 fluxo = lerp(tex2D(_MainTex, uvB).rgb, tex2D(_MainTex, uvA).rgb, mistura);

                float theta = atan2(arco.y, arco.x);
                float onda = pow(0.5 + 0.5 * cos(theta * 3 - _Clock * 2.9 + raio * 15), 4);
                half3 cor = lerp(base.rgb, fluxo, mascara * 0.54);
                cor *= 1 + mascara * (onda - 0.27) * 0.30;
                return half4(cor * i.color.rgb, base.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
