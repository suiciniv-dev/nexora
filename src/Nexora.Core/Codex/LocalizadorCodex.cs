using Nexora.Core.Texto;

namespace Nexora.Core.Codex;

public static class LocalizadorCodex
{
    public static (string Arquivo, string[] Prefixo) Comando()
    {
        var achado = Procurar() ?? throw new FileNotFoundException(Idioma.T("Não achei o Codex. Instale com npm install -g @openai/codex e faça o login com o comando codex."));
        return achado.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ? ("cmd.exe", ["/c", achado]) : (achado, []);
    }

    public static string? Procurar(string? casa = null, string? path = null)
    {
        casa ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var windows = OperatingSystem.IsWindows();
        var pastas = (path ?? Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Trim('"'))
            .Concat(windows
                ? [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm")]
                : ["/opt/homebrew/bin", "/usr/local/bin", Path.Combine(casa, ".npm-global", "bin"), Path.Combine(casa, ".local", "bin")])
            .ToList();
        string[] nomes = windows ? ["codex.exe", "codex.cmd"] : ["codex"];
        foreach (var nome in nomes)
            foreach (var pasta in pastas)
            {
                try
                {
                    var caminho = Path.Combine(pasta, nome);
                    if (!File.Exists(caminho)) continue;
                    return nome == "codex.cmd" ? DoNpm(pasta) ?? caminho : caminho;
                }
                catch (ArgumentException)
                {
                }
            }
        return DaExtensao(casa);
    }

    static string? DoNpm(string pastaDoNpm)
    {
        var vendor = Path.Combine(pastaDoNpm, "node_modules", "@openai", "codex", "vendor");
        if (!Directory.Exists(vendor)) return null;
        return Directory.EnumerateFiles(vendor, "codex.exe", SearchOption.AllDirectories).FirstOrDefault();
    }

    public static string? DaExtensao(string casa)
    {
        var nome = OperatingSystem.IsWindows() ? "codex.exe" : "codex";
        foreach (var editor in new[] { ".vscode", ".vscode-insiders", ".cursor", ".windsurf" })
        {
            var extensoes = Path.Combine(casa, editor, "extensions");
            if (!Directory.Exists(extensoes)) continue;
            foreach (var pasta in Directory.EnumerateDirectories(extensoes, "openai.chatgpt-*").OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var bin = Path.Combine(pasta, "bin");
                if (!Directory.Exists(bin)) continue;
                if (Directory.EnumerateFiles(bin, nome, SearchOption.AllDirectories).FirstOrDefault() is { } achado) return achado;
            }
        }
        return null;
    }
}
