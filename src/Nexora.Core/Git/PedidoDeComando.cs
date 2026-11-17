namespace Nexora.Core.Git;

public sealed record PedidoDeComando(string Programa, string Pasta, IReadOnlyList<string> Args, TimeSpan? Limite, IReadOnlyDictionary<string, string>? Ambiente, string? Entrada);
