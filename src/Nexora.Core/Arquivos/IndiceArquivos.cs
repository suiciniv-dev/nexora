using Nexora.Core.Git;

namespace Nexora.Core.Arquivos;

public sealed record ArquivoIndexado(string Raiz, string Caminho, string Relativo, string Nome)
{
    public string Local
    {
        get
        {
            var partes = (Path.GetDirectoryName(Caminho) ?? "").Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
            return partes.Length > 3 ? "…/" + string.Join('/', partes[^3..]) : string.Join('/', partes);
        }
    }
}

public sealed class IndiceArquivos
{
    const int MaiorVarredura = 20_000;

    readonly List<ArquivoIndexado> _arquivos;

    IndiceArquivos(List<ArquivoIndexado> arquivos) => _arquivos = arquivos;

    public int Total => _arquivos.Count;

    public static async Task<IndiceArquivos> MontarAsync(IEnumerable<string> raizes)
    {
        var tarefas = raizes.Where(Directory.Exists).Select(ListarAsync).ToList();
        var listas = await Task.WhenAll(tarefas);
        return new IndiceArquivos(listas.SelectMany(l => l).ToList());
    }

    public static IndiceArquivos Montar(IEnumerable<string> raizes) => MontarAsync(raizes).GetAwaiter().GetResult();

    public static IndiceArquivos DeLista(IEnumerable<ArquivoIndexado> arquivos) => new(arquivos.ToList());

    static async Task<List<ArquivoIndexado>> ListarAsync(string raiz)
    {
        var nomeRaiz = Path.GetFileName(raiz.TrimEnd('\\', '/'));
        var git = await ComandoGit.ExecutarAsync(raiz, ["ls-files", "--cached", "--others", "--exclude-standard", "-z"], TimeSpan.FromSeconds(20));
        IEnumerable<string> relativos = git.Ok
            ? git.Saida.Split('\0', StringSplitOptions.RemoveEmptyEntries).Distinct()
            : await Task.Run(() => Pastas.TodosOsArquivos(raiz, MaiorVarredura).Select(a => Path.GetRelativePath(raiz, a).Replace('\\', '/')).ToList());
        return relativos
            .Select(r => new ArquivoIndexado(nomeRaiz, Path.Combine(raiz, r.Replace('/', Path.DirectorySeparatorChar)), r, Path.GetFileName(r)))
            .ToList();
    }

    public IReadOnlyList<ArquivoIndexado> Buscar(string termo, int maximo = 30)
    {
        termo = termo.Trim().Replace('\\', '/');
        if (termo.Length == 0) return _arquivos.Take(maximo).ToList();
        var notas = _arquivos.Select(a => (Arquivo: a, Nota: Nota(a, termo))).Where(x => x.Nota > 0).ToList();
        if (notas.Count == 0) return [];
        var piso = notas.Max(x => x.Nota) switch { >= 800 => 800, >= 400 => 400, _ => 1 };
        return notas
            .Where(x => x.Nota >= piso)
            .OrderByDescending(x => x.Nota)
            .ThenBy(x => x.Arquivo.Nome.Length)
            .ThenBy(x => x.Arquivo.Relativo.Length)
            .Take(maximo)
            .Select(x => x.Arquivo)
            .ToList();
    }

    static int Nota(ArquivoIndexado a, string termo)
    {
        if (a.Nome.Equals(termo, StringComparison.OrdinalIgnoreCase)) return 1000;
        if (a.Nome.StartsWith(termo, StringComparison.OrdinalIgnoreCase)) return 800;
        if (a.Nome.Contains(termo, StringComparison.OrdinalIgnoreCase)) return 600;
        if (a.Relativo.Contains(termo, StringComparison.OrdinalIgnoreCase)) return 400;
        return EmOrdem(a.Nome, termo) ? 200 : EmOrdem(a.Relativo, termo) ? 100 : 0;
    }

    static bool EmOrdem(string texto, string termo)
    {
        var i = 0;
        foreach (var c in texto)
            if (i < termo.Length && char.ToLowerInvariant(c) == char.ToLowerInvariant(termo[i])) i++;
        return i == termo.Length;
    }
}
