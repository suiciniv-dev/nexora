using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class PedidoDaFilaVM(ChatVM chat, ItemPermissao item, Action<ChatVM> abrir)
{
    public ChatVM Chat { get; } = chat;
    public ItemPermissao Item { get; } = item;
    public string Sessao => Chat.Titulo;
    public string Onde => Chat.Onde;
    public string Titulo => Item.Titulo;
    public string Detalhe => Resumos.UmaLinha(Item.Detalhe ?? "", 160);
    public bool TemDetalhe => Detalhe.Length > 0;
    public bool PodeEmTodas => Chat.PermissaoLiberavel(Item);
    public string DicaEmTodas => Idioma.F("Permitir {0} em todas as sessões até fechar o Nexora", ChaveDePermissao.Descrever(Item.Pedido.Ferramenta, Item.Pedido.Entrada));

    [RelayCommand]
    void Abrir() => abrir(Chat);
}

public sealed partial class VisaoGeralVM
{
    public JanelaVM Janela => _janela;
    public ObservableCollection<PedidoDaFilaVM> Fila { get; } = [];
    public bool TemFila => Fila.Count > 0;
    public string TituloFila => Fila.Count == 1 ? Idioma.T("1 pedido de permissão") : Idioma.F("{0} pedidos de permissão", Fila.Count);

    public void AtualizarFila()
    {
        var pendentes = _janela.Abertas
            .SelectMany(c => c.Itens.OfType<ItemPermissao>().Where(p => p.MostraBotoes).Select(p => (Chat: c, Item: p)))
            .ToList();
        if (pendentes.Count == Fila.Count && pendentes.Select(p => p.Item).SequenceEqual(Fila.Select(f => f.Item))) return;
        Fila.Clear();
        foreach (var (chat, item) in pendentes) Fila.Add(new PedidoDaFilaVM(chat, item, c => _janela.Navegar(c)));
        OnPropertyChanged(nameof(TemFila));
        OnPropertyChanged(nameof(TituloFila));
    }
}
