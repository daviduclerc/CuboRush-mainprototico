Shader "CuboRush/MeteorTrail"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+2" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Space/RuidoEstelar.hlsl"

            struct Input { float4 pos : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Output { float4 clip : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Output vert(Input v)
            {
                Output o;
                o.clip = TransformObjectToHClip(v.pos.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Output i) : SV_Target
            {
                float2 p = float2(i.uv.x, i.uv.y * 2.0 - 1.0);
                float fluxo = EstRuido2(float2(p.x * 23.0 - _Time.y * 12.0, p.y * 2.8));
                float deslocamento = (fluxo - 0.5) * 0.33;
                float distancia = abs(p.y + deslocamento);
                float contorno = 1 - smoothstep(0.16, 0.90, distancia);
                float miolo = 1 - smoothstep(0.04, 0.37 + fluxo * 0.08, distancia);
                float3 cor = lerp(i.color.rgb * float3(1.7, 0.47, 0.18),
                    float3(2.4, 1.39, 0.48), miolo * 0.80);
                return half4(cor, i.color.a * contorno * (0.58 + fluxo * 0.36));
            }
            ENDHLSL
        }
    }
}
