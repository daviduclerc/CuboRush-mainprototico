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
            #include "PocoGravitacional.hlsl"
            #include "RuidoEstelar.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            TEXTURE2D(_TrackPlanarTexture); SAMPLER(sampler_TrackPlanarTexture);
            float4x4 _TrackReflectionVP;
            float4 _TrackPlanarTexel;
            float4 _TrackSunPosition;
            half4 _TrackSunColor;
            float _TrackReflectionStrength;
            float4 _PistaFaixas;        // x = meia largura da pista | y = largura de cada faixa
            struct A { float4 p:POSITION; float3 normal:NORMAL; };
            struct V { float4 p:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float4 reflexo:TEXCOORD2; float3 malha:TEXCOORD3; };
            V vert(A i) {
                V o;
                float3 original=TransformObjectToWorld(i.p.xyz);
                // O piso afunda em funil em volta do Sol (poço gravitacional).
                float deslocamento=PocoDeslocamento(original.xz);
                o.world=original;
                o.world.y-=deslocamento;
                o.p=TransformWorldToHClip(o.world);
                float3 n=TransformObjectToWorldNormal(i.normal);
                if(n.y>0.5) n=PocoNormal(original.xz);
                o.normal=n;
                o.reflexo=mul(_TrackReflectionVP,float4(o.world,1));
                // Coordenadas antes da curvatura: a grade desenhada com elas se curva junto com o piso.
                // A grade forte só aparece na deformação local; a curva do horizonte ganha um traço leve.
                o.malha=float3(original.xz,PocoDeslocamentoLocal(original.xz)+min(CampoQueda(original.z)*0.08,0.12));
                return o;
            }
            half4 frag(V i):SV_Target {
                float3 n=normalize(i.normal);
                Light principal=GetMainLight();
                float difusa=saturate(dot(n,principal.direction));
                half3 baseColor=_BaseColor.rgb*(0.25+difusa*principal.color*0.7);

                // Vidro/mármore escuro com veios roxos luminosos (arte de referência).
                float2 pv=i.malha.xy*0.09;
                float baseRuido=EstFbm2(pv);
                float veio=pow(saturate(1-abs(2*frac(EstFbm2(pv*1.3+baseRuido*2.5)*3)-1)),10);
                float veioFino=pow(saturate(1-abs(2*frac(EstFbm2(pv*3.1+9)*4)-1)),18);
                float manchas=smoothstep(0.35,0.75,EstFbm2(pv*0.6+3));
                baseColor*=0.75+0.5*manchas;
                baseColor+=half3(0.34,0.16,0.78)*(veio*0.8+veioFino*0.5)*0.45;
                baseColor+=half3(0.30,0.85,1.0)*veioFino*0.06;

                // Brilho ciano das linhas espalhado sobre o vidro, violeta perto das bordas.
                float meiaPista=max(_PistaFaixas.x,0.01);
                float faixaPista=max(_PistaFaixas.y,0.01);
                float kFaixa=(i.malha.x+meiaPista)/faixaPista;
                float distLinhaFaixa=abs(kFaixa-round(kFaixa))*faixaPista;
                float pertoBorda=saturate(1-(meiaPista-abs(i.malha.x))/(faixaPista*0.6));
                half3 corEspalhada=lerp(half3(0.30,0.85,1.0),half3(0.62,0.32,1.0),pertoBorda);
                baseColor+=corEspalhada*exp(-distLinhaFaixa/0.22)*0.32;
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
                half3 cor=baseColor+reflexo*forca;

                // Malha do espaço-tempo: aparece só onde o piso está curvado.
                float curvatura=saturate(abs(i.malha.z)*3.0);
                float2 celula=i.malha.xy/0.5;
                float2 largura=max(fwidth(celula),0.0001);
                float2 distLinha=abs(frac(celula-0.5)-0.5)/largura;
                float linha=1-saturate(min(distLinha.x,distLinha.y)-0.25);
                half3 corMalha=lerp(_TrackSunColor.rgb,half3(0.55,0.35,1.0),0.5)*2.6;
                // O fundo do funil escurece, como a garganta de um buraco negro.
                cor*=1-0.55*saturate(i.malha.z/0.6);
                cor+=corMalha*linha*curvatura;
                return half4(cor,1);
            }
            ENDHLSL
        }
    }
}
