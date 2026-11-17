using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class OpcaoVM(string rotulo, string? descricao, Action<OpcaoVM> aoEscolher) : ObservableObject
{
    public string Rotulo { get; } = rotulo;
    public string? Descricao { get; } = descricao;
    public bool TemDescricao => !string.IsNullOrWhiteSpace(Descricao);

    public Action<OpcaoVM>? Rapida { get; set; }

    [ObservableProperty]
    public partial bool Selecionada { get; set; }

    partial void OnSelecionadaChanged(bool value) => aoEscolher(this);

    [RelayCommand]
    void Escolher()
    {
        if (Rapida is { } rapida) rapida(this);
        else Selecionada = !Selecionada;
    }
}
