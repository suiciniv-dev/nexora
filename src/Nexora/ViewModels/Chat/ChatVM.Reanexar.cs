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

    public bool SegueNoServidorAoFechar => _sessao is SessaoRemota { Ativa: true } && Estado is EstadoChat.Trabalhando or EstadoChat.PrecisaDeVoce;

    public async Task AvisarQueParouSemAppAsync()
    {
        await Carregamento;
        Adicionar(ItemAviso.Info(Idioma.T("Esta conversa seguiu rodando depois que o Nexora fechou e já parou. O que o agente fez está acima.")));
    }

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
        if (resumo.Dono is { } dono) Core.Orquestra.ServidorMcp.Apelidar(dono, chat.Codigo);
        if (!JanelaVM.Isolada) chat.ConfiguracaoComSegredos(null);
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
