using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nexora.Core.Mcp;

public sealed record RegistroHub(string Chave, string? Configuracao, IReadOnlyList<string> Nomes);

public sealed record EstadoServidor(
    string Nome, string Pasta, bool PorSessao, bool Ativo, int? Pid, long Memoria, int Processos,
    int Sessoes, DateTime UltimoUso, int Subidas, string? Dono, string Instancia, string? SubiuPor = null);

public sealed class HubMcp : IHubMcp
{
    static readonly string[] Guardaveis = ["tools/list", "prompts/list", "resources/list", "resources/templates/list", "server/discover"];
    const int MetodoInexistente = -32601;

    sealed record Sessao(string Pasta, string Rotulo, IReadOnlyDictionary<string, DefinicaoMcp> Servidores);

    sealed class Memoria
    {
        public JsonObject? Inicio { get; set; }
        public Dictionary<string, JsonObject> Respostas { get; set; } = [];
    }

    readonly HttpListener _ouvinte;
    readonly string? _pastaCache;
    readonly ConcurrentDictionary<string, Sessao> _sessoes = new();
    readonly ConcurrentDictionary<string, ProcessoMcp> _processos = new();
    readonly ConcurrentDictionary<string, Memoria> _memorias = new();
    readonly ConcurrentDictionary<string, CancellationTokenSource> _emCurso = new();
    readonly CancellationTokenSource _fim = new();
    readonly Timer _vigia;

    public HubMcp(string? pastaCache = null)
    {
        _pastaCache = pastaCache;
        (_ouvinte, Porta) = Abrir();
        _ = OuvirAsync();
        _vigia = new Timer(_ => _ = VigiarAsync(), null, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));
    }

    public int Porta { get; }
    public TimeSpan Ocioso { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan Folga { get; set; } = TimeSpan.FromMinutes(2);
    public event Action? Mudou;

    public RegistroHub Registrar(string pasta, string rotulo, string? casa = null)
    {
        var definicoes = ServidoresDoProjeto.Ler(pasta, casa);
        var chave = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        _sessoes[chave] = new Sessao(pasta, rotulo, definicoes.ToDictionary(d => d.Chave));
        Avisar();
        if (definicoes.Count == 0) return new RegistroHub(chave, null, []);
        var servidores = new JsonObject();
        foreach (var d in definicoes)
            servidores[d.Nome] = new JsonObject
            {
                ["type"] = "http",
                ["url"] = $"http://localhost:{Porta}/mcp/{d.Chave}",
                ["headers"] = new JsonObject { ["Authorization"] = $"Bearer {chave}" },
            };
        return new RegistroHub(chave, new JsonObject { ["mcpServers"] = servidores }.ToJsonString(), [.. definicoes.Select(d => d.Nome)]);
    }

    public void Renomear(string chave, string rotulo)
    {
        if (_sessoes.TryGetValue(chave, out var s)) _sessoes[chave] = s with { Rotulo = rotulo };
    }

    public async Task EsquecerAsync(string chave)
    {
        if (!_sessoes.TryRemove(chave, out _)) return;
        foreach (var (instancia, processo) in _processos.Where(p => p.Key.EndsWith("|" + chave, StringComparison.Ordinal)).ToList())
            if (_processos.TryRemove(instancia, out _)) await processo.PararAsync();
        Avisar();
    }

    public async Task PararAsync(string instancia)
    {
        if (_processos.TryGetValue(instancia, out var processo)) await processo.PararAsync();
        Avisar();
    }

    public int Ativos => _processos.Values.Count(p => p.Ativo);

    public IReadOnlyList<EstadoServidor> Estado()
    {
        var lista = new List<EstadoServidor>();
        foreach (var (instancia, p) in _processos)
        {
            var d = p.Definicao;
            var dono = d.PorSessao && instancia.Split('|') is [_, var token] && _sessoes.TryGetValue(token, out var s) ? s.Rotulo : null;
            var ativo = p.Ativo;
            lista.Add(new EstadoServidor(d.Nome, d.Pasta, d.PorSessao, ativo, p.Pid, ativo ? p.Memoria() : 0, ativo ? p.Processos : 0,
                Usuarios(instancia, d), p.UltimoUso, p.Subidas, dono, instancia, p.SubiuPor));
        }
        return [.. lista.OrderBy(e => e.Pasta, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Nome, StringComparer.OrdinalIgnoreCase)];
    }

    public IReadOnlyList<int> Pids() => [.. _processos.Values.SelectMany(p => p.Pids())];

    public int SessoesRegistradas => _sessoes.Count;

    int Usuarios(string instancia, DefinicaoMcp d) =>
        d.PorSessao ? (instancia.Split('|') is [_, var token] && _sessoes.ContainsKey(token) ? 1 : 0)
                    : _sessoes.Values.Count(s => s.Servidores.ContainsKey(d.Chave));

    async Task VigiarAsync()
    {
        var agora = DateTime.UtcNow;
        var mudou = false;
        foreach (var (instancia, p) in _processos.ToList())
        {
            var usuarios = Usuarios(instancia, p.Definicao);
            if (!p.Ativo)
            {
                if (usuarios == 0 && _processos.TryRemove(instancia, out _)) mudou = true;
                continue;
            }
            if (p.EmVoo > 0) continue;
            var limite = usuarios == 0 ? Folga : Ocioso;
            if (agora - p.UltimoUso < limite) continue;
            await p.PararAsync();
            if (usuarios == 0) _processos.TryRemove(instancia, out _);
            mudou = true;
        }
        if (mudou) Avisar();
    }

    void Avisar() => Mudou?.Invoke();

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
            var partes = pedido.Url?.AbsolutePath.Trim('/').Split('/') ?? [];
            var token = pedido.Headers["Authorization"] is { } valor && valor.StartsWith("Bearer ", StringComparison.Ordinal) ? valor["Bearer ".Length..].Trim() : null;
            if (partes is not ["mcp", var id])
                resposta.StatusCode = 404;
            else if (token is null || !_sessoes.TryGetValue(token, out var sessao) || !sessao.Servidores.TryGetValue(id, out var definicao))
                resposta.StatusCode = 401;
            else if (pedido.HttpMethod != "POST")
            {
                resposta.StatusCode = 405;
                resposta.AddHeader("Allow", "POST");
            }
            else
            {
                using var leitor = new StreamReader(pedido.InputStream, Encoding.UTF8);
                var saida = await ResponderAsync(token, definicao, await leitor.ReadToEndAsync(), _fim.Token);
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

    async Task<string?> ResponderAsync(string token, DefinicaoMcp definicao, string corpo, CancellationToken cancelar)
    {
        JsonNode? raiz;
        try
        {
            raiz = JsonNode.Parse(corpo);
        }
        catch (JsonException)
        {
            return Erro(null, -32700, "JSON inválido.").ToJsonString();
        }
        if (raiz is JsonArray lote)
        {
            var respostas = new JsonArray();
            foreach (var item in lote.OfType<JsonObject>())
                if (await MensagemAsync(token, definicao, item, cancelar) is { } r) respostas.Add(r);
            return respostas.Count == 0 ? null : respostas.ToJsonString();
        }
        return raiz is JsonObject m ? (await MensagemAsync(token, definicao, m, cancelar))?.ToJsonString() : Erro(null, -32600, "Pedido inválido.").ToJsonString();
    }

    async Task<JsonObject?> MensagemAsync(string token, DefinicaoMcp definicao, JsonObject m, CancellationToken cancelar)
    {
        var metodo = m["method"] is JsonValue v && v.TryGetValue<string>(out var nomeMetodo) ? nomeMetodo : null;
        var id = m["id"]?.DeepClone();
        var parametros = m["params"] as JsonObject;
        if (metodo is null) return null;
        if (id is null)
        {
            if (metodo == "notifications/cancelled" && parametros?["requestId"] is { } alvo && _emCurso.TryRemove($"{token}|{alvo.ToJsonString()}", out var cts))
                cts.Cancel();
            return null;
        }
        var instancia = definicao.PorSessao ? $"{definicao.Chave}|{token}" : definicao.Chave;
        var processo = _processos.GetOrAdd(instancia, _ => NovoProcesso(definicao));
        var memoria = _memorias.GetOrAdd(definicao.Chave, Carregar);
        var versao = parametros?["protocolVersion"]?.GetValue<string>() ?? memoria.Inicio?["protocolVersion"]?.GetValue<string>();
        var paginada = parametros is JsonObject p && p.ContainsKey("cursor");
        var chaveCurso = $"{token}|{id.ToJsonString()}";
        using var curso = CancellationTokenSource.CreateLinkedTokenSource(cancelar);
        _emCurso[chaveCurso] = curso;
        try
        {
            switch (metodo)
            {
                case "ping":
                    return Resultado(id, []);
                case "initialize":
                    if (!processo.Ativo && memoria.Inicio is { } guardado) return Resultado(id, (JsonObject)guardado.DeepClone());
                    Marcar(processo, metodo);
                    var inicio = await processo.GarantirAsync(versao, curso.Token);
                    Guardar(definicao.Chave, memoria, inicio, null, null);
                    return Resultado(id, (JsonObject)inicio.DeepClone());
                case "logging/setLevel" when !processo.Ativo:
                    return Resultado(id, []);
                case var guardavel when !paginada && Guardaveis.Contains(guardavel):
                    if (!processo.Ativo && memoria.Respostas.TryGetValue(guardavel, out var pronta)) return Repassar(id, pronta);
                    Marcar(processo, guardavel);
                    var respondida = await processo.PedirAsync(guardavel, parametros, versao, curso.Token);
                    if (ParaGuardar(respondida) is { } envelope) Guardar(definicao.Chave, memoria, processo.Inicio, guardavel, envelope);
                    return Repassar(id, respondida);
                default:
                    Marcar(processo, metodo);
                    var r = await processo.PedirAsync(metodo, parametros, versao, curso.Token);
                    return Repassar(id, r);
            }
        }
        catch (OperationCanceledException)
        {
            return Erro(id, -32800, "Pedido cancelado.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return Erro(id, -32000, $"O Nexora não conseguiu falar com {definicao.Nome}: {ex.Message}");
        }
        finally
        {
            _emCurso.TryRemove(chaveCurso, out _);
            Avisar();
        }
    }

    static JsonObject? ParaGuardar(JsonObject resposta)
    {
        if (resposta["result"] is JsonObject resultado) return new JsonObject { ["result"] = resultado.DeepClone() };
        if (resposta["error"] is JsonObject erro && erro["code"] is JsonValue codigo && codigo.TryGetValue<int>(out var c) && c == MetodoInexistente)
            return new JsonObject { ["error"] = erro.DeepClone() };
        return null;
    }

    static void Marcar(ProcessoMcp processo, string metodo)
    {
        if (!processo.Ativo) processo.SubiuPor = metodo;
    }

    ProcessoMcp NovoProcesso(DefinicaoMcp definicao)
    {
        var p = new ProcessoMcp(definicao);
        p.Mudou += _ => Avisar();
        return p;
    }

    static JsonObject Repassar(JsonNode id, JsonObject r)
    {
        var saida = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id };
        if (r["error"] is { } erro) saida["error"] = erro.DeepClone();
        else saida["result"] = r["result"]?.DeepClone() ?? new JsonObject();
        return saida;
    }

    Memoria Carregar(string chave)
    {
        if (_pastaCache is null) return new Memoria();
        try
        {
            var arquivo = Path.Combine(_pastaCache, chave + ".json");
            if (!File.Exists(arquivo)) return new Memoria();
            var no = JsonNode.Parse(File.ReadAllText(arquivo)) as JsonObject;
            var m = new Memoria { Inicio = no?["inicio"] as JsonObject };
            if (no?["listas"] is JsonObject listas)
                foreach (var (nome, valor) in listas)
                    if (valor is JsonObject o) m.Respostas[nome] = new JsonObject { ["result"] = o.DeepClone() };
            if (no?["respostas"] is JsonObject respostas)
                foreach (var (nome, valor) in respostas)
                    if (valor is JsonObject o && (o.ContainsKey("result") || o.ContainsKey("error"))) m.Respostas[nome] = (JsonObject)o.DeepClone();
            return m;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Memoria();
        }
    }

    void Guardar(string chave, Memoria m, JsonObject? inicio, string? metodo, JsonObject? envelope)
    {
        lock (m)
        {
            if (inicio is not null) m.Inicio = (JsonObject)inicio.DeepClone();
            if (metodo is not null && envelope is not null) m.Respostas[metodo] = (JsonObject)envelope.DeepClone();
            if (_pastaCache is null) return;
            try
            {
                Directory.CreateDirectory(_pastaCache);
                var respostas = new JsonObject();
                foreach (var (nome, valor) in m.Respostas) respostas[nome] = valor.DeepClone();
                File.WriteAllText(Path.Combine(_pastaCache, chave + ".json"),
                    new JsonObject { ["inicio"] = m.Inicio?.DeepClone(), ["respostas"] = respostas }.ToJsonString());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    static JsonObject Resultado(JsonNode id, JsonObject resultado) => new() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = resultado };

    static JsonObject Erro(JsonNode? id, int codigo, string mensagem) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = codigo, ["message"] = mensagem } };

    public static string Juntar(string? a, string? b)
    {
        var servidores = new JsonObject();
        foreach (var texto in new[] { a, b })
        {
            if (string.IsNullOrWhiteSpace(texto)) continue;
            if (JsonNode.Parse(texto)?["mcpServers"] is JsonObject lista)
                foreach (var (nome, valor) in lista) servidores[nome] = valor?.DeepClone();
        }
        return new JsonObject { ["mcpServers"] = servidores }.ToJsonString();
    }

    public void Dispose()
    {
        _vigia.Dispose();
        _fim.Cancel();
        _ouvinte.Close();
        foreach (var p in _processos.Values)
            try
            {
                p.PararAsync().Wait(TimeSpan.FromSeconds(3));
            }
            catch (AggregateException)
            {
            }
        _processos.Clear();
    }
}
