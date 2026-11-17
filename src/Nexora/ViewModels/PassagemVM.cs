using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Senna;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class PassagemVM(Passagem dados, ChatVM chat) : ObservableObject
{
    public Passagem Dados { get; } = dados;
    public string Titulo => Dados.Titulo;
    public string Rotulo => Dados.Rotulo;
    public string Detalhe => $"{Dados.Projeto}  ·  {Core.Texto.Tempo.Relativo(Dados.Quando.LocalDateTime)}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Situacao))]
    public partial bool Copiado { get; set; }

    public string Situacao => Copiado ? Idioma.T("nome copiado") : Detalhe;

    public async void MarcarCopiado()
    {
        Copiado = true;
        await Task.Delay(1600);
        Copiado = false;
    }

    [RelayCommand]
    void Retomar() => chat.RetomarPassagem(this);
}
