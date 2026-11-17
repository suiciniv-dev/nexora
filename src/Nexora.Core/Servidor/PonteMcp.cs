using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Orquestra;

namespace Nexora.Core.Servidor;

public sealed class PonteMcp : IDisposable
{
    public const string CabecalhoDoDono = "X-Nexora-Dono";

    sealed record Alvo(int Porta, string Segredo, long Conexao);

    readonly HttpListener _ouvinte;
    readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };
    readonly ConcurrentDictionary<string, Alvo> _alvos = new();
    readonly ConcurrentDictionary<string, (string Servidor, string Dono)> _chaves = new();
    readonly CancellationTokenSource _fim = new();

    public PonteMcp()
    {
        (_ouvinte, Porta) = Abrir();
        _ = OuvirAsync();
    }

    public int Porta { get; }

    public static string MensagemSemApp => Texto.Idioma.T("O Nexora está fechado. Abra o Nexora para usar esta ferramenta.");

    public string Endereco(string servidor) => $"http://localhost:{Porta}/mcp/{Uri.EscapeDataString(servidor)}";

    public void Apontar(string servidor, int porta, string segredo, long conexao) => _alvos[servidor] = new Alvo(porta, segredo, conexao);

    public void Soltar(long conexao)
    {
        foreach (var (servidor, alvo) in _alvos)
            if (alvo.Conexao == conexao) _alvos.TryRemove(new KeyValuePair<string, Alvo>(servidor, alvo));
    }

    public RegistroNaPonte Registrar(string servidor, string dono)
    {
        var chave = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        _chaves[chave] = (servidor, dono);
        return new RegistroNaPonte(Endereco(servidor), chave);
    }

    public void Esquecer(string chave) => _chaves.TryRemove(chave, out _);

    static (HttpListener, int) Abrir()
    {
        for (var tentativa = 0; ; tentativa++)
        {
            var tcp = new TcpListener(IPAddress.Loopback, 0);
            tcp.Start();
            var porta = ((IPEndPoint)tcp.LocalEndpoint).Port;
            tcp.Stop();
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

    async Task OuvirAsync()
    {
        while (!_fim.IsCancellationRequested)
        {
            HttpListenerContext contexto;
            try
            {
                contexto = await _ouvinte.GetContextAsync().ConfigureAwait(false);
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
            var caminho = pedido.Url?.AbsolutePath.TrimEnd('/') ?? "";
            var servidor = caminho.StartsWith("/mcp/", StringComparison.Ordinal) ? Uri.UnescapeDataString(caminho["/mcp/".Length..]) : null;
            var chave = pedido.Headers["Authorization"] is { } valor && valor.StartsWith("Bearer ", StringComparison.Ordinal) ? valor["Bearer ".Length..].Trim() : null;
            if (servidor is null)
                resposta.StatusCode = 404;
            else if (chave is null || !_chaves.TryGetValue(chave, out var registro) || registro.Servidor != servidor)
                resposta.StatusCode = 401;
            else if (pedido.HttpMethod != "POST")
            {
                resposta.StatusCode = 405;
                resposta.AddHeader("Allow", "POST");
            }
            else
            {
                using var leitor = new StreamReader(pedido.InputStream, Encoding.UTF8);
                var corpo = await leitor.ReadToEndAsync(_fim.Token).ConfigureAwait(false);
                var (status, tipo, saida) = await EncaminharAsync(servidor, registro.Dono, corpo, pedido.Headers["Accept"]).ConfigureAwait(false);
                resposta.StatusCode = status;
                if (saida.Length > 0)
                {
                    resposta.ContentType = tipo ?? "application/json";
                    resposta.ContentLength64 = saida.Length;
                    await resposta.OutputStream.WriteAsync(saida, _fim.Token).ConfigureAwait(false);
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

    async Task<(int Status, string? Tipo, byte[] Saida)> EncaminharAsync(string servidor, string dono, string corpo, string? aceita)
    {
        if (_alvos.TryGetValue(servidor, out var alvo))
            try
            {
                using var envio = new HttpRequestMessage(HttpMethod.Post, $"http://localhost:{alvo.Porta}/mcp") { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
                envio.Headers.TryAddWithoutValidation("Authorization", "Bearer " + alvo.Segredo);
                envio.Headers.TryAddWithoutValidation(CabecalhoDoDono, dono);
                if (aceita is { Length: > 0 }) envio.Headers.TryAddWithoutValidation("Accept", aceita);
                using var volta = await _http.SendAsync(envio, _fim.Token).ConfigureAwait(false);
                return ((int)volta.StatusCode, volta.Content.Headers.ContentType?.ToString(), await volta.Content.ReadAsByteArrayAsync(_fim.Token).ConfigureAwait(false));
            }
            catch (HttpRequestException)
            {
            }
        return SemApp(corpo);
    }

    static (int Status, string? Tipo, byte[] Saida) SemApp(string corpo)
    {
        JsonNode? id = null;
        try
        {
            if (JsonNode.Parse(corpo) is JsonObject mensagem)
            {
                if (mensagem["id"] is null) return (202, null, []);
                id = mensagem["id"]!.DeepClone();
            }
        }
        catch (JsonException)
        {
        }
        var erro = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = -32000, ["message"] = MensagemSemApp } };
        return (200, "application/json", Encoding.UTF8.GetBytes(erro.ToJsonString()));
    }

    public void Dispose()
    {
        _fim.Cancel();
        _ouvinte.Close();
        _http.Dispose();
    }
}
