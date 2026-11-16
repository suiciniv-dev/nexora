using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.Core.Historico;

public sealed record ConversaSalva(
    string SessaoId,
    string Arquivo,
    string Titulo,
    string? Pasta,
    string? Branch,
    DateTime Atualizada,
    string? PrimeiraMensagem = null,
    string? UltimaMensagem = null,
    string? Agente = null);

public static class ConversasSalvas
{
    const int Cabeca = 128 * 1024;
    const int Cauda = 256 * 1024;
    const int MaiorLinha = 64 * 1024;
    static readonly ConcurrentDictionary<string, string> Primeiras = new(StringComparer.OrdinalIgnoreCase);

    public static string PastaPadrao =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    public static IReadOnlyList<ConversaSalva> Listar(string? raiz = null, int maximo = 300)
    {
        raiz ??= PastaPadrao;
        if (!Directory.Exists(raiz)) return [];
        var arquivos = new DirectoryInfo(raiz).EnumerateDirectories()
            .SelectMany(d => d.EnumerateFiles("*.jsonl"))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Take(maximo);
        var lista = new List<ConversaSalva>();
        foreach (var arquivo in arquivos)
            if (Resumir(arquivo) is { } conversa) lista.Add(conversa);
        return lista;
    }

    public static string? ArquivoDe(string sessaoId, string? pasta, string? raiz = null)
    {
        raiz ??= PastaPadrao;
        if (pasta is not null)
        {
            var provavel = Path.Combine(raiz, string.Concat(pasta.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')), sessaoId + ".jsonl");
            if (File.Exists(provavel)) return provavel;
        }
        if (!Directory.Exists(raiz)) return null;
        return Directory.EnumerateDirectories(raiz).Select(d => Path.Combine(d, sessaoId + ".jsonl")).FirstOrDefault(File.Exists);
    }

    public static ConversaSalva? Resumir(FileInfo arquivo)
    {
        string? pasta = null, branch = null, tituloIa = null, tituloProprio = null, ultimoPrompt = null, primeiroPrompt = null;
        string? primeiraMensagem = null, ultimaMensagem = null;
        DateTimeOffset? ultimaVez = null;
        var temConversa = false;
        try
        {
            foreach (var linha in Linhas(arquivo))
            {
                if (linha.Length > MaiorLinha) continue;
                using var doc = JsonDocument.Parse(linha);
                var o = doc.RootElement;
                if (o.ValueKind != JsonValueKind.Object) continue;
                var tipo = Protocolo.Texto(o, "type");
                switch (tipo)
                {
                    case "ai-title":
                        tituloIa = Protocolo.Texto(o, "aiTitle") ?? tituloIa;
                        continue;
                    case "custom-title":
                        tituloProprio = Protocolo.Texto(o, "customTitle") ?? tituloProprio;
                        continue;
                    case "summary":
                        tituloIa ??= Protocolo.Texto(o, "summary");
                        continue;
                    case "last-prompt":
                        ultimoPrompt = Protocolo.Texto(o, "lastPrompt") ?? ultimoPrompt;
                        continue;
                }
                pasta ??= Protocolo.Texto(o, "cwd");
                branch = Protocolo.Texto(o, "gitBranch") ?? branch;
                if (tipo is not ("user" or "assistant") || o.TryGetProperty("isSidechain", out var lado) && lado.ValueKind == JsonValueKind.True)
                    continue;
                temConversa = true;
                if (DateTimeOffset.TryParse(Protocolo.Texto(o, "timestamp"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var quando))
                    ultimaVez = quando;
                if (Protocolo.Texto(o, "uuid") is { Length: > 0 } uuid)
                {
                    primeiraMensagem ??= uuid;
                    ultimaMensagem = uuid;
                }
                if (tipo == "user" && primeiroPrompt is null)
                    primeiroPrompt = Protocolo.LerBlocosUsuario(Protocolo.Objeto(o, "message"), incluirTexto: true)
                        .OfType<MensagemDoUsuario>().FirstOrDefault()?.Texto;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
        if (!temConversa && ultimoPrompt is null) return null;
        var titulo = tituloProprio ?? tituloIa ?? Resumos.UmaLinha(primeiroPrompt ?? ultimoPrompt ?? "", 80);
        return new ConversaSalva(
            Path.GetFileNameWithoutExtension(arquivo.Name),
            arquivo.FullName,
            string.IsNullOrWhiteSpace(titulo) ? Idioma.T("(sem título)") : titulo,
            pasta,
            branch is "HEAD" or "" ? null : branch,
            ultimaVez?.LocalDateTime ?? arquivo.LastWriteTime,
            PrimeiraMensagemDe(arquivo) ?? primeiraMensagem,
            ultimaMensagem);
    }

    static string? PrimeiraMensagemDe(FileInfo arquivo)
    {
        if (Primeiras.TryGetValue(arquivo.FullName, out var conhecida)) return conhecida;
        try
        {
            using var fluxo = new FileStream(arquivo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var leitor = new StreamReader(fluxo, Encoding.UTF8);
            for (var n = 0; n < 300 && leitor.ReadLine() is { } linha; n++)
            {
                if (!linha.Contains("\"type\":\"user\"") && !linha.Contains("\"type\":\"assistant\"")) continue;
                using var doc = JsonDocument.Parse(linha);
                var o = doc.RootElement;
                if (Protocolo.Texto(o, "type") is not ("user" or "assistant")) continue;
                if (o.TryGetProperty("isSidechain", out var lado) && lado.ValueKind == JsonValueKind.True) continue;
                if (Protocolo.Texto(o, "uuid") is not { Length: > 0 } uuid) continue;
                Primeiras[arquivo.FullName] = uuid;
                return uuid;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
        return null;
    }

    static IEnumerable<string> Linhas(FileInfo arquivo)
    {
        using var fluxo = new FileStream(arquivo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (fluxo.Length <= Cabeca + Cauda)
        {
            using var leitor = new StreamReader(fluxo, Encoding.UTF8);
            while (leitor.ReadLine() is { } linha)
                if (linha.Length > 0) yield return linha;
            yield break;
        }
        foreach (var linha in Trecho(fluxo, 0, Cabeca, cortarInicio: false)) yield return linha;
        foreach (var linha in Trecho(fluxo, fluxo.Length - Cauda, Cauda, cortarInicio: true)) yield return linha;
    }

    public static string? UltimoModo(string arquivo)
    {
        try
        {
            string? modo = null;
            foreach (var linha in UltimasLinhas(arquivo, Cauda))
            {
                if (linha.Length > MaiorLinha || !linha.Contains("\"permissionMode\"")) continue;
                using var doc = JsonDocument.Parse(linha);
                if (Protocolo.Texto(doc.RootElement, "type") == "user" && Protocolo.Texto(doc.RootElement, "permissionMode") is { Length: > 0 } valor) modo = valor;
            }
            return modo;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    internal static List<string> UltimasLinhas(string arquivo, int bytes)
    {
        using var fluxo = new FileStream(arquivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var inicio = Math.Max(0, fluxo.Length - bytes);
        return Trecho(fluxo, inicio, (int)(fluxo.Length - inicio), cortarInicio: inicio > 0);
    }

    static List<string> Trecho(FileStream fluxo, long inicio, int tamanho, bool cortarInicio)
    {
        var buffer = new byte[tamanho];
        fluxo.Seek(inicio, SeekOrigin.Begin);
        var lidos = fluxo.ReadAtLeast(buffer, tamanho, throwOnEndOfStream: false);
        var linhas = Encoding.UTF8.GetString(buffer, 0, lidos).Split('\n').ToList();
        if (cortarInicio && linhas.Count > 0) linhas.RemoveAt(0);
        if (!cortarInicio && linhas.Count > 0) linhas.RemoveAt(linhas.Count - 1);
        linhas.RemoveAll(l => l.Length == 0);
        return linhas;
    }
}
