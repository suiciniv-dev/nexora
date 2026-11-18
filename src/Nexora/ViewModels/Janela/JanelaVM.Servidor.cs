using Avalonia.Threading;
using Nexora.Aplicativo;
using Nexora.Core.Servidor;

namespace Nexora.ViewModels;

public sealed partial class JanelaVM
{
    public void ReabrirSessoesVivas()
    {
        if (Demo || Isolada) return;
        _ = ReabrirSessoesVivasAsync();
        _ = App.HubPronto.ContinueWith(_ => Dispatcher.UIThread.Post(LigarHub), TaskScheduler.Default);
    }

    public static (IReadOnlyList<ResumoDaSessao> Anexar, IReadOnlyList<SessaoSolta> DoHistorico) PlanejarReabertura(
        IReadOnlyList<ResumoDaSessao> vivas, IReadOnlyList<SessaoSolta> soltas)
    {
        var anexar = vivas.Where(r => r.Ativa && r.Assinantes == 0).ToList();
        bool Viva(SessaoSolta s) => vivas.Any(r => r.Ativa && ((s.IdNoServidor is { } id && r.Id == id) || (s.SessaoId is { } sessao && r.SessaoId == sessao)));
        var doHistorico = soltas.Where(s => s.SessaoId is { Length: > 0 } && !Viva(s))
            .DistinctBy(s => s.SessaoId)
            .ToList();
        return (anexar, doHistorico);
    }

    public void LembrarSoltas()
    {
        if (Demo || Isolada) return;
        App.Config.SessoesSoltas =
        [
            .. Abertas.Where(c => c.SegueNoServidorAoFechar).Select(c => new SessaoSolta
            {
                IdNoServidor = c.IdNoServidor,
                SessaoId = c.SessaoId,
                Pasta = c.Pasta,
                Agente = c.Agente,
                Titulo = c.Titulo,
            }),
        ];
        App.Config.Salvar();
    }

    async Task ReabrirSessoesVivasAsync()
    {
        var soltas = App.Config.SessoesSoltas.ToList();
        var remoto = await ProcessoDoNucleo.Pronto.ConfigureAwait(false) is { Conectado: true } conectado ? conectado : null;
        var vivas = remoto is null ? [] : await remoto.ListarSessoesAsync().ConfigureAwait(false);
        var (anexar, doHistorico) = PlanejarReabertura(vivas, soltas);
        foreach (var resumo in anexar)
        {
            var conversa = resumo.SessaoId is { } id ? await Task.Run(() => ConversaDe(id, resumo.Pasta)).ConfigureAwait(false) : null;
            await Dispatcher.UIThread.InvokeAsync(() => Reanexar(remoto!, resumo, conversa));
        }
        foreach (var solta in doHistorico)
        {
            var conversa = await Task.Run(() => ConversaDe(solta.SessaoId!, solta.Pasta)).ConfigureAwait(false);
            if (conversa is not null) await Dispatcher.UIThread.InvokeAsync(() => ReabrirParada(conversa));
        }
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var id in anexar.Select(r => r.SessaoId).Concat(doHistorico.Select(s => s.SessaoId)).OfType<string>())
                if (OrquestraVM.EhOrquestrador(id)) Orquestra.Retomar(id);
            if (soltas.Count == 0) return;
            App.Config.SessoesSoltas = [];
            App.Config.Salvar();
        });
    }

    void Reanexar(NucleoRemoto remoto, ResumoDaSessao resumo, Core.Historico.ConversaSalva? conversa)
    {
        if (Abertas.Any(a => a.IdNoServidor == resumo.Id || (resumo.SessaoId is { } id && a.SessaoId == id))) return;
        var sessao = SessaoRemota.Anexar(remoto, resumo);
        var chat = ChatVM.DeSessaoViva(sessao, resumo, conversa, LocalDe(resumo.Pasta), this, AoUsar, Fechar);
        Abertas.Insert(0, chat);
    }

    void ReabrirParada(Core.Historico.ConversaSalva conversa)
    {
        if (Abertas.Any(a => a.SessaoId == conversa.SessaoId)) return;
        _ = ChatDe(conversa).AvisarQueParouSemAppAsync();
    }
}
