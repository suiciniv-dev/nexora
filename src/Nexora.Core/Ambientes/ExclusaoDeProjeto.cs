using System.Globalization;
using Nexora.Core.Git;
using Nexora.Core.Historico;
using Nexora.Core.Texto;

namespace Nexora.Core.Ambientes;

public sealed record DiagnosticoDeExclusao(
    bool Git, int Alteracoes, bool TemRemoto, int NaoEnviados, string? RepositorioPrincipal, IReadOnlyList<string> OutrosWorktrees, string? Branch, int Conversas)
{
    public bool EhWorktree => RepositorioPrincipal is not null;
}

public static class ExclusaoDeProjeto
{
    public static string PastaDasConversas(string principal, string? raiz = null) =>
        Path.Combine(raiz ?? ConversasSalvas.PastaPadrao, string.Concat(principal.TrimEnd('\\', '/').Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')));

    public static int Conversas(string principal, string? raiz = null)
    {
        var pasta = PastaDasConversas(principal, raiz);
        try
        {
            return Directory.Exists(pasta) ? Directory.EnumerateFiles(pasta, "*.jsonl").Count() : 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    public static bool DentroDe(string caminho, string pasta)
    {
        var a = Path.GetFullPath(caminho);
        var p = Path.GetFullPath(pasta).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
        return a.StartsWith(p, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    public static async Task<DiagnosticoDeExclusao> DiagnosticarAsync(string principal, string? raizConversas = null)
    {
        var conversas = Conversas(principal, raizConversas);
        var git = Directory.Exists(Path.Combine(principal, ".git")) || File.Exists(Path.Combine(principal, ".git"));
        if (!git) return new(false, 0, false, 0, null, [], null, conversas);
        var alteracoes = await Repositorio.AlteracoesAsync(principal);
        var remotos = await ComandoGit.ExecutarAsync(principal, "remote");
        var temRemoto = remotos.Ok && remotos.Saida.Trim().Length > 0;
        var contagem = temRemoto
            ? await ComandoGit.ExecutarAsync(principal, "rev-list", "--count", "--branches", "--not", "--remotes")
            : await ComandoGit.ExecutarAsync(principal, "rev-list", "--count", "--all");
        var naoEnviados = contagem.Ok && int.TryParse(contagem.Saida.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
        var worktrees = await Repositorio.WorktreesAsync(principal);
        var raiz = worktrees.FirstOrDefault(w => w.Principal);
        var ehWorktree = raiz is not null && !Workspaces.MesmaPasta(raiz.Caminho, principal);
        var outros = ehWorktree ? [] : worktrees.Where(w => !w.Principal).Select(w => w.Caminho).ToList();
        return new(true, alteracoes, temRemoto, naoEnviados, ehWorktree ? raiz!.Caminho : null, outros, Repositorio.BranchAtual(principal), conversas);
    }

    public static IReadOnlyList<string> Avisos(DiagnosticoDeExclusao d)
    {
        var avisos = new List<string>();
        if (!d.Git) return avisos;
        if (d.Alteracoes > 0)
            avisos.Add(d.EhWorktree
                ? d.Alteracoes == 1
                    ? Idioma.T("Tem 1 alteração sem commit, e o git não remove o worktree assim. Faça commit ou descarte antes.")
                    : Idioma.F("Tem {0} alterações sem commit, e o git não remove o worktree assim. Faça commit ou descarte antes.", d.Alteracoes)
                : d.Alteracoes == 1
                    ? Idioma.T("1 arquivo alterado sem commit vão junto.")
                    : Idioma.F("{0} arquivos alterados sem commit vão junto.", d.Alteracoes));
        if (!d.EhWorktree && !d.TemRemoto)
            avisos.Add(Idioma.T("O repositório não tem remoto: o histórico do git só existe nesta pasta."));
        else if (!d.EhWorktree && d.NaoEnviados > 0)
            avisos.Add(d.NaoEnviados == 1
                ? Idioma.T("1 commit em branches locais ainda não está no remoto.")
                : Idioma.F("{0} commits em branches locais ainda não estão no remoto.", d.NaoEnviados));
        if (d.OutrosWorktrees.Count > 0)
        {
            var nomes = string.Join(", ", d.OutrosWorktrees.Select(w => Path.GetFileName(w.TrimEnd('\\', '/'))));
            avisos.Add(d.OutrosWorktrees.Count == 1
                ? Idioma.F("1 worktree deste repositório ({0}) para de funcionar.", nomes)
                : Idioma.F("{0} worktrees deste repositório ({1}) param de funcionar.", d.OutrosWorktrees.Count, nomes));
        }
        return avisos;
    }

    public static bool Impede(DiagnosticoDeExclusao d) => d.EhWorktree && d.Alteracoes > 0;
}
