using System.Globalization;
using System.Text.Json;

namespace Nexora.Core.Git;

public sealed record DiaDeContribuicao(DateOnly Dia, int Quantidade, int Nivel);

public sealed record PainelDoGitHub(string Login, int Total, IReadOnlyList<DiaDeContribuicao> Dias, IReadOnlyList<PrRecente> Meus, IReadOnlyList<PrRecente> ParaRevisar);

public static class GitHubDoUsuario
{
    const string Consulta = """
        query {
          viewer {
            login
            contributionsCollection {
              contributionCalendar {
                totalContributions
                weeks { contributionDays { date contributionCount contributionLevel } }
              }
            }
          }
          meus: search(query: "is:pr author:@me sort:updated-desc", type: ISSUE, first: 6) { nodes { ...Pr } }
          revisar: search(query: "is:pr is:open review-requested:@me sort:updated-desc", type: ISSUE, first: 6) { nodes { ...Pr } }
        }
        fragment Pr on PullRequest { number title state isDraft updatedAt url repository { name } author { login } }
        """;

    public static int Nivel(string? nivel) => nivel switch
    {
        "FIRST_QUARTILE" => 1,
        "SECOND_QUARTILE" => 2,
        "THIRD_QUARTILE" => 3,
        "FOURTH_QUARTILE" => 4,
        _ => 0,
    };

    public static PainelDoGitHub? Ler(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var dados) || dados.ValueKind != JsonValueKind.Object) return null;
            var login = "";
            var total = 0;
            var dias = new List<DiaDeContribuicao>();
            if (dados.TryGetProperty("viewer", out var viewer) && viewer.ValueKind == JsonValueKind.Object)
            {
                login = Texto(viewer, "login");
                if (viewer.TryGetProperty("contributionsCollection", out var colecao)
                    && colecao.TryGetProperty("contributionCalendar", out var calendario))
                {
                    total = calendario.TryGetProperty("totalContributions", out var t) && t.TryGetInt32(out var n) ? n : 0;
                    if (calendario.TryGetProperty("weeks", out var semanas) && semanas.ValueKind == JsonValueKind.Array)
                        foreach (var semana in semanas.EnumerateArray())
                            if (semana.TryGetProperty("contributionDays", out var lista) && lista.ValueKind == JsonValueKind.Array)
                                foreach (var dia in lista.EnumerateArray())
                                    if (DateOnly.TryParse(Texto(dia, "date"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
                                        dias.Add(new DiaDeContribuicao(data,
                                            dia.TryGetProperty("contributionCount", out var c) && c.TryGetInt32(out var q) ? q : 0,
                                            Nivel(Texto(dia, "contributionLevel"))));
                }
            }
            return new PainelDoGitHub(login, total, dias, Prs(dados, "meus"), Prs(dados, "revisar"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static IReadOnlyList<PrRecente> Prs(JsonElement dados, string nome)
    {
        if (!dados.TryGetProperty(nome, out var busca) || !busca.TryGetProperty("nodes", out var nos) || nos.ValueKind != JsonValueKind.Array) return [];
        var lista = new List<PrRecente>();
        foreach (var pr in nos.EnumerateArray())
        {
            if (pr.ValueKind != JsonValueKind.Object || !pr.TryGetProperty("number", out var numero) || !numero.TryGetInt32(out var n)) continue;
            var atualizado = DateTimeOffset.TryParse(Texto(pr, "updatedAt"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var quando) ? quando : DateTimeOffset.MinValue;
            var repositorio = pr.TryGetProperty("repository", out var r) && r.ValueKind == JsonValueKind.Object ? Texto(r, "name") : "";
            var autor = pr.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.Object ? Texto(a, "login") : "";
            lista.Add(new PrRecente(n, Texto(pr, "title"), Texto(pr, "state"), pr.TryGetProperty("isDraft", out var d) && d.ValueKind == JsonValueKind.True,
                atualizado, Texto(pr, "url"), autor, repositorio));
        }
        return lista;
    }

    static string Texto(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    public static async Task<PainelDoGitHub?> LerAsync()
    {
        var r = await ComandoGit.ExecutarGhAsync(Path.GetTempPath(), ["api", "graphql", "-f", "query=" + Consulta], TimeSpan.FromSeconds(25));
        return r.Ok ? Ler(r.Saida) : null;
    }
}
