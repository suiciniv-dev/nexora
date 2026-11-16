using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Nexora.Core.Segredos;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    string? _chaveSegredos;
    bool _reinicioPorSegredo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemSegredoNoCartao))]
    public partial ItemSegredo? SegredoPendente { get; set; }

    public bool TemSegredoNoCartao => SegredoPendente is not null;

    string? ConfiguracaoComSegredos(string? mcp)
    {
        _chaveSegredos ??= App.Segredos.Registrar(Codigo, Pasta, PedidoDeSegredoChegou);
        return Core.Mcp.HubMcp.Juntar(mcp, App.Segredos.Configuracao(_chaveSegredos));
    }

    IReadOnlyList<string> PermitidasComSegredos(IReadOnlyList<string>? permitidas) => [.. permitidas ?? [], FerramentaDeSegredo.NomeCompleto];

    IReadOnlyDictionary<string, string> AmbienteDoProjeto() => App.Segredos.Cofre.VariaveisDe(Pasta);

    void PedidoDeSegredoChegou(PedidoDeSegredo pedido) => Dispatcher.UIThread.Post(() => MostrarPedidoDeSegredo(pedido));

    public ItemSegredo MostrarPedidoDeSegredo(PedidoDeSegredo pedido)
    {
        DescarregarParcial();
        FecharPensamento();
        _textoAtual = null;
        var item = new ItemSegredo(pedido, SalvarSegredoAsync, RecusarSegredo);
        Adicionar(item);
        SegredoPendente ??= item;
        Atividade(item.Resumo);
        Conversou();
        Estado = EstadoChat.PrecisaDeVoce;
        return item;
    }

    async Task<string?> SalvarSegredoAsync(ItemSegredo item, string valor)
    {
        var erro = await App.Segredos.SalvarAsync(item.Pedido.Id, valor);
        if (erro is not null) return erro;
        if (item.Pedido.Destino == DestinoDoSegredo.Variavel && _sessao is { Ativa: true })
        {
            _reiniciar = true;
            _reinicioPorSegredo = true;
        }
        Dispatcher.UIThread.Post(AposSegredo);
        return null;
    }

    void RecusarSegredo(ItemSegredo item)
    {
        App.Segredos.Recusar(item.Pedido.Id);
        item.Encerrar(Idioma.T("Recusado"));
        AposSegredo();
    }

    void AposSegredo()
    {
        SegredoPendente = Itens.OfType<ItemSegredo>().FirstOrDefault(s => s.Pendente);
        if (SegredoPendente is null && PermissaoPendente is null && Estado == EstadoChat.PrecisaDeVoce)
            Estado = _sessao is { Ativa: true } ? EstadoChat.Trabalhando : EstadoChat.Pronta;
    }

    void EncerrarSegredosPendentes(string motivo)
    {
        foreach (var item in Itens.OfType<ItemSegredo>().Where(s => s.Pendente).ToList())
        {
            App.Segredos.Cancelar(item.Pedido.Id, "O turno acabou antes da resposta do usuário.");
            item.Encerrar(motivo);
        }
        SegredoPendente = null;
    }

    void EsquecerSegredos()
    {
        App.Segredos.Esquecer(Codigo, _chaveSegredos);
        _chaveSegredos = null;
    }
}
