Shader "Sol3D/Labaredas"
{
    Properties
    {
        [HDR] _Tint ("Cor do plasma", Color) = (3.5, 0.3, 0.1, 1)
        _LateralSide ("Lado permitido (zero desativa)", Float) = 0
        _SourceCenter ("Centro do Sol", Vector) = (0,0,0,0)
        _CutDistance ("Limite lateral", Float) = 0
        _RearOnly ("Somente atras", Float) = 0
        _RearClipDistance ("Limite traseiro", Float) = 0
        _SolarMaskRadii ("Raios de protecao do Sol", Vector) = (0,0,0,0)
        _RastroContinuo ("Rastro continuo", Float) = 0
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
            float _RearOnly;
            float _RearClipDistance;
            float4 _SolarMaskRadii;
            float _RastroContinuo;
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
                if (_RearOnly > 0.5)
                    clip(_SourceCenter.z - input.positionWS.z - _RearClipDistance);
                // Recorta a silhueta da fotosfera, inclusive quando ela se achata.
                // Evita que os billboards transparentes se sobreponham ao corpo.
                if (min(_SolarMaskRadii.x, min(_SolarMaskRadii.y, _SolarMaskRadii.z)) > 0.001)
                {
                    float3 camera = GetCameraPositionWS();
                    float3 raio = normalize(input.positionWS - camera) / _SolarMaskRadii.xyz;
                    float3 origem = (camera - _SourceCenter.xyz) / _SolarMaskRadii.xyz;
                    float t = -dot(origem, raio) / max(dot(raio, raio), 0.0001);
                    if (t > 0) clip(length(origem + raio * t) - 1.0);
                }
                // A linha tem o comprimento no eixo U e sua malha já afina ao longo do percurso.
                bool linha = _RastroContinuo > 0.5;
                float2 p = (linha ? input.uv.yx : input.uv) * 2.0 - 1.0;
                float tempo = _Time.y;
                // Volta ao rastro macio e contínuo: núcleo quente e bordas difusas.
                float comprimento = saturate((p.y + 1.0) * 0.5);
                float ondulacao = 0.075 * sin(p.y * 7.0 - tempo * 6.5)
                    + 0.028 * sin(p.y * 15.0 + tempo * 8.0);
                float eixo = p.x - ondulacao;
                float largura = linha ? 0.94 : 0.12 + 0.68 * pow(1.0 - comprimento, 0.82);
                float contorno = exp(-pow(abs(eixo) / largura, 2.2) * 2.2);
                float ponta = 1.0 - smoothstep(linha ? 0.90 : 0.76, 1.0, comprimento);
                float origem = linha ? 1.0 : smoothstep(0.0, 0.10, comprimento);
                float plume = contorno * ponta * origem;

                // Filetes sutis dão textura de plasma sem quebrar a silhueta macia.
                float fluxo = 0.5 + 0.5 * sin(p.y * 18.0 - tempo * 10.0
                    + sin(p.x * 13.0 + tempo * 2.0) * 0.45);
                float filetes = smoothstep(0.72, 0.98, fluxo) * plume;
                float eixoNucleo = eixo / (largura * 0.34);
                float nucleo = exp(-eixoNucleo * eixoNucleo * 2.0) * ponta * origem;
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

