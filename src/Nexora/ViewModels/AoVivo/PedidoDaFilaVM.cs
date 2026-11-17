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
