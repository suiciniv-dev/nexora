using Nexora.Core.Claude;
using Nexora.Core.Codex;
using Nexora.Core.Texto;

namespace Nexora.Core.Provedores;

public enum ProtocoloAgente
{
    Claude,
    Codex,
    Acp,
}

public sealed record DescricaoAgente(
    string Id,
    string Nome,
    string Empresa,
    ProtocoloAgente Protocolo,
    IReadOnlyList<string> Executaveis,
    IReadOnlyList<string> Argumentos,
    string Instalar,
    string Descricao)
{
    public bool EhClaude => Protocolo == ProtocoloAgente.Claude;
    public bool TemRemoto => Protocolo == ProtocoloAgente.Claude;
    public bool VoltaConversa => Protocolo is ProtocoloAgente.Claude or ProtocoloAgente.Codex;
    public bool TemEsforco => Protocolo is ProtocoloAgente.Claude or ProtocoloAgente.Codex;
    public string Inicial => Nome[..1].ToUpperInvariant();
}

public static class CatalogoAgentes
{
    public static IReadOnlyList<DescricaoAgente> Todos { get; } =
    [
        new(Agentes.Claude, "Claude Code", "Anthropic", ProtocoloAgente.Claude, ["claude"], [], "npm install -g @anthropic-ai/claude-code",
            Idioma.T("O agente da Anthropic. Todas as funções do Nexora.")),
        new(Agentes.Codex, "Codex", "OpenAI", ProtocoloAgente.Codex, ["codex"], [], "npm install -g @openai/codex",
            Idioma.T("O agente da OpenAI, pelo codex app-server.")),
        new("gemini", "Gemini CLI", "Google", ProtocoloAgente.Acp, ["gemini"], ["--acp"], "npm install -g @google/gemini-cli",
            Idioma.T("Antigo. Com conta Google pessoal o Google não aceita mais; só funciona com chave de API ou Vertex. Prefira o Antigravity.")),
        new("antigravity", "Antigravity", "Google", ProtocoloAgente.Acp, ["agy"], ["acp"], "antigravity.google/download",
            Idioma.T("O agente do Antigravity, do Google, com login da sua conta.")),
        new("copilot", "GitHub Copilot", "GitHub", ProtocoloAgente.Acp, ["copilot"], ["--acp"], "npm install -g @github/copilot",
            Idioma.T("O Copilot CLI do GitHub, com os modelos do seu plano.")),
        new("cursor", "Cursor", "Anysphere", ProtocoloAgente.Acp, ["cursor-agent"], ["acp"], "cursor.com/cli",
            Idioma.T("O agente de terminal do Cursor.")),
        new("opencode", "OpenCode", "SST", ProtocoloAgente.Acp, ["opencode"], ["acp"], "npm install -g opencode-ai",
            Idioma.T("Agente aberto que usa o modelo e a conta que você configurar.")),
        new("kiro", "Kiro", "Amazon", ProtocoloAgente.Acp, ["kiro-cli"], ["acp", "--agent-engine=v3"], "kiro.dev/cli",
            Idioma.T("O agente de terminal do Kiro, da AWS.")),
        new("qwen", "Qwen Code", "Alibaba", ProtocoloAgente.Acp, ["qwen"], ["--acp"], "npm install -g @qwen-code/qwen-code",
            Idioma.T("O agente da Alibaba, com os modelos Qwen.")),
        new("grok", "Grok", "xAI", ProtocoloAgente.Acp, ["grok"], ["agent", "stdio"], "npm install -g @xai-official/grok",
            Idioma.T("O agente da xAI, com os modelos Grok.")),
        new("goose", "goose", "Block", ProtocoloAgente.Acp, ["goose"], ["acp"], "block.github.io/goose",
            Idioma.T("Agente aberto da Block, com o provedor que você configurar.")),
    ];

    public static DescricaoAgente Claude => Todos[0];

    public static DescricaoAgente? Achar(string? id) => Todos.FirstOrDefault(a => a.Id == id);

    public static DescricaoAgente De(string? id) => Achar(id) ?? Claude;

    public static string? Procurar(DescricaoAgente agente, string? casa = null, string? path = null) => agente.Protocolo switch
    {
        ProtocoloAgente.Claude => LocalizadorClaude.Procurar(),
        ProtocoloAgente.Codex => LocalizadorCodex.Procurar(casa, path),
        _ => Executavel(agente.Executaveis, casa, path),
    };

    public static (string Arquivo, IReadOnlyList<string> Argumentos) Comando(DescricaoAgente agente)
    {
        var achado = Procurar(agente) ?? throw new FileNotFoundException(Idioma.F("Não achei o {0}. Instale com: {1}", agente.Nome, agente.Instalar));
        return (achado, agente.Argumentos);
    }

    public static string? Executavel(IReadOnlyList<string> nomes, string? casa = null, string? path = null)
    {
        casa ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var windows = OperatingSystem.IsWindows();
        var pastas = (path ?? Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Trim('"'))
            .Where(p => !p.Contains("copilot-shim", StringComparison.OrdinalIgnoreCase))
            .Concat(windows
                ? [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm"), Path.Combine(casa, ".local", "bin"), Path.Combine(casa, "AppData", "Local", "Programs"), Path.Combine(casa, "AppData", "Local", "agy", "bin"), Path.Combine(casa, ".gemini", "bin")]
                : ["/opt/homebrew/bin", "/usr/local/bin", Path.Combine(casa, ".npm-global", "bin"), Path.Combine(casa, ".local", "bin"), Path.Combine(casa, ".bun", "bin")])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        string[] extensoes = windows ? [".exe", ".cmd", ".bat"] : [""];
        foreach (var nome in nomes)
            foreach (var extensao in extensoes)
                foreach (var pasta in pastas)
                {
                    try
                    {
                        var caminho = Path.Combine(pasta, nome + extensao);
                        if (File.Exists(caminho)) return caminho;
                    }
                    catch (ArgumentException)
                    {
                    }
                }
        return null;
    }
}
