using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;

Console.InputEncoding = new UTF8Encoding(false);
var saida = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
var trava = new object();
void Emitir(JsonNode n)
{
    var linha = n.ToJsonString();
    lock (trava) saida.WriteLine(linha);
}

var argumentos = args.ToList();
var retomar = argumentos.IndexOf("--resume") is var i && i >= 0 && i + 1 < argumentos.Count ? argumentos[i + 1] : null;
var sessao = retomar ?? "teste-" + Guid.NewGuid().ToString("N")[..12];
var registro = Environment.GetEnvironmentVariable("AGENTE_DE_TESTE_REGISTRO");
void Registrar(string texto)
{
    if (registro is null) return;
    var bytes = Encoding.UTF8.GetBytes($"{Environment.ProcessId} {sessao} {texto}\n");
    lock (trava)
        for (var tentativa = 0; ; tentativa++)
        {
            try
            {
                using var fluxo = new FileStream(registro, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                fluxo.Write(bytes);
                return;
            }
            catch (IOException) when (tentativa < 200)
            {
                Thread.Sleep(5);
            }
        }
}

Registrar("iniciou " + string.Join(' ', args));
var pedidos = Channel.CreateUnbounded<(string Texto, CancellationTokenSource Cancelar)>();
var respostas = new ConcurrentDictionary<string, TaskCompletionSource<JsonNode?>>();
var pendentes = new ConcurrentDictionary<CancellationTokenSource, byte>();
var iniciado = false;
var numeroPermissao = 0;

var turnos = Task.Run(async () =>
{
    await foreach (var (texto, cancelar) in pedidos.Reader.ReadAllAsync())
    {
        try
        {
            cancelar.Token.ThrowIfCancellationRequested();
            await Turno(texto, cancelar.Token);
        }
        catch (OperationCanceledException)
        {
            Emitir(Resultado(true, "interrompido", "error_during_execution"));
        }
        pendentes.TryRemove(cancelar, out _);
        cancelar.Dispose();
    }
});

string? linha;
while ((linha = Console.In.ReadLine()) is not null)
{
    JsonNode? n;
    try { n = JsonNode.Parse(linha); } catch { continue; }
    var tipo = (string?)n?["type"];
    if (tipo == "control_request")
    {
        var id = (string?)n!["request_id"] ?? "";
        var sub = (string?)n["request"]?["subtype"];
        if (sub == "interrupt")
            foreach (var pendente in pendentes.Keys) pendente.Cancel();
        Registrar("controle " + sub);
        Emitir(new JsonObject { ["type"] = "control_response", ["response"] = new JsonObject { ["subtype"] = "success", ["request_id"] = id, ["response"] = new JsonObject { ["commands"] = new JsonArray() } } });
    }
    else if (tipo == "control_response")
    {
        var resposta = n!["response"];
        var id = (string?)resposta?["request_id"] ?? "";
        if (respostas.TryRemove(id, out var espera)) espera.TrySetResult(resposta?["response"]?.DeepClone());
    }
    else if (tipo == "user")
    {
        var texto = string.Join("", (n!["message"]?["content"] as JsonArray ?? []).Select(b => (string?)b?["text"] ?? ""));
        Registrar("mensagem " + texto);
        if (!iniciado)
        {
            iniciado = true;
            Emitir(new JsonObject { ["type"] = "system", ["subtype"] = "init", ["session_id"] = sessao, ["model"] = "claude-de-teste", ["cwd"] = Directory.GetCurrentDirectory(), ["permissionMode"] = "default", ["tools"] = new JsonArray() });
        }
        var cancelar = new CancellationTokenSource();
        pendentes[cancelar] = 0;
        await pedidos.Writer.WriteAsync((texto, cancelar));
    }
}
pedidos.Writer.TryComplete();
foreach (var pendente in pendentes.Keys) pendente.Cancel();
await turnos;
return 0;

async Task Turno(string texto, CancellationToken cancelar)
{
    var partes = texto.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
    var comando = partes.Length > 0 ? partes[0].ToLowerInvariant() : "";
    var resto = partes.Length > 1 ? partes[1] : "";
    switch (comando)
    {
        case "demore":
            await Task.Delay(int.TryParse(resto.Split(' ')[0], out var ms) ? ms : 1000, cancelar);
            Falar("pronto depois de " + resto);
            break;
        case "trave":
            await Task.Delay(Timeout.Infinite, cancelar);
            break;
        case "falhe":
            Console.Error.WriteLine("falha simulada: " + resto);
            Registrar("saiu com 3");
            Environment.Exit(3);
            break;
        case "permissao":
            var id = "perm-" + Interlocked.Increment(ref numeroPermissao);
            var espera = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
            respostas[id] = espera;
            Emitir(new JsonObject
            {
                ["type"] = "control_request",
                ["request_id"] = id,
                ["request"] = new JsonObject
                {
                    ["subtype"] = "can_use_tool",
                    ["tool_name"] = "Bash",
                    ["input"] = new JsonObject { ["command"] = resto.Length > 0 ? resto : "echo teste", ["description"] = "Comando de teste" },
                    ["tool_use_id"] = "uso-" + id,
                },
            });
            using (cancelar.Register(() => espera.TrySetCanceled()))
            {
                var decisao = await espera.Task;
                var comportamento = (string?)decisao?["behavior"];
                Falar(comportamento == "allow" ? "permitido" : "negado: " + ((string?)decisao?["message"] ?? ""));
            }
            break;
        default:
            Falar("ok: " + texto);
            break;
    }
    Emitir(Resultado(false, "ok", "success"));
}

void Falar(string texto) => Emitir(new JsonObject
{
    ["type"] = "assistant",
    ["message"] = new JsonObject { ["id"] = "m-" + Guid.NewGuid().ToString("N")[..8], ["role"] = "assistant", ["model"] = "claude-de-teste", ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = texto }) },
    ["session_id"] = sessao,
});

JsonObject Resultado(bool erro, string texto, string subtipo) => new()
{
    ["type"] = "result",
    ["subtype"] = subtipo,
    ["is_error"] = erro,
    ["duration_ms"] = 1,
    ["result"] = texto,
    ["session_id"] = sessao,
    ["total_cost_usd"] = 0,
};
