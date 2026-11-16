using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Claude;

namespace Nexora.Core.Mcp;

public sealed class ProcessoMcp : IAsyncDisposable
{
    public const string VersaoPadrao = "2025-06-18";

    readonly object _escrita = new();
    readonly SemaphoreSlim _subida = new(1, 1);
    readonly ConcurrentDictionary<long, TaskCompletionSource<JsonObject>> _pendentes = new();
    readonly StringBuilder _erros = new();
    Process? _processo;
    Microsoft.Win32.SafeHandles.SafeFileHandle? _grupo;
    long _ids;
    int _emVoo;

    public ProcessoMcp(DefinicaoMcp definicao) => Definicao = definicao;

    public DefinicaoMcp Definicao { get; }
    public JsonObject? Inicio { get; private set; }
    public DateTime UltimoUso { get; private set; } = DateTime.UtcNow;
    public int Subidas { get; private set; }
    public bool Ativo => _processo is { HasExited: false } && Inicio is not null;
    public string? SubiuPor { get; set; }
    public int? Pid => _processo is { HasExited: false } p ? p.Id : null;
    public int EmVoo => Volatile.Read(ref _emVoo);
    public string Erros { get { lock (_erros) return _erros.ToString(); } }
    public event Action<ProcessoMcp>? Mudou;

    public async Task<JsonObject> GarantirAsync(string? versao, CancellationToken cancelar)
    {
        if (Ativo) return Inicio!;
        await _subida.WaitAsync(cancelar);
        try
        {
            if (Ativo) return Inicio!;
            await PararInternoAsync();
            Iniciar();
            var resposta = await EnviarAsync("initialize", new JsonObject
            {
                ["protocolVersion"] = versao ?? VersaoPadrao,
                ["capabilities"] = new JsonObject { ["roots"] = new JsonObject { ["listChanged"] = false } },
                ["clientInfo"] = new JsonObject { ["name"] = "nexora-hub", ["version"] = "1.0.0" },
            }, cancelar, TimeSpan.FromSeconds(60));
            if (resposta["result"] is not JsonObject resultado)
                throw new InvalidOperationException(MensagemErro(resposta) ?? $"{Definicao.Nome} não respondeu ao initialize.");
            Notificar("notifications/initialized", null);
            Inicio = resultado;
            Subidas++;
            Mudou?.Invoke(this);
            return resultado;
        }
        catch
        {
            await PararInternoAsync();
            throw;
        }
        finally
        {
            _subida.Release();
        }
    }

    public async Task<JsonObject> PedirAsync(string metodo, JsonNode? parametros, string? versao, CancellationToken cancelar)
    {
        Interlocked.Increment(ref _emVoo);
        try
        {
            await GarantirAsync(versao, cancelar);
            return await EnviarAsync(metodo, parametros, cancelar, null);
        }
        finally
        {
            Interlocked.Decrement(ref _emVoo);
            UltimoUso = DateTime.UtcNow;
        }
    }

    public void Notificar(string metodo, JsonNode? parametros)
    {
        var m = new JsonObject { ["jsonrpc"] = "2.0", ["method"] = metodo };
        if (parametros is not null) m["params"] = parametros.DeepClone();
        Escrever(m);
    }

    public void Cancelar(long idInterno, string? motivo) =>
        Notificar("notifications/cancelled", new JsonObject { ["requestId"] = idInterno, ["reason"] = motivo });

    public Task PararAsync() => PararInternoAsync();

    public async ValueTask DisposeAsync() => await PararInternoAsync();

    async Task<JsonObject> EnviarAsync(string metodo, JsonNode? parametros, CancellationToken cancelar, TimeSpan? limite)
    {
        var id = Interlocked.Increment(ref _ids);
        var espera = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendentes[id] = espera;
        var m = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = metodo };
        if (parametros is not null) m["params"] = parametros.DeepClone();
        Escrever(m);
        using var registro = cancelar.Register(() =>
        {
            if (_pendentes.TryRemove(id, out var p))
            {
                Cancelar(id, "O pedido foi cancelado.");
                p.TrySetCanceled(cancelar);
            }
        });
        try
        {
            return limite is { } l ? await espera.Task.WaitAsync(l, cancelar) : await espera.Task;
        }
        catch (TimeoutException)
        {
            _pendentes.TryRemove(id, out _);
            throw new InvalidOperationException($"{Definicao.Nome} não respondeu a {metodo} a tempo.");
        }
    }

    void Iniciar()
    {
        var (arquivo, argumentos) = Resolver(Definicao.Comando, Definicao.Argumentos, Definicao.Pasta);
        var info = new ProcessStartInfo(arquivo)
        {
            WorkingDirectory = Definicao.Pasta,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            StandardInputEncoding = new UTF8Encoding(false),
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in argumentos) info.ArgumentList.Add(a);
        foreach (var (k, v) in Definicao.Ambiente) info.Environment[k] = v;
        lock (_erros) _erros.Clear();
        var processo = Process.Start(info) ?? throw new InvalidOperationException($"{Definicao.Nome} não abriu.");
        GrupoDeProcessos.Adicionar(processo);
        _grupo = GrupoDeProcessos.Novo();
        GrupoDeProcessos.Adicionar(_grupo, processo);
        _processo = processo;
        _ = LerSaidaAsync(processo);
        _ = LerErrosAsync(processo);
    }

    public static (string Arquivo, IReadOnlyList<string> Argumentos) Resolver(string comando, IReadOnlyList<string> argumentos, string pasta)
    {
        if (!OperatingSystem.IsWindows()) return (comando, argumentos);
        var achado = Procurar(comando, pasta) ?? comando;
        var extensao = Path.GetExtension(achado).ToLowerInvariant();
        if (extensao is ".cmd" or ".bat")
            return (Path.Combine(Environment.SystemDirectory, "cmd.exe"), ["/d", "/c", achado, .. argumentos]);
        return (achado, argumentos);
    }

    static string? Procurar(string comando, string pasta)
    {
        var extensoes = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries);
        IEnumerable<string> candidatos(string baseCaminho) =>
            Path.HasExtension(baseCaminho) ? [baseCaminho, .. extensoes.Select(e => baseCaminho + e)] : extensoes.Select(e => baseCaminho + e);
        if (comando.Contains('\\') || comando.Contains('/'))
            return candidatos(Path.GetFullPath(Path.Combine(pasta, comando))).FirstOrDefault(File.Exists);
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string limpo;
            try
            {
                limpo = Path.Combine(dir.Trim('"'), comando);
            }
            catch (ArgumentException)
            {
                continue;
            }
            if (candidatos(limpo).FirstOrDefault(File.Exists) is { } achado) return achado;
        }
        return null;
    }

    void Escrever(JsonObject mensagem)
    {
        lock (_escrita)
        {
            if (_processo is not { HasExited: false } processo) return;
            try
            {
                processo.StandardInput.Write(mensagem.ToJsonString() + "\n");
                processo.StandardInput.Flush();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
            {
            }
        }
    }

    async Task LerSaidaAsync(Process processo)
    {
        try
        {
            while (await processo.StandardOutput.ReadLineAsync() is { } linha)
            {
                if (linha.Length == 0 || linha[0] != '{') continue;
                JsonObject? m;
                try
                {
                    m = JsonNode.Parse(linha) as JsonObject;
                }
                catch (JsonException)
                {
                    continue;
                }
                if (m is null) continue;
                var temMetodo = m["method"] is JsonValue;
                var id = m["id"];
                if (!temMetodo && id is JsonValue valor && valor.TryGetValue<long>(out var numero))
                {
                    if (_pendentes.TryRemove(numero, out var espera)) espera.TrySetResult(m);
                }
                else if (temMetodo && id is not null)
                    Escrever(ResponderAoServidor(m));
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
        if (_processo == processo) FalharPendentes();
    }

    JsonObject ResponderAoServidor(JsonObject pedido)
    {
        var metodo = pedido["method"]!.GetValue<string>();
        var resposta = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = pedido["id"]!.DeepClone() };
        switch (metodo)
        {
            case "ping":
                resposta["result"] = new JsonObject();
                break;
            case "roots/list":
                resposta["result"] = new JsonObject
                {
                    ["roots"] = new JsonArray(new JsonObject { ["uri"] = new Uri(Definicao.Pasta).AbsoluteUri, ["name"] = Path.GetFileName(Definicao.Pasta.TrimEnd('\\', '/')) }),
                };
                break;
            default:
                resposta["error"] = new JsonObject { ["code"] = -32601, ["message"] = $"O Nexora não atende {metodo}." };
                break;
        }
        return resposta;
    }

    async Task LerErrosAsync(Process processo)
    {
        try
        {
            while (await processo.StandardError.ReadLineAsync() is { } linha)
                lock (_erros)
                {
                    _erros.AppendLine(linha);
                    if (_erros.Length > 6000) _erros.Remove(0, _erros.Length - 6000);
                }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
    }

    void FalharPendentes()
    {
        var detalhe = Erros.Trim() is { Length: > 0 } e ? $" {e[Math.Max(0, e.Length - 400)..]}" : "";
        foreach (var id in _pendentes.Keys.ToList())
            if (_pendentes.TryRemove(id, out var p))
                p.TrySetResult(new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id,
                    ["error"] = new JsonObject { ["code"] = -32000, ["message"] = $"O servidor MCP {Definicao.Nome} fechou.{detalhe}" },
                });
        Inicio = null;
        Mudou?.Invoke(this);
    }

    public long Memoria()
    {
        long total = 0;
        foreach (var pid in GrupoDeProcessos.Processos(_grupo))
            try
            {
                using var p = Process.GetProcessById(pid);
                total += p.PrivateMemorySize64;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
        return total;
    }

    public int Processos => GrupoDeProcessos.Processos(_grupo).Length;

    public int[] Pids()
    {
        var grupo = GrupoDeProcessos.Processos(_grupo);
        return grupo.Length > 0 ? grupo : Pid is { } pid ? [pid] : [];
    }

    async Task PararInternoAsync()
    {
        var processo = _processo;
        var grupo = _grupo;
        _processo = null;
        _grupo = null;
        Inicio = null;
        if (processo is null) return;
        try
        {
            lock (_escrita) processo.StandardInput.Close();
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await processo.WaitForExitAsync(limite.Token);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or OperationCanceledException)
        {
        }
        GrupoDeProcessos.Terminar(grupo);
        try
        {
            if (!processo.HasExited) processo.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
        grupo?.Dispose();
        processo.Dispose();
        FalharPendentes();
    }

    public static string? MensagemErro(JsonObject resposta) => resposta["error"]?["message"]?.GetValue<string>();
}
