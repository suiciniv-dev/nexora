using Nexora.Core.Texto;

namespace Nexora.Core.Claude;

public static class LocalizadorClaude
{
    public static string Encontrar() =>
        Procurar() ?? throw new FileNotFoundException(Idioma.T("Não achei o Claude Code. Instale pelo terminal (ou pela extensão do VS Code) e faça o login com o comando claude."));

    public static string? Procurar(string? casa = null)
    {
        var nome = OperatingSystem.IsWindows() ? "claude.exe" : "claude";
        casa ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var pastas = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Append(Path.Combine(casa, ".local", "bin"));
        foreach (var pasta in pastas)
        {
            try
            {
                var caminho = Path.Combine(pasta.Trim('"'), nome);
                if (File.Exists(caminho)) return caminho;
            }
            catch (ArgumentException)
            {
            }
        }
        return DaExtensao(casa, nome);
    }

    public static string? DaExtensao(string casa, string nome)
    {
        foreach (var editor in new[] { ".vscode", ".vscode-insiders", ".cursor", ".windsurf" })
        {
            var extensoes = Path.Combine(casa, editor, "extensions");
            if (!Directory.Exists(extensoes)) continue;
            var achado = Directory.EnumerateDirectories(extensoes, "anthropic.claude-code-*")
                .Select(d => (Pasta: d, Versao: Versao(Path.GetFileName(d))))
                .OrderByDescending(d => d.Versao)
                .Select(d => Path.Combine(d.Pasta, "resources", "native-binary", nome))
                .FirstOrDefault(File.Exists);
            if (achado is not null) return achado;
        }
        return null;
    }

    static Version Versao(string pasta)
    {
        var partes = pasta["anthropic.claude-code-".Length..].Split('-')[0];
        return Version.TryParse(partes, out var v) ? v : new Version(0, 0);
    }
}
