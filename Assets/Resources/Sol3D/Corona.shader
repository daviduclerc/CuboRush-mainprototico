Shader "Sol3D/Corona"
{
    Properties
    {
        [HDR] _GlowColor ("Cor da corona", Color) = (1, 0.24, 0.045, 1)
        _Glow ("Intensidade", Range(0, 3)) = 1.25
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Front
            ZWrite Off
            Blend One One
            ColorMask RGB

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _GlowColor;
            float _Glow;

            struct Varyings
            {
                float4 clip : SV_POSITION;
                float3 localPosition : TEXCOORD0;
            };

            Varyings vert(appdata_base input)
            {
                Varyings output;
                output.clip = UnityObjectToClipPos(input.vertex);
                output.localPosition = input.vertex.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 cameraLocal = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                float3 orthoView = mul((float3x3)unity_WorldToObject, UNITY_MATRIX_V[2].xyz);
                float3 ray = normalize(lerp(cameraLocal - input.localPosition, orthoView, unity_OrthoParams.w));
                float3 closest = input.localPosition - dot(input.localPosition, ray) * ray;
                float radius = length(closest) * 1.32;
                clip(radius - 0.997);

                float3 direction = closest / max(length(closest), 0.001);
                float3 viewX = normalize(mul((float3x3)unity_WorldToObject, UNITY_MATRIX_V[0].xyz));
                float3 viewY = normalize(mul((float3x3)unity_WorldToObject, UNITY_MATRIX_V[1].xyz));
                float angle = atan2(dot(direction, viewY), dot(direction, viewX));
                // Mantém uma base de emissão contínua ao redor da esfera; as
                // variações são sutis para o lado inferior não apagar ao rolar.
                float plume = saturate(0.82 + 0.08 * sin(11 * angle + 0.8)
                    + 0.06 * sin(23 * angle - 1.2) + 0.035 * sin(37 * angle));
                float distanceFromSurface = max(0, radius - 1);
                float halo = 0.72 * exp(-distanceFromSurface * 24)
                    + 0.38 * exp(-distanceFromSurface * 9);
                float taper = saturate((1.40 - radius) / 0.15);
                return half4(_GlowColor.rgb * (_Glow * plume * halo * taper), 0);
            }
            ENDCG
        }
    }
}
