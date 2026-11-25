Shader "CuboRush/FundoEspacial"
{
    // Imagem de fundo (buraco negro + nebulosas). Desenhada antes de tudo e sem gravar
    // profundidade: qualquer objeto do jogo aparece na frente dela, por mais longe que esteja.
    Properties
    {
        _MainTex ("Imagem de fundo", 2D) = "black" {}
        _Exposicao ("Exposicao", Float) = 1
        _Realce ("Brilho extra nas partes quentes (bloom)", Range(0, 3)) = 0.6
        _Borda ("Suavizacao das bordas", Range(0, 0.3)) = 0.06
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background+10" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float _Exposicao;
            float _Realce;
            float _Borda;
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
                half3 cor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
                half luz = max(cor.r, max(cor.g, cor.b));
                // Só o gás mais quente do disco passa de 1: o bloom acende o anel, não o céu inteiro.
                cor *= _Exposicao * (1.0h + smoothstep(0.70h, 1.0h, luz) * _Realce);
                // Bordas suaves, caso a imagem não cubra a tela toda.
                float2 d = min(input.uv, 1.0 - input.uv);
                float alpha = smoothstep(0.0, max(_Borda, 0.0001), min(d.x, d.y));
                return half4(cor, alpha);
            }
            ENDHLSL
        }
    }
}
