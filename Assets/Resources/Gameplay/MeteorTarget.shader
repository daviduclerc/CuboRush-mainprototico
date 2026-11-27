Shader "CuboRush/MeteorTarget"
{
    Properties
    {
        _Progresso ("Progresso da queda", Range(0,1)) = 0
        _Brasa ("Brasa após impacto", Range(0,1)) = 0
        _Opacidade ("Opacidade", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+6" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off
            Blend SrcAlpha One
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Progresso, _Brasa, _Opacidade;
            CBUFFER_END
            struct Input { float3 pos : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 clip : SV_POSITION; float2 uv : TEXCOORD0; };
            Output vert(Input v) { Output o; o.clip = TransformObjectToHClip(v.pos); o.uv = v.uv; return o; }
            half4 frag(Output i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float r = length(p);
                float anel = 1 - smoothstep(0.030, 0.072, abs(r - 0.86));
                float enche = 1 - smoothstep(0.01, 0.075, r - lerp(0.09, 0.86, _Progresso));
                float pulso = 0.65 + 0.35 * sin(_Time.y * 10 + _Progresso * 7);
                float cruz = (1 - smoothstep(0.024, 0.050, min(abs(p.x), abs(p.y)))) *
                    (1 - smoothstep(0.26, 0.34, r));
                float alpha = saturate(anel * (0.65 + 0.35 * pulso) + enche * (0.20 + _Brasa * 0.22) + cruz * 0.75);
                float3 cor = lerp(float3(2.2, 0.28, 0.013), float3(2.8, 0.61, 0.025), _Brasa);
                return half4(cor, alpha * _Opacidade);
            }
            ENDHLSL
        }
    }
}
