Shader "CuboRush/UI/MenuEclipseSun"
{
    Properties
    {
        [PerRendererData] _MainTex ("Fotosfera", 2D) = "white" {}
        _Center ("Centro na arte", Vector) = (0.66, 0.42, 0, 0)
        _Size ("Tamanho na arte", Vector) = (0.1, 0.1, 0, 0)
        _Clock ("Tempo do menu", Float) = 0
        _Occlusion ("Oclusão pelo disco", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "../Sol3D/PlasmaSolar.cginc"

            sampler2D _MainTex;
            float4 _Center, _Size;
            float _Clock, _Occlusion;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varying { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            Varying vert(Input v)
            {
                Varying o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varying i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.9;
                float r = length(p);
                float z = sqrt(saturate(1 - r*r));
                float2 esfera = float2(frac(atan2(p.x,z)/6.2831853 + 0.5 + _Clock*0.065),
                    asin(clamp(p.y,-1,1))/3.14159265 + 0.5);
                float3 fotosfera = tex2D(_MainTex, esfera).rgb;
                float intensidade = dot(fotosfera, float3(0.299,0.587,0.114));
                float3 disco = PlasmaSolar(intensidade, esfera, z, _Clock);
                float angulo = atan2(p.y,p.x);
                float filamentos = 0.58 + 0.19*sin(angulo*17 - _Clock*3) + 0.16*sin(angulo*27 + _Clock*4);
                float coroa = exp(-max(0,r-1)*(22-10*filamentos));
                float aura = exp(-max(0,r-1)*7)*0.25;
                float alphaDisco = 1-smoothstep(0.967,1.01,r);
                float alpha = max(alphaDisco, max(coroa*0.83,aura)) * (1-smoothstep(1.22,1.44,r));

                // Enquanto o Sol emerge, o disco luminoso da arte passa à frente dele.
                float2 naArte = _Center.xy + (i.uv-0.5)*_Size.xy;
                float discoFrontal = 0.463 + (naArte.x-0.66)*0.51;
                float acimaDoDisco = smoothstep(discoFrontal-0.008, discoFrontal+0.009, naArte.y);
                float3 halo = lerp(float3(0.92, 0.10, 0.005), float3(1.0, 0.29, 0.012), filamentos);
                float3 cor = lerp(halo, disco, alphaDisco);
                return half4(cor, alpha * lerp(1, acimaDoDisco, _Occlusion) * i.color.a);
            }
            ENDHLSL
        }
    }
}

