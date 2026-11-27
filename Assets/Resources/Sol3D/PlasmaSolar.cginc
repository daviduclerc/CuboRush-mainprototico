#ifndef CUBORUSH_PLASMA_SOLAR_INCLUDED
#define CUBORUSH_PLASMA_SOLAR_INCLUDED

float3 PlasmaSolar(float intensidade, float2 uv, float face, float tempo)
{
    float ondulacao = sin(uv.x * 182.212 + sin(uv.y * 47.0 + tempo) * 2.1 - tempo * 1.7);
    float calor = intensidade + ondulacao * 0.025;
    float brasa = smoothstep(0.14, 0.60, calor);
    float filamentos = smoothstep(0.40, 0.68, calor);
    float3 cor = lerp(float3(0.26, 0.009, 0.001), float3(1.0, 0.24, 0.008), brasa);
    cor += filamentos * float3(0.24, 0.46, 0.065);

    float limbo = pow(1.0 - face, 2.8);
    return cor * lerp(0.76, 1.0, face) + limbo * float3(0.58, 0.16, 0.005);
}

#endif
