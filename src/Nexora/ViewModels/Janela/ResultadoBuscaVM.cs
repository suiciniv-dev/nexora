using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class ResultadoBuscaVM(string icone, string titulo, string detalhe, Action abrir)
{
    public string Icone { get; } = icone;
    public string Titulo { get; } = titulo;
    public string Detalhe { get; } = detalhe;
    public bool TemDetalhe => Detalhe.Length > 0;

    [RelayCommand]
    public void Abrir() => abrir();
}
