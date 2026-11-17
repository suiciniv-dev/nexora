using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Navegador;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class AnotacaoVM(Anotacao dados, Action<AnotacaoVM> remover) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Comentario), nameof(Resumo), nameof(Seletor))]
    public partial Anotacao Dados { get; set; } = dados;

    [ObservableProperty]
    public partial int Numero { get; set; }

    public int Id => Dados.Id;
    public string Comentario => Dados.Comentario;
    public string Resumo => Dados.Resumo;
    public string Seletor => Dados.Seletor;

    [RelayCommand]
    void Remover() => remover(this);
}
