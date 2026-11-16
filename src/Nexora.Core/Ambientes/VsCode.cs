using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nexora.Core.Ambientes;

public sealed record Achado(string Caminho, bool Workspace)
{
    public string Nome => Workspace ? Path.GetFileNameWithoutExtension(Caminho) : Path.GetFileName(Caminho.TrimEnd('\\', '/'));
}

public static class VsCode
{
    static readonly string[] Produtos = ["Code", "Code - Insiders", "Cursor", "Windsurf", "VSCodium"];

    public static IReadOnlyList<Achado> Recentes(string appData)
    {
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var achados = new List<Achado>();
        foreach (var produto in Produtos)
        {
            var arquivo = Path.Combine(appData, produto, "User", "globalStorage", "storage.json");
            JsonNode? raiz;
            try
            {
                if (!File.Exists(arquivo)) continue;
                raiz = JsonNode.Parse(File.ReadAllText(arquivo));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                continue;
            }
            foreach (var uri in Uris(raiz))
            {
                if (Local(uri) is not { } caminho || !vistos.Add(caminho)) continue;
                if (caminho.EndsWith(".code-workspace", StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(caminho)) achados.Add(new Achado(caminho, true));
                }
                else if (caminho.Length > 3 && Directory.Exists(caminho) && EhRepositorio(caminho))
                    achados.Add(new Achado(caminho, false));
            }
        }
        return achados.OrderBy(a => a.Nome, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static IReadOnlyList<(string Pasta, int Quantos)> Raizes(IEnumerable<Achado> achados, string casa)
    {
        var candidatos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in achados)
        {
            if (Path.GetDirectoryName(a.Caminho.TrimEnd('\\', '/')) is not { } dir) continue;
            candidatos.Add(dir);
            if (a.Workspace && Path.GetDirectoryName(dir) is { } acima) candidatos.Add(acima);
        }
        var boas = candidatos.Where(c => c.Length > 3 && !Workspaces.MesmaPasta(c, casa))
            .Select(c => (Pasta: c, Quantos: Workspaces.Procurar([c]).Count))
            .Where(c => c.Quantos >= 2)
            .ToList();
        return boas.Where(c => !boas.Any(o => o.Pasta != c.Pasta && Dentro(c.Pasta, o.Pasta)))
            .OrderByDescending(c => c.Quantos).ThenBy(c => c.Pasta, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool Dentro(string pasta, string raiz)
    {
        var r = raiz.TrimEnd('\\', '/');
        return Workspaces.MesmaPasta(pasta, r) || pasta.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    static IEnumerable<string> Uris(JsonNode? no)
    {
        switch (no)
        {
            case JsonObject objeto:
                foreach (var (chave, valor) in objeto)
                {
                    if (chave.StartsWith("file:///", StringComparison.OrdinalIgnoreCase)) yield return chave;
                    foreach (var u in Uris(valor)) yield return u;
                }
                break;
            case JsonArray lista:
                foreach (var item in lista)
                    foreach (var u in Uris(item)) yield return u;
                break;
            case JsonValue valor when valor.TryGetValue<string>(out var texto) && texto.StartsWith("file:///", StringComparison.OrdinalIgnoreCase):
                yield return texto;
                break;
        }
    }

    static string? Local(string uri)
    {
        try
        {
            var texto = System.Uri.UnescapeDataString(uri["file:///".Length..]).Replace('/', Path.DirectorySeparatorChar);
            if (texto.Length < 2 || texto[1] != ':' || !char.IsAsciiLetter(texto[0])) return null;
            var caminho = Path.GetFullPath(char.ToUpperInvariant(texto[0]) + texto[1..]).TrimEnd('\\', '/');
            return caminho.Length == 2 ? caminho + "\\" : caminho;
        }
        catch (Exception ex) when (ex is UriFormatException or ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    static bool EhRepositorio(string pasta) =>
        Directory.Exists(Path.Combine(pasta, ".git")) || File.Exists(Path.Combine(pasta, ".git"));
}
