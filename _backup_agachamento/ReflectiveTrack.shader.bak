Shader "CuboRush/ReflectiveTrack"
{
    Properties { _BaseColor ("Liga metalica", Color) = (0.22,0.24,0.28,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            TEXTURE2D(_TrackPlanarTexture); SAMPLER(sampler_TrackPlanarTexture);
            float4x4 _TrackReflectionVP;
            float4 _TrackPlanarTexel;
            float4 _TrackSunPosition;
            half4 _TrackSunColor;
            float _TrackReflectionStrength;
            struct A { float4 p:POSITION; float3 normal:NORMAL; };
            struct V { float4 p:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float4 reflexo:TEXCOORD2; };
            V vert(A i) {
                V o;
                o.world=TransformObjectToWorld(i.p.xyz);
                o.p=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(i.normal);
                o.reflexo=mul(_TrackReflectionVP,float4(o.world,1));
                return o;
            }
            half4 frag(V i):SV_Target {
                float3 n=normalize(i.normal);
                Light principal=GetMainLight();
                float difusa=saturate(dot(n,principal.direction));
                half3 baseColor=_BaseColor.rgb*(0.25+difusa*principal.color*0.7);
                float distancia=distance(i.world.xz,_TrackSunPosition.xz);
                float proximidade=1-smoothstep(4,20,distancia);
                float3 v=normalize(GetCameraPositionWS()-i.world);
                float3 l=normalize(_TrackSunPosition.xyz-i.world+float3(0,0.2,0));
                float especular=pow(saturate(dot(n,normalize(l+v))),64);
                baseColor+=_TrackSunColor.rgb*especular*proximidade*0.35;
                float2 uv=i.reflexo.xy/max(i.reflexo.w,0.0001)*0.5+0.5;
                // A captura URP em RenderTexture usa a origem superior no Direct3D.
                #if UNITY_UV_STARTS_AT_TOP
                uv.y=1-uv.y;
                #endif
                half3 reflexo=SAMPLE_TEXTURE2D(_TrackPlanarTexture,sampler_TrackPlanarTexture,uv).rgb;
                half3 suavizado=SAMPLE_TEXTURE2D(_TrackPlanarTexture,sampler_TrackPlanarTexture,uv+_TrackPlanarTexel.xy).rgb;
                suavizado+=SAMPLE_TEXTURE2D(_TrackPlanarTexture,sampler_TrackPlanarTexture,uv-_TrackPlanarTexel.xy).rgb;
                reflexo=lerp(reflexo,suavizado*0.5,0.18);
                float borda=smoothstep(0,0.025,uv.x)*smoothstep(0,0.025,uv.y)*(1-smoothstep(0.975,1,uv.x))*(1-smoothstep(0.975,1,uv.y));
                float topo=smoothstep(0.8,0.98,n.y);
                float forca=_TrackReflectionStrength*proximidade*topo*borda;
                return half4(baseColor+reflexo*forca,1);
            }
            ENDHLSL
        }
    }
}


