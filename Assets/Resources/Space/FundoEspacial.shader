Shader "CuboRush/FundoEspacial"
{
    // Imagem de fundo (buraco negro + nebulosas). Fica "colada" no fundo da cena, como um skybox:
    // só aparece onde não há nenhum objeto, então tudo do jogo fica na frente dela, por mais longe
    // que esteja. As bordas se misturam com o céu estrelado.
    Properties
    {
        _MainTex ("Imagem de fundo", 2D) = "black" {}
        _Exposicao ("Exposicao", Float) = 1
        _Realce ("Brilho extra nas partes quentes (bloom)", Range(0, 3)) = 0
        _Limite ("Brilho maximo (abaixo de 0.9 o bloom nao acende)", Range(0.3, 3)) = 0.85
        _Borda ("Suavizacao das bordas", Range(0, 0.45)) = 0.14
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-499" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
            float _Limite;
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
                // Empurra para o plano de fundo da câmera (profundidade máxima).
                // (um fio antes do limite, para não ser cortada pelo plano de corte distante)
                #if UNITY_REVERSED_Z
                output.positionCS.z = output.positionCS.w * 0.000001;
                #else
                output.positionCS.z = output.positionCS.w * 0.999999;
                #endif
                output.uv = input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half3 cor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
                half luz = max(cor.r, max(cor.g, cor.b));
                // Só o gás mais quente do disco passa de 1: o bloom acende o anel, não o céu inteiro.
                cor *= _Exposicao * (1.0h + smoothstep(0.70h, 1.0h, luz) * _Realce);
                // Teto de brilho: as partes mais quentes do disco não estouram em clarão branco no bloom.
                half pico = max(cor.r, max(cor.g, cor.b));
                cor *= min(1.0h, _Limite / max(pico, 0.0001h));
                // Bordas suaves, caso a imagem não cubra a tela toda.
                float2 d = min(input.uv, 1.0 - input.uv);
                float alpha = smoothstep(0.0, max(_Borda, 0.0001), min(d.x, d.y));
                return half4(cor, alpha);
            }
            ENDHLSL
        }
    }
}
