using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class OpcaoEsforcoVM(string rotulo, bool ativa, bool padrao, Action escolher) : ObservableObject
{
    public string Rotulo { get; } = rotulo;
    public bool Ativa { get; } = ativa;
    public bool Padrao { get; } = padrao;
    public string Dica => Padrao ? Idioma.F("{0} (padrão do Claude)", Rotulo) : Rotulo;

    [RelayCommand]
    void Escolher() => escolher();
}
