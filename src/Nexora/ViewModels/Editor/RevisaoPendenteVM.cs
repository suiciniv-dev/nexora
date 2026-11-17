using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Nexora.Core.Revisao;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class RevisaoPendenteVM : ObservableObject
{
    readonly Action<string> _mandar;

    public RevisaoPendenteVM(Action<string> mandar)
    {
        _mandar = mandar;
        Itens.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(TemItens));
            OnPropertyChanged(nameof(TextoQuantidade));
            MandarCommand.NotifyCanExecuteChanged();
            if (Itens.Count == 0) ListaAberta = false;
        };
    }

    public ObservableCollection<ComentarioPendenteVM> Itens { get; } = [];
    public bool TemItens => Itens.Count > 0;
    public string TextoQuantidade => Itens.Count == 1 ? Idioma.T("1 comentário") : Idioma.F("{0} comentários", Itens.Count);

    [ObservableProperty]
    public partial bool ListaAberta { get; set; }

    public IEnumerable<ComentarioPendenteVM> De(string origem) => Itens.Where(i => i.Origem == origem);

    public void Adicionar(ComentarioPendenteVM comentario) => Itens.Add(comentario);

    public void Remover(ComentarioPendenteVM comentario)
    {
        if (!Itens.Remove(comentario)) return;
        comentario.AoRemover?.Invoke();
    }

    public string Pedido() => ComentariosDeRevisao.PedidoDoDiff([.. Itens.Select(i => i.Dados)]);

    [RelayCommand(CanExecute = nameof(TemItens))]
    void Mandar()
    {
        if (Itens.Count == 0) return;
        var texto = Pedido();
        Descartar();
        _mandar(texto);
    }

    [RelayCommand]
    void Descartar()
    {
        foreach (var item in Itens.ToList()) Remover(item);
        ListaAberta = false;
    }

    [RelayCommand]
    void AlternarLista() => ListaAberta = !ListaAberta && TemItens;
}
