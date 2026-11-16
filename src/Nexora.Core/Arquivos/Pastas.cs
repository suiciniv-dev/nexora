namespace Nexora.Core.Arquivos;

public sealed record Entrada(string Nome, string Caminho, bool EhPasta);

public static class Pastas
{
    static readonly HashSet<string> Ignoradas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".idea", ".vscode-test", "bin", "obj", "node_modules", ".gradle", "target", "__pycache__", ".venv", ".angular", ".next",
    };

    public static bool Ignorada(string nome) => Ignoradas.Contains(nome);

    public static IReadOnlyList<Entrada> Filhos(string pasta)
    {
        try
        {
            var dir = new DirectoryInfo(pasta);
            var pastas = dir.EnumerateDirectories()
                .Where(d => !Ignorada(d.Name) && !d.Attributes.HasFlag(FileAttributes.System))
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .Select(d => new Entrada(d.Name, d.FullName, true));
            var arquivos = dir.EnumerateFiles()
                .Where(f => !f.Attributes.HasFlag(FileAttributes.System))
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .Select(f => new Entrada(f.Name, f.FullName, false));
            return [.. pastas, .. arquivos];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static IEnumerable<string> TodosOsArquivos(string raiz, int maximo = 60_000)
    {
        var pilha = new Stack<string>([raiz]);
        var total = 0;
        while (pilha.Count > 0 && total < maximo)
        {
            var atual = pilha.Pop();
            string[] arquivos, subpastas;
            try
            {
                arquivos = Directory.GetFiles(atual);
                subpastas = Directory.GetDirectories(atual);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            foreach (var arquivo in arquivos)
            {
                if (++total > maximo) yield break;
                yield return arquivo;
            }
            foreach (var sub in subpastas)
                if (!Ignorada(Path.GetFileName(sub))) pilha.Push(sub);
        }
    }
}
