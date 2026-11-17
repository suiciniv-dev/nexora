using Nexora.Core.Historico;
using Nexora.Core.Uso;

namespace Nexora.Core.Servidor;

public interface INucleo
{
    Task<IReadOnlyList<ConversaSalva>> ListarConversasAsync(CancellationToken cancelar = default);

    Task<IReadOnlyList<Parcela>> LerUsoAsync(string? cache, DateTime desde, bool porDia, CancellationToken cancelar = default);

    Task<Andamento> LerAndamentoAsync(string arquivo, string? pasta, CancellationToken cancelar = default);
}
