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
            // ---------- Rachaduras do vidro (Voronoi com distância exata até a borda da célula) ----------
            float2 RachaHash22(float2 p)
            {
                p=float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3)));
                return frac(sin(p)*43758.5453);
            }
            // x = distância até a rachadura mais próxima (em unidades de célula) | y = sorteio da célula
            float2 RachaVoronoi(float2 x)
            {
                float2 n=floor(x), f=frac(x);
                float2 mg=0, mr=0;
                float md=8.0;
                [unroll] for(int j=-1;j<=1;j++)
                [unroll] for(int i=-1;i<=1;i++)
                {
                    float2 g=float2(i,j);
                    float2 r=g+RachaHash22(n+g)-f;
                    float d=dot(r,r);
                    if(d<md){ md=d; mr=r; mg=g; }
                }
                md=8.0;
                [unroll] for(int j2=-2;j2<=2;j2++)
                [unroll] for(int i2=-2;i2<=2;i2++)
                {
                    float2 g=mg+float2(i2,j2);
                    float2 r=g+RachaHash22(n+g)-f;
                    float2 dr=r-mr;
                    if(dot(dr,dr)>0.00001) md=min(md,dot(0.5*(mr+r),normalize(dr)));
                }
                return float2(md,RachaHash22(n+mg).x);
            }
            // Linha fina com antisserrilhado: 1 em cima da rachadura, 0 longe dela.
            float RachaLinha(float d,float espessura)
            {
                float aa=max(fwidth(d),0.0001);
                return 1.0-smoothstep(espessura,espessura+aa*1.5,d);
            }
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
                float3 paraCamera=GetCameraPositionWS()-i.world;
                float distCamera=length(paraCamera);
                float3 v=paraCamera/max(distCamera,0.0001);
                float2 pm=i.malha.xy;

                // Mármore/vidro escuro azul-ardósia, com manchas violeta mais claras (arte de referência).
                float manchas=smoothstep(0.30,0.78,EstFbm2(pm*0.055+3.0));
                half3 baseColor=lerp(half3(0.010,0.010,0.026),half3(0.034,0.024,0.072),manchas);
                baseColor*=0.85+0.30*difusa;
                // Nebulosa presa dentro do vidro: nuvens roxas e azuis bem suaves.
                float nuvem=EstFbm2(pm*0.09+float2(11.0,4.0));
                baseColor+=half3(0.22,0.06,0.40)*smoothstep(0.55,0.82,nuvem)*0.16;
                baseColor+=half3(0.03,0.08,0.26)*smoothstep(0.50,0.78,EstFbm2(pm*0.13+27.0))*0.12;

                // Rachaduras: rede angulosa de linhas claras, entortada por ruído para não parecer grade.
                float2 torcao=float2(EstFbm2(pm*0.22),EstFbm2(pm*0.22+19.0))-0.5;
                float2 serrilha=float2(EstFbm2(pm*1.9+5.0),EstFbm2(pm*1.9+41.0))-0.5;
                float2 pr=pm*0.21+torcao*0.9+serrilha*0.07;
                float2 grande=RachaVoronoi(pr);
                float2 fina=RachaVoronoi(pm*0.55+torcao*1.4+serrilha*0.16+7.3);
                float longe=1.0-smoothstep(28.0,85.0,distCamera);
                float longeFina=1.0-smoothstep(14.0,45.0,distCamera);
                // Só parte das bordas vira rachadura acesa; o resto fica um risco apagado.
                float acesa=smoothstep(0.45,0.80,EstFbm2(pm*0.15+grande.y*3.0+2.0));
                float linhaGrande=RachaLinha(grande.x,0.0045)*longe;
                float linhaFina=RachaLinha(fina.x,0.004)*longeFina*smoothstep(0.45,0.75,EstFbm2(pm*0.3+13.0));
                half3 corRacha=lerp(half3(0.42,0.40,0.62),half3(0.80,0.72,1.05),acesa);
                baseColor+=corRacha*linhaGrande*(0.07+0.95*acesa);
                baseColor+=half3(0.34,0.30,0.52)*linhaFina*0.12;
                // Brilho roxo vazando das rachaduras acesas, como luz por baixo do vidro.
                float halo=exp(-grande.x/0.035)*acesa*longe;
                baseColor+=half3(0.40,0.14,0.95)*halo*0.38;

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
                // O vidro espelha os objetos ao longo da pista toda; perto do Sol, com mais força.
                // Sobre as rachaduras o espelho "quebra" um pouco.
                float quebra=1.0-0.45*saturate(linhaGrande+linhaFina*0.5);
                float forca=_TrackReflectionStrength*lerp(0.55,1.0,proximidade)*topo*borda*quebra;
                half3 cor=baseColor+reflexo*forca;
                // Em ângulo rasante (lá longe) o vidro reflete o disco dourado do buraco negro.
                float fresnel=pow(1.0-saturate(dot(n,v)),5.0);
                float aoFundo=smoothstep(10.0,60.0,distCamera);
                cor+=half3(1.0,0.52,0.18)*fresnel*(0.25+0.55*aoFundo)*0.55;

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
