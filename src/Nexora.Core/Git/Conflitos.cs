namespace Nexora.Core.Git;

public sealed record ConflitoComBase(string Base, IReadOnlyList<string> Arquivos)
{
    public bool TemConflito => Arquivos.Count > 0;
}

public static class Conflitos
{
    public static async Task<string?> BaseAsync(string pasta, string? preferida)
    {
        var candidatas = new List<string>();
        if (preferida is { Length: > 0 } p) candidatas.AddRange(p.StartsWith("origin/", StringComparison.Ordinal) ? [p] : [$"origin/{p}", p]);
        var padrao = await ComandoGit.ExecutarAsync(pasta, ["rev-parse", "--abbrev-ref", "origin/HEAD"], TimeSpan.FromSeconds(10));
        if (padrao.Ok && padrao.Saida.Trim() is { Length: > 0 } origem && origem != "origin/HEAD") candidatas.Add(origem);
        candidatas.AddRange(["origin/main", "origin/master", "origin/dev", "origin/develop"]);
        foreach (var candidata in candidatas.Distinct(StringComparer.Ordinal))
        {
            var existe = await ComandoGit.ExecutarAsync(pasta, ["rev-parse", "--verify", "--quiet", candidata + "^{commit}"], TimeSpan.FromSeconds(10));
            if (existe.Ok) return candidata;
        }
        return null;
    }

    public static async Task<ConflitoComBase?> VerificarAsync(string pasta, string? basePreferida)
    {
        if (await BaseAsync(pasta, basePreferida) is not { } baseRef) return null;
        var r = await ComandoGit.ExecutarAsync(pasta, ["merge-tree", "--write-tree", "--name-only", "--no-messages", baseRef, "HEAD"], TimeSpan.FromSeconds(30));
        if (r.Codigo == 0) return new ConflitoComBase(baseRef, []);
        if (r.Codigo != 1) return null;
        return new ConflitoComBase(baseRef, Ler(r.Saida));
    }

    public static IReadOnlyList<string> Ler(string saida) =>
        [.. saida.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(l => l.Trim()).Where(l => l.Length > 0).Distinct()];
}
