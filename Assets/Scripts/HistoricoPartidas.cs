using System;
using System.Collections.Generic;
using UnityEngine;

internal enum ResultadoPartida { Interrompida, Vitoria, Derrota }

[Serializable]
internal sealed class PartidaSalva
{
    public int pontos;
    public int moedas;
    public int ondas;
    public int raids;
    public bool usaRaids;
    public ResultadoPartida resultado;

    public string Rotulo => resultado switch
    {
        ResultadoPartida.Vitoria => "Vitória",
        ResultadoPartida.Derrota => "Derrota",
        _ => "Partida interrompida"
    };

    public PartidaSalva(int pontos, int moedas, int quantidade, ResultadoPartida resultado, bool usaRaids = false)
    {
        this.pontos = pontos;
        this.moedas = moedas;
        this.usaRaids = usaRaids;
        if (usaRaids) raids = quantidade;
        else ondas = quantidade;
        this.resultado = resultado;
    }
}

[Serializable]
internal sealed class HistoricoPartidas
{
    public const int Capacidade = 3;
    const string Chave = "HistoricoPartidasV1";
    [SerializeField] List<PartidaSalva> partidas = new List<PartidaSalva>(Capacidade);

    public PartidaSalva Obter(int posicao) => posicao >= 0 && posicao < partidas.Count ? partidas[posicao] : null;

    public static HistoricoPartidas Carregar()
    {
        if (PlayerPrefs.HasKey(Chave))
        {
            try
            {
                HistoricoPartidas salvo = JsonUtility.FromJson<HistoricoPartidas>(PlayerPrefs.GetString(Chave));
                if (salvo?.partidas != null)
                {
                    salvo.partidas.RemoveAll(partida => partida == null);
                    salvo.Limitar();
                    return salvo;
                }
            }
            catch (ArgumentException)
            {
                Debug.LogWarning("Não foi possível carregar o histórico. Tentando recuperar a última partida salva.");
            }
        }

        var historico = new HistoricoPartidas();
        if (PlayerPrefs.HasKey("UltimaPontuacao"))
        {
            ResultadoPartida resultado = PlayerPrefs.GetString("UltimoResultado") switch
            {
                "Vitória" => ResultadoPartida.Vitoria,
                "Derrota" => ResultadoPartida.Derrota,
                _ => ResultadoPartida.Interrompida
            };
            bool usaRaids = PlayerPrefs.HasKey("UltimasRaids");
            int quantidade = PlayerPrefs.GetInt(usaRaids ? "UltimasRaids" : "UltimasOndas");
            historico.partidas.Add(new PartidaSalva(PlayerPrefs.GetInt("UltimaPontuacao"),
                PlayerPrefs.GetInt("UltimasMoedas"), quantidade, resultado, usaRaids));
        }
        return historico;
    }

    public void Registrar(PartidaSalva partida)
    {
        partidas.Insert(0, partida);
        Limitar();
        PlayerPrefs.SetString(Chave, JsonUtility.ToJson(this));

        // Mantém a partida mais recente legível pelas versões anteriores do jogo.
        PlayerPrefs.SetInt("UltimaPontuacao", partida.pontos);
        PlayerPrefs.SetInt("UltimasMoedas", partida.moedas);
        PlayerPrefs.SetInt("UltimasOndas", partida.ondas);
        if (partida.usaRaids) PlayerPrefs.SetInt("UltimasRaids", partida.raids);
        else PlayerPrefs.DeleteKey("UltimasRaids");
        PlayerPrefs.SetString("UltimoResultado", partida.Rotulo);
    }

    void Limitar()
    {
        if (partidas.Count > Capacidade)
            partidas.RemoveRange(Capacidade, partidas.Count - Capacidade);
    }
}
