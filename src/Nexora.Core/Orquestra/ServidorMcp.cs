using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Claude;

namespace Nexora.Core.Orquestra;

public sealed record ResultadoFerramenta(string Texto, bool Erro = false);

public delegate Task<ResultadoFerramenta> ExecutorMcp(string dono, string ferramenta, JsonElement argumentos, CancellationToken cancelar);

public sealed class ServidorMcp : IDisposable
{
    public const string Nome = "nexora";
    static readonly string[] Versoes = ["2025-11-25", "2025-06-18", "2025-03-26", "2024-11-05"];

    readonly HttpListener _ouvinte;
    readonly ConcurrentDictionary<string, string> _chaves = new();
    readonly IReadOnlyList<FerramentaMcp> _ferramentas;
    readonly ExecutorMcp _executar;
    readonly CancellationTokenSource _fim = new();

    public ServidorMcp(IReadOnlyList<FerramentaMcp> ferramentas, ExecutorMcp executar, string nome = Nome)
    {
        _ferramentas = ferramentas;
        _executar = executar;
        NomeDoServidor = nome;
        (_ouvinte, Porta) = Abrir();
        _ = OuvirAsync();
    }

    public int Porta { get; }
    public string NomeDoServidor { get; }
    public string Endereco => $"http://localhost:{Porta}/mcp";

    public string Registrar(string dono)
    {
        var chave = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        _chaves[chave] = dono;
        return chave;
    }

    public void Esquecer(string chave) => _chaves.TryRemove(chave, out _);

    public string Configuracao(string chave) => new JsonObject
    {
        ["mcpServers"] = new JsonObject
        {
            [NomeDoServidor] = new JsonObject
            {
                ["type"] = "http",
                ["url"] = Endereco,
                ["headers"] = new JsonObject { ["Authorization"] = $"Bearer {chave}" },
            },
        },
    }.ToJsonString();

    public static string NomeCompleto(string ferramenta) => Ferramentas.Prefixo + ferramenta;

    static (HttpListener, int) Abrir()
    {
        for (var tentativa = 0; ; tentativa++)
        {
            var porta = PortaLivre();
            var ouvinte = new HttpListener();
            ouvinte.Prefixes.Add($"http://localhost:{porta}/");
            try
            {
                ouvinte.Start();
                return (ouvinte, porta);
            }
            catch (HttpListenerException) when (tentativa < 5)
            {
                ouvinte.Close();
            }
        }
    }

    static int PortaLivre()
    {
        var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var porta = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        return porta;
    }

    async Task OuvirAsync()
    {
        while (!_fim.IsCancellationRequested)
        {
            HttpListenerContext contexto;
            try
            {
                contexto = await _ouvinte.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }
            _ = AtenderAsync(contexto);
        }
    }

    async Task AtenderAsync(HttpListenerContext contexto)
    {
        var pedido = contexto.Request;
        var resposta = contexto.Response;
        try
        {
            if (pedido.Url?.AbsolutePath.TrimEnd('/') != "/mcp")
                resposta.StatusCode = 404;
            else if (Dono(pedido) is not { } dono)
                resposta.StatusCode = 401;
            else if (pedido.HttpMethod != "POST")
            {
                resposta.StatusCode = 405;
                resposta.AddHeader("Allow", "POST");
            }
            else
            {
                using var leitor = new StreamReader(pedido.InputStream, Encoding.UTF8);
                var saida = await ResponderAsync(dono, await leitor.ReadToEndAsync(), _fim.Token);
                if (saida is null)
                    resposta.StatusCode = 202;
                else
                {
                    var bytes = Encoding.UTF8.GetBytes(saida);
                    resposta.ContentType = "application/json";
                    resposta.ContentLength64 = bytes.Length;
                    await resposta.OutputStream.WriteAsync(bytes, _fim.Token);
                }
            }
        }
        catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException or OperationCanceledException)
        {
        }
        finally
        {
            try
            {
                resposta.Close();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
            {
            }
        }
    }

    string? Dono(HttpListenerRequest pedido) =>
        pedido.Headers["Authorization"] is { } valor && valor.StartsWith("Bearer ", StringComparison.Ordinal)
            && _chaves.TryGetValue(valor["Bearer ".Length..].Trim(), out var dono) ? dono : null;

    public async Task<string?> ResponderAsync(string dono, string corpo, CancellationToken cancelar)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(corpo);
        }
        catch (JsonException)
        {
            return Erro(null, -32700, "JSON inválido.").ToJsonString();
        }
        using (doc)
        {
            var raiz = doc.RootElement;
            if (raiz.ValueKind != JsonValueKind.Array) return (await MensagemAsync(dono, raiz, cancelar))?.ToJsonString();
            var respostas = new JsonArray();
            foreach (var item in raiz.EnumerateArray())
                if (await MensagemAsync(dono, item, cancelar) is { } r) respostas.Add(r);
            return respostas.Count == 0 ? null : respostas.ToJsonString();
        }
    }

    async Task<JsonObject?> MensagemAsync(string dono, JsonElement m, CancellationToken cancelar)
    {
        if (Protocolo.Texto(m, "method") is not { } metodo) return Erro(null, -32600, "Pedido inválido.");
        if (!m.TryGetProperty("id", out var idBruto)) return null;
        var id = Protocolo.ParaNo(idBruto);
        var parametros = Protocolo.Objeto(m, "params");
        return metodo switch
        {
            "initialize" => Resultado(id, Inicio(parametros, NomeDoServidor)),
            "ping" => Resultado(id, []),
            "tools/list" => Resultado(id, new JsonObject { ["tools"] = new JsonArray([.. _ferramentas.Select(f => f.ParaJson())]) }),
            "tools/call" => Resultado(id, await ChamarAsync(dono, parametros, cancelar)),
            _ => Erro(id, -32601, $"Método desconhecido: {metodo}"),
        };
    }

    static JsonObject Inicio(JsonElement parametros, string nome)
    {
        var pedida = Protocolo.Texto(parametros, "protocolVersion");
        return new JsonObject
        {
            ["protocolVersion"] = pedida is not null && Versoes.Contains(pedida) ? pedida : Versoes[1],
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
            ["serverInfo"] = new JsonObject { ["name"] = nome, ["version"] = "1.0.0" },
        };
    }

    async Task<JsonObject> ChamarAsync(string dono, JsonElement parametros, CancellationToken cancelar)
    {
        var nome = Protocolo.Texto(parametros, "name") ?? "";
        ResultadoFerramenta r;
        if (_ferramentas.All(f => f.Nome != nome))
            r = new ResultadoFerramenta($"Ferramenta desconhecida: {nome}", Erro: true);
        else
            try
            {
                r = await _executar(dono, nome, Protocolo.Objeto(parametros, "arguments"), cancelar);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                r = new ResultadoFerramenta(ex.Message, Erro: true);
            }
        return new JsonObject
        {
            ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = r.Texto }),
            ["isError"] = r.Erro,
        };
    }

    static JsonObject Resultado(JsonNode? id, JsonObject resultado) => new() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = resultado };

    static JsonObject Erro(JsonNode? id, int codigo, string mensagem) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = codigo, ["message"] = mensagem } };

    public void Dispose()
    {
        _fim.Cancel();
        _ouvinte.Close();
    }
}
