using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Uso;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class FiltroUsoVM(string rotulo, string? valor, bool marcado, Action<string?> escolher) : ObservableObject
{
    public string Rotulo { get; } = rotulo;
    public string? Valor { get; } = valor;
    public bool Marcado { get; } = marcado;

    [RelayCommand]
    void Escolher() => escolher(Valor);
}
