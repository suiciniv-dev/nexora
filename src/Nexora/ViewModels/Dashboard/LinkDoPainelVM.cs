using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Nexora.Core.Painel;
using Nexora.Core.Texto;
using Nexora.Core.Uso;
using Nexora.Views;

namespace Nexora.ViewModels;

public sealed partial class LinkDoPainelVM(string titulo, string detalhe, string? link, string? selo = null, string? estado = null) : ObservableObject
{
    public string Titulo { get; } = titulo;
    public string Detalhe { get; } = detalhe;
    public string? Link { get; } = link;
    public string? Selo { get; } = selo;
    public string? Estado { get; } = estado;
    public bool TemSelo => !string.IsNullOrEmpty(Selo);
    public bool Mergeado => Estado == "merged";
    public bool Aberto => Estado == "open";
    public bool Fechado => Estado == "closed";

    [RelayCommand]
    void Abrir()
    {
        if (string.IsNullOrWhiteSpace(Link)) return;
        try
        {
            Sistema.Abrir(new ProcessStartInfo(Link) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }
}
