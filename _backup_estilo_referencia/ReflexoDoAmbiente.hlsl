#ifndef CUBORUSH_REFLEXO_DO_AMBIENTE_INCLUDED
#define CUBORUSH_REFLEXO_DO_AMBIENTE_INCLUDED

// Reflexo "falso" do cenário para espelhos e vidros: céu estrelado, pista com as linhas
// neon e o próprio Sol. Valores globais enviados por ReflexoDoAmbiente.cs e TrackPlanarReflection.cs.
TEXTURE2D(_CeuPanorama); SAMPLER(sampler_CeuPanorama);
float _CeuExposicao;
float4 _PistaFaixas;        // x = meia largura da pista | y = largura de cada faixa
float4 _TrackSunPosition;
half4 _TrackSunColor;

// Mesmo mapeamento do Skybox/Panoramic (latitude-longitude, sem rotação).
float3 CeuNaDirecao(float3 r)
{
    r = normalize(r);
    float latitude = acos(clamp(r.y, -1.0, 1.0));
    float longitude = atan2(r.z, r.x);
    float2 uv = float2(0.5, 1.0) - float2(longitude, latitude) * float2(0.5 / PI, 1.0 / PI);
    return SAMPLE_TEXTURE2D_LOD(_CeuPanorama, sampler_CeuPanorama, uv, 0).rgb * _CeuExposicao;
}

// Raio refletido que desce: acerta o piso (y = 0) e mostra metal escuro com as linhas neon.
float3 PistaNaDirecao(float3 origem, float3 r)
{
    float t = origem.y / max(-r.y, 0.0001);
    float3 ponto = origem + r * t;
    float meia = max(_PistaFaixas.x, 0.01);
    float faixa = max(_PistaFaixas.y, 0.01);
    float3 cor = float3(0.025, 0.03, 0.045);
    float dentro = 1.0 - step(meia + 0.1, abs(ponto.x));
    float k = (ponto.x + meia) / faixa;
    float distLinha = abs(k - round(k)) * faixa;
    float linha = (1.0 - smoothstep(0.02, 0.06, distLinha)) * dentro;
    cor += float3(1.0, 0.06, 0.55) * 2.2 * linha;
    // Ao longe o piso some no escuro, como no reflexo de verdade.
    return cor * exp(-t * 0.04) * dentro;
}

// Disco do Sol visto no reflexo, com halo.
float3 SolNaDirecao(float3 origem, float3 r)
{
    float3 paraSol = _TrackSunPosition.xyz - origem;
    float distancia = max(length(paraSol), 0.001);
    float cosAngulo = dot(normalize(r), paraSol / distancia);
    float raioAngular = atan(0.5 / distancia);
    float angulo = acos(clamp(cosAngulo, -1.0, 1.0));
    float disco = 1.0 - smoothstep(raioAngular * 0.85, raioAngular * 1.1, angulo);
    float halo = exp(-max(angulo - raioAngular, 0.0) * 9.0) * 0.35;
    return _TrackSunColor.rgb * (disco * 3.0 + halo);
}

float3 AmbienteRefletido(float3 origem, float3 r)
{
    float3 ambiente = r.y >= 0.0 ? CeuNaDirecao(r) : PistaNaDirecao(origem, r);
    // Faixa clara suave no horizonte, para o espelho ter leitura de volume.
    ambiente += float3(0.06, 0.07, 0.1) * exp(-abs(r.y) * 12.0);
    return ambiente + SolNaDirecao(origem, r);
}

#endif
