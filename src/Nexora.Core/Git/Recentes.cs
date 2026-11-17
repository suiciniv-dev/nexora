using System.Globalization;
using System.Text.Json;

namespace Nexora.Core.Git;

public sealed record CommitRecente(string Sha, string Autor, DateTimeOffset Quando, string Assunto);

public sealed record PrRecente(int Numero, string Titulo, string Estado, bool Rascunho, DateTimeOffset Atualizado, string Url, string Autor, string Repositorio = "")
{
    public bool Mergeado => Estado.Equals("MERGED", StringComparison.OrdinalIgnoreCase);
    public bool Aberto => Estado.Equals("OPEN", StringComparison.OrdinalIgnoreCase);
}

public static class Recentes
{
    const char Separador = '\u001f';

    public static IReadOnlyList<CommitRecente> LerCommits(string saida)
    {
        var lista = new List<CommitRecente>();
        foreach (var linha in saida.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var partes = linha.TrimEnd('\r').Split(Separador);
            if (partes.Length < 4) continue;
            if (!DateTimeOffset.TryParse(partes[2], CultureInfo.InvariantCulture, DateTimeStyles.None, out var quando)) continue;
            lista.Add(new CommitRecente(partes[0], partes[1], quando, string.Join(Separador, partes[3..])));
        }
        return lista;
    }

    public static async Task<IReadOnlyList<CommitRecente>> CommitsAsync(string pasta, int quantos = 8)
    {
        var r = await ComandoGit.ExecutarAsync(pasta, ["log", "-n", quantos.ToString(CultureInfo.InvariantCulture), "--no-merges", $"--format=%h{Separador}%an{Separador}%aI{Separador}%s"], TimeSpan.FromSeconds(10));
        return r.Ok ? LerCommits(r.Saida) : [];
    }

    public static async Task<IReadOnlyList<(DateOnly Dia, int Commits)>> CommitsPorDiaAsync(string pasta, int dias)
    {
        var r = await ComandoGit.ExecutarAsync(pasta, ["log", "--all", $"--since={dias}.days", "--format=%aI"], TimeSpan.FromSeconds(15));
        if (!r.Ok) return [];
        return [.. r.Saida.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => DateTimeOffset.TryParse(l.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var q) ? DateOnly.FromDateTime(q.LocalDateTime) : (DateOnly?)null)
            .Where(d => d is not null)
            .GroupBy(d => d!.Value)
            .Select(g => (g.Key, g.Count()))];
    }

    public static IReadOnlyList<PrRecente> LerPrs(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];
            var lista = new List<PrRecente>();
            foreach (var pr in doc.RootElement.EnumerateArray())
            {
                if (!pr.TryGetProperty("number", out var numero) || !numero.TryGetInt32(out var n)) continue;
                var atualizado = pr.TryGetProperty("updatedAt", out var u) && u.ValueKind == JsonValueKind.String
                    && DateTimeOffset.TryParse(u.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var quando) ? quando : DateTimeOffset.MinValue;
                var autor = pr.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.Object && a.TryGetProperty("login", out var login) ? login.GetString() ?? "" : "";
                lista.Add(new PrRecente(n, Texto(pr, "title"), Texto(pr, "state"), pr.TryGetProperty("isDraft", out var r) && r.ValueKind == JsonValueKind.True,
                    atualizado, Texto(pr, "url"), autor));
            }
            return lista;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    static string Texto(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    public static async Task<IReadOnlyList<PrRecente>> PrsAsync(string pasta, int quantos = 8)
    {
        var r = await GitHub.GhAsync(pasta, ["pr", "list", "--state", "all", "--limit", quantos.ToString(CultureInfo.InvariantCulture),
            "--json", "number,title,state,isDraft,updatedAt,url,author"], TimeSpan.FromSeconds(20));
        return r.Ok ? LerPrs(r.Saida) : [];
    }
}
