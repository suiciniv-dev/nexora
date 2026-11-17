using System.Text.Json;
using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.Modelos;

public sealed record IssuesGuardadas(bool SoMinhas, IReadOnlyList<RepoGuardado> Repos, IReadOnlyList<QuadroGh> Quadros, string Fonte, string? Visao, IReadOnlyList<ItemDoQuadro>? Itens);
