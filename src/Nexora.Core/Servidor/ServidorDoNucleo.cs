using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nexora.Core.Servidor;

public static class ServidorDoNucleo
{
    public const string Argumento = "--servidor";
    public const int OutroServidor = 2;

    internal static JsonSerializerOptions Json => JsonDoNucleo.Opcoes;

    public static string NomeDoCano(string usuario, string pasta, Guid versao)
    {
        var pastaNormal = Path.GetFullPath(pasta).TrimEnd('\\', '/').ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{usuario.ToLowerInvariant()}|{pastaNormal}|{versao:N}"));
        return "nexora-nucleo-" + Convert.ToHexString(hash, 0, 12).ToLowerInvariant();
    }

    public static async Task<int> RodarAsync(string nome, INucleo nucleo, TimeSpan ocioso, CancellationToken cancelar = default, Func<bool>? ocupado = null)
    {
        NamedPipeServerStream proximo;
        try
        {
            proximo = Abrir(nome, primeiro: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return OutroServidor;
        }
        using var parar = CancellationTokenSource.CreateLinkedTokenSource(cancelar);
        var conectados = 0;
        var sozinhoDesde = Environment.TickCount64;
        var atendeu = false;
        var clientes = new List<Task>();
        var vigia = Vigiar();
        try
        {
            while (true)
            {
                try
                {
                    await proximo.WaitForConnectionAsync(parar.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                var cano = proximo;
                Interlocked.Increment(ref conectados);
                atendeu = true;
                clientes.RemoveAll(t => t.IsCompleted);
                clientes.Add(Task.Run(async () =>
                {
                    try
                    {
                        await AtenderAsync(cano, nucleo, parar.Token).ConfigureAwait(false);
                    }
                    finally
                    {
                        await cano.DisposeAsync().ConfigureAwait(false);
                        Volatile.Write(ref sozinhoDesde, Environment.TickCount64);
                        Interlocked.Decrement(ref conectados);
                    }
                }, CancellationToken.None));
                proximo = Abrir(nome, primeiro: false);
            }
        }
        finally
        {
            await proximo.DisposeAsync().ConfigureAwait(false);
            parar.Cancel();
        }
        await Task.WhenAll(clientes).ConfigureAwait(false);
        await vigia.ConfigureAwait(false);
        return atendeu ? 0 : 1;

        async Task Vigiar()
        {
            var intervalo = TimeSpan.FromMilliseconds(Math.Clamp(ocioso.TotalMilliseconds / 4, 10, 1000));
            try
            {
                while (true)
                {
                    await Task.Delay(intervalo, parar.Token).ConfigureAwait(false);
                    if (Volatile.Read(ref conectados) > 0) continue;
                    if (ocupado?.Invoke() == true)
                    {
                        Volatile.Write(ref sozinhoDesde, Environment.TickCount64);
                        continue;
                    }
                    if (Environment.TickCount64 - Volatile.Read(ref sozinhoDesde) < ocioso.TotalMilliseconds) continue;
                    parar.Cancel();
                    return;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    static NamedPipeServerStream Abrir(string nome, bool primeiro)
    {
        var opcoes = PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;
        if (primeiro && OperatingSystem.IsWindows()) opcoes |= PipeOptions.FirstPipeInstance;
        return new NamedPipeServerStream(nome, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, opcoes);
    }

    static async Task AtenderAsync(NamedPipeServerStream cano, INucleo nucleo, CancellationToken cancelar)
    {
        var leitor = new StreamReader(cano, new UTF8Encoding(false));
        var escritor = new StreamWriter(cano, new UTF8Encoding(false));
        var trava = new SemaphoreSlim(1, 1);
        async Task Responder(JsonObject resposta)
        {
            await trava.WaitAsync(cancelar).ConfigureAwait(false);
            try
            {
                await escritor.WriteLineAsync(resposta.ToJsonString().AsMemory(), cancelar).ConfigureAwait(false);
                await escritor.FlushAsync(cancelar).ConfigureAwait(false);
            }
            finally
            {
                trava.Release();
            }
        }
        async Task Atender(string linha)
        {
            long id = 0;
            JsonObject resposta;
            try
            {
                var pedido = JsonNode.Parse(linha)!.AsObject();
                id = pedido["id"]!.GetValue<long>();
                var dados = pedido["dados"] as JsonObject;
                object resultado = pedido["metodo"]!.GetValue<string>() switch
                {
                    MetodosDoNucleo.Conversas => await nucleo.ListarConversasAsync(cancelar).ConfigureAwait(false),
                    MetodosDoNucleo.Uso => await nucleo.LerUsoAsync((string?)dados?["cache"], dados!["desde"]!.GetValue<DateTime>(), dados["porDia"]!.GetValue<bool>(), cancelar).ConfigureAwait(false),
                    MetodosDoNucleo.Andamento => await nucleo.LerAndamentoAsync(dados!["arquivo"]!.GetValue<string>(), (string?)dados["pasta"], cancelar).ConfigureAwait(false),
                    MetodosDoNucleo.Turnos => await nucleo.LerTurnosAsync(dados!["arquivo"]!.GetValue<string>(), dados["turnos"]!.GetValue<int>(), dados["ate"]!.GetValue<long>(), cancelar).ConfigureAwait(false),
                    MetodosDoNucleo.Modo => new RespostaDeTexto(await nucleo.UltimoModoAsync(dados!["arquivo"]!.GetValue<string>(), cancelar).ConfigureAwait(false)),
                    MetodosDoNucleo.Comando => await nucleo.RodarComandoAsync(dados.Deserialize<Git.PedidoDeComando>(Json)!, cancelar).ConfigureAwait(false),
                    var outro => throw new InvalidOperationException($"Método desconhecido: {outro}"),
                };
                resposta = new JsonObject { ["id"] = id, ["dados"] = JsonSerializer.SerializeToNode(resultado, resultado.GetType(), Json) };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                resposta = new JsonObject { ["id"] = id, ["erro"] = ex.Message };
            }
            await Responder(resposta).ConfigureAwait(false);
        }
        var emAndamento = new List<Task>();
        try
        {
            while (await leitor.ReadLineAsync(cancelar).ConfigureAwait(false) is { } linha)
            {
                emAndamento.RemoveAll(t => t.IsCompleted);
                emAndamento.Add(Task.Run(() => Atender(linha), cancelar));
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
        }
    }
}
