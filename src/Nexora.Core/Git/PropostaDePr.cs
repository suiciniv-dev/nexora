using System.Text;
using System.Text.RegularExpressions;
using Nexora.Core.Texto;

namespace Nexora.Core.Git;

public sealed record CommitDaBranch(string Sha, string Assunto, string Corpo);

public sealed record BranchParaPr(string Referencia, IReadOnlyList<CommitDaBranch> Commits, IReadOnlyList<LinhasDoArquivo> Arquivos, int SemCommit);

public sealed record PropostaDePr(string Titulo, string Corpo);

public static partial class GeradorDePr
{
    public static IReadOnlyList<CommitDaBranch> LerCommits(string saida) =>
        [.. saida.Split('\x1e')
            .Select(b => b.Trim('\r', '\n'))
            .Where(b => b.Length > 0)
            .Select(b => b.Split('\x1f'))
            .Where(p => p.Length >= 2 && p[0].Trim().Length > 0)
            .Select(p => new CommitDaBranch(p[0].Trim(), p[1].Trim(), p.Length > 2 ? p[2].Trim() : ""))];

    public static async Task<string> ReferenciaDaBaseAsync(string pasta, string baseBranch)
    {
        var remota = await ComandoGit.ExecutarAsync(pasta, ["rev-parse", "--verify", "--quiet", "origin/" + baseBranch], TimeSpan.FromSeconds(10));
        return remota.Ok ? "origin/" + baseBranch : baseBranch;
    }

    public static async Task<BranchParaPr> LerBranchAsync(string pasta, string baseBranch)
    {
        var referencia = await ReferenciaDaBaseAsync(pasta, baseBranch);
        var log = await ComandoGit.ExecutarAsync(pasta, ["log", "--reverse", "--no-merges", "--format=%H%x1f%s%x1f%b%x1e", referencia + "..HEAD"], TimeSpan.FromSeconds(20));
        var diff = await ComandoGit.ExecutarAsync(pasta, ["diff", "--numstat", referencia + "...HEAD"], TimeSpan.FromSeconds(20));
        var semCommit = await Repositorio.AlteracoesAsync(pasta);
        return new BranchParaPr(referencia, log.Ok ? LerCommits(log.Saida) : [], diff.Ok ? Alteracoes.LerNumstat(diff.Saida) : [], semCommit);
    }

    [GeneratedRegex(@"^(?<tipo>[a-zA-Z]+)(?:\([^)]*\))?!?:\s*(?<texto>.+)$")]
    private static partial Regex Convencional();

    public static (string? Tipo, string Texto) Classificar(string assunto) =>
        Convencional().Match(assunto.Trim()) is { Success: true } m ? (m.Groups["tipo"].Value.ToLowerInvariant(), m.Groups["texto"].Value.Trim()) : (null, assunto.Trim());

    public static string BranchLegivel(string branch)
    {
        var nome = branch.Split('/').Last();
        nome = Regex.Replace(nome, @"-\d{8}$", "");
        nome = Regex.Replace(nome, @"^\d+[-_]", "");
        nome = Regex.Replace(nome, @"[-_]+", " ").Trim();
        return nome.Length == 0 ? branch : char.ToUpperInvariant(nome[0]) + nome[1..];
    }

    static string Maiuscula(string texto) => texto.Length == 0 ? texto : char.ToUpperInvariant(texto[0]) + texto[1..];

    public static PropostaDePr Propor(string branch, IReadOnlyList<CommitDaBranch> commits, IReadOnlyList<LinhasDoArquivo> arquivos, IssueDaSessao? issue)
    {
        var titulo = commits.Count == 1 ? commits[0].Assunto
            : issue is { EhPr: false } i ? i.Titulo
            : commits.Count > 1 && commits.Select(c => Classificar(c.Assunto).Tipo).Distinct().Count() == 1 && Classificar(commits[0].Assunto).Tipo is { } tipo
                ? $"{tipo}: {BranchLegivel(branch).ToLowerInvariant()}"
            : BranchLegivel(branch);
        var corpo = new StringBuilder();
        if (commits.Count == 1 && commits[0].Corpo.Length > 0) corpo.Append(commits[0].Corpo.Trim()).Append("\n\n");
        var classificados = commits.Select(c => (Commit: c, Partes: Classificar(c.Assunto))).ToList();
        if (classificados.Any(c => c.Partes.Tipo is not null))
        {
            corpo.Append("## Changelog\n");
            void Grupo(string nome, Func<string?, bool> filtro)
            {
                var linhas = classificados.Where(c => filtro(c.Partes.Tipo)).Select(c => "- " + Maiuscula(c.Partes.Texto)).ToList();
                if (linhas.Count == 0) return;
                corpo.Append("\n### ").Append(nome).Append('\n').Append(string.Join("\n", linhas)).Append('\n');
            }
            Grupo(Idioma.T("Novidades"), t => t == "feat");
            Grupo(Idioma.T("Correções"), t => t == "fix");
            Grupo(Idioma.T("Outras mudanças"), t => t is not "feat" and not "fix");
            corpo.Append('\n');
        }
        else if (commits.Count > 1)
        {
            corpo.Append("## ").Append(Idioma.T("Mudanças")).Append('\n');
            corpo.Append(string.Join("\n", commits.Select(c => "- " + c.Assunto))).Append("\n\n");
        }
        if (arquivos.Count > 0)
        {
            var mais = arquivos.Sum(a => a.Mais);
            var menos = arquivos.Sum(a => a.Menos);
            corpo.Append(arquivos.Count == 1 ? Idioma.F("1 arquivo alterado, +{0} −{1}", mais, menos) : Idioma.F("{0} arquivos alterados, +{1} −{2}", arquivos.Count, mais, menos)).Append("\n\n");
        }
        if (issue is { EhPr: false } fechada) corpo.Append("Closes ").Append(fechada.Repositorio).Append('#').Append(fechada.Numero).Append('\n');
        return new PropostaDePr(titulo.Trim(), corpo.ToString().Trim());
    }
}
