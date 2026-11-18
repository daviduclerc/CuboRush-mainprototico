Shader "CuboRush/StarDot"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+90" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; };
            V vert(A i) { V o; o.p=TransformObjectToHClip(i.p.xyz); o.uv=i.uv; return o; }
            half4 frag(V i):SV_Target {
                float r=length(i.uv*2-1);
                float brilho=1-smoothstep(0.05,0.95,r);
                return half4(half3(1.5,1.65,1.85),brilho*0.8);
            }
            ENDHLSL
        }
    }
}
