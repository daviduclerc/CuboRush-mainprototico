Shader "CuboRush/MuroEspelho"
{
    // Muro de cor: espelho colorido. Pinta a parede inteira com uma cor e reflete
    // o céu, a pista e o Sol tingidos por ela.
    Properties
    {
        _Cor ("Cor do espelho", Color) = (1, 0.05, 0.05, 1)
        _Brilho ("Brilho proprio da cor", Range(0, 2)) = 0.4
        _Clarao ("Clarao de impacto", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ReflexoDoAmbiente.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Cor;
            float _Brilho;
            float _Clarao;
            CBUFFER_END
            struct A { float4 p:POSITION; float3 normal:NORMAL; };
            struct V { float4 p:SV_POSITION; float3 mundo:TEXCOORD0; float3 normal:TEXCOORD1; };
            V vert(A i)
            {
                V o;
                o.mundo = TransformObjectToWorld(i.p.xyz);
                o.p = TransformWorldToHClip(o.mundo);
                o.normal = TransformObjectToWorldNormal(i.normal);
                return o;
            }
            half4 frag(V i):SV_Target
            {
                float3 n = normalize(i.normal);
                float3 v = normalize(GetCameraPositionWS() - i.mundo);
                float3 r = reflect(-v, n);
                float3 reflexo = AmbienteRefletido(i.mundo, r);
                // Espelho colorido: o reflexo sai tingido; nas bordas rasantes fica mais neutro.
                float fresnel = pow(1.0 - saturate(dot(n, v)), 5.0);
                float3 tinta = lerp(_Cor.rgb, float3(1, 1, 1), fresnel * 0.5);
                float3 cor = reflexo * tinta * 1.6 + _Cor.rgb * _Brilho;
                cor += float3(1.0, 0.9, 0.85) * _Clarao * 3.0;
                return half4(cor, 1);
            }
            ENDHLSL
        }
    }
}
