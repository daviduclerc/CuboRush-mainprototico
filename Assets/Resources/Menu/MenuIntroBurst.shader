Shader "CuboRush/UI/MenuIntroBurst"
{
    Properties
    {
        _Origem ("Origem do clarão e proporção", Vector) = (0.5, 0.5, 1.7778, 0)
        _Frente ("Avanço da onda", Range(0, 1)) = 0
        _Clarao ("Intensidade", Range(0, 1)) = 0
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

            float4 _Origem;
            float _Frente, _Clarao;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varying { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            Varying vert(Input v)
            {
                Varying o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varying i) : SV_Target
            {
                float2 p = (i.uv - _Origem.xy) * float2(_Origem.z, 1.0);
                float r = length(p);
                float frente = lerp(0.06, 1.70, _Frente);
                float espessura = lerp(0.015, 0.074, _Frente);
                float distanciaDaFrente = (r - frente) / espessura;
                float onda = exp(-distanciaDaFrente * distanciaDaFrente) * (1 - _Frente);
                float angulo = atan2(p.y, p.x);
                float raios = pow(0.5 + 0.5 * cos(angulo * 13.0 + 0.8 * sin(angulo * 5)), 14);
                raios *= exp(-r * 4.5) * (1 - smoothstep(0.14, 0.65, _Frente));
                float risco = exp(-abs(p.y) * 87) * exp(-abs(p.x) * 3.3);
                float nucleo = exp(-r * 7.4);
                float brilho = saturate(onda * 0.56 + raios * 0.32 + (risco * 0.70 + nucleo) * (1 - _Frente));
                float3 quente = lerp(float3(1.0, 0.44, 0.08), float3(1.0, 0.91, 0.67), brilho);
                return half4(lerp(quente, float3(1.0, 0.99, 0.96), _Clarao),
                    saturate(_Clarao + brilho) * i.color.a);
            }
            ENDHLSL
        }
    }
}
