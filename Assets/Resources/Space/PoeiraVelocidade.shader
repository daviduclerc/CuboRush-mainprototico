Shader "CuboRush/PoeiraVelocidade"
{
    // Grão de poeira estelar: ponto suave e aditivo. Esticado pelo sistema de partículas,
    // vira um risco de luz que some nas pontas.
    Properties
    {
        _Intensidade ("Intensidade", Range(0, 4)) = 2.6
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half _Intensidade;
            CBUFFER_END
            struct A { float4 p:POSITION; half4 cor:COLOR; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; half4 cor:COLOR; float2 uv:TEXCOORD0; };
            V vert(A i)
            {
                V o;
                o.p = TransformObjectToHClip(i.p.xyz);
                o.cor = i.cor;
                o.uv = i.uv;
                return o;
            }
            half4 frag(V i):SV_Target
            {
                float2 d = i.uv * 2.0 - 1.0;
                half forma = saturate(1.0 - dot(d, d));
                forma *= forma;
                return half4(i.cor.rgb * _Intensidade, i.cor.a * forma);
            }
            ENDHLSL
        }
    }
}
