using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Nexora.Servicos;

public sealed record VersaoNova(Version Versao, string Tag, string Notas, IReadOnlyDictionary<string, string> Arquivos)
{
    public string Rotulo => Versao.ToString(3);
}

public static class Atualizacoes
{
    const string Ultima = "https://api.github.com/repos/suiciniv-dev/nexora-releases/releases/latest";
    public const string Pagina = "https://nexora.suiciniv.dev/#baixar";

    public static Version Atual => typeof(Atualizacoes).Assembly.GetName().Version is { } v ? new Version(v.Major, v.Minor, Math.Max(0, v.Build)) : new Version(0, 0, 0);

    static HttpClient Cliente(TimeSpan limite)
    {
        var http = new HttpClient { Timeout = limite };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Nexora/{Atual.ToString(3)}");
        return http;
    }

    public static async Task<VersaoNova?> ProcurarAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = Cliente(TimeSpan.FromSeconds(20));
            var json = await http.GetStringAsync(Ultima, ct);
            return Ler(json, Atual);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    public static VersaoNova? Ler(string json, Version atual)
    {
        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement;
        var tag = raiz.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var versao) || versao <= atual) return null;
        var arquivos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (raiz.TryGetProperty("assets", out var lista) && lista.ValueKind == JsonValueKind.Array)
            foreach (var a in lista.EnumerateArray())
                if (a.TryGetProperty("name", out var n) && a.TryGetProperty("browser_download_url", out var u) && n.GetString() is { } nome && u.GetString() is { } url && url.StartsWith("https://", StringComparison.Ordinal))
                    arquivos[nome] = url;
        var notas = raiz.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
        return new VersaoNova(versao, tag, notas, arquivos);
    }

    public static string? ArquivoParaEstePc(VersaoNova v)
    {
        var rotulo = v.Rotulo;
        string nome;
        if (OperatingSystem.IsMacOS()) nome = $"Nexora-Mac-{(RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "AppleSilicon" : "Intel")}-{rotulo}.dmg";
        else if (Instalado()) nome = $"Nexora-Setup-{rotulo}.exe";
        else nome = $"Nexora-Portatil-{rotulo}.zip";
        return v.Arquivos.ContainsKey(nome) ? nome : null;
    }

    static bool Instalado() => File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    public static async Task<bool> PrepararAsync(VersaoNova v, Action<double> progresso, CancellationToken ct = default)
    {
        if (ArquivoParaEstePc(v) is not { } nome) return false;
        var pasta = Path.Combine(Path.GetTempPath(), $"nexora-atualizacao-{v.Rotulo}");
        try
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, true);
            Directory.CreateDirectory(pasta);
            var arquivo = Path.Combine(pasta, nome);
            await BaixarAsync(v.Arquivos[nome], arquivo, progresso, ct);
            var pid = Environment.ProcessId;
            if (OperatingSystem.IsMacOS()) return PrepararMac(arquivo, pasta, pid);
            if (nome.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return PrepararSetup(arquivo, pasta, pid);
            return PrepararPortatil(arquivo, pasta, pid);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or InvalidDataException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    static async Task BaixarAsync(string url, string destino, Action<double> progresso, CancellationToken ct)
    {
        using var http = Cliente(TimeSpan.FromMinutes(15));
        using var resposta = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resposta.EnsureSuccessStatusCode();
        var total = resposta.Content.Headers.ContentLength ?? 0;
        await using var origem = await resposta.Content.ReadAsStreamAsync(ct);
        await using var saida = File.Create(destino);
        var buffer = new byte[81920];
        long lidos = 0;
        int n;
        var ultimo = DateTime.MinValue;
        while ((n = await origem.ReadAsync(buffer, ct)) > 0)
        {
            await saida.WriteAsync(buffer.AsMemory(0, n), ct);
            lidos += n;
            if (total > 0 && (DateTime.UtcNow - ultimo).TotalMilliseconds > 200)
            {
                ultimo = DateTime.UtcNow;
                progresso((double)lidos / total);
            }
        }
        progresso(1);
    }

    static string Ps(string texto) => "'" + texto.Replace("'", "''") + "'";

    static bool RodarPowerShell(string pasta, string script)
    {
        var arquivo = Path.Combine(pasta, "atualizar.ps1");
        File.WriteAllText(arquivo, script, new System.Text.UTF8Encoding(true));
        var info = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = pasta };
        foreach (var a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden", "-File", arquivo }) info.ArgumentList.Add(a);
        Sistema.Abrir(info)?.Dispose();
        return true;
    }

    public static bool ReabrirDepoisDeFechar()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "nexora-reinicio");
        Directory.CreateDirectory(pasta);
        var (alvo, argumentos) = IdentidadeJanela.Abertura();
        var abrir = $"Start-Process -FilePath {Ps(alvo)}" + (argumentos.Length > 0 ? $" -ArgumentList {Ps(argumentos)}" : "");
        return RodarPowerShell(pasta, EsperarSair(Environment.ProcessId) + abrir + "\n");
    }

    static string EsperarSair(int pid) => $"while (Get-Process -Id {pid} -ErrorAction SilentlyContinue) {{ Start-Sleep -Milliseconds 400 }}\n";

    static bool PrepararSetup(string setup, string pasta, int pid)
    {
        var exe = Path.Combine(AppContext.BaseDirectory, "Nexora.exe");
        return RodarPowerShell(pasta, EsperarSair(pid)
            + $"Start-Process -FilePath {Ps(setup)} -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CLOSEAPPLICATIONS' -Wait\n"
            + $"Start-Process -FilePath {Ps(exe)}\n");
    }

    static bool PrepararPortatil(string zip, string pasta, int pid)
    {
        var extraido = Path.Combine(pasta, "portatil");
        ZipFile.ExtractToDirectory(zip, extraido, overwriteFiles: true);
        var instalar = Directory.EnumerateFiles(extraido, "instalar.ps1", SearchOption.AllDirectories).FirstOrDefault();
        if (instalar is null) return false;
        var destino = AppContext.BaseDirectory.TrimEnd('\\', '/');
        return RodarPowerShell(pasta, EsperarSair(pid)
            + $"& {Ps(instalar)} -Destino {Ps(destino)}\n"
            + $"Start-Process -FilePath explorer.exe -ArgumentList {Ps("\"" + IdentidadeJanela.AtalhoDoMenu + "\"")}\n");
    }

    static bool PrepararMac(string dmg, string pasta, int pid)
    {
        var app = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", ".."));
        if (!app.EndsWith(".app", StringComparison.OrdinalIgnoreCase)) return false;
        var montagem = Path.Combine(pasta, "dmg");
        var script = Path.Combine(pasta, "atualizar.sh");
        File.WriteAllText(script, $"""
            #!/bin/bash
            while kill -0 {pid} 2>/dev/null; do sleep 0.4; done
            mkdir -p {Sistema.Aspas(montagem)}
            hdiutil attach {Sistema.Aspas(dmg)} -nobrowse -quiet -mountpoint {Sistema.Aspas(montagem)} || exit 1
            rm -rf {Sistema.Aspas(app)}
            ditto {Sistema.Aspas(Path.Combine(montagem, "Nexora.app"))} {Sistema.Aspas(app)}
            hdiutil detach {Sistema.Aspas(montagem)} -quiet
            xattr -dr com.apple.quarantine {Sistema.Aspas(app)} 2>/dev/null
            open {Sistema.Aspas(app)}
            """.Replace("\r\n", "\n"));
        var info = new ProcessStartInfo("/bin/bash") { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add(script);
        Sistema.Abrir(info)?.Dispose();
        return true;
    }
}
