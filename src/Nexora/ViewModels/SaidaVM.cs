using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class SaidaVM(string caminho, Action<string, bool> abrir) : ObservableObject
{
    public string Caminho { get; } = caminho;
    public string Nome => Path.GetFileName(Caminho);
    public string Diretorio => Path.GetFileName(Path.GetDirectoryName(Caminho) ?? "");
    public string Icone => Path.GetExtension(Caminho).ToLowerInvariant() is ".cs" or ".ts" or ".js" or ".razor" or ".py" or ".java" or ".sql" ? "codigo" : "arquivo";

    [ObservableProperty]
    public partial int Edicoes { get; set; } = 1;

    [RelayCommand]
    void VerDiff() => abrir(Caminho, true);

    [RelayCommand]
    void Abrir() => abrir(Caminho, false);
}
