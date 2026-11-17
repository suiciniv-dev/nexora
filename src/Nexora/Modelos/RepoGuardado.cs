using System.Text.Json;
using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.Modelos;

public sealed record RepoGuardado(string Pasta, string? Repo, IReadOnlyList<IssueGh>? Issues);
