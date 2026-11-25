Shader "CuboRush/NeonDaPista"
{
    // Linhas neon da pista: cor emissiva lisa que acompanha a curvatura do poço gravitacional.
    Properties { [HDR] _Cor ("Cor neon", Color) = (3.5, 0.21, 1.9, 1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PocoGravitacional.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Cor;
            CBUFFER_END
            struct A { float4 p:POSITION; };
            struct V { float4 p:SV_POSITION; };
            V vert(A i)
            {
                V o;
                float3 mundo = PocoAplicar(TransformObjectToWorld(i.p.xyz));
                o.p = TransformWorldToHClip(mundo);
                return o;
            }
            half4 frag(V i):SV_Target { return half4(_Cor.rgb, 1); }
            ENDHLSL
        }
    }
}
