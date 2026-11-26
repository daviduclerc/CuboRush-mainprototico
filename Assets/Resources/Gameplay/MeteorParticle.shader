Shader "CuboRush/MeteorParticle"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input { float3 pos : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Output { float4 clip : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Output vert(Input v) { Output o; o.clip = TransformObjectToHClip(v.pos); o.uv = v.uv; o.color = v.color; return o; }
            half4 frag(Output i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float miolo = 1 - smoothstep(0.05, 0.99, dot(p, p));
                return half4(i.color.rgb, i.color.a * miolo * miolo);
            }
            ENDHLSL
        }
    }
}
