using System.Text.Json;
using System.Text.RegularExpressions;
using Nexora.Core.Claude;

namespace Nexora.Core.Executar;

public sealed record Alvo(
    string Nome,
    string Detalhe,
    string Pasta,
    string Comando,
    IReadOnlyList<string> Argumentos,
    IReadOnlyDictionary<string, string> Variaveis,
    string? Url = null,
    int? Porta = null,
    string? AbrirFormato = null,
    int? PortaLocal = null)
{
    public string Chave => $"{Pasta}|{Nome}";
    public int? PortaSugerida => PortaLocal ?? Porta;

    public IReadOnlyList<string> ArgumentosCom(int? porta) =>
        porta is { } p && p != Porta && Url is { } url && Comando == "dotnet"
            ? [.. Argumentos, "--urls", Alvos.TrocarPorta(url, p)]
            : Argumentos;

    public IReadOnlyDictionary<string, string> VariaveisCom(int? porta) =>
        porta is { } p && Comando != "dotnet" ? new Dictionary<string, string>(Variaveis) { ["PORT"] = p.ToString() } : Variaveis;

    public string? EnderecoCom(int? porta, string? ouvindo)
    {
        var final = porta ?? Porta;
        if (AbrirFormato is { } formato && (final ?? PortaDe(ouvindo)) is { } p) return formato.Replace("%s", p.ToString());
        if (Url is { } url && final is { } q) return Alvos.TrocarPorta(url, q);
        return ouvindo;
    }

    static int? PortaDe(string? url) => url is not null && Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Port : null;
}

public static partial class Alvos
{
    static readonly JsonDocumentOptions Leitura = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    static readonly HashSet<string> Ignoradas = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj", "node_modules", ".git", ".vs", ".idea", "packages" };

    public static IReadOnlyList<Alvo> Descobrir(IEnumerable<string> raizes)
    {
        var lista = new List<Alvo>();
        foreach (var raiz in raizes.Where(Directory.Exists))
        {
            var doLaunch = DoLaunchJson(raiz).ToList();
            lista.AddRange(doLaunch);
            var cobertos = doLaunch.Select(a => (a.Pasta.ToLowerInvariant(), a.Detalhe)).ToHashSet();
            foreach (var alvo in DosLaunchSettings(raiz))
                if (!cobertos.Contains((alvo.Pasta.ToLowerInvariant(), alvo.Detalhe))) lista.Add(alvo);
            lista.AddRange(DosScripts(raiz));
        }
        return lista;
    }

    static IEnumerable<Alvo> DoLaunchJson(string raiz)
    {
        if (Ler(Path.Combine(raiz, ".vscode", "launch.json")) is not { } doc) yield break;
        using (doc)
            foreach (var config in Protocolo.Itens(Protocolo.Objeto(doc.RootElement, "configurations")))
            {
                if (Protocolo.Texto(config, "type") != "coreclr" || Protocolo.Texto(config, "request") != "launch") continue;
                var pasta = Expandir(Protocolo.Texto(config, "cwd"), raiz) ?? raiz;
                if (Projeto(pasta) is not { } projeto) continue;
                var perfis = Perfis(pasta);
                var nomePerfil = Protocolo.Texto(config, "launchSettingsProfile") ?? perfis.FirstOrDefault().Nome;
                var perfil = perfis.FirstOrDefault(p => p.Nome == nomePerfil);
                var variaveis = new Dictionary<string, string>(perfil.Variaveis ?? new Dictionary<string, string>());
                foreach (var (chave, valor) in Variaveis(Protocolo.Objeto(config, "env"))) variaveis[chave] = valor;
                var formato = Protocolo.Texto(Protocolo.Objeto(config, "serverReadyAction"), "uriFormat");
                yield return CriarDotnet(Protocolo.Texto(config, "name") ?? Path.GetFileNameWithoutExtension(projeto), pasta, projeto, nomePerfil, perfil.Url, variaveis, formato);
            }
    }

    static IEnumerable<Alvo> DosLaunchSettings(string raiz)
    {
        foreach (var arquivo in Procurar(raiz, "launchSettings.json", 7).Where(a => Path.GetFileName(Path.GetDirectoryName(a)) == "Properties"))
        {
            var pasta = Path.GetDirectoryName(Path.GetDirectoryName(arquivo))!;
            if (Projeto(pasta) is not { } projeto) continue;
            foreach (var perfil in Perfis(pasta))
                yield return CriarDotnet($"{Path.GetFileNameWithoutExtension(projeto)} · {perfil.Nome}", pasta, projeto, perfil.Nome, perfil.Url, perfil.Variaveis ?? new Dictionary<string, string>(), null);
        }
    }

    static IEnumerable<Alvo> DosScripts(string raiz)
    {
        foreach (var arquivo in Procurar(raiz, "package.json", 3))
        {
            if (Ler(arquivo) is not { } doc) continue;
            var pasta = Path.GetDirectoryName(arquivo)!;
            var relativo = Path.GetRelativePath(raiz, pasta);
            using (doc)
                foreach (var script in Protocolo.Objeto(doc.RootElement, "scripts") is { ValueKind: JsonValueKind.Object } s ? s.EnumerateObject().ToList() : [])
                    yield return new Alvo($"npm run {script.Name}", relativo == "." ? Path.GetFileName(pasta) : relativo, pasta,
                        OperatingSystem.IsWindows() ? "cmd.exe" : "npm", OperatingSystem.IsWindows() ? ["/c", "npm", "run", script.Name] : ["run", script.Name], new Dictionary<string, string>());
        }
    }

    static Alvo CriarDotnet(string nome, string pasta, string projeto, string? perfil, string? url, IReadOnlyDictionary<string, string> variaveis, string? formato)
    {
        List<string> args = ["run", "--project", projeto];
        if (perfil is not null) args.AddRange(["--launch-profile", perfil]);
        var porta = url is not null && Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Port : (int?)null;
        return new Alvo(nome, perfil is null ? Path.GetFileName(projeto) : $"{Path.GetFileNameWithoutExtension(projeto)} · {perfil}",
            pasta, "dotnet", args, variaveis, url, porta, formato, PortaLocalDe(pasta, porta));
    }

    static List<(string Nome, string? Url, Dictionary<string, string>? Variaveis)> Perfis(string pasta)
    {
        if (Ler(Path.Combine(pasta, "Properties", "launchSettings.json")) is not { } doc) return [];
        using (doc)
            return Protocolo.Objeto(doc.RootElement, "profiles") is { ValueKind: JsonValueKind.Object } perfis
                ? [.. perfis.EnumerateObject()
                    .Where(p => Protocolo.Texto(p.Value, "commandName") == "Project")
                    .Select(p => (p.Name, (Protocolo.Texto(p.Value, "applicationUrl") ?? "").Split(';').FirstOrDefault(u => u.StartsWith("https")) ?? (Protocolo.Texto(p.Value, "applicationUrl") ?? "").Split(';').FirstOrDefault(u => u.Length > 0),
                        (Dictionary<string, string>?)Variaveis(Protocolo.Objeto(p.Value, "environmentVariables"))))]
                : [];
    }

    public static int? PortaLocalDe(string pasta, int? porta)
    {
        if (Ler(Path.Combine(pasta, "appsettings.Development.json")) is not { } doc) return null;
        using (doc)
        {
            var raiz = doc.RootElement;
            var url = Protocolo.Texto(Protocolo.Objeto(raiz, "App"), "SelfUrl") ?? Protocolo.Texto(raiz, "Urls") ?? Protocolo.Texto(raiz, "urls");
            return url is not null && Uri.TryCreate(url.Split(';')[0], UriKind.Absolute, out var u) && u.Port != porta ? u.Port : null;
        }
    }

    public static string TrocarPorta(string url, int porta) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) ? new UriBuilder(u) { Port = porta }.Uri.ToString().TrimEnd('/') : url;

    static Dictionary<string, string> Variaveis(JsonElement objeto) =>
        objeto.ValueKind == JsonValueKind.Object
            ? objeto.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String).ToDictionary(p => p.Name, p => p.Value.GetString()!)
            : [];

    static string? Expandir(string? caminho, string raiz) =>
        caminho is null ? null : Path.GetFullPath(caminho.Replace("${workspaceFolder}", raiz).Replace('/', Path.DirectorySeparatorChar));

    static string? Projeto(string pasta) =>
        Directory.Exists(pasta) ? Directory.EnumerateFiles(pasta, "*.csproj").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).FirstOrDefault() : null;

    static JsonDocument? Ler(string arquivo)
    {
        try
        {
            return File.Exists(arquivo) ? JsonDocument.Parse(File.ReadAllText(arquivo), Leitura) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    static IEnumerable<string> Procurar(string raiz, string nome, int profundidade)
    {
        var fila = new Queue<(string Pasta, int Nivel)>([(raiz, 0)]);
        while (fila.TryDequeue(out var atual))
        {
            string[] arquivos, pastas;
            try
            {
                arquivos = Directory.GetFiles(atual.Pasta, nome);
                pastas = atual.Nivel < profundidade ? Directory.GetDirectories(atual.Pasta) : [];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            foreach (var arquivo in arquivos) yield return arquivo;
            foreach (var pasta in pastas.Where(p => !Ignoradas.Contains(Path.GetFileName(p))).Order(StringComparer.OrdinalIgnoreCase))
                fila.Enqueue((pasta, atual.Nivel + 1));
        }
    }

    [GeneratedRegex(@"\x1B\[[0-9;?]*[ -/]*[@-~]")]
    public static partial Regex Ansi();

    [GeneratedRegex(@"Now listening on:\s*(\S+)", RegexOptions.IgnoreCase)]
    public static partial Regex Ouvindo();
}
