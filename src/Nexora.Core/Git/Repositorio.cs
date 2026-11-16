using System.Globalization;
using System.Text;
using Nexora.Core.Texto;

namespace Nexora.Core.Git;

public sealed record Worktree(string Caminho, string? Branch, string? Head, bool Principal);

public sealed record Remocao(bool Ok, string Mensagem, bool BranchApagada);

public sealed record PullRequest(int Numero, string Titulo, string Estado, bool Rascunho, string Url, string? Revisao, string? Checks)
{
    public bool Mergeado => Estado == "MERGED";
    public bool Aberto => Estado == "OPEN";

    public string EstadoTexto => Estado switch
    {
        "MERGED" => Idioma.T("mergeado"),
        "CLOSED" => Idioma.T("fechado"),
        _ when Rascunho => Idioma.T("rascunho"),
        _ => Idioma.T("aberto"),
    };

    public string RevisaoTexto => Revisao switch
    {
        "APPROVED" => Idioma.T("aprovado"),
        "CHANGES_REQUESTED" => Idioma.T("pediram mudanças"),
        "REVIEW_REQUIRED" => Idioma.T("esperando revisão"),
        _ => "",
    };

    public string ChecksTexto => Checks switch
    {
        "ok" => Idioma.T("checks ok"),
        "falhou" => Idioma.T("checks falharam"),
        "rodando" => Idioma.T("checks rodando"),
        _ => "",
    };

    public string Resumo => string.Join("  ·  ", new[] { EstadoTexto, RevisaoTexto, ChecksTexto }.Where(t => t.Length > 0));

    public static PullRequest? Ler(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var o = doc.RootElement;
            if (!o.TryGetProperty("number", out var numero) || !numero.TryGetInt32(out var n)) return null;
            string? T(System.Text.Json.JsonElement e, string nome) =>
                e.ValueKind == System.Text.Json.JsonValueKind.Object && e.TryGetProperty(nome, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;
            var checks = o.TryGetProperty("statusCheckRollup", out var lista) && lista.ValueKind == System.Text.Json.JsonValueKind.Array ? lista.EnumerateArray().ToList() : [];
            string? resumo = null;
            if (checks.Count > 0)
            {
                var falhou = checks.Any(c => T(c, "conclusion") is "FAILURE" or "TIMED_OUT" or "CANCELLED" or "ACTION_REQUIRED" || T(c, "state") is "FAILURE" or "ERROR");
                var rodando = checks.Any(c => T(c, "status") is { } s && s != "COMPLETED" || T(c, "state") is "PENDING" or "EXPECTED");
                resumo = falhou ? "falhou" : rodando ? "rodando" : "ok";
            }
            return new PullRequest(n, T(o, "title") ?? "", T(o, "state") ?? "OPEN",
                o.TryGetProperty("isDraft", out var rascunho) && rascunho.ValueKind == System.Text.Json.JsonValueKind.True,
                T(o, "url") ?? "", T(o, "reviewDecision") is { Length: > 0 } d ? d : null, resumo);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}

public static class Repositorio
{
    const string Ramo = "ref: refs/heads/";
    static readonly string[] BasesComuns = ["dev", "develop", "main", "master"];

    public static string? BranchAtual(string pasta)
    {
        try
        {
            for (var dir = new DirectoryInfo(pasta); dir is not null; dir = dir.Parent)
            {
                if (ArquivoHead(dir.FullName) is not { } head) continue;
                var conteudo = File.ReadAllText(head).Trim();
                return conteudo.StartsWith(Ramo) ? conteudo[Ramo.Length..] : conteudo[..Math.Min(8, conteudo.Length)];
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
        }
        return null;
    }

    public static IReadOnlyList<Worktree> LerWorktrees(string porcelana)
    {
        var lista = new List<Worktree>();
        string? caminho = null, branch = null, head = null;

        void Fechar()
        {
            if (caminho is not null) lista.Add(new Worktree(caminho.Replace('/', Path.DirectorySeparatorChar), branch, head, lista.Count == 0));
            caminho = branch = head = null;
        }

        foreach (var linha in porcelana.Replace("\r", "").Split('\n'))
        {
            if (linha.StartsWith("worktree "))
            {
                Fechar();
                caminho = linha["worktree ".Length..];
            }
            else if (linha.StartsWith("HEAD ")) head = linha["HEAD ".Length..];
            else if (linha.StartsWith("branch ")) branch = linha["branch ".Length..].Replace("refs/heads/", "");
            else if (linha.Length == 0) Fechar();
        }
        Fechar();
        return lista;
    }

    public static async Task<IReadOnlyList<Worktree>> WorktreesAsync(string pasta)
    {
        var r = await ComandoGit.ExecutarAsync(pasta, "worktree", "list", "--porcelain");
        return r.Ok ? LerWorktrees(r.Saida) : [];
    }

    public static async Task<IReadOnlyList<string>> BranchesAsync(string pasta)
    {
        var r = await ComandoGit.ExecutarAsync(pasta, "for-each-ref", "--sort=-committerdate", "--format=%(refname:short)", "refs/heads");
        return r.Ok ? Linhas(r.Saida) : [];
    }

    public static async Task<string?> BranchPadraoAsync(string pasta)
    {
        var r = await ComandoGit.ExecutarAsync(pasta, "symbolic-ref", "--short", "refs/remotes/origin/HEAD");
        if (r.Ok && r.Saida.Trim() is { Length: > 0 } remota) return remota.StartsWith("origin/") ? remota["origin/".Length..] : remota;
        var locais = await BranchesAsync(pasta);
        return BasesComuns.FirstOrDefault(locais.Contains) ?? locais.FirstOrDefault();
    }

    public static async Task<int> AlteracoesAsync(string pasta)
    {
        var r = await ComandoGit.ExecutarAsync(pasta, "status", "--porcelain");
        return r.Ok ? Linhas(r.Saida).Count : 0;
    }

    public static Task<ResultadoGit> TrocarAsync(string pasta, string branch) => ComandoGit.ExecutarAsync(pasta, "switch", branch);

    public static async Task<ResultadoGit> CriarWorktreeAsync(string repositorio, string caminho, string branch, string baseBranch)
    {
        await ComandoGit.ExecutarAsync(repositorio, ["fetch", "origin", baseBranch], TimeSpan.FromSeconds(60));
        var remota = await ComandoGit.ExecutarAsync(repositorio, "rev-parse", "--verify", "--quiet", $"refs/remotes/origin/{baseBranch}");
        var inicio = remota.Ok ? $"origin/{baseBranch}" : baseBranch;
        return await ComandoGit.ExecutarAsync(repositorio, "worktree", "add", "--no-track", "-b", branch, caminho, inicio);
    }

    public static async Task<Remocao> RemoverWorktreeAsync(string repositorio, string caminho, string? branch, bool apagarBranch)
    {
        var nome = Path.GetFileName(caminho.TrimEnd('\\', '/'));
        var status = await ComandoGit.ExecutarAsync(caminho, "status", "--porcelain");
        if (!status.Ok) return new Remocao(false, status.Mensagem, false);
        var workspace = nome + ".code-workspace";
        var pendentes = Linhas(status.Saida).Where(l => l.Trim() != $"?? {workspace}").ToList();
        if (pendentes.Count > 0)
            return new Remocao(false, pendentes.Count == 1
                ? Idioma.F("{0} tem 1 alteração sem commit. Faça commit ou descarte antes de remover.", nome)
                : Idioma.F("{0} tem {1} alterações sem commit. Faça commit ou descarte antes de remover.", nome, pendentes.Count), false);
        var arquivo = Path.Combine(caminho, workspace);
        if (File.Exists(arquivo)) File.Delete(arquivo);
        var r = await ComandoGit.ExecutarAsync(repositorio, "worktree", "remove", caminho);
        if (!r.Ok) return new Remocao(false, r.Mensagem, false);
        if (!apagarBranch || branch is null) return new Remocao(true, Idioma.F("Worktree {0} removido. A branch {1} continua.", nome, branch), false);
        var b = await ComandoGit.ExecutarAsync(repositorio, "branch", "-d", branch);
        return b.Ok
            ? new Remocao(true, Idioma.F("Worktree {0} removido e a branch {1} apagada.", nome, branch), true)
            : new Remocao(true, Idioma.F("Worktree {0} removido. A branch {1} ficou porque o git não a vê mergeada aqui (num PR por squash isso é normal); apague com git branch -D quando tiver certeza.", nome, branch), false);
    }

    public static async Task<PullRequest?> PrAsync(string pasta, string branch)
    {
        var r = await ComandoGit.ExecutarGhAsync(pasta, ["pr", "view", branch, "--json", "number,title,state,isDraft,url,reviewDecision,statusCheckRollup"], TimeSpan.FromSeconds(20));
        return r.Ok ? PullRequest.Ler(r.Saida) : null;
    }

    public static string NomeDaTarefa(string texto, DateOnly dia) => $"{Slug(texto)}-{dia:yyyyMMdd}";

    public static string PastaDoWorktree(string repositorioPrincipal, string slug)
    {
        var principal = repositorioPrincipal.TrimEnd('\\', '/');
        return Path.Combine(Path.GetDirectoryName(principal) ?? principal, $"{Path.GetFileName(principal)}-{slug}");
    }

    public static string Slug(string texto)
    {
        var sb = new StringBuilder();
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            var minuscula = char.ToLowerInvariant(c);
            if (minuscula is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(minuscula);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        return slug.Length > 40 ? slug[..40].TrimEnd('-') : slug;
    }

    static List<string> Linhas(string texto) =>
        texto.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public static string? PrincipalDoWorktree(string pasta)
    {
        try
        {
            var git = Path.Combine(pasta, ".git");
            if (!File.Exists(git)) return null;
            var linha = File.ReadAllText(git).Trim();
            if (!linha.StartsWith("gitdir:")) return null;
            var gitdir = linha["gitdir:".Length..].Trim();
            if (!Path.IsPathRooted(gitdir)) gitdir = Path.GetFullPath(Path.Combine(pasta, gitdir));
            var worktrees = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(gitdir));
            if (worktrees is null || !string.Equals(Path.GetFileName(worktrees), "worktrees", StringComparison.OrdinalIgnoreCase)) return null;
            var comum = Path.GetDirectoryName(worktrees);
            if (comum is null || !string.Equals(Path.GetFileName(comum), ".git", StringComparison.OrdinalIgnoreCase)) return null;
            return Path.GetDirectoryName(comum);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    static string? ArquivoHead(string pasta)
    {
        var git = Path.Combine(pasta, ".git");
        if (Directory.Exists(git)) return Path.Combine(git, "HEAD");
        if (!File.Exists(git)) return null;
        var linha = File.ReadAllText(git).Trim();
        if (!linha.StartsWith("gitdir:")) return null;
        var gitdir = linha["gitdir:".Length..].Trim();
        if (!Path.IsPathRooted(gitdir)) gitdir = Path.GetFullPath(Path.Combine(pasta, gitdir));
        return Path.Combine(gitdir, "HEAD");
    }
}
