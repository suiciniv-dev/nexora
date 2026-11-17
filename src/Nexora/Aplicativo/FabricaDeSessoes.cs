using Nexora.Core.Claude;
using Nexora.Core.Codex;
using Nexora.Core.Provedores;
using Nexora.Core.Servidor;
using Nexora.Servicos;

namespace Nexora.Aplicativo;

public static class FabricaDeSessoes
{
    public static ISessaoAgente Criar(string agente, OpcoesSessao opcoes)
    {
        var descricao = CatalogoAgentes.De(agente);
        return descricao.Protocolo switch
        {
            ProtocoloAgente.Claude => new SessaoClaude(opcoes),
            ProtocoloAgente.Codex => new SessaoCodex(opcoes, Atualizacoes.Atual.ToString(3)),
            _ => CriarAcp(descricao, opcoes),
        };
    }

    public static ISessaoAgente CriarNoNucleo(string agente, OpcoesSessao opcoes, string? titulo) =>
        App.Nucleo is NucleoRemoto { Conectado: true } remoto && opcoes.Lancador is null
            ? new SessaoRemota(remoto, agente, opcoes, titulo)
            : Criar(agente, opcoes);

    static ISessaoAgente CriarAcp(DescricaoAgente agente, OpcoesSessao opcoes)
    {
        if (opcoes.Lancador is { Remoto: true })
            return new Core.Acp.SessaoAcp(opcoes, agente.Id, agente.Nome, agente.Executaveis[0], agente.Argumentos, null, Atualizacoes.Atual.ToString(3));
        var (arquivo, argumentos) = CatalogoAgentes.Comando(agente);
        return new Core.Acp.SessaoAcp(opcoes, agente.Id, agente.Nome, arquivo, argumentos, null, Atualizacoes.Atual.ToString(3));
    }
}
