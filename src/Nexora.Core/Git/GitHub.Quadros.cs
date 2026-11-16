using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nexora.Core.Texto;

namespace Nexora.Core.Git;

public sealed record Etiqueta(string Nome, string Cor);

public sealed record ValorDeCampo(string Nome, string? Cor, string? OpcaoId);

public sealed record CampoDoQuadro(string Id, string Nome, IReadOnlyList<OpcaoStatus> Opcoes);

public sealed record VisaoDoQuadro(string Nome, string Filtro, string? AgruparPor);

public sealed record ContaGh(string Conta, bool Ativa, IReadOnlyList<string> Escopos)
{
    public bool VeQuadros => Escopos.Contains("project") || Escopos.Contains("read:project");
}

public sealed record QuadroGh(string Id, string Titulo, int Numero, string Url, string Repositorio, string Pasta)
{
    public IReadOnlyList<CampoDoQuadro> Campos { get; init; } = [];
    public IReadOnlyList<VisaoDoQuadro> Visoes { get; init; } = [];
    public string? CampoIteracao { get; init; }
    public bool Detalhado { get; init; }

    public CampoDoQuadro? Campo(params string[] nomes) =>
        Campos.FirstOrDefault(c => nomes.Any(n => c.Nome.Equals(n, StringComparison.OrdinalIgnoreCase)));

    public CampoDoQuadro? Status => Campo("Status");
}

public sealed record PrLigado(int Numero, string Url, string Estado, bool Rascunho, string Branch, string Repositorio, string? Revisao, string? Checks)
{
    public bool Aberto => Estado == "OPEN";
    public bool Mergeado => Estado == "MERGED";
    public bool ChecksFalharam => Checks is "FAILURE" or "ERROR";
    public bool ChecksRodando => Checks is "PENDING" or "EXPECTED";
    public bool ChecksOk => Checks == "SUCCESS";
}

public sealed record ItemDoQuadro(
    string ItemId, string Tipo, int Numero, string Titulo, string Url, string Estado, string Repositorio,
    IReadOnlyList<string> Responsaveis, IReadOnlyList<Etiqueta> Etiquetas, IReadOnlyDictionary<string, ValorDeCampo> Campos,
    string? Iteracao, IReadOnlyList<int> Prs, DateTimeOffset? Atualizada)
{
    public IReadOnlyList<PrLigado> PrsLigados { get; init; } = [];
    public bool EhPr => Tipo == "PullRequest";
    public bool Aberto => Estado is "OPEN" or "";

    public bool Combina(string busca)
    {
        var alvo = FluxoDaIssue.Normalizar(string.Join(' ', [Titulo, Numero.ToString(CultureInfo.InvariantCulture), Repositorio,
            .. Responsaveis, .. Etiquetas.Select(e => e.Nome), .. Campos.Values.Select(v => v.Nome)]));
        return FluxoDaIssue.Normalizar(busca).Split(' ', StringSplitOptions.RemoveEmptyEntries).All(alvo.Contains);
    }

    public bool MeuOuLivre(string? eu) =>
        Responsaveis.Count == 0 || eu is { Length: > 0 } && Responsaveis.Any(r => r.Equals(eu, StringComparison.OrdinalIgnoreCase));

    public ValorDeCampo? Valor(params string[] nomes) =>
        nomes.Select(n => Campos.FirstOrDefault(c => c.Key.Equals(n, StringComparison.OrdinalIgnoreCase)).Value).FirstOrDefault(v => v is not null);

    public static ItemDoQuadro DaIssue(IssueGh issue, string repositorio) =>
        new("", "Issue", issue.Numero, issue.Titulo, issue.Url, "OPEN", repositorio, issue.Responsaveis, issue.EtiquetasComCor,
            new Dictionary<string, ValorDeCampo>(), null, [.. issue.PrsLigados.Select(p => p.Numero)], issue.Atualizada)
        {
            PrsLigados = issue.PrsLigados,
        };
}

public sealed record RespostaGh<T>(T? Valor, bool SemPermissao, string? Erro = null) where T : class;

public sealed record IssuesEQuadros(IReadOnlyList<IssueGh>? Issues, RespostaGh<IReadOnlyList<QuadroGh>> Quadros);

public static partial class GitHub
{
    const string ConsultaRepositorio = """
        query($dono:String!,$repo:String!,$minhas:String!,$livres:String!,$soMinhas:Boolean!,$quadros:Boolean!){
          minhas:search(query:$minhas,type:ISSUE,first:50)@include(if:$soMinhas){nodes{...Campos}}
          livres:search(query:$livres,type:ISSUE,first:50)@include(if:$soMinhas){nodes{...Campos}}
          repository(owner:$dono,name:$repo){
            abertas:issues(first:50,states:OPEN,orderBy:{field:UPDATED_AT,direction:DESC})@skip(if:$soMinhas){nodes{...Campos}}
            projectsV2(first:10,orderBy:{field:UPDATED_AT,direction:DESC})@include(if:$quadros){nodes{id title number url closed}}
            issues(first:40,orderBy:{field:UPDATED_AT,direction:DESC})@include(if:$quadros){nodes{projectItems(first:5){nodes{project{id title number url closed}}}}}}}
        fragment Campos on Issue{number title url updatedAt labels(first:10){nodes{name color}} assignees(first:6){nodes{login}} closedByPullRequestsReferences(first:5,includeClosedPrs:true){nodes{...Pr}}}
        fragment Pr on PullRequest{number url state isDraft headRefName reviewDecision repository{nameWithOwner} commits(last:1){nodes{commit{statusCheckRollup{state}}}}}
        """;

    const string ConsultaDoQuadro = """
        query($id:ID!){node(id:$id){... on ProjectV2{id title number url
          fields(first:40){nodes{... on ProjectV2SingleSelectField{id name options{id name color}} ... on ProjectV2IterationField{id name}}}
          views(first:30,orderBy:{field:POSITION,direction:ASC}){nodes{name number filter layout groupByFields(first:1){nodes{... on ProjectV2SingleSelectField{name} ... on ProjectV2IterationField{name} ... on ProjectV2Field{name}}}}}}}}
        """;

    const string ConsultaItens = """
        query($id:ID!,$filtro:String,$depois:String){node(id:$id){... on ProjectV2{items(first:100,after:$depois,query:$filtro){pageInfo{hasNextPage endCursor} nodes{
          id
          content{__typename
            ... on Issue{number title url state updatedAt repository{nameWithOwner} assignees(first:6){nodes{login}} labels(first:10){nodes{name color}} closedByPullRequestsReferences(first:5,includeClosedPrs:true){nodes{...Pr}}}
            ... on PullRequest{number title url state updatedAt repository{nameWithOwner} assignees(first:6){nodes{login}} labels(first:10){nodes{name color}} ...Pr}
            ... on DraftIssue{title updatedAt}}
          fieldValues(first:20){nodes{__typename
            ... on ProjectV2ItemFieldSingleSelectValue{name color optionId field{... on ProjectV2SingleSelectField{name}}}
            ... on ProjectV2ItemFieldIterationValue{title field{... on ProjectV2IterationField{name}}}}}}}}}}
        fragment Pr on PullRequest{number url state isDraft headRefName reviewDecision repository{nameWithOwner} commits(last:1){nodes{commit{statusCheckRollup{state}}}}}
        """;

    public static IReadOnlyList<ContaGh> LerContasGh(string saidaDoStatus)
    {
        var lista = new List<ContaGh>();
        foreach (var bloco in Regex.Split(saidaDoStatus, @"(?=Logged in to \S+ account )"))
        {
            var m = Regex.Match(bloco, @"^Logged in to \S+ account (\S+)");
            if (!m.Success) continue;
            var escopos = Regex.Match(bloco, @"Token scopes:\s*(.*)") is { Success: true } e
                ? e.Groups[1].Value.Split(',').Select(s => s.Trim().Trim('\'', '"')).Where(s => s.Length > 0).ToList()
                : [];
            lista.Add(new ContaGh(m.Groups[1].Value, Regex.IsMatch(bloco, @"Active account:\s*true", RegexOptions.IgnoreCase), escopos));
        }
        return lista;
    }

    public static async Task<IReadOnlyList<ContaGh>?> ContasGhAsync()
    {
        var r = await ComandoGit.ExecutarGhAsync(Path.GetTempPath(), ["auth", "status", "--hostname", "github.com"], TimeSpan.FromSeconds(15));
        if (r.Codigo == -1 && r.Saida.Length == 0 && !r.Erro.Contains("Logged in", StringComparison.Ordinal)) return null;
        return LerContasGh(r.Saida + "\n" + r.Erro);
    }

    public static string? RepositorioDoRemoto(string url) =>
        Regex.Match(url.Trim(), @"github\.com[:/]+([^/\s]+)/([^/\s]+?)(?:\.git)?/?$") is { Success: true } m ? $"{m.Groups[1].Value}/{m.Groups[2].Value}" : null;

    public static async Task<string?> RepositorioDaPastaAsync(string pasta)
    {
        var r = await ComandoGit.ExecutarAsync(pasta, ["remote", "get-url", "origin"], TimeSpan.FromSeconds(10));
        return r.Ok ? RepositorioDoRemoto(r.Saida) : null;
    }

    static RespostaGh<T> Resposta<T>(ResultadoGit r, Func<string, T> ler) where T : class =>
        FaltaPermissaoDeQuadro(r.Saida + r.Erro) ? new(null, true)
        : r.Ok ? new(ler(r.Saida), false)
        : new(null, false, r.Mensagem);

    public static async Task<IssuesEQuadros> IssuesEQuadrosAsync(string pasta, string repositorio, bool soMinhas, bool quadros = true)
    {
        if (repositorio.Split('/') is not [var dono, var repo]) return new(null, new(null, false, Idioma.T("Repositório inválido.")));
        var busca = $"repo:{repositorio} is:issue is:open sort:updated-desc";
        var r = await GhAsync(pasta, ["api", "graphql", "-f", "query=" + ConsultaRepositorio, "-f", "dono=" + dono, "-f", "repo=" + repo,
            "-f", "minhas=" + busca + " assignee:@me", "-f", "livres=" + busca + " no:assignee",
            "-F", "soMinhas=" + (soMinhas ? "true" : "false"), "-F", "quadros=" + (quadros ? "true" : "false")], TimeSpan.FromSeconds(20));
        if (quadros && FaltaPermissaoDeQuadro(r.Saida + r.Erro))
            return await IssuesEQuadrosAsync(pasta, repositorio, soMinhas, false) with { Quadros = new(null, true) };
        if (!r.Ok) return new(null, new(null, false, r.Mensagem));
        return new(LerIssuesDoRepositorio(r.Saida), new(quadros ? LerQuadros(r.Saida, repositorio, pasta) : [], false));
    }

    public static IReadOnlyList<IssueGh> LerIssuesDoRepositorio(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) return [];
            var nos = new List<JsonElement>();
            foreach (var caminho in new[] { new[] { "minhas", "nodes" }, ["livres", "nodes"], ["repository", "abertas", "nodes"] })
                if (Caminho(data, out var lista, caminho) && lista.ValueKind == JsonValueKind.Array)
                    nos.AddRange(lista.EnumerateArray().Where(o => o.ValueKind == JsonValueKind.Object && Inteiro(o, "number") > 0));
            return [.. nos.Select(IssueDe).DistinctBy(i => i.Numero).OrderByDescending(i => i.Atualizada ?? DateTimeOffset.MinValue)];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static async Task<RespostaGh<QuadroGh>> DetalharQuadroAsync(QuadroGh quadro)
    {
        var r = await GhAsync(quadro.Pasta, ["api", "graphql", "-f", "query=" + ConsultaDoQuadro, "-f", "id=" + quadro.Id], TimeSpan.FromSeconds(20));
        return Resposta(r, json => LerDetalheDoQuadro(json, quadro));
    }

    public static async Task<RespostaGh<IReadOnlyList<ItemDoQuadro>>> ItensDoQuadroAsync(QuadroGh quadro, string? filtro, int limite = 300)
    {
        var itens = new List<ItemDoQuadro>();
        string? depois = null;
        while (itens.Count < limite)
        {
            List<string> args = ["api", "graphql", "-f", "query=" + ConsultaItens, "-f", "id=" + quadro.Id];
            if (filtro is { Length: > 0 }) args.AddRange(["-f", "filtro=" + filtro]);
            if (depois is not null) args.AddRange(["-f", "depois=" + depois]);
            var r = await GhAsync(quadro.Pasta, args, TimeSpan.FromSeconds(30));
            if (FaltaPermissaoDeQuadro(r.Saida + r.Erro)) return new(null, true);
            if (!r.Ok) return itens.Count > 0 ? new(itens, false) : new(null, false, r.Mensagem);
            var (pagina, temMais, cursor) = LerItens(r.Saida);
            itens.AddRange(pagina);
            if (!temMais || cursor is null) break;
            depois = cursor;
        }
        return new(itens, false);
    }

    public static IReadOnlyList<QuadroGh> LerQuadros(string json, string repositorio, string pasta)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!Caminho(doc.RootElement, out var repo, "data", "repository") || repo.ValueKind != JsonValueKind.Object) return [];
            var projetos = new List<JsonElement>();
            if (Caminho(repo, out var nos, "projectsV2", "nodes") && nos.ValueKind == JsonValueKind.Array) projetos.AddRange(nos.EnumerateArray());
            var usados = new Dictionary<string, (JsonElement Projeto, int Vezes)>();
            if (Caminho(repo, out var issues, "issues", "nodes") && issues.ValueKind == JsonValueKind.Array)
                foreach (var issue in issues.EnumerateArray())
                    if (Caminho(issue, out var itens, "projectItems", "nodes") && itens.ValueKind == JsonValueKind.Array)
                        foreach (var item in itens.EnumerateArray())
                            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("project", out var p) && p.ValueKind == JsonValueKind.Object && Texto(p, "id") is { Length: > 0 } id)
                                usados[id] = (p, usados.TryGetValue(id, out var antes) ? antes.Vezes + 1 : 1);
            projetos.AddRange(usados.Values.OrderByDescending(u => u.Vezes).Select(u => u.Projeto));
            return [.. projetos
                .Where(n => n.ValueKind == JsonValueKind.Object && !(n.TryGetProperty("closed", out var f) && f.ValueKind == JsonValueKind.True))
                .Select(n => new QuadroGh(Texto(n, "id"), Texto(n, "title"), Inteiro(n, "number"), Texto(n, "url"), repositorio, pasta))
                .Where(q => q.Id.Length > 0)
                .DistinctBy(q => q.Id)];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static QuadroGh LerDetalheDoQuadro(string json, QuadroGh quadro)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!Caminho(doc.RootElement, out var no, "data", "node") || no.ValueKind != JsonValueKind.Object) return quadro;
            var campos = new List<CampoDoQuadro>();
            string? iteracao = null;
            if (Caminho(no, out var nosCampos, "fields", "nodes") && nosCampos.ValueKind == JsonValueKind.Array)
                foreach (var c in nosCampos.EnumerateArray())
                {
                    if (c.ValueKind != JsonValueKind.Object || Texto(c, "id").Length == 0) continue;
                    if (c.TryGetProperty("options", out var opcoes) && opcoes.ValueKind == JsonValueKind.Array)
                        campos.Add(new CampoDoQuadro(Texto(c, "id"), Texto(c, "name"),
                            [.. opcoes.EnumerateArray().Select(o => new OpcaoStatus(Texto(o, "id"), Texto(o, "name"), Texto(o, "color"))).Where(o => o.Id.Length > 0)]));
                    else iteracao ??= Texto(c, "name");
                }
            var visoes = new List<VisaoDoQuadro>();
            if (Caminho(no, out var nosVisoes, "views", "nodes") && nosVisoes.ValueKind == JsonValueKind.Array)
                foreach (var v in nosVisoes.EnumerateArray())
                {
                    if (v.ValueKind != JsonValueKind.Object) continue;
                    var grupo = Caminho(v, out var g, "groupByFields", "nodes") && g.ValueKind == JsonValueKind.Array && g.GetArrayLength() > 0 ? Texto(g[0], "name") : "";
                    visoes.Add(new VisaoDoQuadro(Texto(v, "name"), Texto(v, "filter"), grupo.Length > 0 ? grupo : null));
                }
            return quadro with
            {
                Titulo = Texto(no, "title") is { Length: > 0 } t ? t : quadro.Titulo,
                Campos = campos,
                Visoes = visoes,
                CampoIteracao = iteracao,
                Detalhado = true,
            };
        }
        catch (JsonException)
        {
            return quadro;
        }
    }

    public static (IReadOnlyList<ItemDoQuadro> Itens, bool TemMais, string? Cursor) LerItens(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!Caminho(doc.RootElement, out var itens, "data", "node", "items") || itens.ValueKind != JsonValueKind.Object) return ([], false, null);
            var temMais = Caminho(itens, out var mais, "pageInfo", "hasNextPage") && mais.ValueKind == JsonValueKind.True;
            var cursor = Caminho(itens, out var fim, "pageInfo", "endCursor") && fim.ValueKind == JsonValueKind.String ? fim.GetString() : null;
            var lista = new List<ItemDoQuadro>();
            if (itens.TryGetProperty("nodes", out var nos) && nos.ValueKind == JsonValueKind.Array)
                foreach (var no in nos.EnumerateArray())
                    if (LerItem(no) is { } item) lista.Add(item);
            return (lista, temMais, cursor);
        }
        catch (JsonException)
        {
            return ([], false, null);
        }
    }

    static ItemDoQuadro? LerItem(JsonElement no)
    {
        if (no.ValueKind != JsonValueKind.Object || !no.TryGetProperty("content", out var c) || c.ValueKind != JsonValueKind.Object) return null;
        var tipo = Texto(c, "__typename");
        var campos = new Dictionary<string, ValorDeCampo>(StringComparer.OrdinalIgnoreCase);
        string? iteracao = null;
        if (Caminho(no, out var valores, "fieldValues", "nodes") && valores.ValueKind == JsonValueKind.Array)
            foreach (var v in valores.EnumerateArray())
            {
                if (v.ValueKind != JsonValueKind.Object) continue;
                var campo = v.TryGetProperty("field", out var f) && f.ValueKind == JsonValueKind.Object ? Texto(f, "name") : "";
                switch (Texto(v, "__typename"))
                {
                    case "ProjectV2ItemFieldSingleSelectValue" when campo.Length > 0:
                        campos[campo] = new ValorDeCampo(Texto(v, "name"), Texto(v, "color") is { Length: > 0 } cor ? cor : null, Texto(v, "optionId"));
                        break;
                    case "ProjectV2ItemFieldIterationValue":
                        iteracao = Texto(v, "title");
                        break;
                }
            }
        var repositorio = c.TryGetProperty("repository", out var r) && r.ValueKind == JsonValueKind.Object ? Texto(r, "nameWithOwner") : "";
        IReadOnlyList<int> prs = Caminho(c, out var p, "closedByPullRequestsReferences", "nodes") && p.ValueKind == JsonValueKind.Array
            ? [.. p.EnumerateArray().Select(x => Inteiro(x, "number")).Where(n => n > 0)]
            : [];
        IReadOnlyList<PrLigado> ligados = tipo == "PullRequest" && LerPrLigado(c) is { } proprio ? [proprio] : PrsLigadosDe(c);
        return new ItemDoQuadro(Texto(no, "id"), tipo, Inteiro(c, "number"), Texto(c, "title"), Texto(c, "url"), Texto(c, "state").ToUpperInvariant(), repositorio,
            Logins(c, "assignees"), Etiquetas(c, "labels"), campos, iteracao is { Length: > 0 } ? iteracao : null, prs,
            DateTimeOffset.TryParse(Texto(c, "updatedAt"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var quando) ? quando : null)
        {
            PrsLigados = ligados,
        };
    }

    static IReadOnlyList<PrLigado> PrsLigadosDe(JsonElement c) =>
        Caminho(c, out var p, "closedByPullRequestsReferences", "nodes") && p.ValueKind == JsonValueKind.Array
            ? [.. p.EnumerateArray().Select(LerPrLigado).OfType<PrLigado>()]
            : [];

    public static PrLigado? LerPrLigado(JsonElement x)
    {
        if (x.ValueKind != JsonValueKind.Object || Inteiro(x, "number") is var numero && numero <= 0) return null;
        var checks = Caminho(x, out var nos, "commits", "nodes") && nos.ValueKind == JsonValueKind.Array && nos.GetArrayLength() > 0
            && Caminho(nos[0], out var estado, "commit", "statusCheckRollup", "state") && estado.ValueKind == JsonValueKind.String ? estado.GetString() : null;
        var revisao = Texto(x, "reviewDecision");
        var repositorio = x.TryGetProperty("repository", out var r) && r.ValueKind == JsonValueKind.Object ? Texto(r, "nameWithOwner") : "";
        return new PrLigado(numero, Texto(x, "url"), Texto(x, "state").ToUpperInvariant(), x.TryGetProperty("isDraft", out var d) && d.ValueKind == JsonValueKind.True,
            Texto(x, "headRefName"), repositorio, revisao.Length > 0 ? revisao : null, checks is { Length: > 0 } ? checks.ToUpperInvariant() : null);
    }

    static JsonElement? Lista(JsonElement o, string nome)
    {
        if (!o.TryGetProperty(nome, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Array) return v;
        return v.ValueKind == JsonValueKind.Object && v.TryGetProperty("nodes", out var nos) && nos.ValueKind == JsonValueKind.Array ? nos : null;
    }

    static IReadOnlyList<string> Logins(JsonElement o, string nome) =>
        Lista(o, nome) is { } l ? [.. l.EnumerateArray().Select(e => Texto(e, "login")).Where(n => n.Length > 0)] : [];

    static IReadOnlyList<Etiqueta> Etiquetas(JsonElement o, string nome) =>
        Lista(o, nome) is { } l ? [.. l.EnumerateArray().Select(e => new Etiqueta(Texto(e, "name"), Texto(e, "color"))).Where(e => e.Nome.Length > 0)] : [];

    static bool Caminho(JsonElement inicio, out JsonElement fim, params string[] nomes)
    {
        fim = inicio;
        foreach (var nome in nomes)
        {
            if (fim.ValueKind != JsonValueKind.Object || !fim.TryGetProperty(nome, out var proximo)) return false;
            fim = proximo;
        }
        return true;
    }
}
