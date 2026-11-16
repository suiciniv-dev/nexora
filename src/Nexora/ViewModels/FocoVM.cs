using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed record OpcaoNumeradaVM(int Numero, OpcaoVM Opcao)
{
    public string Tecla => Numero.ToString(Idioma.Cultura);
}

public sealed class PedidoFocoVM(ChatVM chat, ItemPermissao item)
{
    public ChatVM Chat { get; } = chat;
    public ItemPermissao Item { get; } = item;
    public bool EhPergunta => Item.EhPergunta;
    public bool EhPlano => Item.TemPlano;
    public bool EhPermissao => !Item.EhPergunta;
    public bool PodeEmTodas => !Item.EhPergunta && !Item.TemPlano && Chat.PermissaoLiberavel(Item);
    public bool PodeSempre => !Item.EhPergunta && !Item.TemPlano && Item.PodeSempre;
    public PerguntaVM? Pergunta => Item.Perguntas.FirstOrDefault();
    public IReadOnlyList<PerguntaVM> OutrasPerguntas => [.. Item.Perguntas.Skip(1)];
    public bool TemOutrasPerguntas => Item.Perguntas.Count > 1;
    public IReadOnlyList<OpcaoNumeradaVM> Opcoes => Pergunta is { } p ? [.. p.Opcoes.Take(9).Select((o, i) => new OpcaoNumeradaVM(i + 1, o))] : [];
    public bool TemUltimaAtividade => Chat.UltimaAtividade is { Length: > 0 } fala
        && !fala.Contains(Item.Titulo, StringComparison.OrdinalIgnoreCase)
        && !Item.Titulo.Contains(fala.Trim(), StringComparison.OrdinalIgnoreCase)
        && !(Item.Detalhe is { Length: > 0 } d && fala.Contains(d, StringComparison.OrdinalIgnoreCase));
}

public sealed partial class FocoVM : ObservableObject
{
    readonly JanelaVM _janela;
    readonly DispatcherTimer _relogio;
    DateTime? _vazioDesde;

    public FocoVM(JanelaVM janela)
    {
        _janela = janela;
        _relogio = new DispatcherTimer(TimeSpan.FromMilliseconds(400), DispatcherPriority.Background, (_, _) => Atualizar());
    }

    public ObservableCollection<PedidoFocoVM> Pedidos { get; } = [];

    [ObservableProperty]
    public partial bool Aberto { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAtual), nameof(Contagem), nameof(Vazio), nameof(Proximos))]
    public partial PedidoFocoVM? Atual { get; set; }

    public bool TemAtual => Atual is not null;
    public bool Vazio => Atual is null;
    public int Indice => Atual is null ? -1 : Pedidos.IndexOf(Atual);
    public string Contagem => Atual is null ? "" : Idioma.F("{0} de {1}", Indice + 1, Pedidos.Count);
    public IReadOnlyList<PedidoFocoVM> Proximos => Atual is null ? [] : [.. Pedidos.Skip(Indice + 1).Concat(Pedidos.Take(Indice))];

    public static IReadOnlyList<(ChatVM Chat, ItemPermissao Item)> Pendentes(IEnumerable<ChatVM> chats) =>
        [.. chats.SelectMany(c => c.Itens.OfType<ItemPermissao>().Where(p => p.Pendente).Select(p => (c, p)))];

    public void Abrir()
    {
        Pedidos.Clear();
        Atual = null;
        _vazioDesde = null;
        Atualizar();
        Aberto = true;
        _relogio.Start();
    }

    [RelayCommand]
    void Fechar()
    {
        _relogio.Stop();
        Aberto = false;
        Atual = null;
        Pedidos.Clear();
    }

    public void Atualizar()
    {
        var pendentes = Pendentes(_janela.Abertas);
        var vivos = pendentes.Select(p => p.Item).ToHashSet();
        var atualIndice = Indice;
        foreach (var feito in Pedidos.Where(p => !vivos.Contains(p.Item)).ToList()) Pedidos.Remove(feito);
        foreach (var (chat, item) in pendentes)
            if (Pedidos.All(p => p.Item != item)) Pedidos.Add(new PedidoFocoVM(chat, item));
        if (Atual is null || !Pedidos.Contains(Atual))
            Atual = Pedidos.Count == 0 ? null : Pedidos[Math.Clamp(atualIndice, 0, Pedidos.Count - 1)];
        OnPropertyChanged(nameof(Contagem));
        OnPropertyChanged(nameof(Proximos));
        if (!Aberto) return;
        if (Atual is not null)
        {
            _vazioDesde = null;
            return;
        }
        _vazioDesde ??= DateTime.UtcNow;
        if (DateTime.UtcNow - _vazioDesde > TimeSpan.FromSeconds(2)) Fechar();
    }

    [RelayCommand]
    void Proximo() => Passar(1);

    [RelayCommand]
    void Anterior() => Passar(-1);

    void Passar(int passo)
    {
        if (Pedidos.Count == 0) return;
        Atual = Pedidos[((Indice + passo) % Pedidos.Count + Pedidos.Count) % Pedidos.Count];
    }

    [RelayCommand]
    void Ir(PedidoFocoVM? pedido)
    {
        if (pedido is not null && Pedidos.Contains(pedido)) Atual = pedido;
    }

    [RelayCommand]
    public void Permitir()
    {
        if (Atual is not { } a) return;
        if (a.EhPergunta)
        {
            if (a.Item.PodeResponder) a.Item.ResponderCommand.Execute(null);
        }
        else a.Item.PermitirCommand.Execute(null);
        Atualizar();
    }

    [RelayCommand]
    public void Negar()
    {
        if (Atual is not { EhPergunta: false } a) return;
        a.Item.NegarCommand.Execute(null);
        Atualizar();
    }

    [RelayCommand]
    public void EmTodas()
    {
        if (Atual is not { PodeEmTodas: true } a) return;
        a.Item.PermitirEmTodasCommand.Execute(null);
        Atualizar();
    }

    [RelayCommand]
    public void Sempre()
    {
        if (Atual is not { PodeSempre: true } a) return;
        a.Item.PermitirSempreCommand.Execute(null);
        Atualizar();
    }

    public void Escolher(int numero)
    {
        if (Atual is not { EhPergunta: true } a || a.Opcoes.FirstOrDefault(o => o.Numero == numero) is not { } escolhida) return;
        escolhida.Opcao.EscolherCommand.Execute(null);
        Atualizar();
    }

    [RelayCommand]
    public void AbrirConversa()
    {
        if (Atual is not { } a) return;
        Fechar();
        _janela.Navegar(a.Chat);
    }
}
