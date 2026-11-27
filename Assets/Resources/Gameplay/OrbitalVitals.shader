Shader "CuboRush/UI/OrbitalVitals"
{
 Properties
 {
  [PerRendererData] _MainTex ("Fotosfera", 2D) = "white" {}
  _Color ("Cor UI", Color) = (1,1,1,1)
  _SunColor ("Cor da estabilidade", Color) = (0.08,0.4,1,1)
  _LifeColor ("Cor da vida", Color) = (0.1,1,0.2,1)
  _Fill ("Estabilidade", Range(0,1)) = 1
  _Life ("Vida", Range(0,1)) = 1
  _FarScale ("Escala da extremidade distante", Range(0.4,1)) = 0.68
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
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
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
   float4 _SunColor, _LifeColor, _ClipRect;
   float _Fill, _Life, _FarScale;
   struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
   struct V { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; float4 world:TEXCOORD1; };
   V vert(A i) { V o; o.world=i.vertex; o.vertex=UnityObjectToClipPos(i.vertex); o.uv=i.uv; o.color=i.color*_Color; return o; }
   float Energy(float level,float height)
   {
    level=saturate(level);
    float lit=1-smoothstep(level-0.008,level+0.008,height);
    return lerp(lit,1,smoothstep(0.98,1,level))*smoothstep(0,0.02,level);
   }
   fixed4 frag(V i):SV_Target
   {
    // Projecao de um painel: a esquerda esta perto, a direita recua.
    // A inversa projetiva comprime tambem os segmentos, como a pista.
    float farScale=clamp(_FarScale,0.4,1);
    float depth=1/farScale;
    float sourceX=i.uv.x/(depth-(depth-1)*i.uv.x);
    float heightScale=lerp(1,farScale,i.uv.x);
    float2 projected=float2(sourceX,0.5+(i.uv.y-0.5)/heightScale);
    float2 p=(projected-0.5)*float2(2.4,1);
    float top=step(0,p.y);
    float level=lerp(_Fill,_Life,top);
    float3 tint=lerp(_SunColor.rgb,_LifeColor.rgb,top);
    float4 outColor=0;
    float radius=0.245;
    float r=length(p)/radius;
    float z=sqrt(saturate(1-r*r));
    float2 solarUV=float2(atan2(p.x/radius,z)/6.2831853+0.5+_Time.y*0.008,
      asin(clamp(p.y/radius,-1,1))/3.14159265+0.5);
    float lum=dot(tex2D(_MainTex,solarUV).rgb,float3(0.299,0.587,0.114));
    // As duas metades esvaziam de cima para baixo, sem salto em 100%.
    float height=lerp(p.y/radius+1,p.y/radius,top);
    float lit=Energy(level,height);
    float3 bright=(tint*(0.12+pow(saturate(lum),1.2)*0.85)+smoothstep(0.55,0.95,lum)*0.1)*(0.48+0.52*pow(z,0.45));
    float3 dark=tint*(0.025+lum*0.045)+0.008;
    if(r<=1) outColor=float4(lerp(dark,bright,lit),1);
    else if(r<1.25)
    {
     float a=atan2(p.y,p.x);
     float rays=0.65+0.2*sin(a*19-_Time.y*1.5)+0.15*sin(a*31+_Time.y*2.1);
     float h=lerp((p.y/r)/radius+1,(p.y/r)/radius,top);
     float alpha=exp(-(r-1)*(18-4*rays))*lerp(0.10,0.7,Energy(level,h));
     outColor=float4(tint*(0.7+rays*0.4),alpha*(1-smoothstep(1.15,1.25,r)));
    }
    // Barras curvas segmentadas, com extensoes laterais e aro metalico.
    float u=p.x/0.43;
    float curve=0.09+0.27*exp(-pow(abs(u),4));
    float slope=-0.27*exp(-pow(abs(u),4))*4*u*u*u/0.43;
    float dist=abs(abs(p.y)-curve)/sqrt(1+slope*slope);
    float progress=saturate((p.x+1.12)/2.24);
    if(abs(p.x)<1.12 && dist<0.046)
    {
     float brushed=0.5+0.5*sin(p.y*3400);
     float3 metal=float3(0.16,0.20,0.24)+brushed*0.025;
     if(dist>0.037) metal=lerp(float3(0.17,0.21,0.25),float3(0.65,0.74,0.80),saturate((dist-0.037)/0.009));
     else if(dist<0.030)
     {
      float cell=frac(progress*24);
      float segment=smoothstep(0.04,0.10,cell)*(1-smoothstep(0.90,0.96,cell));
      float active=step(progress,level)*step(0.0001,level);
      float plasma=dot(tex2D(_MainTex,float2(progress*1.4+_Time.y*0.01,abs(p.y)*4)).rgb,float3(0.299,0.587,0.114));
      float3 energy=tint*(0.25+plasma*0.9)+smoothstep(0.65,0.95,plasma)*0.08;
      metal=lerp(float3(0.028,0.040,0.050),energy,segment*active);
     }
     outColor=float4(metal,1);
    }
    float cap=max(abs(abs(p.x)-1.14)/0.035,abs(abs(p.y)-0.09)/0.055);
    if(cap<1)
    {
     float strip=1-smoothstep(0.004,0.009,abs(abs(p.x)-1.15));
     outColor=float4(lerp(float3(0.28,0.34,0.39),float3(0.05,0.9,1),strip),1);
    }
    // Fenda gravitacional fina: separa os percentuais sem ocultar o Sol.
    float slit=0.008+0.008*exp(-pow(p.x/0.38,2));
    if(abs(p.x)<1.12 && abs(p.y)<slit+0.003)
    {
     float edge=smoothstep(slit-0.001,slit+0.003,abs(p.y));
     float fade=1-smoothstep(0.8,1.12,abs(p.x));
     outColor=float4(lerp(float3(0.002,0.003,0.006),lerp(float3(1,0.5,0.08),float3(0.1,0.8,1),top),edge),fade);
    }
    outColor.rgb*=lerp(1.06,0.85,i.uv.x);
    outColor*=i.color;
    #ifdef UNITY_UI_CLIP_RECT
    outColor.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
    #endif
    #ifdef UNITY_UI_ALPHACLIP
    clip(outColor.a-0.001);
    #endif
    return outColor;
   }
   ENDCG
  }
 }
}

