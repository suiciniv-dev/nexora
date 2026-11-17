using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class CitacaoVM(string texto, Action<CitacaoVM>? remover = null)
{
    public string Texto { get; } = texto;
    public string Previa { get; } = Resumos.Limitar(string.Join("\n", texto.Replace("\r", "").Split('\n').Select(Resumos.SemMarcas).Where(l => l.Length > 0)), 600);
    public bool Removivel => remover is not null;

    [RelayCommand]
    void Remover() => remover?.Invoke(this);
}
