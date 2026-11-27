Shader "Sol3D/Fotosfera"
{
    Properties
    {
        _MainTex ("Textura solar", 2D) = "white" {}
        _Brightness ("Intensidade", Range(0, 5)) = 1.10
        _LimbDarkening ("Escurecimento na borda", Range(0, 1)) = 0.64
        _Tint ("Cor do núcleo", Color) = (1, 1, 1, 1)
        _TintInfluence ("Influência da cor", Range(0, 1)) = 0
        _PlasmaDourado ("Plasma dourado", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "PlasmaSolar.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Brightness;
            float _LimbDarkening;
            half4 _Tint;
            float _TintInfluence;
            float _PlasmaDourado;

            struct Varyings
            {
                float4 clip : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
            };

            Varyings vert(appdata_base input)
            {
                Varyings output;
                output.clip = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 view = normalize(lerp(_WorldSpaceCameraPos - input.worldPosition,
                    UNITY_MATRIX_V[2].xyz, unity_OrthoParams.w));
                float facing = saturate(dot(normalize(input.worldNormal), view));
                float brightness = lerp(1.0, pow(facing, 0.55), _LimbDarkening) * _Brightness;
                half3 solar = tex2D(_MainTex, input.uv).rgb;
                half luminance = dot(solar, half3(0.299, 0.587, 0.114));
                half3 solarColor = _Tint.rgb * (0.30 + luminance * 1.60);
                solar = lerp(solar, solarColor, saturate(_TintInfluence));
                solar *= brightness;
                if (_PlasmaDourado > 0.0)
                    solar = lerp(solar, PlasmaSolar(luminance, input.uv, facing, _Time.y), _PlasmaDourado);
                return half4(solar, 1);
            }
            ENDCG
        }
    }
}
