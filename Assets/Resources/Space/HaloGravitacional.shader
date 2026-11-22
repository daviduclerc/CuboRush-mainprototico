Shader "CuboRush/HaloGravitacional"
{
    // Halo e anéis de luz aditivos para o mini buraco negro e os corpos do estilingue.
    // _Interno/_Externo definem o anel no raio da UV (0 = centro, 1 = borda do quad).
    // _Espiral desenha braços girando, como num disco de acreção.
    Properties
    {
        [HDR] _Cor ("Cor", Color) = (1, 1, 1, 1)
        _Interno ("Raio interno", Range(0, 1)) = 0
        _Externo ("Raio externo", Range(0, 1)) = 1
        _Suave ("Suavidade da borda", Range(0.001, 0.5)) = 0.08
        _Queda ("Queda do brilho para fora", Range(0.2, 6)) = 1.5
        _Espiral ("Braços em espiral", Range(0, 1)) = 0
        _Giro ("Velocidade do giro", Float) = 3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Cor;
            float _Interno;
            float _Externo;
            float _Suave;
            float _Queda;
            float _Espiral;
            float _Giro;
            CBUFFER_END
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; };
            V vert(A i)
            {
                V o;
                o.p = TransformObjectToHClip(i.p.xyz);
                o.uv = i.uv;
                return o;
            }
            half4 frag(V i):SV_Target
            {
                float2 d = i.uv * 2.0 - 1.0;
                float r = length(d);
                float anel = smoothstep(_Interno - _Suave, _Interno, r) * (1.0 - smoothstep(_Externo - _Suave, _Externo, r));
                if (_Interno <= 0.0) anel = 1.0 - smoothstep(_Externo - _Suave, _Externo, r);
                float t = saturate((r - _Interno) / max(_Externo - _Interno, 0.001));
                float brilho = anel * pow(1.0 - t, _Queda);
                float angulo = atan2(d.y, d.x);
                float bracos = 0.5 + 0.5 * sin(angulo * 3.0 + r * 11.0 - _Time.y * _Giro);
                brilho *= lerp(1.0, 0.35 + 0.65 * bracos, _Espiral);
                return half4(_Cor.rgb * brilho, 0);
            }
            ENDHLSL
        }
    }
}
