using Nexora.Core.Texto;

namespace Nexora.Core.Git;

public sealed record Alteracao(string Codigo, string Caminho, string? Anterior)
{
    public bool Nova => Codigo is "??" or "A " or "A";
    public bool Removida => Codigo.Contains('D');

    public bool RotuloNovo => Codigo.Trim() is "??" or "A";
    public bool RotuloApagado => Codigo.Trim() == "D";

    public string Rotulo => Codigo.Trim() switch
    {
        _ when RotuloNovo => Idioma.T("novo"),
        _ when RotuloApagado => Idioma.T("apagado"),
        var c when c.StartsWith('R') => Idioma.T("renomeado"),
        _ => Idioma.T("alterado"),
    };
}

public sealed record LinhasDoArquivo(string Caminho, int Mais, int Menos, bool Binario, bool Novo);

public static class Alteracoes
{
    public static bool DeAgente(string caminho) => caminho.Replace('\\', '/').StartsWith(".claude/worktrees/", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<LinhasDoArquivo> LerNumstat(string saida) =>
        [.. saida.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r').Split('\t'))
            .Where(p => p.Length >= 3)
            .Select(p => new LinhasDoArquivo(
                Renomeado(string.Join('\t', p.Skip(2))),
                int.TryParse(p[0], out var mais) ? mais : 0,
                int.TryParse(p[1], out var menos) ? menos : 0,
                p[0] == "-" && p[1] == "-",
                false))];

    static string Renomeado(string caminho)
    {
        var chave = caminho.IndexOf('{');
        var fim = caminho.IndexOf('}');
        if (chave >= 0 && fim > chave && caminho[chave..fim].Contains(" => "))
        {
            var dentro = caminho[(chave + 1)..fim];
            var novo = dentro[(dentro.IndexOf(" => ", StringComparison.Ordinal) + 4)..];
            return (caminho[..chave] + novo + caminho[(fim + 1)..]).Replace("//", "/");
        }
        var seta = caminho.IndexOf(" => ", StringComparison.Ordinal);
        return seta >= 0 ? caminho[(seta + 4)..] : caminho;
    }

    public static async Task<IReadOnlyList<LinhasDoArquivo>> ContarAsync(string pasta)
    {
        var diff = await ComandoGit.ExecutarAsync(pasta, "diff", "HEAD", "--numstat");
        var lista = diff.Ok ? LerNumstat(diff.Saida).ToList() : [];
        var novos = await ComandoGit.ExecutarAsync(pasta, "ls-files", "--others", "--exclude-standard");
        if (novos.Ok)
            foreach (var caminho in novos.Saida.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(c => !DeAgente(c)))
                lista.Add(new LinhasDoArquivo(caminho, ContarLinhas(Path.Combine(pasta, caminho)), 0, false, true));
        return lista;
    }

    static int ContarLinhas(string arquivo)
    {
        try
        {
            var info = new FileInfo(arquivo);
            if (!info.Exists || info.Length > 2_000_000) return 0;
            return File.ReadLines(arquivo).Count();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    public static IReadOnlyList<Alteracao> Ler(string porcelana)
    {
        var lista = new List<Alteracao>();
        var partes = porcelana.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < partes.Length; i++)
        {
            var parte = partes[i];
            if (parte.Length < 4) continue;
            var codigo = parte[..2];
            var caminho = parte[3..];
            string? anterior = null;
            if (codigo.Contains('R') || codigo.Contains('C')) anterior = partes.ElementAtOrDefault(++i);
            lista.Add(new Alteracao(codigo, caminho, anterior));
        }
        return lista;
    }

    public static async Task<IReadOnlyList<Alteracao>> ListarAsync(string pasta)
    {
        var r = await ComandoGit.ExecutarAsync(pasta, "status", "--porcelain=v1", "-z", "--untracked-files=all");
        return r.Ok ? [.. Ler(r.Saida).Where(a => !DeAgente(a.Caminho))] : [];
    }

    public static async Task<string> DiffAsync(string pasta, Alteracao alteracao)
    {
        if (alteracao.Codigo == "??")
        {
            var completo = Path.Combine(pasta, alteracao.Caminho);
            var conteudo = File.Exists(completo) ? await File.ReadAllTextAsync(completo) : "";
            var linhas = conteudo.Replace("\r\n", "\n").Split('\n');
            return $"@@ -0,0 +1,{linhas.Length} @@\n" + string.Join("\n", linhas.Select(l => "+" + l));
        }
        var r = await ComandoGit.ExecutarAsync(pasta, "diff", "HEAD", "--no-color", "--", alteracao.Caminho);
        return r.Ok ? r.Saida : r.Mensagem;
    }
}
