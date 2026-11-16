using System.Globalization;
using System.Text.Json;
using Nexora.Core.Texto;

namespace Nexora.Core.Git;

public sealed record IssueGh(int Numero, string Titulo, string Url, IReadOnlyList<string> Etiquetas, DateTimeOffset? Atualizada)
{
    public IReadOnlyList<string> Responsaveis { get; init; } = [];
    public IReadOnlyList<Etiqueta> EtiquetasComCor { get; init; } = [];
    public IReadOnlyList<PrLigado> PrsLigados { get; init; } = [];
}

public sealed record DetalheIssue(int Numero, string Titulo, string Corpo, string Url, string Autor = "", IReadOnlyList<string>? Etiquetas = null, DateTimeOffset? Criada = null, int Comentarios = 0)
{
    public IReadOnlyList<ComentarioIssue> Lista { get; init; } = [];
}

public sealed record ComentarioIssue(string Autor, string Corpo, DateTimeOffset? Quando);

public sealed record OpcaoStatus(string Id, string Nome, string? Cor = null);

public sealed record StatusNoQuadro(string Quadro, string QuadroId, string ItemId, string CampoId, string? AtualId, IReadOnlyList<OpcaoStatus> Opcoes);

public sealed record StatusDaIssue(bool SemPermissao, IReadOnlyList<StatusNoQuadro> Quadros, string? Erro = null);

public sealed record EstadoPr(int Numero, string Url, string Estado, bool Rascunho, string? Revisao, int ChecksOk, int ChecksFalhos, int ChecksPendentes)
{
    public bool Aberto => Estado == "OPEN";
    public bool Mergeado => Estado == "MERGED";
}

public static partial class GitHub
{
    static readonly string[] BranchesPrincipais = ["main", "master", "develop", "dev", "trunk", "HEAD"];

    public static bool TemPr(string? branch) =>
        branch is { Length: > 0 } b && !BranchesPrincipais.Contains(b, StringComparer.OrdinalIgnoreCase);

    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> ContaDaPasta = new(StringComparer.OrdinalIgnoreCase);
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> Tokens = new(StringComparer.OrdinalIgnoreCase);
    static Task<IReadOnlyList<string>>? _contas;
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task<IReadOnlyList<string>>> OrdensDaPasta = new(StringComparer.OrdinalIgnoreCase);
    const string Ativa = "";

    public static IReadOnlyList<string> LerContas(string saidaDoStatus) =>
        [.. System.Text.RegularExpressions.Regex.Matches(saidaDoStatus, @"Logged in to \S+ account (\S+)").Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase)];

    public static IReadOnlyList<(string Conta, bool Ativa)> LerContasComAtiva(string saidaDoStatus)
    {
        var lista = new List<(string, bool)>();
        var blocos = System.Text.RegularExpressions.Regex.Split(saidaDoStatus, @"(?=Logged in to \S+ account )");
        foreach (var bloco in blocos)
        {
            var m = System.Text.RegularExpressions.Regex.Match(bloco, @"^Logged in to \S+ account (\S+)");
            if (!m.Success) continue;
            lista.Add((m.Groups[1].Value, System.Text.RegularExpressions.Regex.IsMatch(bloco, @"Active account:\s*true", System.Text.RegularExpressions.RegexOptions.IgnoreCase)));
        }
        return lista;
    }

    public static async Task<IReadOnlyList<(string Conta, bool Ativa)>?> ContasDoGhAsync()
    {
        var r = await ComandoGit.ExecutarGhAsync(Path.GetTempPath(), ["auth", "status", "--hostname", "github.com"], TimeSpan.FromSeconds(15));
        if (r.Codigo == -1 && r.Saida.Length == 0 && !r.Erro.Contains("Logged in", StringComparison.Ordinal)) return null;
        EsquecerOrdem();
        return LerContasComAtiva(r.Saida + "\n" + r.Erro);
    }

    public static async Task<bool> TornarAtivaAsync(string conta)
    {
        var r = await ComandoGit.ExecutarGhAsync(Path.GetTempPath(), ["auth", "switch", "--hostname", "github.com", "--user", conta], TimeSpan.FromSeconds(15));
        return r.Ok;
    }

    public static string? ContaDoAjudante(string config) =>
        System.Text.RegularExpressions.Regex.Match(config, @"--user\s+([A-Za-z0-9-]+)") is { Success: true } m ? m.Groups[1].Value : null;

    public static string? DonoDoRemoto(string url) =>
        System.Text.RegularExpressions.Regex.Match(url.Trim(), @"github\.com[:/]+([^/]+)/") is { Success: true } m ? m.Groups[1].Value : null;

    static Task<IReadOnlyList<string>> ContasAsync() => _contas ??= LerContasDoGhAsync();

    static async Task<IReadOnlyList<string>> LerContasDoGhAsync()
    {
        if (ContasDoArquivo() is { Count: > 0 } doArquivo) return doArquivo;
        var r = await ComandoGit.ExecutarGhAsync(Path.GetTempPath(), ["auth", "status", "--hostname", "github.com"], TimeSpan.FromSeconds(15));
        return LerContas(r.Saida + "\n" + r.Erro);
    }

    static IReadOnlyList<string>? ContasDoArquivo()
    {
        try
        {
            var pasta = Environment.GetEnvironmentVariable("GH_CONFIG_DIR") is { Length: > 0 } definida ? definida
                : OperatingSystem.IsWindows() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitHub CLI")
                : Path.Combine(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } xdg ? xdg : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"), "gh");
            var arquivo = Path.Combine(pasta, "hosts.yml");
            return File.Exists(arquivo) ? LerContasDoHosts(File.ReadAllText(arquivo)) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static IReadOnlyList<string> LerContasDoHosts(string yml)
    {
        var contas = new List<string>();
        var noHost = false;
        int? recuoUsuarios = null, recuoConta = null;
        foreach (var bruta in yml.Replace("\r", "").Split('\n'))
        {
            if (bruta.Trim().Length == 0 || bruta.TrimStart().StartsWith('#')) continue;
            var recuo = bruta.Length - bruta.TrimStart().Length;
            var linha = bruta.Trim();
            if (recuo == 0)
            {
                noHost = linha == "github.com:";
                recuoUsuarios = recuoConta = null;
                continue;
            }
            if (!noHost) continue;
            if (recuoUsuarios is { } r && recuo > r)
            {
                recuoConta ??= recuo;
                if (recuo == recuoConta && linha.EndsWith(':') && linha.TrimEnd(':') is { Length: > 0 } conta) contas.Add(conta);
                continue;
            }
            recuoUsuarios = linha == "users:" ? recuo : null;
            recuoConta = null;
        }
        return contas;
    }

    static async Task<string?> TokenAsync(string conta)
    {
        if (Tokens.TryGetValue(conta, out var guardado)) return guardado;
        var r = await ComandoGit.ExecutarGhAsync(Path.GetTempPath(), ["auth", "token", "--hostname", "github.com", "--user", conta], TimeSpan.FromSeconds(15));
        if (!r.Ok || r.Saida.Trim() is not { Length: > 0 } token) return null;
        Tokens[conta] = token;
        return token;
    }

    static async Task<IReadOnlyList<string>> OrdemDeContasAsync(string pasta)
    {
        var ordem = await OrdensDaPasta.GetOrAdd(pasta, CalcularOrdemAsync);
        return ContaDaPasta.TryGetValue(pasta, out var conhecida) ? [conhecida, .. ordem.Where(c => !c.Equals(conhecida, StringComparison.OrdinalIgnoreCase))] : ordem;
    }

    static async Task<IReadOnlyList<string>> CalcularOrdemAsync(string pasta)
    {
        var contas = await ContasAsync();
        var ordem = new List<string>();
        var ajudante = await ComandoGit.ExecutarAsync(pasta, ["config", "--get-regexp", @"^credential\..*helper$"], TimeSpan.FromSeconds(10));
        if (ajudante.Ok && ContaDoAjudante(ajudante.Saida) is { } daConfig) ordem.Add(daConfig);
        var remoto = await ComandoGit.ExecutarAsync(pasta, ["remote", "get-url", "origin"], TimeSpan.FromSeconds(10));
        if (remoto.Ok && DonoDoRemoto(remoto.Saida) is { } dono && contas.FirstOrDefault(c => c.Equals(dono, StringComparison.OrdinalIgnoreCase)) is { } igual) ordem.Add(igual);
        ordem.Add(Ativa);
        ordem.AddRange(contas);
        return [.. ordem.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    static bool SemAcesso(ResultadoGit r) =>
        r.Erro.Contains("Could not resolve to a Repository", StringComparison.OrdinalIgnoreCase)
        || r.Erro.Contains("HTTP 404", StringComparison.Ordinal)
        || r.Erro.Contains("HTTP 403", StringComparison.Ordinal)
        || r.Erro.Contains("Resource not accessible", StringComparison.OrdinalIgnoreCase)
        || r.Erro.Contains("SAML", StringComparison.Ordinal);

    public static async Task<ResultadoGit> GhAsync(string pasta, IEnumerable<string> args, TimeSpan? limite = null)
    {
        var lista = args.ToList();
        ResultadoGit? ultimo = null;
        foreach (var conta in await OrdemDeContasAsync(pasta))
        {
            Dictionary<string, string>? ambiente = null;
            if (conta != Ativa)
            {
                if (await TokenAsync(conta) is not { } token) continue;
                ambiente = new() { ["GH_TOKEN"] = token };
            }
            ultimo = await ComandoGit.ExecutarGhAsync(pasta, lista, limite, ambiente);
            if (ultimo.Ok)
            {
                ContaDaPasta[pasta] = conta;
                return ultimo;
            }
            if (!SemAcesso(ultimo)) return ultimo;
        }
        return ultimo ?? new ResultadoGit(-1, "", Idioma.T("Nenhuma conta do gh tem acesso a este repositório."));
    }

    public static async Task<IReadOnlyList<IssueGh>?> IssuesAsync(string pasta)
    {
        var r = await GhAsync(pasta, ["issue", "list", "--state", "open", "--limit", "50", "--json", "number,title,url,labels,updatedAt,assignees"], TimeSpan.FromSeconds(20));
        return r.Ok ? LerIssues(r.Saida) : null;
    }

    public static async Task<DetalheIssue?> IssueAsync(string pasta, int numero, string? repositorio = null, bool pr = false)
    {
        List<string> args = [pr ? "pr" : "issue", "view", numero.ToString(CultureInfo.InvariantCulture), "--json", "number,title,body,url,author,labels,createdAt,comments"];
        if (repositorio is { Length: > 0 }) args.AddRange(["-R", repositorio]);
        var r = await GhAsync(pasta, args, TimeSpan.FromSeconds(20));
        return r.Ok ? LerIssue(r.Saida) : null;
    }

    public static async Task<IReadOnlyDictionary<string, string>> ImagensAssinadasAsync(string pasta, string repositorio, int numero)
    {
        var mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var n = numero.ToString(CultureInfo.InvariantCulture);
        foreach (var rota in new[] { $"repos/{repositorio}/issues/{n}", $"repos/{repositorio}/issues/{n}/comments?per_page=100" })
        {
            var r = await GhAsync(pasta, ["api", rota, "-H", "Accept: application/vnd.github.full+json"], TimeSpan.FromSeconds(20));
            if (!r.Ok) continue;
            try
            {
                using var doc = JsonDocument.Parse(r.Saida);
                var itens = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToList() : [doc.RootElement];
                foreach (var item in itens)
                    foreach (var fonte in Core.Texto.ImagensMd.FontesDoHtml(Texto(item, "body_html")))
                        if (Core.Texto.ImagensMd.Chave(fonte) is { } chave) mapa.TryAdd(chave, fonte);
            }
            catch (JsonException)
            {
            }
        }
        return mapa;
    }

    public static async Task<ResultadoGit> ComentarAsync(string pasta, int numero, string? repositorio, string texto, bool pr = false)
    {
        List<string> args = [pr ? "pr" : "issue", "comment", numero.ToString(CultureInfo.InvariantCulture), "--body", texto];
        if (repositorio is { Length: > 0 }) args.AddRange(["-R", repositorio]);
        return await GhAsync(pasta, args, TimeSpan.FromSeconds(30));
    }

    public static DetalheIssue? LerIssue(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var o = doc.RootElement;
            var autor = o.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.Object ? Texto(a, "login") : "";
            IReadOnlyList<string> etiquetas = o.TryGetProperty("labels", out var l) && l.ValueKind == JsonValueKind.Array ? [.. l.EnumerateArray().Select(e => Texto(e, "name")).Where(n => n.Length > 0)] : [];
            var comentarios = o.TryGetProperty("comments", out var c) && c.ValueKind == JsonValueKind.Array ? c.GetArrayLength() : 0;
            var criada = DateTimeOffset.TryParse(Texto(o, "createdAt"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var quando) ? quando : (DateTimeOffset?)null;
            IReadOnlyList<ComentarioIssue> lista = comentarios == 0 ? [] : [.. c.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.Object)
                .Select(x => new ComentarioIssue(
                    x.TryGetProperty("author", out var a2) && a2.ValueKind == JsonValueKind.Object ? Texto(a2, "login") : "",
                    Texto(x, "body"),
                    DateTimeOffset.TryParse(Texto(x, "createdAt"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var q) ? q : null))];
            return new DetalheIssue(Inteiro(o, "number"), Texto(o, "title"), Texto(o, "body"), Texto(o, "url"), autor, etiquetas, criada, comentarios) { Lista = lista };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    const string ConsultaStatus = """
        query($dono:String!,$repo:String!,$n:Int!){repository(owner:$dono,name:$repo){issue(number:$n){projectItems(first:10){nodes{
          id isArchived
          project{id title closed field(name:"Status"){... on ProjectV2SingleSelectField{id options{id name}}}}
          fieldValueByName(name:"Status"){... on ProjectV2ItemFieldSingleSelectValue{optionId}}}}}}}
        """;

    const string MudancaStatus = """
        mutation($quadro:ID!,$item:ID!,$campo:ID!,$opcao:String!){updateProjectV2ItemFieldValue(input:{projectId:$quadro,itemId:$item,fieldId:$campo,value:{singleSelectOptionId:$opcao}}){projectV2Item{id}}}
        """;

    public static (string Dono, string Repo, int Numero)? PartesDaUrl(string url) =>
        System.Text.RegularExpressions.Regex.Match(url, @"github\.com/([^/]+)/([^/]+)/(?:issues|pull)/(\d+)") is { Success: true } m
            && int.TryParse(m.Groups[3].Value, CultureInfo.InvariantCulture, out var n)
            ? (m.Groups[1].Value, m.Groups[2].Value, n) : null;

    public static bool FaltaPermissaoDeQuadro(string texto) =>
        texto.Contains("INSUFFICIENT_SCOPES", StringComparison.Ordinal) || texto.Contains("read:project", StringComparison.Ordinal);

    public static async Task<StatusDaIssue> StatusAsync(string pasta, string urlDaIssue)
    {
        if (PartesDaUrl(urlDaIssue) is not { } partes) return new StatusDaIssue(false, []);
        var (dono, repo, numero) = partes;
        var r = await GhAsync(pasta, ["api", "graphql", "-f", "query=" + ConsultaStatus, "-f", "dono=" + dono, "-f", "repo=" + repo, "-F", "n=" + numero.ToString(CultureInfo.InvariantCulture)], TimeSpan.FromSeconds(20));
        if (FaltaPermissaoDeQuadro(r.Saida + r.Erro)) return new StatusDaIssue(true, []);
        return r.Ok ? new StatusDaIssue(false, LerStatus(r.Saida)) : new StatusDaIssue(false, [], r.Mensagem);
    }

    public static IReadOnlyList<StatusNoQuadro> LerStatus(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data)
                || !data.TryGetProperty("repository", out var repo) || repo.ValueKind != JsonValueKind.Object
                || !repo.TryGetProperty("issue", out var issue) || issue.ValueKind != JsonValueKind.Object
                || !issue.TryGetProperty("projectItems", out var itens)
                || !itens.TryGetProperty("nodes", out var nos) || nos.ValueKind != JsonValueKind.Array) return [];
            var lista = new List<StatusNoQuadro>();
            foreach (var no in nos.EnumerateArray())
            {
                if (no.TryGetProperty("isArchived", out var arquivado) && arquivado.ValueKind == JsonValueKind.True) continue;
                if (!no.TryGetProperty("project", out var quadro) || quadro.ValueKind != JsonValueKind.Object) continue;
                if (quadro.TryGetProperty("closed", out var fechado) && fechado.ValueKind == JsonValueKind.True) continue;
                if (!quadro.TryGetProperty("field", out var campo) || campo.ValueKind != JsonValueKind.Object || Texto(campo, "id") is not { Length: > 0 } campoId) continue;
                if (!campo.TryGetProperty("options", out var opcoes) || opcoes.ValueKind != JsonValueKind.Array) continue;
                var atual = no.TryGetProperty("fieldValueByName", out var valor) && valor.ValueKind == JsonValueKind.Object ? Texto(valor, "optionId") : "";
                lista.Add(new StatusNoQuadro(Texto(quadro, "title"), Texto(quadro, "id"), Texto(no, "id"), campoId, atual.Length > 0 ? atual : null,
                    [.. opcoes.EnumerateArray().Select(o => new OpcaoStatus(Texto(o, "id"), Texto(o, "name"))).Where(o => o.Id.Length > 0)]));
            }
            return lista;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static async Task<ResultadoGit> MudarStatusAsync(string pasta, StatusNoQuadro quadro, string opcaoId) =>
        await GhAsync(pasta, ["api", "graphql", "-f", "query=" + MudancaStatus, "-f", "quadro=" + quadro.QuadroId, "-f", "item=" + quadro.ItemId, "-f", "campo=" + quadro.CampoId, "-f", "opcao=" + opcaoId], TimeSpan.FromSeconds(20));

    public static async Task<string?> ContaUsadaAsync(string pasta)
    {
        if (!ContaDaPasta.TryGetValue(pasta, out var conta)) return null;
        if (conta != Ativa) return conta;
        var contas = await ContasDoGhAsync();
        return contas?.FirstOrDefault(c => c.Ativa).Conta;
    }

    public static void EsquecerContas()
    {
        Tokens.Clear();
        EsquecerOrdem();
    }

    static void EsquecerOrdem()
    {
        _contas = null;
        OrdensDaPasta.Clear();
    }

    public static async Task<EstadoPr?> PrDoBranchAsync(string pasta, string branch)
    {
        var r = await GhAsync(pasta, ["pr", "view", branch, "--json", "number,url,state,isDraft,reviewDecision,statusCheckRollup"], TimeSpan.FromSeconds(20));
        return r.Ok ? LerPr(r.Saida) : null;
    }

    public static IReadOnlyList<IssueGh> LerIssues(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];
            return [.. doc.RootElement.EnumerateArray().Select(IssueDe)];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    static IssueGh IssueDe(JsonElement o)
    {
        var etiquetas = Etiquetas(o, "labels");
        return new IssueGh(Inteiro(o, "number"), Texto(o, "title"), Texto(o, "url"), [.. etiquetas.Select(e => e.Nome)],
            DateTimeOffset.TryParse(Texto(o, "updatedAt"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var quando) ? quando : null)
        {
            Responsaveis = Logins(o, "assignees"),
            EtiquetasComCor = etiquetas,
            PrsLigados = PrsLigadosDe(o),
        };
    }

    public static EstadoPr? LerPr(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var o = doc.RootElement;
            if (Inteiro(o, "number") is var numero && numero <= 0) return null;
            int ok = 0, falhos = 0, pendentes = 0;
            if (o.TryGetProperty("statusCheckRollup", out var checks) && checks.ValueKind == JsonValueKind.Array)
                foreach (var c in checks.EnumerateArray())
                {
                    var conclusao = Texto(c, "conclusion").ToUpperInvariant();
                    var situacao = Texto(c, "status").ToUpperInvariant();
                    var estado = Texto(c, "state").ToUpperInvariant();
                    if (estado.Length > 0)
                    {
                        if (estado == "SUCCESS") ok++;
                        else if (estado is "FAILURE" or "ERROR") falhos++;
                        else pendentes++;
                    }
                    else if (situacao is not "COMPLETED" and not "") pendentes++;
                    else if (conclusao is "SUCCESS" or "NEUTRAL" or "SKIPPED") ok++;
                    else if (conclusao.Length > 0) falhos++;
                    else pendentes++;
                }
            var revisao = Texto(o, "reviewDecision");
            return new EstadoPr(numero, Texto(o, "url"), Texto(o, "state").ToUpperInvariant(), o.TryGetProperty("isDraft", out var d) && d.ValueKind == JsonValueKind.True,
                revisao.Length > 0 ? revisao : null, ok, falhos, pendentes);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static string Texto(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    static int Inteiro(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : 0;
}
