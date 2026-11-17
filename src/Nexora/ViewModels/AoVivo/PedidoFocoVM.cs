using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

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
