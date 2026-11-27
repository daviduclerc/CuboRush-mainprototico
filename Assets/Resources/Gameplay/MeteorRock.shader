Shader "CuboRush/MeteorRock"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Input { float3 pos : POSITION; float3 normal : NORMAL; float2 bary : TEXCOORD0; };
            struct Output { float4 clip : SV_POSITION; float3 local : TEXCOORD0; float3 normal : TEXCOORD1; float2 bary : TEXCOORD2; };

            Output vert(Input v)
            {
                Output o;
                o.clip = TransformObjectToHClip(v.pos);
                o.local = v.pos;
                o.normal = TransformObjectToWorldNormal(v.normal);
                o.bary = v.bary;
                return o;
            }

            half4 frag(Output i) : SV_Target
            {
                float3 p = i.local * 34;
                float granulos = 0.5 + 0.5 * sin(p.x + sin(p.z * 0.86) * 2.3) * sin(p.y * 1.4 + p.z);
                float borda = min(min(i.bary.x, i.bary.y), 1 - i.bary.x - i.bary.y);
                float fissura = 1 - smoothstep(0.008, 0.043, borda + 0.013 * granulos);
                float veio = 1 - smoothstep(0.019, 0.13, borda);
                float luz = saturate(dot(normalize(i.normal), normalize(float3(-0.3, 0.8, -0.45))));
                float3 rocha = lerp(float3(0.017, 0.015, 0.021), float3(0.11, 0.079, 0.061), luz) * (0.7 + granulos * 0.55);
                float calor = 0.70 + 0.30 * sin(p.y * 0.8 + p.z * 0.6 + _Time.y * 5);
                return half4(rocha + float3(0.38, 0.038, 0.004) * veio +
                    float3(4.0, 0.62, 0.045) * fissura * calor, 1);
            }
            ENDHLSL
        }
    }
}
