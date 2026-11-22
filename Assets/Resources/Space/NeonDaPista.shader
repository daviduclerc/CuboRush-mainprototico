Shader "CuboRush/NeonDaPista"
{
    // Linhas neon da pista: cor emissiva lisa que acompanha a curvatura do poço gravitacional.
    Properties
    {
        [HDR] _Cor ("Cor neon", Color) = (3.5, 0.21, 1.9, 1)
        _Pulso ("Intensidade dos pulsos de energia", Range(0, 3)) = 1.1
        _VelocidadePulso ("Velocidade dos pulsos", Float) = 22
        _Espacamento ("Distancia entre pulsos", Float) = 24
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
            #include "PocoGravitacional.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Cor;
            half _Pulso;
            float _VelocidadePulso;
            float _Espacamento;
            CBUFFER_END
            struct A { float4 p:POSITION; };
            struct V { float4 p:SV_POSITION; float z:TEXCOORD0; float crista:TEXCOORD1; };
            V vert(A i)
            {
                V o;
                float3 mundo = PocoAplicar(TransformObjectToWorld(i.p.xyz));
                o.p = TransformWorldToHClip(mundo);
                o.z = mundo.z;
                // Brilho extra na crista da onda gravitacional: dá para ver a onda chegando.
                o.crista = saturate(-CampoOndaPlana(mundo.z) / max(_CampoOnda.y, 0.001));
                return o;
            }
            half4 frag(V i):SV_Target
            {
                // Pulsos de energia correm pelas linhas em direção ao jogador.
                float fase = frac((i.z + _Time.y * _VelocidadePulso) / _Espacamento);
                half pulso = exp(-pow((fase - 0.5) * 10.0, 2.0)) * _Pulso;
                half3 cor = _Cor.rgb * (1.0h + pulso) + pulso * half3(0.9h, 0.7h, 0.9h);
                cor += i.crista * half3(1.6h, 2.4h, 3.2h);
                return half4(cor, 1);
            }
            ENDHLSL
        }
    }
}
