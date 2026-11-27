Shader "CuboRush/BlackHoleVideo"
{
    Properties
    {
        _MainTex ("Video do buraco negro", 2D) = "black" {}
        _Aspect ("Proporcao do video", Float) = 1.777778
        _Contraste ("Contraste do disco", Range(1, 2.5)) = 1.6
        _Realce ("Brilho extra nas partes mais quentes", Range(0, 3)) = 0.55
    }
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
            float _Contraste;
            float _Realce;
            CBUFFER_END
            float _SolarDeathDarkness;
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

                // Gradação: meios-tons mais profundos (o disco deixa de parecer uma placa lisa)
                // e só o gás mais quente passa de 1, para o bloom fazer o brilho em volta.
                half3 cor = pow(max(color, 0.0h), _Contraste);
                cor *= lerp(half3(1.0h, 0.86h, 0.80h), half3(1.0h, 1.0h, 1.0h), saturate(luz * 1.4h));
                cor *= 0.88h + smoothstep(0.62h, 1.0h, luz) * _Realce;
                cor *= lerp(half3(1.0h, 1.0h, 1.0h), half3(0.09h, 0.17h, 0.28h), saturate(_SolarDeathDarkness));
                return half4(cor, alpha);
            }
            ENDHLSL
        }
    }
}
