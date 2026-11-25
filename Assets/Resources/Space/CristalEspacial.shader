Shader "CuboRush/CristalEspacial"
{
    // Cristal facetado e luminoso (decoração do cenário): núcleo que respira,
    // borda acesa (fresnel), arestas brilhando e reflexo do céu.
    Properties
    {
        [HDR] _Cor ("Cor da borda", Color) = (1.4, 0.25, 0.35, 1)
        [HDR] _CorInterna ("Cor interna", Color) = (0.55, 0.04, 0.12, 1)
        _Intensidade ("Intensidade", Range(0, 8)) = 2.5
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
            #include "ReflexoDoAmbiente.hlsl"
            #include "RuidoEstelar.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Cor;
            half4 _CorInterna;
            float _Intensidade;
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

                // Destaques diminuem com a distância: em cristais de poucos pixels a normal facetada
                // (derivadas) fica instável e gerava piscadas.
                float distCamera = distance(GetCameraPositionWS(), i.mundo);
                float perto = 1.0 - smoothstep(40.0, 130.0, distCamera);
                float faceta = EstHash13(floor(n * 3.0 + 0.5) + 1.7);
                float fresnel = pow(1.0 - saturate(dot(n, v)), 2.2);
                float difusa = saturate(dot(n, l)) * 0.5 + 0.5;
                float especular = pow(saturate(dot(n, normalize(l + v))), 48.0);
                float pulso = 1.0 + 0.15 * sin(_Time.y * 1.6 + i.mundo.x * 0.7 + i.mundo.z * 0.3);
                float interno = 0.55 + 0.45 * EstRuido3(i.objeto * 4.0 + _Time.y * 0.3);
                float aresta = saturate(length(fwidth(n)) * 2.0) * perto;

                float3 cor = _CorInterna.rgb * (0.4 + faceta * 1.2) * difusa * interno;
                cor += _Cor.rgb * (fresnel + aresta * 0.5) * _Intensidade * pulso;
                cor += float3(1.0, 0.95, 0.9) * especular * 0.7 * perto;
                // Reflete o céu e a pista, mas não o disco do Sol: ele virava um clarão a cada giro.
                float3 r = reflect(-v, n);
                float3 ambiente = r.y >= 0.0 ? CeuNaDirecao(r) : PistaNaDirecao(i.mundo, r);
                cor += ambiente * (0.15 + fresnel * 0.6);
                // Teto de brilho: o cristal brilha, mas sem estourar no bloom.
                float pico = max(cor.r, max(cor.g, cor.b));
                cor *= min(1.0, 1.6 / max(pico, 0.0001));
                return half4(cor, 1);
            }
            ENDHLSL
        }
    }
}
