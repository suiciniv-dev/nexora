using System.Diagnostics;
using System.Text.Json;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.Core.Historico;

public sealed record SessaoAtiva(int Pid, string SessaoId, string? Pasta, string? Nome, string? Origem, bool Ocupada, DateTimeOffset? AtualizadaEm,
    bool Esperando = false, DateTimeOffset? EstadoDesde = null)
{
    public bool Encerrada => Pid <= 0;

    public string OrigemLegivel => Origem switch
    {
        "claude-vscode" => "VS Code",
        "cli" => Idioma.T("terminal"),
        null or "" => Idioma.T("outro lugar"),
        var o when o.StartsWith("sdk") => Idioma.T("outro app"),
        var o => o,
    };
}

public static class SessoesAtivas
{
    public static string PastaPadrao { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "sessions");

    public static IReadOnlyList<SessaoAtiva> Listar(string? pasta = null, Func<int, bool>? vivo = null)
    {
        pasta ??= PastaPadrao;
        vivo ??= ProcessoVivo;
        if (!Directory.Exists(pasta)) return [];
        var lista = new List<SessaoAtiva>();
        foreach (var arquivo in Directory.EnumerateFiles(pasta, "*.json"))
            if (Ler(arquivo) is { } sessao && vivo(sessao.Pid)) lista.Add(sessao);
        return lista;
    }

    public static SessaoAtiva? Ler(string arquivo)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(arquivo));
            var o = doc.RootElement;
            if (!o.TryGetProperty("pid", out var pid) || !pid.TryGetInt32(out var numero)) return null;
            if (Protocolo.Texto(o, "sessionId") is not { Length: > 0 } id) return null;
            var estado = Protocolo.Texto(o, "status");
            return new SessaoAtiva(numero, id, Protocolo.Texto(o, "cwd"), Protocolo.Texto(o, "name"), Protocolo.Texto(o, "entrypoint"),
                estado == "busy", Momento(o, "updatedAt"), estado == "waiting", Momento(o, "statusUpdatedAt"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static IReadOnlyList<SessaoAtiva> Recentes(TimeSpan janela, Func<string, bool> ignorar, string? raiz = null)
    {
        raiz ??= ConversasSalvas.PastaPadrao;
        if (!Directory.Exists(raiz)) return [];
        var limite = DateTime.UtcNow - janela;
        var lista = new List<SessaoAtiva>();
        try
        {
            foreach (var pasta in new DirectoryInfo(raiz).EnumerateDirectories())
            {
                foreach (var arquivo in pasta.EnumerateFiles("*.jsonl"))
                {
                    if (arquivo.LastWriteTimeUtc < limite) continue;
                    var id = Path.GetFileNameWithoutExtension(arquivo.Name);
                    if (ignorar(id) || Origem(arquivo) is not { } origem) continue;
                    lista.Add(new SessaoAtiva(0, id, origem.Pasta, null, origem.Entrada, false, new DateTimeOffset(arquivo.LastWriteTimeUtc)));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return lista;
    }

    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string? Pasta, string? Entrada)> Origens = new(StringComparer.OrdinalIgnoreCase);

    static (string? Pasta, string? Entrada)? Origem(FileInfo arquivo)
    {
        if (Origens.TryGetValue(arquivo.FullName, out var conhecida)) return conhecida;
        try
        {
            using var fluxo = new FileStream(arquivo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var leitor = new StreamReader(fluxo, System.Text.Encoding.UTF8);
            for (var n = 0; n < 60 && leitor.ReadLine() is { } linha; n++)
            {
                if (!linha.Contains("\"entrypoint\"", StringComparison.Ordinal)) continue;
                using var doc = JsonDocument.Parse(linha);
                var o = doc.RootElement;
                if (o.ValueKind != JsonValueKind.Object) continue;
                if (o.TryGetProperty("isSidechain", out var lado) && lado.ValueKind == JsonValueKind.True) return null;
                var origem = (Protocolo.Texto(o, "cwd"), Protocolo.Texto(o, "entrypoint"));
                Origens[arquivo.FullName] = origem;
                return origem;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
        return null;
    }

    static DateTimeOffset? Momento(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var valor) && valor.TryGetInt64(out var ms) ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null;

    static bool ProcessoVivo(int pid)
    {
        try
        {
            using var processo = Process.GetProcessById(pid);
            return !processo.HasExited && processo.ProcessName.Contains("claude", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
