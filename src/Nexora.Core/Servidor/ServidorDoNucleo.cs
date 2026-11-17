using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nexora.Core.Servidor;

public static class ServidorDoNucleo
{
    public const string Argumento = "--servidor";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.General);

    public static async Task<int> RodarAsync(string nome, INucleo nucleo, TimeSpan esperaPeloApp, CancellationToken cancelar = default)
    {
        await using var cano = new NamedPipeServerStream(nome, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using (var espera = CancellationTokenSource.CreateLinkedTokenSource(cancelar))
        {
            espera.CancelAfter(esperaPeloApp);
            try
            {
                await cano.WaitForConnectionAsync(espera.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return 1;
            }
        }
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
        return 0;
    }
}
