using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class ItemPensamento : ItemChat
{
    readonly DateTime _inicio;

    public ItemPensamento(DateTime? inicio = null) => _inicio = inicio ?? DateTime.Now;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Rotulo), nameof(TemTokens))]
    public partial int Tokens { get; set; }

    public bool TemTokens => Tokens > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Rotulo), nameof(Fase), nameof(Rodando), nameof(Concluido))]
    public partial TimeSpan? Duracao { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemTexto))]
    public partial string Texto { get; set; } = "";

    [ObservableProperty]
    public partial bool Aberto { get; set; }

    public bool Rodando => Duracao is null;
    public bool Concluido => Duracao is not null;
    public bool TemTexto => Texto.Length > 0;

    public string Rotulo
    {
        get
        {
            var tokens = Tokens > 0 ? "  ·  " + TextoTokens(Tokens) : "";
            return Fase + tokens;
        }
    }

    public string Fase => Duracao is { } d ? Idioma.F("Pensou por {0}", Tempo.Duracao(Math.Max(1000, (long)d.TotalMilliseconds))) : Idioma.T("Pensando…");

    public static string TextoTokens(int tokens) =>
        tokens < 1000 ? Idioma.F("{0} tokens", tokens) : Idioma.F("{0:0.#} mil tokens", tokens / 1000.0);

    public void Encerrar(DateTime? agora = null)
    {
        if (Duracao is null) Duracao = (agora ?? DateTime.Now) - _inicio;
    }

    [RelayCommand]
    void Alternar()
    {
        if (TemTexto) Aberto = !Aberto;
    }
}
