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
        var listas = await Task.WhenAll(tarefas).ConfigureAwait(false);
        return new IndiceArquivos(listas.SelectMany(l => l).ToList());
    }

    public static IndiceArquivos Montar(IEnumerable<string> raizes) => MontarAsync(raizes).GetAwaiter().GetResult();

    public static IndiceArquivos DeLista(IEnumerable<ArquivoIndexado> arquivos) => new(arquivos.ToList());

    static async Task<List<ArquivoIndexado>> ListarAsync(string raiz)
    {
        var nomeRaiz = Path.GetFileName(raiz.TrimEnd('\\', '/'));
        var git = await ComandoGit.ExecutarAsync(raiz, ["ls-files", "--cached", "--others", "--exclude-standard", "-z"], TimeSpan.FromSeconds(20)).ConfigureAwait(false);
        IEnumerable<string> relativos = git.Ok
            ? git.Saida.Split('\0', StringSplitOptions.RemoveEmptyEntries).Distinct()
            : await Task.Run(() => Pastas.TodosOsArquivos(raiz, MaiorVarredura).Select(a => Path.GetRelativePath(raiz, a).Replace('\\', '/')).ToList()).ConfigureAwait(false);
        return relativos
            .Select(r => new ArquivoIndexado(nomeRaiz, Path.Combine(raiz, r.Replace('/', Path.DirectorySeparatorChar)), r, Path.GetFileName(r)))
            .ToList();
    }

    public IReadOnlyList<ArquivoIndexado> Buscar(string termo, int maximo = 30, CancellationToken cancelar = default)
    {
        termo = termo.Trim().Replace('\\', '/');
        if (termo.Length == 0) return _arquivos.Take(maximo).ToList();
        var minusculo = termo.ToLowerInvariant();
        var notas = new List<(ArquivoIndexado Arquivo, int Nota)>();
        var maior = 0;
        foreach (var a in _arquivos)
        {
            cancelar.ThrowIfCancellationRequested();
            var nota = Nota(a, termo, minusculo, maior < 400);
            if (nota == 0) continue;
            notas.Add((a, nota));
            if (nota > maior) maior = nota;
        }
        if (notas.Count == 0) return [];
        var piso = maior switch { >= 800 => 800, >= 400 => 400, _ => 1 };
        return notas
            .Where(x => x.Nota >= piso)
            .OrderByDescending(x => x.Nota)
            .ThenBy(x => x.Arquivo.Nome.Length)
            .ThenBy(x => x.Arquivo.Relativo.Length)
            .Take(maximo)
            .Select(x => x.Arquivo)
            .ToList();
    }

    static int Nota(ArquivoIndexado a, string termo, string minusculo, bool aproximada)
    {
        if (a.Nome.Equals(termo, StringComparison.OrdinalIgnoreCase)) return 1000;
        if (a.Nome.StartsWith(termo, StringComparison.OrdinalIgnoreCase)) return 800;
        if (a.Nome.Contains(termo, StringComparison.OrdinalIgnoreCase)) return 600;
        if (a.Relativo.Contains(termo, StringComparison.OrdinalIgnoreCase)) return 400;
        if (!aproximada) return 0;
        return EmOrdem(a.Nome, minusculo) ? 200 : EmOrdem(a.Relativo, minusculo) ? 100 : 0;
    }

    static bool EmOrdem(string texto, string minusculo)
    {
        var i = 0;
        foreach (var c in texto)
            if (char.ToLowerInvariant(c) == minusculo[i] && ++i == minusculo.Length) return true;
        return false;
    }
}
