using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Ambientes;
using Nexora.Core.Claude;
using Nexora.Core.Provedores;
using Nexora.Core.Texto;
using Nexora.Temas;

namespace Nexora.ViewModels;

public sealed partial class RaizVM(string pasta, Action<RaizVM> remover)
{
    public string Pasta { get; } = pasta;
    public bool Existe => Directory.Exists(Pasta);

    [RelayCommand]
    void Remover() => remover(this);
}
