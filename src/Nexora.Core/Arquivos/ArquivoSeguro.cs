using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Nexora.Core.Arquivos;

public enum OrigemDaLeitura
{
    Novo,
    Principal,
    Copia,
    Perdido,
    Ilegivel,
}

public sealed record LeituraSegura<T>(T Valor, OrigemDaLeitura Origem, string? Guardado)
{
    public bool Recuperado => Origem is OrigemDaLeitura.Copia or OrigemDaLeitura.Perdido;
    public bool PodeGravar => Origem != OrigemDaLeitura.Ilegivel;
}

public sealed class ArquivoIlegivelException(string caminho, string motivo)
    : IOException(Texto.Idioma.F("Não mexi em {0} porque ele não pôde ser lido: {1}", caminho, motivo))
{
    public string Caminho { get; } = caminho;
}

public static class ArquivoSeguro
{
    static readonly ConcurrentDictionary<string, object> Travas = new(StringComparer.OrdinalIgnoreCase);

    public static int Tentativas { get; set; } = 5;
    public static TimeSpan Pausa { get; set; } = TimeSpan.FromMilliseconds(60);

    public static string Copia(string caminho) => Path.GetFullPath(caminho) + ".bak";

    static object Trava(string completo) => Travas.GetOrAdd(completo, _ => new object());

    public static void Gravar(string caminho, string texto, Encoding? codificacao = null, bool manterCopia = false)
    {
        var enc = codificacao ?? new UTF8Encoding(false);
        Gravar(caminho, [.. enc.GetPreamble(), .. enc.GetBytes(texto)], manterCopia);
    }

    public static void Gravar(string caminho, byte[] dados, bool manterCopia = false)
    {
        var completo = Path.GetFullPath(caminho);
        lock (Trava(completo))
        {
            if (Path.GetDirectoryName(completo) is { Length: > 0 } pasta) Directory.CreateDirectory(pasta);
            var temporario = completo + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
            try
            {
                using (var fluxo = new FileStream(temporario, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    fluxo.Write(dados);
                    fluxo.Flush(flushToDisk: true);
                }
                Repetir(() =>
                {
                    if (manterCopia && File.Exists(completo)) File.Copy(completo, Copia(completo), overwrite: true);
                    File.Move(temporario, completo, overwrite: true);
                });
            }
            finally
            {
                Apagar(temporario);
            }
        }
    }

    public static LeituraSegura<T> LerJson<T>(string caminho, Func<T> vazio, JsonSerializerOptions? opcoes = null) where T : class =>
        Ler(caminho, vazio, texto => JsonSerializer.Deserialize<T>(texto, opcoes));

    public static LeituraSegura<T> Ler<T>(string caminho, Func<T> vazio, Func<string, T?> interpretar) where T : class
    {
        var completo = Path.GetFullPath(caminho);
        lock (Trava(completo))
        {
            if (!File.Exists(completo)) return new(vazio(), OrigemDaLeitura.Novo, null);
            string texto;
            try
            {
                texto = LerTexto(completo);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new(vazio(), OrigemDaLeitura.Ilegivel, null);
            }
            if (Interpretar(texto, interpretar) is { } principal) return new(principal, OrigemDaLeitura.Principal, null);
            var guardado = Guardar(completo);
            var copia = Copia(completo);
            if (File.Exists(copia))
            {
                try
                {
                    if (Interpretar(LerTexto(copia), interpretar) is { } salvo) return new(salvo, OrigemDaLeitura.Copia, guardado);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
            return new(vazio(), OrigemDaLeitura.Perdido, guardado);
        }
    }

    static T? Interpretar<T>(string texto, Func<string, T?> interpretar) where T : class
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        try
        {
            return interpretar(texto);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or FormatException or InvalidOperationException)
        {
            return null;
        }
    }

    static string LerTexto(string arquivo)
    {
        string texto = "";
        Repetir(() => texto = File.ReadAllText(arquivo));
        return texto;
    }

    static string? Guardar(string completo)
    {
        var destino = completo + ".corrompido-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            File.Copy(completo, destino, overwrite: true);
            return destino;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    static void Repetir(Action acao)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                acao();
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && tentativa < Tentativas && ex is not FileNotFoundException and not DirectoryNotFoundException)
            {
                Thread.Sleep(Pausa);
            }
        }
    }

    static void Apagar(string arquivo)
    {
        try
        {
            if (File.Exists(arquivo)) File.Delete(arquivo);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
