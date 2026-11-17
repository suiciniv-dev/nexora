using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class CitadoVM(string caminho, Action<CitadoVM>? remover = null)
{
    public string Caminho { get; } = caminho;
    public string Nome => Path.GetFileName(Caminho.TrimEnd('/', '\\')) is { Length: > 0 } nome ? nome : Caminho;
    public string Pasta => (Path.GetDirectoryName(Caminho.TrimEnd('/', '\\')) ?? "").Replace('\\', '/');
    public bool TemPasta => Pasta.Length > 0;
    public bool Removivel => remover is not null;

    [RelayCommand]
    void Remover() => remover?.Invoke(this);
}
