Shader "CuboRush/ShootingStar"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent" }
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
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            V vert(A i) { V o; o.p=TransformObjectToHClip(i.p.xyz); o.uv=i.uv; o.color=i.color; return o; }
            half4 frag(V i):SV_Target {
                float centro=1-abs(i.uv.y*2-1);
                return half4(i.color.rgb*2.6,i.color.a*smoothstep(0,0.75,centro));
            }
            ENDHLSL
        }
    }
}
