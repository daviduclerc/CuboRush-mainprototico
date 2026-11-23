Shader "CuboRush/VidroNeon"
{
    // Obstáculos de vidro: corpo transparente tingido, mais opaco e brilhante nas bordas
    // (Fresnel), refletindo o céu, a pista e o Sol. As arestas neon vêm de LineRenderers.
    Properties
    {
        _Cor ("Cor do vidro", Color) = (1, 0.2, 0.7, 1)
        _Opacidade ("Opacidade minima", Range(0, 1)) = 0.12
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "ReflexoDoAmbiente.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _Cor;
        float _Opacidade;
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
        half4 Vidro(V i, float lado)
        {
            float3 n = normalize(i.normal) * lado;
            float3 v = normalize(GetCameraPositionWS() - i.mundo);
            float fresnel = pow(1.0 - saturate(dot(n, v)), 3.0);
            float3 reflexo = AmbienteRefletido(i.mundo, reflect(-v, n));
            // Cristal: cada faceta com um tom diferente, núcleo que pulsa e borda bem acesa.
            float faceta = frac(sin(dot(floor(n * 4.0 + 0.5), float3(12.9898, 78.233, 37.719))) * 43758.5453);
            float pulso = 1.0 + 0.12 * sin(_Time.y * 1.8 + i.mundo.z * 0.35 + i.mundo.x);
            float3 cor = _Cor.rgb * (0.45 + faceta * 0.55 + fresnel * 2.4) * pulso + reflexo * (0.15 + fresnel * 0.7);
            float brilhoReflexo = saturate(dot(reflexo, float3(0.3, 0.59, 0.11)));
            float alfa = saturate(_Opacidade + faceta * 0.12 + fresnel * 0.6 + brilhoReflexo * 0.3);
            return half4(cor, alfa);
        }
        ENDHLSL

        // Primeiro as faces de trás (o interior do vidro), depois as da frente.
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(V i):SV_Target { half4 c = Vidro(i, -1.0); c.a *= 0.6; return c; }
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(V i):SV_Target { return Vidro(i, 1.0); }
            ENDHLSL
        }
    }
}
