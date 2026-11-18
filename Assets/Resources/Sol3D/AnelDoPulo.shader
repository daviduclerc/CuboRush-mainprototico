Shader "Sol3D/AnelDoPulo"
{
    Properties
    {
        [HDR] _Tint ("Cor do cinturao", Color) = (3.2, 0.3, 0.1, 1)
        _Opacity ("Visibilidade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            float _Opacity;
            CBUFFER_END
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
                float r = input.uv.x;
                float bordas = smoothstep(0.0, 0.12, r) * (1.0 - smoothstep(0.80, 1.0, r));
                // Faixas concêntricas finas, como um cinturão, com uma borda luminosa.
                float faixas = 0.45 + 0.55 * pow(0.5 + 0.5 * cos(r * 62.83185), 3.0);
                float nucleo = exp(-pow((r - 0.30) * 16.0, 2.0));
                float alpha = bordas * saturate(faixas * 0.6 + nucleo * 0.4) * _Opacity;
                return half4(_Tint.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
