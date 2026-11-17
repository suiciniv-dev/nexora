using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Ambientes;
using Nexora.Core.Git;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class ProjetosVM(JanelaVM janela) : ObservableObject
{
    public ObservableCollection<CartaoProjetoVM> Cartoes { get; } = [];
    public bool Vazio => Cartoes.Count == 0 && !janela.CarregandoAmbientes;
    public bool TemRaizes => App.Config.Raizes.Count > 0;
    public string TextoRaizes => Idioma.F("O Nexora procurou em: {0}", string.Join(", ", App.Config.Raizes));

    public void Atualizar()
    {
        Cartoes.Clear();
        foreach (var a in janela.Ambientes) Cartoes.Add(new CartaoProjetoVM(janela, a));
        OnPropertyChanged(nameof(Vazio));
        OnPropertyChanged(nameof(TemRaizes));
        OnPropertyChanged(nameof(TextoRaizes));
    }

    public void AdicionarPasta(string pasta)
    {
        if (janela.AdicionarPastaComoProjeto(pasta) is null) return;
        Atualizar();
    }

    public Task AdicionarRaiz(string pasta) => janela.AdicionarRaiz(pasta);

    public void Importar(string arquivo)
    {
        if (janela.ImportarWorkspace(arquivo) is null) return;
        Atualizar();
    }

    [RelayCommand]
    void NovoProjeto() => janela.NovoAmbienteCommand.Execute(null);
}
