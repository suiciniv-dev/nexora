using System.Text.Json;

namespace Nexora.Core.Claude;

public abstract record EventoClaude;

public sealed record SessaoIniciada(string SessaoId, string? Modelo, string? Pasta, string? ModoPermissao) : EventoClaude;

public sealed record Pensando(int TokensEstimados) : EventoClaude;

public sealed record TextoParcial(string Texto) : EventoClaude;

public sealed record PensamentoComecou : EventoClaude;

public sealed record ConversaCompactada(bool Automatica, long TokensAntes, long? TokensDepois) : EventoClaude;

public sealed record PensamentoParcial(string Texto) : EventoClaude;

public sealed record FerramentaComecou(string Nome) : EventoClaude;

public sealed record BlocoEncerrado(int Indice) : EventoClaude;

public sealed record TextoCompleto(string Texto) : EventoClaude;

public sealed record MensagemDoUsuario(string Texto, int Imagens = 0) : EventoClaude;

public sealed record Anexo(string Nome, string TipoMidia, byte[] Dados);

public sealed record FerramentaChamada(string Id, string Nome, JsonElement Entrada) : EventoClaude;

public sealed record FerramentaRespondeu(string Id, string Conteudo, bool Erro, ResumoDeSubagente? Subagente = null) : EventoClaude;

public sealed record ResumoDeSubagente(string? Estado, string? AgenteId, string? Tipo, int? Ferramentas, long? DuracaoMs, long? Tokens, string? Relato = null)
{
    public bool EmSegundoPlano => Estado == "async_launched";
}

public sealed record EventoDeSubagente(string PaiId, EventoClaude Evento) : EventoClaude;

public enum FaseTarefa { Andou, Terminou }

public sealed record TarefaAtualizada(
    FaseTarefa Fase,
    string FerramentaId,
    string? Estado = null,
    int? Ferramentas = null,
    long? DuracaoMs = null,
    string? UltimaFerramenta = null,
    string? Resumo = null,
    string? Resultado = null,
    string? TarefaId = null) : EventoClaude;

public sealed record PermissaoPedida(
    string PedidoId,
    string Ferramenta,
    JsonElement Entrada,
    string? Descricao,
    string? Motivo,
    JsonElement? Sugestoes,
    string? AgenteId = null,
    string? FerramentaId = null) : EventoClaude;

public sealed record TurnoEncerrado(bool Erro, string? Texto, double? CustoUsd, long DuracaoMs, IReadOnlyDictionary<string, long>? Janelas = null) : EventoClaude
{
    public long? JanelaDe(string? modelo)
    {
        if (Janelas is not { Count: > 0 } janelas || string.IsNullOrEmpty(modelo)) return null;
        if (janelas.TryGetValue(modelo, out var exata)) return exata;
        foreach (var (chave, janela) in janelas)
            if (chave.StartsWith(modelo, StringComparison.OrdinalIgnoreCase) || modelo.StartsWith(chave, StringComparison.OrdinalIgnoreCase)) return janela;
        return null;
    }
}

public sealed record AvisoDoAgente(string Texto) : EventoClaude;

public sealed record ContextoUsado(long Tokens, bool? CacheLonga, string? Modelo, DateTimeOffset? Quando = null) : EventoClaude;

public sealed record UsoDoPlano(double? Sessao, double? Semana, DateTimeOffset? SessaoReinicia, DateTimeOffset? SemanaReinicia = null) : EventoClaude;

public sealed record ControleRespondido(string PedidoId, bool Sucesso, string? Erro, JsonElement? Dados) : EventoClaude;

public sealed record ComandoBarra(string Nome, string? Descricao, string? Dica);

public sealed record ModeloDisponivel(string Valor, string Nome, string? Descricao, string? Resolvido, IReadOnlyList<string> Esforcos, bool Rapido = false);

public sealed record ComandosDisponiveis(IReadOnlyList<ComandoBarra> Comandos, bool RemotoDisponivel, IReadOnlyList<ModeloDisponivel>? Modelos = null) : EventoClaude;

public sealed record ControleNaoSuportado(string PedidoId, string Subtipo) : EventoClaude;

public sealed record SubagentePedido(string PedidoId, string? FerramentaId, JsonElement Entrada) : EventoClaude;
