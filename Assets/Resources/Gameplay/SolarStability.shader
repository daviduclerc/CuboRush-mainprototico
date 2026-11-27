Shader "CuboRush/UI/SolarStability"
{
    Properties
    {
        [PerRendererData] _MainTex ("Fotosfera", 2D) = "white" {}
        _Color ("Cor UI", Color) = (1,1,1,1)
        _SunColor ("Cor do personagem", Color) = (1,0.035,0.045,1)
        _Fill ("Estabilidade", Range(0,1)) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            sampler2D _MainTex;
            fixed4 _Color;
            float4 _SunColor;
            float _Fill;
            float4 _ClipRect;
            struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct V { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; float4 world:TEXCOORD1; };
            V vert(A i) {
                V o; o.world=i.vertex; o.vertex=UnityObjectToClipPos(i.vertex);
                o.uv=i.uv; o.color=i.color*_Color; return o;
            }
            fixed4 frag(V i):SV_Target {
                float2 p=(i.uv-0.5)/0.36;
                float r=length(p);
                float z=sqrt(saturate(1-dot(p,p)));
                float2 uv=float2(atan2(p.x,z)/6.2831853+0.5+_Time.y*0.008,
                    asin(clamp(p.y,-1,1))/3.14159265+0.5);
                float3 surface=tex2D(_MainTex,uv).rgb;
                float lum=dot(surface,float3(0.299,0.587,0.114));
                float fill=saturate(_Fill);
                float y=p.y*0.5+0.5;
                // A energia permanece embaixo enquanto o nivel desce do topo para a base.
                float filled=1-smoothstep(fill-0.006,fill+0.006,y);
                // Cheio e vazio usam a mesma transicao continua, sem mudar de modo.
                float full=smoothstep(0.98,1,fill);
                float visible=smoothstep(0,0.02,fill);
                filled=lerp(filled,1,full)*visible;
                // A coroa acompanha o ponto mais proximo na borda do disco.
                // Evita que a faixa do nivel se estenda para fora da esfera.
                float coronaY=(p.y/max(1,r))*0.5+0.5;
                float coronaFill=1-smoothstep(fill-0.012,fill+0.012,coronaY);
                coronaFill=lerp(coronaFill,1,full)*visible;
                float limb=0.48+0.52*pow(z,0.45);
                float3 bright=(_SunColor.rgb*(0.12+pow(saturate(lum),1.2)*0.85)
                    +smoothstep(0.55,0.95,lum)*0.10)*limb;
                float3 dark=_SunColor.rgb*(0.025+lum*0.045)+0.008;
                float brilhoNivel=exp(-abs(y-fill)*170)*smoothstep(0,0.03,fill)*(1-smoothstep(0.97,1,fill));
                float3 disc=lerp(dark,bright,filled)+_SunColor.rgb*brilhoNivel*0.45;
                float angle=atan2(p.y,p.x);
                float flames=0.60+0.25*sin(angle*19-_Time.y*1.5)+0.15*sin(angle*31+_Time.y*2.1);
                float glow=exp(-max(r-1,0)*(17-5*flames));
                float rim=exp(-abs(r-1)*100);
                float activity=lerp(0.12,0.9,coronaFill);
                float outside=step(1,r);
                float alpha=lerp(1,glow*activity,outside)*(1-smoothstep(1.30,1.38,r));
                float3 rgb=lerp(disc,_SunColor.rgb*(0.65+flames*0.65),outside);
                rgb+=_SunColor.rgb*rim*lerp(0.20,0.65,filled);
                fixed4 result=fixed4(rgb,alpha)*i.color;
                #ifdef UNITY_UI_CLIP_RECT
                result.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a-0.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}




