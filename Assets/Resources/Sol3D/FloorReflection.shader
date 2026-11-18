Shader "CuboRush/FloorReflection"
{
    Properties { _MainTex ("Fotosfera", 2D) = "white" {} _Tint ("Cor e intensidade", Color) = (1,1,1,0.5) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
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
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            CBUFFER_END
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; };
            V vert(A i) { V o; o.p=TransformObjectToHClip(i.p.xyz); o.uv=i.uv; return o; }
            half4 frag(V i):SV_Target {
                float2 p=i.uv*2-1;
                float r=length(p);
                float halo=exp(-r*r*3.0)*(1-smoothstep(0.65,1,r));
                float sphere=1-smoothstep(0.30,0.58,r);
                half3 tex=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+float2(_Time.y*0.018,0)).rgb;
                float plasma=dot(tex,half3(0.3,0.59,0.11));
                return half4(_Tint.rgb*(halo*1.5+sphere*(1.8+plasma*1.4)),halo*_Tint.a);
            }
            ENDHLSL
        }
    }
}
