using Nexora.Core.Git;
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

    public Task<ResultadoGit> RodarComandoAsync(PedidoDeComando pedido, CancellationToken cancelar = default) =>
        ComandoGit.RodarAquiAsync(pedido);

    public Task<TrechoBruto> LerTurnosAsync(string arquivo, int turnos, long ate, CancellationToken cancelar = default) =>
        Task.Run(() => Transcricao.LerTurnosBrutos(arquivo, turnos, ate), cancelar);

    public Task<string?> UltimoModoAsync(string arquivo, CancellationToken cancelar = default) =>
        Task.Run(() => ConversasSalvas.UltimoModo(arquivo), cancelar);
}
