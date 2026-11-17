using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nexora.Core.Claude;

namespace Nexora.Core.Historico;

public sealed record TrechoDaTranscricao(IReadOnlyList<EventoClaude> Eventos, long Inicio, int Turnos)
{
    public bool TemAnteriores => Inicio > 0;
}

public static partial class Transcricao
{
    [GeneratedRegex("\"sessionId\":\"[0-9A-Za-z_-]+\"")]
    private static partial Regex IdDaSessao();

    public static string? CopiarAte(string arquivo, long bytes, string novoId)
    {
        if (bytes <= 0 || novoId.Length == 0 || !novoId.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')) return null;
        try
        {
            byte[] dados;
            using (var fluxo = new FileStream(arquivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (fluxo.Length < bytes) return null;
                dados = new byte[bytes];
                fluxo.ReadExactly(dados);
            }
            var corte = Array.LastIndexOf(dados, (byte)'\n') + 1;
            if (corte <= 0) return null;
            var texto = IdDaSessao().Replace(Encoding.UTF8.GetString(dados, 0, corte), $"\"sessionId\":\"{novoId}\"");
            var destino = Path.Combine(Path.GetDirectoryName(arquivo) ?? "", novoId + ".jsonl");
            if (File.Exists(destino)) return null;
            File.WriteAllText(destino, texto, new UTF8Encoding(false));
            return destino;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static IReadOnlyList<EventoClaude> Ler(string arquivo, int maximo = 600) => LerArquivo(arquivo, maximo, LerLinha);

    public static TrechoDaTranscricao LerTurnos(string arquivo, int turnos, long ate = long.MaxValue, int maximoDeEventos = 800) =>
        DeBruto(LerTurnosBrutos(arquivo, turnos, ate, maximoDeEventos));

    public static TrechoDaTranscricao DeBruto(TrechoBruto bruto) =>
        new([.. bruto.Linhas.SelectMany(LerLinha)], bruto.Inicio, bruto.Turnos);

    static bool PodeTerConversa(ReadOnlySpan<byte> linha) =>
        linha.IndexOf("\"type\":\"user\""u8) >= 0 || linha.IndexOf("\"type\":\"assistant\""u8) >= 0 || linha.IndexOf("<task-notification>"u8) >= 0;

    public static TrechoBruto LerTurnosBrutos(string arquivo, int turnos, long ate = long.MaxValue, int maximoDeEventos = 800)
    {
        var linhas = new List<string>();
        var achados = 0;
        var eventos = 0;
        try
        {
            using var fluxo = new FileStream(arquivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var posicao = Math.Min(ate, fluxo.Length);
            var tamanho = 256 * 1024;
            byte[]? sobra = null;
            var sobraN = 0;
            try
            {
                while (posicao > 0)
                {
                    var ler = (int)Math.Min(tamanho, posicao);
                    posicao -= ler;
                    var total = ler + sobraN;
                    var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(total);
                    fluxo.Seek(posicao, SeekOrigin.Begin);
                    fluxo.ReadExactly(buffer, 0, ler);
                    if (sobra is not null)
                    {
                        Buffer.BlockCopy(sobra, 0, buffer, ler, sobraN);
                        System.Buffers.ArrayPool<byte>.Shared.Return(sobra);
                    }
                    sobra = buffer;
                    sobraN = total;
                    var corte = posicao > 0 ? Array.IndexOf(buffer, (byte)'\n', 0, total) + 1 : 0;
                    if (posicao > 0 && corte == 0)
                    {
                        tamanho *= 2;
                        continue;
                    }
                    var fimDaLinha = total;
                    for (var i = total - 1; i >= corte - 1; i--)
                    {
                        if (i >= 0 && buffer[i] != (byte)'\n') continue;
                        var inicio = i + 1;
                        var comprimento = fimDaLinha - inicio;
                        fimDaLinha = i;
                        if (comprimento <= 0 || !PodeTerConversa(buffer.AsSpan(inicio, comprimento))) continue;
                        var linha = Encoding.UTF8.GetString(buffer, inicio, comprimento).TrimEnd('\r');
                        var lidos = LerLinha(linha);
                        if (lidos.Length == 0) continue;
                        linhas.Add(linha);
                        eventos += lidos.Length;
                        if (!lidos.Any(e => e is MensagemDoUsuario)) continue;
                        achados++;
                        if (achados >= turnos || eventos >= maximoDeEventos)
                            return new TrechoBruto(Invertidas(linhas), posicao + inicio, achados);
                    }
                    sobraN = corte;
                }
            }
            finally
            {
                if (sobra is not null) System.Buffers.ArrayPool<byte>.Shared.Return(sobra);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return new TrechoBruto(Invertidas(linhas), 0, achados);
    }

    static List<string> Invertidas(List<string> linhasDoFim)
    {
        linhasDoFim.Reverse();
        return linhasDoFim;
    }

    public static IReadOnlyList<EventoClaude> LerSubagente(string arquivo, string agenteId, int maximo = 300)
    {
        if (agenteId.Length == 0 || !agenteId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')) return [];
        var caminho = Path.Combine(Path.GetDirectoryName(arquivo) ?? "", Path.GetFileNameWithoutExtension(arquivo), "subagents", $"agent-{agenteId}.jsonl");
        return File.Exists(caminho) ? LerArquivo(caminho, maximo, LerLinhaDeSubagente) : [];
    }

    static IReadOnlyList<EventoClaude> LerArquivo(string arquivo, int maximo, Func<string, EventoClaude[]> ler)
    {
        var eventos = new List<EventoClaude>();
        try
        {
            using var fluxo = new FileStream(arquivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var leitor = new StreamReader(fluxo, Encoding.UTF8);
            while (leitor.ReadLine() is { } linha)
            {
                if (!linha.Contains("\"type\":\"user\"") && !linha.Contains("\"type\":\"assistant\"") && !linha.Contains("<task-notification>")) continue;
                eventos.AddRange(ler(linha));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return eventos.Count > maximo ? eventos[^maximo..] : eventos;
    }

    static EventoClaude[] LerLinhaDeSubagente(string linha)
    {
        try
        {
            using var doc = JsonDocument.Parse(linha);
            var o = doc.RootElement;
            if (o.ValueKind != JsonValueKind.Object || o.TryGetProperty("isMeta", out var meta) && meta.ValueKind == JsonValueKind.True) return [];
            var mensagem = Protocolo.Objeto(o, "message");
            return Protocolo.Texto(o, "type") switch
            {
                "user" => Protocolo.LerBlocosUsuario(mensagem, incluirTexto: false, Protocolo.Objeto(o, "toolUseResult")),
                "assistant" => Protocolo.LerBlocosAssistente(mensagem),
                _ => [],
            };
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static IReadOnlyList<EventoClaude> LerFim(string arquivo, int maximo = 40, int bytes = 256 * 1024, int limite = 16 * 1024 * 1024)
    {
        while (true)
        {
            List<string> linhas;
            long tamanho;
            try
            {
                tamanho = new FileInfo(arquivo).Length;
                linhas = ConversasSalvas.UltimasLinhas(arquivo, bytes);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return [];
            }
            var eventos = linhas
                .Where(l => l.Contains("\"type\":\"user\"") || l.Contains("\"type\":\"assistant\""))
                .SelectMany(LerLinha)
                .ToList();
            if (eventos.Count >= maximo || bytes >= tamanho || bytes >= limite) return eventos.Count > maximo ? eventos[^maximo..] : eventos;
            bytes = (int)Math.Min(limite, (long)bytes * 4);
        }
    }

    internal static DateTimeOffset? Quando(JsonElement o) =>
        Protocolo.Texto(o, "timestamp") is { } t && DateTimeOffset.TryParse(t, out var quando) ? quando : null;

    public static EventoClaude[] LerLinha(string linha)
    {
        try
        {
            using var doc = JsonDocument.Parse(linha);
            return LerLinha(doc.RootElement);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    internal static EventoClaude[] LerLinha(JsonElement o)
    {
        if (o.ValueKind != JsonValueKind.Object) return [];
        if (o.TryGetProperty("isSidechain", out var lado) && lado.ValueKind == JsonValueKind.True) return [];
        if (o.TryGetProperty("isMeta", out var meta) && meta.ValueKind == JsonValueKind.True) return [];
        var mensagem = Protocolo.Objeto(o, "message");
        return Protocolo.Texto(o, "type") switch
        {
            "user" => Protocolo.LerBlocosUsuario(mensagem, incluirTexto: true, Protocolo.Objeto(o, "toolUseResult")),
            "assistant" => [.. Protocolo.LerBlocosAssistente(mensagem), .. Protocolo.LerUso(mensagem, Quando(o))],
            "attachment" => Protocolo.AvisosDeTarefa(Protocolo.Texto(Protocolo.Objeto(o, "attachment"), "prompt")),
            _ => [],
        };
    }
}
