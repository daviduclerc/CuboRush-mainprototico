#ifndef CUBO_RUSH_ECLIPSE_ACCRETION_INCLUDED
#define CUBO_RUSH_ECLIPSE_ACCRETION_INCLUDED

static const float2 EclipseCentro = float2(0.69, 0.494);
static const float2 EclipseAspecto = float2(1.7777778, 1.0);
// Eixo do brilho no disco da arte, medido na faixa de acreção.
static const float2 EclipseEixo = float2(0.971549, 0.236838);

float2 EclipseParaDisco(float2 arco)
{
    return float2(dot(arco, EclipseEixo), dot(arco, float2(-EclipseEixo.y, EclipseEixo.x)));
}

float2 EclipseParaArte(float2 disco)
{
    return float2(EclipseEixo.x * disco.x - EclipseEixo.y * disco.y,
        EclipseEixo.y * disco.x + EclipseEixo.x * disco.y);
}

float2 EclipseGirar(float2 ponto, float seno, float cosseno)
{
    return float2(cosseno * ponto.x - seno * ponto.y, seno * ponto.x + cosseno * ponto.y);
}

float2 EclipseAmostraEmOrbita(float2 arco, float2 disco, float pesoDisco, float angulo)
{
    float seno, cosseno;
    sincos(angulo, seno, cosseno);
    float2 arcoGirado = EclipseGirar(arco, seno, cosseno);
    float2 discoGirado = EclipseGirar(disco, seno, cosseno);
    discoGirado.y *= 0.31;
    return EclipseCentro + lerp(arcoGirado, EclipseParaArte(discoGirado), pesoDisco) / EclipseAspecto;
}

half3 EclipsePlasma(float2 uv, half3 corBase, float relogio)
{
    float2 arco = (uv - EclipseCentro) * EclipseAspecto;
    float2 disco = EclipseParaDisco(arco);
    float pesoDisco = 1 - smoothstep(0.12, 0.21, abs(disco.y));
    disco.y /= 0.31;
    float raio = length(arco);
    float calor = smoothstep(0.072, 0.33, corBase.r - corBase.b) * smoothstep(0.09, 0.42, corBase.r);
    float mascara = calor * smoothstep(0.17, 0.23, raio)
        * (1 - smoothstep(0.44, 0.78, raio)) * smoothstep(0.35, 0.52, uv.x);
    if (mascara <= 0) return corBase;

    // As duas fases mantêm o fluxo contínuo e preservam a silhueta escura.
    float faseA = frac(relogio * 0.50);
    float faseB = frac(faseA + 0.5);
    float mistura = smoothstep(0, 1, 1 - abs(2 * faseA - 1));
    float2 uvA = EclipseAmostraEmOrbita(arco, disco, pesoDisco, (0.5 - faseA) * 0.29);
    float2 uvB = EclipseAmostraEmOrbita(arco, disco, pesoDisco, (0.5 - faseB) * 0.29);
    half3 fluxo = lerp(AMOSTRAR_ECLIPSE(uvB), AMOSTRAR_ECLIPSE(uvA), mistura);

    float theta = atan2(arco.y, arco.x);
    float onda = pow(0.5 + 0.5 * cos(theta * 3 - relogio * 2.9 + raio * 15), 4);
    return lerp(corBase, fluxo, mascara * 0.54) * (1 + mascara * (onda - 0.27) * 0.30);
}

#endif
