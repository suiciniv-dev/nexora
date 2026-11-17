using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed class ItemAviso(string texto, TipoAviso tipo, string? chave = null) : ItemChat
{
    public string Texto { get; } = texto;
    public string? Chave { get; } = chave;
    public bool EhNovaSessao => tipo == TipoAviso.NovaSessao;
    public bool EhErro => tipo == TipoAviso.Erro;
    public bool EhInfo => tipo == TipoAviso.Info;
    public bool EhRodape => tipo == TipoAviso.Rodape;
    public bool EhTroca => tipo == TipoAviso.Troca;

    public static ItemAviso Erro(string texto) => new(texto, TipoAviso.Erro);
    public static ItemAviso Info(string texto) => new(texto, TipoAviso.Info);

    public static ItemAviso Troca(string texto, string? chave = null) => new(texto, TipoAviso.Troca, chave);

    public static ItemAviso NovaSessao(string texto) => new(texto, TipoAviso.NovaSessao);

    public static ItemAviso Rodape(TurnoEncerrado fim, string? modelo = null) =>
        new(modelo is { Length: > 0 } ? $"{Tempo.Duracao(fim.DuracaoMs)}  ·  {modelo}" : Tempo.Duracao(fim.DuracaoMs), TipoAviso.Rodape);
}
