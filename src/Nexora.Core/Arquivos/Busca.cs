using System.Text;
using System.Text.RegularExpressions;
using Nexora.Core.Git;

namespace Nexora.Core.Arquivos;

public sealed record Achado(string Caminho, string Raiz, string Relativo, int Linha, int Coluna, string Texto);

public sealed record ResultadoBusca(IReadOnlyList<Achado> Achados, bool Cortado);

public static class Busca
{
    const int MaiorArquivo = 1_500_000;
    const int MaiorTrecho = 240;

    public static async Task<ResultadoBusca> ProcurarAsync(IEnumerable<string> raizes, string termo, bool maiusculas, bool regex, int maximo = 600, CancellationToken cancelar = default)
    {
        if (string.IsNullOrEmpty(termo)) return new ResultadoBusca([], false);
        var achados = new List<Achado>();
        foreach (var raiz in raizes.Where(Directory.Exists))
        {
            cancelar.ThrowIfCancellationRequested();
            var restante = maximo + 1 - achados.Count;
            if (restante <= 0) break;
            achados.AddRange(Directory.Exists(Path.Combine(raiz, ".git")) || File.Exists(Path.Combine(raiz, ".git"))
                ? await ComGitAsync(raiz, termo, maiusculas, regex, restante)
                : NaPasta(raiz, termo, maiusculas, regex, restante, cancelar));
        }
        return achados.Count > maximo ? new ResultadoBusca(achados[..maximo], true) : new ResultadoBusca(achados, false);
    }

    static async Task<IReadOnlyList<Achado>> ComGitAsync(string raiz, string termo, bool maiusculas, bool regex, int maximo)
    {
        List<string> args = ["grep", "-n", "-I", "--column", "--no-color", "--untracked", "--max-count=100", "-z"];
        if (!maiusculas) args.Add("-i");
        args.Add(regex ? "-E" : "-F");
        args.AddRange(["-e", termo, "--", "."]);
        var r = await ComandoGit.ExecutarAsync(raiz, args, TimeSpan.FromSeconds(30));
        if (r.Codigo is not (0 or 1)) return NaPasta(raiz, termo, maiusculas, regex, maximo, default);
        var achados = new List<Achado>();
        foreach (var linha in r.Saida.Split('\n'))
        {
            var partes = linha.Split('\0');
            if (partes.Length < 4 || !int.TryParse(partes[1], out var numero) || !int.TryParse(partes[2], out var coluna)) continue;
            var relativo = partes[0].Replace('/', Path.DirectorySeparatorChar);
            achados.Add(new Achado(Path.Combine(raiz, relativo), raiz, relativo, numero, coluna, Cortar(string.Join('\0', partes[3..]))));
            if (achados.Count >= maximo) break;
        }
        return achados;
    }

    static List<Achado> NaPasta(string raiz, string termo, bool maiusculas, bool regex, int maximo, CancellationToken cancelar)
    {
        Regex? expressao;
        try
        {
            expressao = regex ? new Regex(termo, maiusculas ? RegexOptions.None : RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)) : null;
        }
        catch (ArgumentException)
        {
            return [];
        }
        var comparacao = maiusculas ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var achados = new List<Achado>();
        foreach (var arquivo in Pastas.TodosOsArquivos(raiz))
        {
            if (cancelar.IsCancellationRequested || achados.Count >= maximo) break;
            string[] linhas;
            try
            {
                if (new FileInfo(arquivo).Length > MaiorArquivo) continue;
                var bytes = File.ReadAllBytes(arquivo);
                if (bytes.AsSpan(0, Math.Min(bytes.Length, 8000)).Contains((byte)0)) continue;
                linhas = Encoding.UTF8.GetString(bytes).Split('\n');
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            for (var i = 0; i < linhas.Length && achados.Count < maximo; i++)
            {
                var coluna = expressao is not null ? (expressao.Match(linhas[i]) is { Success: true } m ? m.Index : -1) : linhas[i].IndexOf(termo, comparacao);
                if (coluna >= 0) achados.Add(new Achado(arquivo, raiz, Path.GetRelativePath(raiz, arquivo), i + 1, coluna + 1, Cortar(linhas[i])));
            }
        }
        return achados;
    }

    static string Cortar(string linha)
    {
        var limpa = linha.TrimEnd('\r').Trim();
        return limpa.Length > MaiorTrecho ? limpa[..MaiorTrecho] + "…" : limpa;
    }
}
