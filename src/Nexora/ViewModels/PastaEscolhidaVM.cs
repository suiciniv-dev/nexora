using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Ambientes;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class PastaEscolhidaVM(string pasta, Action<PastaEscolhidaVM> remover) : ObservableObject
{
    public string Pasta { get; } = pasta;
    public string Nome { get; } = Path.GetFileName(pasta.TrimEnd('\\', '/'));

    [RelayCommand]
    void Remover() => remover(this);
}
