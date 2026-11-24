using System.Text.RegularExpressions;

namespace Nexora.Testes;

public sealed partial class PrivacidadeTestes
{
    static readonly string Codigo = Path.Combine(Raiz(), "src");

    static readonly HashSet<string> EnderecosPermitidos =
    [
        "github", "github.com", "api.github.com", "cli.github.com", "github.blog",
        "claude.ai", "code.claude.com",
        "nodejs.org", "git-scm.com", "dotnet.microsoft.com",
        "nexora.suiciniv.dev",
        "hnrss.org", "news.ycombinator.com",
        "api.instabox.sh", "api.freestyle.sh",
        "localhost", "site.com", "schemas.microsoft.com",
    ];

    static string Raiz()
    {
        for (var pasta = new DirectoryInfo(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
            if (Directory.Exists(Path.Combine(pasta.FullName, "src", "Nexora")) && Directory.Exists(Path.Combine(pasta.FullName, "tests")))
                return pasta.FullName;
        throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
    }

    static IEnumerable<(string Arquivo, int Numero, string Texto)> Linhas(params string[] padroes) =>
        padroes.SelectMany(p => Directory.EnumerateFiles(Codigo, p, SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadLines(f).Select((texto, i) => (Path.GetRelativePath(Codigo, f).Replace('\\', '/'), i + 1, texto)));

    [GeneratedRegex(@"(?i)posthog|sentry|mixpanel|amplitude\.com|segment\.(io|com)|applicationinsights|appcenter|datadog|newrelic|bugsnag|honeycomb|axiom\.co|google-analytics|googletagmanager|plausible\.io|umami|opentelemetry|telemetr")]
    private static partial Regex Telemetria();

    [GeneratedRegex(@"\.credentials\.json|auth\.json|Claude Code-credentials")]
    private static partial Regex ArquivoDeLogin();

    [GeneratedRegex(@"(?i)accountUuid|oauthAccount|""user_id""|emailAddress")]
    private static partial Regex Identidade();

    [GeneratedRegex(@"https?://([a-zA-Z0-9.-]+)")]
    private static partial Regex Endereco();

    [Fact]
    public void Nenhuma_biblioteca_ou_servico_de_telemetria()
    {
        var achados = Linhas("*.cs", "*.axaml", "*.csproj").Where(l => Telemetria().IsMatch(l.Texto)).Select(l => $"{l.Arquivo}:{l.Numero}: {l.Texto.Trim()}").ToList();
        Assert.True(achados.Count == 0, "O Nexora não coleta telemetria (PRIVACY.md):\n" + string.Join('\n', achados));
    }

    [Fact]
    public void Arquivos_de_login_dos_agentes_so_sao_conferidos_nunca_lidos()
    {
        var achados = Linhas("*.cs")
            .Where(l => ArquivoDeLogin().IsMatch(l.Texto) && !l.Texto.Contains("File.Exists(", StringComparison.Ordinal) && !l.Texto.Contains("TemNoChaveiro(", StringComparison.Ordinal))
            .Select(l => $"{l.Arquivo}:{l.Numero}: {l.Texto.Trim()}")
            .ToList();
        Assert.True(achados.Count == 0, "Arquivo de login de agente só pode aparecer num File.Exists (para saber se há login), nunca ser aberto:\n" + string.Join('\n', achados));
    }

    [Fact]
    public void Nenhum_dado_de_identidade_das_contas_dos_agentes()
    {
        var achados = Linhas("*.cs").Where(l => Identidade().IsMatch(l.Texto)).Select(l => $"{l.Arquivo}:{l.Numero}: {l.Texto.Trim()}").ToList();
        Assert.True(achados.Count == 0, "O Nexora não lê nem guarda identificadores das contas do Claude ou da OpenAI:\n" + string.Join('\n', achados));
    }

    [Fact]
    public void O_claude_json_so_e_lido_para_os_mcps_desligados_do_projeto()
    {
        var arquivos = Linhas("*.cs").Where(l => l.Texto.Contains("\".claude.json\"", StringComparison.Ordinal)).Select(l => l.Arquivo).Distinct().ToList();
        Assert.Equal(["Nexora.Core/Mcp/ServidoresDoProjeto.cs"], arquivos);
    }

    [Fact]
    public void Enderecos_da_internet_estao_na_lista_aprovada()
    {
        var novos = Linhas("*.cs", "*.axaml")
            .SelectMany(l => Endereco().Matches(l.Texto).Select(m => (l.Arquivo, l.Numero, Host: m.Groups[1].Value.TrimEnd('.').ToLowerInvariant())))
            .Where(e => !EnderecosPermitidos.Contains(e.Host))
            .Select(e => $"{e.Arquivo}:{e.Numero}: {e.Host}")
            .ToList();
        Assert.True(novos.Count == 0, "Endereço novo no código. Se for necessário, ponha em PRIVACY.md e na lista deste teste:\n" + string.Join('\n', novos));
    }
}
