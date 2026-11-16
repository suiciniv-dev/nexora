using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nexora.Core.Texto;

namespace Nexora.Core.Git;

public enum EstadoCheck { Pendente, Ok, Falhou, Pulado }

public enum ModoMerge { Squash, Merge, Rebase }

public sealed record CheckDoPr(string Nome, string Fluxo, EstadoCheck Estado, string Conclusao, string Link, DateTimeOffset? Inicio, DateTimeOffset? Fim)
{
    public string Chave => Fluxo.Length > 0 ? $"{Fluxo} / {Nome}" : Nome;

    public long? Execucao =>
        Regex.Match(Link, @"/actions/runs/(\d+)") is { Success: true } m && long.TryParse(m.Groups[1].Value, CultureInfo.InvariantCulture, out var id) ? id : null;
}

public sealed record RevisaoDoPr(string Id, string Autor, string Estado, string Corpo, DateTimeOffset? Quando);

public sealed record ComentarioDoPr(string Id, string Autor, string Corpo, string Url, DateTimeOffset? Quando);

public sealed record ConversaDoPr(string Id, string Arquivo, int? Linha, bool Resolvida, bool Desatualizada, IReadOnlyList<ComentarioDoPr> Comentarios)
{
    public ComentarioDoPr? Primeiro => Comentarios.FirstOrDefault();
    public DateTimeOffset? UltimaEm => Comentarios.Select(c => c.Quando).Where(q => q is not null).DefaultIfEmpty(null).Max();
    public int? Inicio { get; init; }
    public string Trecho { get; init; } = "";
}

public sealed record RevisorDoPr(string Login, string Estado, DateTimeOffset? Quando);

public sealed record DetalhePr(int Numero, string Titulo, string Url, string Estado)
{
    public string Corpo { get; init; } = "";
    public string Autor { get; init; } = "";
    public bool Rascunho { get; init; }
    public string BaseBranch { get; init; } = "";
    public string HeadBranch { get; init; } = "";
    public string ShaDoHead { get; init; } = "";
    public string Mergeavel { get; init; } = "UNKNOWN";
    public string EstadoDoMerge { get; init; } = "UNKNOWN";
    public string? Revisao { get; init; }
    public bool AutoMerge { get; init; }
    public DateTimeOffset? Atualizado { get; init; }
    public IReadOnlyList<CheckDoPr> Checks { get; init; } = [];
    public IReadOnlyList<RevisaoDoPr> Revisoes { get; init; } = [];
    public IReadOnlyList<string> RevisoresPedidos { get; init; } = [];
    public IReadOnlyList<ComentarioDoPr> Comentarios { get; init; } = [];
    public IReadOnlyList<ConversaDoPr> Conversas { get; init; } = [];
    public bool ConversasLidas { get; init; }
    public string? Eu { get; init; }

    public bool Aberto => Estado == "OPEN";
    public bool Mergeado => Estado == "MERGED";
    public bool TemConflito => Mergeavel == "CONFLICTING" || EstadoDoMerge == "DIRTY";
    public bool Atrasado => EstadoDoMerge == "BEHIND";
    public bool Bloqueado => EstadoDoMerge == "BLOCKED";
    public bool PodeMergear => Aberto && !Rascunho && !TemConflito && EstadoDoMerge is "CLEAN" or "HAS_HOOKS" or "UNSTABLE";
    public int ChecksOk => Checks.Count(c => c.Estado is EstadoCheck.Ok or EstadoCheck.Pulado);
    public int ChecksFalhos => Checks.Count(c => c.Estado == EstadoCheck.Falhou);
    public int ChecksPendentes => Checks.Count(c => c.Estado == EstadoCheck.Pendente);
    public IReadOnlyList<ConversaDoPr> ConversasAbertas => [.. Conversas.Where(c => !c.Resolvida)];

    public IReadOnlyList<RevisorDoPr> Revisores
    {
        get
        {
            var lista = Revisoes.Where(r => r.Autor.Length > 0 && r.Estado != "PENDING")
                .GroupBy(r => r.Autor, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var ordem = g.OrderBy(r => r.Quando ?? DateTimeOffset.MinValue).ToList();
                    var decisiva = ordem.LastOrDefault(r => r.Estado is "APPROVED" or "CHANGES_REQUESTED" or "DISMISSED") ?? ordem[^1];
                    return new RevisorDoPr(g.Key, decisiva.Estado, ordem[^1].Quando);
                })
                .Where(r => !RevisoresPedidos.Contains(r.Login, StringComparer.OrdinalIgnoreCase))
                .ToList();
            lista.AddRange(RevisoresPedidos.Select(l => new RevisorDoPr(l, "REQUESTED", null)));
            return lista;
        }
    }

    public EstadoPr ComoEstado() => new(Numero, Url, Estado, Rascunho, Revisao, ChecksOk, ChecksFalhos, ChecksPendentes);
}

public sealed record ConsultaPr(DetalhePr? Pr, string? Erro)
{
    public bool SemPr => Pr is null && Erro is null;
}

public enum TipoNovidadePr { CheckFalhou, Conflito, ComentarioNovo, RevisaoNova, Mergeado, Fechado }

public sealed record NovidadePr(TipoNovidadePr Tipo, string Chave, string? Autor = null, string? Texto = null, string? Url = null);

public static partial class GitHub
{
    public const string CamposDoPr = "number,title,body,url,state,isDraft,author,baseRefName,headRefName,headRefOid,mergeable,mergeStateStatus,reviewDecision,autoMergeRequest,updatedAt,reviewRequests,statusCheckRollup,reviews,comments";

    const string ConsultaConversas = """
        query($dono:String!,$repo:String!,$n:Int!){viewer{login} repository(owner:$dono,name:$repo){pullRequest(number:$n){reviewThreads(first:80){nodes{
          id isResolved isOutdated path line originalLine startLine originalStartLine
          comments(first:40){nodes{id author{login} body url createdAt diffHunk}}}}}}}
        """;

    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> LoginsDaPasta = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<ConsultaPr> DetalhePrAsync(string pasta, string branchOuNumero)
    {
        var r = await GhAsync(pasta, ["pr", "view", branchOuNumero, "--json", CamposDoPr], TimeSpan.FromSeconds(30));
        if (!r.Ok) return SemPrNaResposta(r) ? new ConsultaPr(null, null) : new ConsultaPr(null, r.Mensagem.Length > 0 ? r.Mensagem : Idioma.T("Não deu para ler o PR pelo gh."));
        if (LerDetalhePr(r.Saida) is not { } pr) return new ConsultaPr(null, Idioma.T("Não deu para ler o PR pelo gh."));
        if (PartesDaUrl(pr.Url) is not { } partes) return new ConsultaPr(pr, null);
        var c = await GhAsync(pasta, ["api", "graphql", "-f", "query=" + ConsultaConversas, "-f", "dono=" + partes.Dono, "-f", "repo=" + partes.Repo, "-F", "n=" + partes.Numero.ToString(CultureInfo.InvariantCulture)], TimeSpan.FromSeconds(30));
        if (!c.Ok) return new ConsultaPr(pr, null);
        var (conversas, eu) = LerConversasDoPr(c.Saida);
        if (eu is { Length: > 0 }) LoginsDaPasta[pasta] = eu;
        return new ConsultaPr(pr with { Conversas = conversas, ConversasLidas = true, Eu = eu }, null);
    }

    public static async Task<string?> DiffDoArquivoNoPrAsync(string pasta, DetalhePr pr, string arquivo)
    {
        var r = await GhAsync(pasta, ["pr", "diff", pr.Numero.ToString(CultureInfo.InvariantCulture), "--color", "never"], TimeSpan.FromSeconds(30));
        if (r.Ok && Nexora.Core.Revisao.ComentariosDeRevisao.ExtrairArquivo(r.Saida, arquivo) is { } doPr) return doPr;
        if (pr.BaseBranch.Length == 0) return null;
        var g = await ComandoGit.ExecutarAsync(pasta, ["diff", "--no-color", $"origin/{pr.BaseBranch}...HEAD", "--", arquivo], TimeSpan.FromSeconds(30));
        return g.Ok && g.Saida.Length > 0 ? g.Saida : null;
    }

    public static bool SemPrNaResposta(ResultadoGit r) =>
        r.Erro.Contains("no pull requests found", StringComparison.OrdinalIgnoreCase)
        || r.Erro.Contains("no open pull requests", StringComparison.OrdinalIgnoreCase);

    public static async Task<string?> EuAsync(string pasta)
    {
        if (LoginsDaPasta.TryGetValue(pasta, out var conhecido)) return conhecido;
        var r = await GhAsync(pasta, ["api", "user", "--jq", ".login"], TimeSpan.FromSeconds(15));
        if (!r.Ok || r.Saida.Trim() is not { Length: > 0 } login) return null;
        LoginsDaPasta[pasta] = login;
        return login;
    }

    public static Task<ResultadoGit> MarcarProntoAsync(string pasta, int numero) =>
        GhAsync(pasta, ["pr", "ready", numero.ToString(CultureInfo.InvariantCulture)], TimeSpan.FromSeconds(30));

    public static IReadOnlyList<string> ArgumentosDoMerge(int numero, ModoMerge modo, bool automatico, string? sha)
    {
        List<string> args = ["pr", "merge", numero.ToString(CultureInfo.InvariantCulture), modo switch
        {
            ModoMerge.Merge => "--merge",
            ModoMerge.Rebase => "--rebase",
            _ => "--squash",
        }];
        if (automatico) args.Add("--auto");
        if (sha is { Length: > 0 }) args.AddRange(["--match-head-commit", sha]);
        return args;
    }

    public static Task<ResultadoGit> MergearAsync(string pasta, int numero, ModoMerge modo, bool automatico, string? sha) =>
        GhAsync(pasta, ArgumentosDoMerge(numero, modo, automatico, sha), TimeSpan.FromSeconds(60));

    public static Task<ResultadoGit> DesligarAutoMergeAsync(string pasta, int numero) =>
        GhAsync(pasta, ["pr", "merge", numero.ToString(CultureInfo.InvariantCulture), "--disable-auto"], TimeSpan.FromSeconds(30));

    public static IReadOnlyList<string> ArgumentosDoNovoPr(string branch, string baseBranch, string titulo, string corpo, bool rascunho)
    {
        List<string> args = ["pr", "create", "--head", branch, "--base", baseBranch, "--title", titulo, "--body", corpo];
        if (rascunho) args.Add("--draft");
        return args;
    }

    public static async Task<ResultadoGit> CriarPrAsync(string pasta, string branch, string baseBranch, string titulo, string corpo, bool rascunho)
    {
        var envio = await ComandoGit.ExecutarAsync(pasta, ["push", "-u", "origin", "HEAD"], TimeSpan.FromSeconds(90));
        if (!envio.Ok) return envio;
        return await GhAsync(pasta, ArgumentosDoNovoPr(branch, baseBranch, titulo, corpo, rascunho), TimeSpan.FromSeconds(60));
    }

    public static string? UrlDoPrCriado(string saida) =>
        Regex.Match(saida, @"https://github\.com/[^\s/]+/[^\s/]+/pull/\d+") is { Success: true } m ? m.Value : null;

    public static DetalhePr? LerDetalhePr(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var o = doc.RootElement;
            if (o.ValueKind != JsonValueKind.Object || Inteiro(o, "number") is var numero && numero <= 0) return null;
            var revisao = Texto(o, "reviewDecision");
            return new DetalhePr(numero, Texto(o, "title"), Texto(o, "url"), Texto(o, "state").ToUpperInvariant())
            {
                Corpo = Texto(o, "body"),
                Autor = Login(o, "author"),
                Rascunho = o.TryGetProperty("isDraft", out var d) && d.ValueKind == JsonValueKind.True,
                BaseBranch = Texto(o, "baseRefName"),
                HeadBranch = Texto(o, "headRefName"),
                ShaDoHead = Texto(o, "headRefOid"),
                Mergeavel = Texto(o, "mergeable") is { Length: > 0 } m ? m.ToUpperInvariant() : "UNKNOWN",
                EstadoDoMerge = Texto(o, "mergeStateStatus") is { Length: > 0 } e ? e.ToUpperInvariant() : "UNKNOWN",
                Revisao = revisao.Length > 0 ? revisao : null,
                AutoMerge = o.TryGetProperty("autoMergeRequest", out var a) && a.ValueKind == JsonValueKind.Object,
                Atualizado = Data(o, "updatedAt"),
                Checks = LerChecks(o),
                Revisoes = Lista(o, "reviews") is { } rv
                    ? [.. rv.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object)
                        .Select(x => new RevisaoDoPr(Texto(x, "id"), Login(x, "author"), Texto(x, "state").ToUpperInvariant(), Texto(x, "body"), Data(x, "submittedAt")))]
                    : [],
                RevisoresPedidos = Lista(o, "reviewRequests") is { } rr
                    ? [.. rr.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object)
                        .Select(x => Texto(x, "login") is { Length: > 0 } l ? l : Texto(x, "slug") is { Length: > 0 } s ? s : Texto(x, "name"))
                        .Where(l => l.Length > 0)]
                    : [],
                Comentarios = Lista(o, "comments") is { } cs
                    ? [.. cs.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object && !(x.TryGetProperty("isMinimized", out var min) && min.ValueKind == JsonValueKind.True))
                        .Select(x => new ComentarioDoPr(Texto(x, "id"), Login(x, "author"), Texto(x, "body"), Texto(x, "url"), Data(x, "createdAt")))]
                    : [],
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static IReadOnlyList<CheckDoPr> LerChecks(JsonElement o)
    {
        if (!o.TryGetProperty("statusCheckRollup", out var lista) || lista.ValueKind != JsonValueKind.Array) return [];
        var checks = new List<CheckDoPr>();
        foreach (var c in lista.EnumerateArray())
        {
            if (c.ValueKind != JsonValueKind.Object) continue;
            var estado = Texto(c, "state").ToUpperInvariant();
            if (Texto(c, "__typename") == "StatusContext" || estado.Length > 0)
            {
                checks.Add(new CheckDoPr(Texto(c, "context"), "", estado switch
                {
                    "SUCCESS" => EstadoCheck.Ok,
                    "FAILURE" or "ERROR" => EstadoCheck.Falhou,
                    _ => EstadoCheck.Pendente,
                }, estado, Texto(c, "targetUrl"), Data(c, "startedAt"), null));
                continue;
            }
            var situacao = Texto(c, "status").ToUpperInvariant();
            var conclusao = Texto(c, "conclusion").ToUpperInvariant();
            var resultado = situacao is not "COMPLETED" and not "" ? EstadoCheck.Pendente
                : conclusao switch
                {
                    "SUCCESS" or "NEUTRAL" => EstadoCheck.Ok,
                    "SKIPPED" => EstadoCheck.Pulado,
                    "" => EstadoCheck.Pendente,
                    _ => EstadoCheck.Falhou,
                };
            checks.Add(new CheckDoPr(Texto(c, "name"), Texto(c, "workflowName"), resultado, conclusao.Length > 0 ? conclusao : situacao, Texto(c, "detailsUrl"), Data(c, "startedAt"), Data(c, "completedAt")));
        }
        return checks;
    }

    public static (IReadOnlyList<ConversaDoPr> Conversas, string? Eu) LerConversasDoPr(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) return ([], null);
            var eu = data.TryGetProperty("viewer", out var v) && v.ValueKind == JsonValueKind.Object ? Texto(v, "login") : "";
            var conversas = new List<ConversaDoPr>();
            if (Caminho(data, out var nos, "repository", "pullRequest", "reviewThreads", "nodes") && nos.ValueKind == JsonValueKind.Array)
                foreach (var t in nos.EnumerateArray())
                {
                    if (t.ValueKind != JsonValueKind.Object) continue;
                    IReadOnlyList<ComentarioDoPr> comentarios = Lista(t, "comments") is { } cs
                        ? [.. cs.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object)
                            .Select(x => new ComentarioDoPr(Texto(x, "id"), Login(x, "author"), Texto(x, "body"), Texto(x, "url"), Data(x, "createdAt")))]
                        : [];
                    var linha = Inteiro(t, "line") is > 0 and var l ? l : Inteiro(t, "originalLine") is > 0 and var ol ? ol : (int?)null;
                    var inicio = Inteiro(t, "startLine") is > 0 and var sl ? sl : Inteiro(t, "originalStartLine") is > 0 and var osl ? osl : (int?)null;
                    var trecho = Lista(t, "comments") is { } brutos
                        ? brutos.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).Select(x => Texto(x, "diffHunk")).FirstOrDefault() ?? ""
                        : "";
                    conversas.Add(new ConversaDoPr(Texto(t, "id"), Texto(t, "path"), linha,
                        t.TryGetProperty("isResolved", out var r) && r.ValueKind == JsonValueKind.True,
                        t.TryGetProperty("isOutdated", out var d) && d.ValueKind == JsonValueKind.True, comentarios)
                    {
                        Inicio = inicio is { } i && linha is { } fim && i < fim ? i : null,
                        Trecho = trecho,
                    });
                }
            return (conversas, eu.Length > 0 ? eu : null);
        }
        catch (JsonException)
        {
            return ([], null);
        }
    }

    public static IReadOnlyList<NovidadePr> Novidades(DetalhePr? antes, DetalhePr depois, string? eu = null)
    {
        var lista = new List<NovidadePr>();
        if (antes is null || antes.Numero != depois.Numero) return lista;
        bool DeOutro(string autor) => eu is not { Length: > 0 } || !autor.Equals(eu, StringComparison.OrdinalIgnoreCase);
        if (antes.Aberto && depois.Mergeado) lista.Add(new NovidadePr(TipoNovidadePr.Mergeado, $"merge:{depois.Numero}", Url: depois.Url));
        else if (antes.Aberto && depois.Estado == "CLOSED") lista.Add(new NovidadePr(TipoNovidadePr.Fechado, $"fechado:{depois.Numero}", Url: depois.Url));
        if (!antes.TemConflito && depois.TemConflito) lista.Add(new NovidadePr(TipoNovidadePr.Conflito, $"conflito:{depois.ShaDoHead}", Texto: depois.BaseBranch));
        var falhosAntes = antes.Checks.Where(c => c.Estado == EstadoCheck.Falhou).Select(c => c.Chave + "|" + c.Link).ToHashSet(StringComparer.Ordinal);
        foreach (var c in depois.Checks.Where(c => c.Estado == EstadoCheck.Falhou && !falhosAntes.Contains(c.Chave + "|" + c.Link)))
            lista.Add(new NovidadePr(TipoNovidadePr.CheckFalhou, $"check:{c.Chave}|{c.Link}", Texto: c.Chave, Url: c.Link));
        var vistos = antes.Comentarios.Select(c => c.Id).Concat(antes.Conversas.SelectMany(t => t.Comentarios).Select(c => c.Id)).ToHashSet(StringComparer.Ordinal);
        foreach (var c in depois.Comentarios.Concat(depois.Conversas.SelectMany(t => t.Comentarios)).Where(c => c.Id.Length > 0 && !vistos.Contains(c.Id) && DeOutro(c.Autor)))
            lista.Add(new NovidadePr(TipoNovidadePr.ComentarioNovo, $"comentario:{c.Id}", c.Autor, c.Corpo, c.Url));
        var revisoesAntes = antes.Revisoes.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var r in depois.Revisoes.Where(r => r.Id.Length > 0 && !revisoesAntes.Contains(r.Id) && r.Estado != "PENDING" && DeOutro(r.Autor)))
            lista.Add(new NovidadePr(TipoNovidadePr.RevisaoNova, $"revisao:{r.Id}", r.Autor, r.Estado, depois.Url));
        return lista;
    }

    static string Login(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var a) && a.ValueKind == JsonValueKind.Object ? Texto(a, "login") : "";

    static DateTimeOffset? Data(JsonElement o, string nome) =>
        DateTimeOffset.TryParse(Texto(o, nome), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var quando) && quando.Year > 1 ? quando : null;
}
