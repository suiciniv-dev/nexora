using Avalonia.Threading;
using Nexora.Core.Claude;
using Nexora.Core.Historico;
using Nexora.Core.Servidor;
using Nexora.Core.Texto;
using Nexora.Modelos;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    public string? IdNoServidor => _sessao is SessaoRemota remota ? remota.Id : null;

    public static ChatVM DeSessaoViva(SessaoRemota sessao, ResumoDaSessao resumo, ConversaSalva? conversa, Local local, IAnfitriao anfitriao, Action<UsoDoPlano> aoUsar, Action<ChatVM> aoFechar)
    {
        var chat = new ChatVM(local, conversa?.Titulo ?? resumo.Titulo ?? TituloNovo, resumo.SessaoId, anfitriao, aoUsar, aoFechar)
        {
            Agente = Agentes.Normalizar(resumo.Agente),
            Estado = EstadoChat.Pronta,
            Carregando = true,
        };
        chat.Branch ??= conversa?.Branch;
        if (conversa?.PrimeiraMensagem is { Length: > 0 } primeira) chat._pedidoInicial = Resumos.UmaLinha(primeira, 200);
        chat._sessao = sessao;
        chat.LigarSessao(sessao);
        chat.Carregamento = chat.ReanexarAsync(sessao, conversa);
        return chat;
    }

    void LigarSessao(ISessaoAgente sessao)
    {
        sessao.Evento += e => Dispatcher.UIThread.Post(() => Aplicar(e));
        sessao.Encerrou += (codigo, erros) => Dispatcher.UIThread.Post(() => AoEncerrar(sessao, codigo, erros));
    }

    async Task ReanexarAsync(SessaoRemota sessao, ConversaSalva? conversa)
    {
        if (EhClaude && conversa is not null) await CarregarHistoricoAsync(conversa.Arquivo);
        else if (!EhClaude) await CarregarDoAgenteAsync();
        else Carregando = false;
        if (_encerrada || _sessao != sessao) return;
        if (await sessao.AssinarAsync() is not { } estado || _sessao != sessao) return;
        if (estado.Trabalhando && Estado != EstadoChat.PrecisaDeVoce)
        {
            Estado = EstadoChat.Trabalhando;
            TurnoDesde ??= DateTime.Now;
        }
        foreach (var pendente in estado.Pendentes) Aplicar(pendente);
    }
}
