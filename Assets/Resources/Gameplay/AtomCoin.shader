Shader "CuboRush/AtomCoin"
{
    Properties { _MainTex ("Atomo", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; };
            V vert(A i) { V o; o.p=TransformObjectToHClip(i.p.xyz); o.uv=i.uv; return o; }
            half4 frag(V i):SV_Target {
                half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
                clip(c.a-0.06);
                // Um reflexo estreito e lento atravessa somente a superfície do átomo.
                float fase=frac(_Time.y*0.22);
                float faixa=1-smoothstep(0.015,0.085,abs(i.uv.x+i.uv.y*0.32-lerp(-0.2,1.6,fase)));
                half3 brilho=c.rgb*1.9+half3(0.34,0.34,0.34)*faixa;
                return half4(brilho,saturate(c.a*1.7));
            }
            ENDHLSL
        }
    }
}
