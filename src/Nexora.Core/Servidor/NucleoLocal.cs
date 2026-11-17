using Nexora.Core.Historico;
using Nexora.Core.Uso;

namespace Nexora.Core.Servidor;

public sealed class NucleoLocal : INucleo
{
    public static NucleoLocal Instancia { get; } = new();

    public Task<IReadOnlyList<ConversaSalva>> ListarConversasAsync(CancellationToken cancelar = default) =>
        Task.Run(() => ConversasSalvas.Listar(), cancelar);

    public Task<IReadOnlyList<Parcela>> LerUsoAsync(string? cache, DateTime desde, bool porDia, CancellationToken cancelar = default) =>
        Task.Run(() => UsoLocal.Ler(UsoLocal.FontesPadrao, cache, desde, cancelar, porDia), cancelar);

    public Task<Andamento> LerAndamentoAsync(string arquivo, string? pasta, CancellationToken cancelar = default) =>
        Task.Run(() => Andamento.Ler(arquivo, pasta), cancelar);
}
