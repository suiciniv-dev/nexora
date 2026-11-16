using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nexora.Core.Mcp;

public sealed record DefinicaoMcp(string Nome, string Comando, IReadOnlyList<string> Argumentos, IReadOnlyDictionary<string, string> Ambiente, string Pasta)
{
    static readonly Regex Navegador = new("chrome-devtools|playwright|puppeteer|selenium|browser|navegador", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool PorSessao => Navegador.IsMatch(Nome) || Navegador.IsMatch(Comando) || Argumentos.Any(Navegador.IsMatch);

    public string Chave => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(string.Join('\n',
        [ServidoresDoProjeto.Normalizar(Pasta), Nome, Comando, .. Argumentos, .. Ambiente.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}")]))))[..16].ToLowerInvariant();
}

public static class ServidoresDoProjeto
{
    static readonly JsonDocumentOptions Leniente = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    static readonly Regex Variavel = new(@"\$\{([A-Za-z_][A-Za-z0-9_]*)(?::-([^}]*))?\}", RegexOptions.Compiled);

    public static IReadOnlyList<DefinicaoMcp> Ler(string pasta, string? casa = null)
    {
        casa ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var arquivo = Path.Combine(pasta, ".mcp.json");
        if (!File.Exists(arquivo) || LerJson(arquivo) is not { } doc) return [];
        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("mcpServers", out var servidores) || servidores.ValueKind != JsonValueKind.Object) return [];
            var desligados = Desligados(pasta, casa);
            var lista = new List<DefinicaoMcp>();
            foreach (var s in servidores.EnumerateObject())
            {
                var v = s.Value;
                if (v.ValueKind != JsonValueKind.Object || desligados.Contains(s.Name)) continue;
                var tipo = v.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : "stdio";
                if (tipo != "stdio" || !v.TryGetProperty("command", out var c) || c.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(c.GetString())) continue;
                var args = v.TryGetProperty("args", out var a) && a.ValueKind == JsonValueKind.Array
                    ? a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => Expandir(x.GetString()!)).ToList()
                    : [];
                var ambiente = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (v.TryGetProperty("env", out var e) && e.ValueKind == JsonValueKind.Object)
                    foreach (var p in e.EnumerateObject())
                        if (p.Value.ValueKind == JsonValueKind.String) ambiente[p.Name] = Expandir(p.Value.GetString()!);
                lista.Add(new DefinicaoMcp(s.Name, Expandir(c.GetString()!), args, ambiente, pasta));
            }
            return lista;
        }
    }

    public static string Expandir(string texto) => Variavel.Replace(texto, m =>
        Environment.GetEnvironmentVariable(m.Groups[1].Value) is { Length: > 0 } valor ? valor : m.Groups[2].Success ? m.Groups[2].Value : m.Value);

    public static string Normalizar(string pasta) => pasta.Replace('\\', '/').TrimEnd('/').ToLowerInvariant();

    static HashSet<string> Desligados(string pasta, string casa)
    {
        var nomes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var arquivo in new[]
        {
            Path.Combine(casa, ".claude", "settings.json"),
            Path.Combine(pasta, ".claude", "settings.json"),
            Path.Combine(pasta, ".claude", "settings.local.json"),
        })
            if (File.Exists(arquivo) && LerJson(arquivo) is { } doc)
                using (doc) Juntar(nomes, doc.RootElement);

        var global = Path.Combine(casa, ".claude.json");
        if (File.Exists(global) && LerJson(global) is { } estado)
            using (estado)
                if (estado.RootElement.TryGetProperty("projects", out var projetos) && projetos.ValueKind == JsonValueKind.Object)
                {
                    var alvo = Normalizar(pasta);
                    foreach (var p in projetos.EnumerateObject())
                        if (Normalizar(p.Name) == alvo) Juntar(nomes, p.Value);
                }
        return nomes;
    }

    static void Juntar(HashSet<string> nomes, JsonElement raiz)
    {
        if (raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("disabledMcpjsonServers", out var lista) && lista.ValueKind == JsonValueKind.Array)
            foreach (var n in lista.EnumerateArray())
                if (n.ValueKind == JsonValueKind.String) nomes.Add(n.GetString()!);
    }

    static JsonDocument? LerJson(string arquivo)
    {
        try
        {
            using var fluxo = new FileStream(arquivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return JsonDocument.Parse(fluxo, Leniente);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
