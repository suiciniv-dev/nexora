using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class AtalhosVM : ObservableObject
{
    [ObservableProperty]
    public partial bool Aberto { get; set; }

    public IReadOnlyList<GrupoDeAtalhos> Grupos { get; private set; } = [];

    public void Abrir()
    {
        Grupos = CatalogoDeAtalhos.Agrupados();
        OnPropertyChanged(nameof(Grupos));
        Aberto = true;
    }

    [RelayCommand]
    public void Fechar() => Aberto = false;
}
