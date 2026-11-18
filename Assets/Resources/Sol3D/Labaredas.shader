Shader "Sol3D/Labaredas"
{
    Properties
    {
        [HDR] _Tint ("Cor do plasma", Color) = (3.5, 0.3, 0.1, 1)
        _LateralSide ("Lado permitido (zero desativa)", Float) = 0
        _SourceCenter ("Centro do Sol", Vector) = (0,0,0,0)
        _CutDistance ("Limite lateral", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            float4 _SourceCenter;
            float _LateralSide;
            float _CutDistance;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float3 positionWS : TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                if (abs(_LateralSide) > 0.5)
                    clip((input.positionWS.x - _SourceCenter.x) * _LateralSide - _CutDistance);
                float2 p = input.uv * 2.0 - 1.0;
                float tempo = _Time.y;
                // Volta ao rastro macio e contínuo: núcleo quente e bordas difusas.
                float comprimento = saturate((p.y + 1.0) * 0.5);
                float ondulacao = 0.075 * sin(p.y * 7.0 - tempo * 6.5)
                    + 0.028 * sin(p.y * 15.0 + tempo * 8.0);
                float eixo = p.x - ondulacao;
                float largura = 0.12 + 0.68 * pow(1.0 - comprimento, 0.82);
                float contorno = exp(-pow(abs(eixo) / max(largura, 0.001), 2.2) * 2.2);
                float ponta = 1.0 - smoothstep(0.76, 1.0, comprimento);
                float origem = smoothstep(0.0, 0.10, comprimento);
                float plume = contorno * ponta * origem;

                // Filetes sutis dão textura de plasma sem quebrar a silhueta macia.
                float fluxo = 0.5 + 0.5 * sin(p.y * 18.0 - tempo * 10.0
                    + sin(p.x * 13.0 + tempo * 2.0) * 0.45);
                float filetes = smoothstep(0.72, 0.98, fluxo) * plume;
                float nucleo = exp(-pow(eixo / max(largura * 0.34, 0.001), 2.0) * 2.0) * ponta * origem;
                float alpha = saturate(plume * 0.82 + filetes * 0.18) * input.color.a;

                // A cor principal vem diretamente do Sol (vermelho, verde, azul, etc.).
                // Só o centro ganha um pequeno brilho branco, sem puxar para o laranja.
                half3 fogo = _Tint.rgb * (0.92 + 0.16 * fluxo);
                fogo = lerp(fogo, half3(1.0, 1.0, 1.0), saturate(nucleo * 0.22));
                fogo += _Tint.rgb * filetes * 0.12;
                return half4(fogo * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
