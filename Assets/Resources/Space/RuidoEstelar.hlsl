#ifndef CUBORUSH_RUIDO_ESTELAR_INCLUDED
#define CUBORUSH_RUIDO_ESTELAR_INCLUDED

// Ruído procedural compartilhado pelos shaders do estilo estelar (asteroides, cristais, pista).

float EstHash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float EstHash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float EstRuido3(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    float a = EstHash13(i);
    float b = EstHash13(i + float3(1, 0, 0));
    float c = EstHash13(i + float3(0, 1, 0));
    float d = EstHash13(i + float3(1, 1, 0));
    float e = EstHash13(i + float3(0, 0, 1));
    float g = EstHash13(i + float3(1, 0, 1));
    float h = EstHash13(i + float3(0, 1, 1));
    float k = EstHash13(i + float3(1, 1, 1));
    return lerp(lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y),
                lerp(lerp(e, g, f.x), lerp(h, k, f.x), f.y), f.z);
}

float EstRuido2(float2 x)
{
    float2 i = floor(x);
    float2 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    float a = EstHash12(i);
    float b = EstHash12(i + float2(1, 0));
    float c = EstHash12(i + float2(0, 1));
    float d = EstHash12(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float EstFbm3(float3 p)
{
    float v = 0.0;
    float a = 0.5;
    [unroll]
    for (int k = 0; k < 4; k++)
    {
        v += a * EstRuido3(p);
        p = p * 2.02 + float3(17.1, 9.3, 4.7);
        a *= 0.5;
    }
    return v;
}

float EstFbm2(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    [unroll]
    for (int k = 0; k < 4; k++)
    {
        v += a * EstRuido2(p);
        p = p * 2.03 + float2(13.7, 5.1);
        a *= 0.5;
    }
    return v;
}

// Normal "facetada" (low poly), sempre virada para a câmera.
float3 EstNormalFacetada(float3 posMundo, float3 paraCamera)
{
    // Em pixels minúsculos (objeto longe ou de lado) as derivadas quase se anulam e
    // normalize(0) dava NaN: o bloom espalhava esse pixel e a tela inteira piscava.
    float3 c = cross(ddy(posMundo), ddx(posMundo));
    float comprimento2 = dot(c, c);
    float3 n = comprimento2 > 1e-20 ? c * rsqrt(comprimento2) : paraCamera;
    return dot(n, paraCamera) < 0.0 ? -n : n;
}

#endif
