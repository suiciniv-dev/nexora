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
    }

    async Task ReabrirSessoesVivasAsync()
    {
        if (await ProcessoDoNucleo.Pronto.ConfigureAwait(false) is not { Conectado: true } remoto) return;
        var vivas = await remoto.ListarSessoesAsync().ConfigureAwait(false);
        foreach (var resumo in vivas.Where(r => r.Ativa && r.Assinantes == 0))
        {
            var conversa = resumo.SessaoId is { } id ? await Task.Run(() => ConversaDe(id, resumo.Pasta)).ConfigureAwait(false) : null;
            await Dispatcher.UIThread.InvokeAsync(() => Reanexar(remoto, resumo, conversa));
        }
    }

    void Reanexar(NucleoRemoto remoto, ResumoDaSessao resumo, Core.Historico.ConversaSalva? conversa)
    {
        if (Abertas.Any(a => a.IdNoServidor == resumo.Id || (resumo.SessaoId is { } id && a.SessaoId == id))) return;
        var sessao = SessaoRemota.Anexar(remoto, resumo);
        var chat = ChatVM.DeSessaoViva(sessao, resumo, conversa, LocalDe(resumo.Pasta), this, AoUsar, Fechar);
        Abertas.Insert(0, chat);
    }
}
