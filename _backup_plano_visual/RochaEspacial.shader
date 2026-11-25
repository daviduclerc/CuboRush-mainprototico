Shader "CuboRush/RochaEspacial"
{
    // Asteroide escuro e facetado, com contraluz laranja vindo do buraco negro,
    // luz roxa da nebulosa por baixo e rachaduras de lava.
    Properties
    {
        _Cor ("Cor da rocha", Color) = (0.10, 0.085, 0.095, 1)
        _CorClara ("Cor clara da rocha", Color) = (0.28, 0.22, 0.22, 1)
        [HDR] _CorContraluz ("Contraluz (buraco negro)", Color) = (1.6, 0.75, 0.25, 1)
        [HDR] _CorPreenchimento ("Luz da nebulosa", Color) = (0.30, 0.20, 0.65, 1)
        [HDR] _CorLava ("Cor da lava", Color) = (1.0, 0.38, 0.05, 1)
        _QuantidadeLava ("Quantidade de lava", Range(0, 1)) = 0.3
        _DirLuz ("Direção do buraco negro", Vector) = (0, 0.25, 1, 0)
    }
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
            #include "RuidoEstelar.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Cor;
            half4 _CorClara;
            half4 _CorContraluz;
            half4 _CorPreenchimento;
            half4 _CorLava;
            float _QuantidadeLava;
            float4 _DirLuz;
            CBUFFER_END
            struct A { float4 p:POSITION; };
            struct V { float4 p:SV_POSITION; float3 mundo:TEXCOORD0; float3 objeto:TEXCOORD1; };
            V vert(A i)
            {
                V o;
                o.mundo = TransformObjectToWorld(i.p.xyz);
                o.p = TransformWorldToHClip(o.mundo);
                o.objeto = i.p.xyz;
                return o;
            }
            half4 frag(V i):SV_Target
            {
                float3 v = normalize(GetCameraPositionWS() - i.mundo);
                float3 n = EstNormalFacetada(i.mundo, v);
                float3 l = normalize(_DirLuz.xyz);

                float textura = EstFbm3(i.objeto * 3.0);
                float3 baseCor = lerp(_Cor.rgb, _CorClara.rgb, smoothstep(0.3, 0.75, textura));

                float difusa = saturate(dot(n, l));
                float preenchimento = saturate(-n.y * 0.5 + 0.5);
                float borda = pow(1.0 - saturate(dot(n, v)), 2.5);

                float3 cor = baseCor * (0.22 + difusa * 0.9);
                cor += baseCor * _CorPreenchimento.rgb * preenchimento * 0.7;
                cor += _CorContraluz.rgb * borda * saturate(dot(n, l) + 0.45) * 1.4;

                float lava = EstFbm3(i.objeto * 5.0 + 3.0);
                cor += _CorLava.rgb * smoothstep(1.0 - _QuantidadeLava * 0.45, 1.0, lava) * 4.0;
                return half4(cor, 1);
            }
            ENDHLSL
        }
    }
}
