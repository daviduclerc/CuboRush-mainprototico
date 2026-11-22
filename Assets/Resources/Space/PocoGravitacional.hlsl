#ifndef CUBORUSH_POCO_GRAVITACIONAL_INCLUDED
#define CUBORUSH_POCO_GRAVITACIONAL_INCLUDED

// Poço gravitacional aberto pelo Sol ao abaixar (valores enviados por PocoGravitacional.cs).
// A mesma conta existe em C# (PocoGravitacional.Deslocamento) para as paredes e o Sol.
float4 _PocoCentro;    // xy = centro (x, z) | z = profundidade atual | w = raio do núcleo
float4 _PocoForma;     // x = alcance do funil | y = meia largura da pista | z = intensidade 0..1
float4 _PocoOnda;      // xy = centro da onda (x, z) | z = raio atual da onda | w = amplitude atual
float4 _PocoOndaForma; // x = largura da frente de onda

// Campo gravitacional da pista (valores enviados por CampoGravitacional.cs).
float4 _CampoCurvatura;   // x = z do jogador | y = início da curva | z = intensidade (queda = k·d²)
float4 _CampoOnda;        // x = z da frente da onda plana | y = amplitude | z = largura
float4 _CampoBuraco;      // xy = centro do mini buraco negro (x, z) | z = profundidade | w = núcleo
float4 _CampoBuracoForma; // x = alcance do funil

// A atração do buraco negro dobra a pista para baixo lá na frente.
float CampoQueda(float z)
{
    float d = max(0.0, z - _CampoCurvatura.x - _CampoCurvatura.y);
    return _CampoCurvatura.z * d * d;
}

// Onda gravitacional plana vinda do buraco negro: crista na frente, vale raso atrás.
float CampoOndaPlana(float z)
{
    float u = (z - _CampoOnda.x) / max(_CampoOnda.z, 0.01);
    float v = u - 1.8;
    return _CampoOnda.y * (-exp(-u * u) + 0.4 * exp(-v * v));
}

// Funil do mini buraco negro numa das faixas.
float CampoBuraco(float2 xz)
{
    float nucleo = max(_CampoBuraco.w, 0.01);
    float r = length(xz - _CampoBuraco.xy);
    float funil = _CampoBuraco.z / (1.0 + (r * r) / (nucleo * nucleo));
    return funil * (1.0 - smoothstep(_CampoBuracoForma.x * 0.5, max(_CampoBuracoForma.x, 0.01), r));
}

// As bordas da pista ficam presas às vigas, como uma membrana esticada num quadro.
float PocoFixacao(float x)
{
    return 1.0 - smoothstep(_PocoForma.y - 0.9, _PocoForma.y, abs(x));
}

// Deformação local do espaço (poço do Sol, ondas e mini buraco negro), presa às vigas.
float PocoDeslocamentoLocal(float2 xz)
{
    float nucleo = max(_PocoCentro.w, 0.01);
    float r = length(xz - _PocoCentro.xy);
    // Perfil de funil: fundo arredondado e cauda longa, como nos diagramas de buraco negro.
    float funil = _PocoCentro.z / (1.0 + (r * r) / (nucleo * nucleo));
    funil *= 1.0 - smoothstep(_PocoForma.x * 0.5, max(_PocoForma.x, 0.01), r);

    // Onda gravitacional: crista e vale que se afastam do ponto onde o Sol afundou.
    float u = (length(xz - _PocoOnda.xy) - _PocoOnda.z) / max(_PocoOndaForma.x, 0.01);
    float onda = _PocoOnda.w * 2.0 * u * exp(-u * u);

    // A onda plana e o mini buraco ficam presos às vigas, como o poço.
    return (funil + onda + CampoOndaPlana(xz.y) + CampoBuraco(xz)) * PocoFixacao(xz.x);
}

// Quanto o espaço afunda (para baixo) em cada ponto do plano XZ: a deformação local mais
// a curvatura do horizonte, que dobra a pista inteira (vigas incluídas).
float PocoDeslocamento(float2 xz)
{
    return PocoDeslocamentoLocal(xz) + CampoQueda(xz.y);
}

float3 PocoAplicar(float3 posicaoMundo)
{
    posicaoMundo.y -= PocoDeslocamento(posicaoMundo.xz);
    return posicaoMundo;
}

// Normal da superfície curvada (y = -deslocamento), por diferença finita.
float3 PocoNormal(float2 xz)
{
    const float e = 0.04;
    float dx = PocoDeslocamento(xz + float2(e, 0)) - PocoDeslocamento(xz - float2(e, 0));
    float dz = PocoDeslocamento(xz + float2(0, e)) - PocoDeslocamento(xz - float2(0, e));
    return normalize(float3(dx / (2.0 * e), 1.0, dz / (2.0 * e)));
}

#endif
