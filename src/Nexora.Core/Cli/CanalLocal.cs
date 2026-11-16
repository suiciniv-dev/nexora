using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Nexora.Core.Cli;

public static class CanalLocal
{
    public const int Versao = 1;
    public const string VariavelDoCanal = "NEXORA_CANAL";

    public static string Nome(string pastaDeDados)
    {
        var chave = Environment.UserName + "|" + Path.GetFullPath(pastaDeDados).TrimEnd('\\', '/').ToLowerInvariant();
        return "nexora-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chave)))[..16].ToLowerInvariant();
    }

    public static async Task<JsonObject?> PedirAsync(string canal, JsonObject pedido, TimeSpan esperaParaConectar, CancellationToken cancelar = default)
    {
        await using var cano = new NamedPipeClientStream(".", canal, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await cano.ConnectAsync(esperaParaConectar, cancelar).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
        pedido["v"] = Versao;
        var escritor = new StreamWriter(cano, new UTF8Encoding(false)) { AutoFlush = true };
        await escritor.WriteLineAsync(pedido.ToJsonString().AsMemory(), cancelar).ConfigureAwait(false);
        var linha = await new StreamReader(cano, Encoding.UTF8).ReadLineAsync(cancelar).ConfigureAwait(false);
        return linha is null ? null : JsonNode.Parse(linha) as JsonObject;
    }
}

public delegate Task<JsonObject> AtendenteDoCanal(JsonObject pedido, CancellationToken cancelar);

public sealed class ServidorDoCanal : IDisposable
{
    readonly string _nome;
    readonly AtendenteDoCanal _atender;
    readonly CancellationTokenSource _fim = new();

    public ServidorDoCanal(string nome, AtendenteDoCanal atender)
    {
        _nome = nome;
        _atender = atender;
        _ = OuvirAsync();
    }

    public string Nome => _nome;

    async Task OuvirAsync()
    {
        while (!_fim.IsCancellationRequested)
        {
            NamedPipeServerStream cano;
            try
            {
                cano = new NamedPipeServerStream(_nome, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await cano.WaitForConnectionAsync(_fim.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                try
                {
                    await Task.Delay(1000, _fim.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                continue;
            }
            _ = AtenderAsync(cano);
        }
    }

    async Task AtenderAsync(NamedPipeServerStream cano)
    {
        await using (cano)
        {
            try
            {
                var linha = await new StreamReader(cano, Encoding.UTF8).ReadLineAsync(_fim.Token).ConfigureAwait(false);
                JsonObject resposta;
                try
                {
                    resposta = linha is not null && JsonNode.Parse(linha) is JsonObject pedido
                        ? pedido["v"]?.GetValue<int>() == CanalLocal.Versao ? await _atender(pedido, _fim.Token).ConfigureAwait(false) : Falha("Versão do pedido diferente da do Nexora aberto. Atualize os dois.")
                        : Falha("Pedido inválido.");
                }
                catch (System.Text.Json.JsonException)
                {
                    resposta = Falha("Pedido inválido.");
                }
                var escritor = new StreamWriter(cano, new UTF8Encoding(false)) { AutoFlush = true };
                await escritor.WriteLineAsync(resposta.ToJsonString().AsMemory(), _fim.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
            {
            }
        }
    }

    public static JsonObject Falha(string erro, int codigo = CodigosDaCli.Erro) => new() { ["ok"] = false, ["erro"] = erro, ["codigo"] = codigo };

    public static JsonObject Ok(JsonObject? dados = null)
    {
        var o = dados ?? [];
        o["ok"] = true;
        return o;
    }

    public void Dispose()
    {
        _fim.Cancel();
        _fim.Dispose();
    }
}

public static class CodigosDaCli
{
    public const int Ok = 0;
    public const int Erro = 1;
    public const int PrecisaDeVoce = 2;
    public const int NexoraFechado = 3;
    public const int Uso = 64;
}
