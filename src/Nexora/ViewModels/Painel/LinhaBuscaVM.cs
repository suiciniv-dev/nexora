using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Arquivos;
using Nexora.Core.Git;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class LinhaBuscaVM(Achado achado, Action<Achado> abrir) : ObservableObject
{
    public int Linha => achado.Linha;
    public string Texto => achado.Texto;

    [RelayCommand]
    void Abrir() => abrir(achado);
}
