Shader "CuboRush/ColorPortal"
{
    Properties {
        [HDR] _Tint ("Cor", Color) = (0,1,1,1)
        _Pulse ("Pulso de travessia", Float) = 0
        _Failure ("Cor incorreta", Float) = 0
    }
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
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            float _Pulse;
            float _Failure;
            CBUFFER_END
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; };
            V vert(A i) { V o; o.p=TransformObjectToHClip(i.p.xyz); o.uv=i.uv; return o; }
            half4 frag(V i):SV_Target {
                float2 p=i.uv-0.5;
                float fio=pow(saturate(0.5+0.5*sin(i.uv.y*44+i.uv.x*9+_Time.y*2)),18);
                float scan=pow(saturate(0.5+0.5*sin(i.uv.y*120-_Time.y*3)),32);
                float onda=1-smoothstep(0.015,0.08,abs(length(p)-(1-_Pulse)*0.8));
                float alpha=0.065+fio*0.075+scan*0.025+onda*_Pulse*0.4;
                half3 cor=lerp(_Tint.rgb,half3(1,0.12,0.08),_Failure*_Pulse*0.6);
                return half4(cor*(1.1+_Pulse*0.7),saturate(alpha));
            }
            ENDHLSL
        }
    }
}
