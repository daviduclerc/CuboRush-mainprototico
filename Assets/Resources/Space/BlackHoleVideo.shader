Shader "CuboRush/BlackHoleVideo"
{
    Properties { _MainTex ("Video do buraco negro", 2D) = "black" {} _Aspect ("Proporcao do video", Float) = 1.777778 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Aspect;
            CBUFFER_END
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
                float2 centro = (input.uv - float2(0.5, 0.45)) * float2(_Aspect, 1.0);
                // Preserva a sombra central opaca e remove o preto externo do vídeo.
                float sombra = 1.0 - smoothstep(0.21, 0.235, length(centro));
                float luz = max(color.r, max(color.g, color.b));
                float alpha = max(sombra, smoothstep(0.003, 0.065, luz));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
