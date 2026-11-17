using System.Collections.ObjectModel;
using System.Diagnostics;
using Nexora.Core.Ambientes;
using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.Modelos;

public sealed record Local(string Nome, string Pasta, IReadOnlyList<string> Vinculados)
{
    public string? Branch => Repositorio.BranchAtual(Pasta);
    public string Descricao => Branch is { Length: > 0 } b ? $"{Nome}  ·  {b}" : Nome;
    public override string ToString() => Nome;
}
