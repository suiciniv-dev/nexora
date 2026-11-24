using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class AtalhosVM : ObservableObject
{
    [ObservableProperty]
    public partial bool Aberto { get; set; }

    public IReadOnlyList<GrupoDeAtalhos> Grupos { get; private set; } = [];

    string? _idiomaDosGrupos;

    public void Abrir()
    {
        if (_idiomaDosGrupos != Idioma.Codigo || Grupos.Count == 0)
        {
            Grupos = CatalogoDeAtalhos.Agrupados();
            _idiomaDosGrupos = Idioma.Codigo;
            OnPropertyChanged(nameof(Grupos));
        }
        Aberto = true;
    }

    [RelayCommand]
    public void Fechar() => Aberto = false;
}
