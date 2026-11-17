using Nexora.Core.Git;
using Nexora.Core.Historico;
using Nexora.Core.Uso;

namespace Nexora.Core.Servidor;

public interface INucleo
{
    Task<IReadOnlyList<ConversaSalva>> ListarConversasAsync(CancellationToken cancelar = default);

    Task<IReadOnlyList<Parcela>> LerUsoAsync(string? cache, DateTime desde, bool porDia, CancellationToken cancelar = default);

    Task<Andamento> LerAndamentoAsync(string arquivo, string? pasta, CancellationToken cancelar = default);

    Task<ResultadoGit> RodarComandoAsync(PedidoDeComando pedido, CancellationToken cancelar = default);

    Task<TrechoBruto> LerTurnosAsync(string arquivo, int turnos, long ate, CancellationToken cancelar = default);

    Task<string?> UltimoModoAsync(string arquivo, CancellationToken cancelar = default);
}
