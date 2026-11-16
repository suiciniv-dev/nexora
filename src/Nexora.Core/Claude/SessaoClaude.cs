using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Nexora.Core.Texto;

namespace Nexora.Core.Claude;

public sealed record OpcoesSessao(
    string Pasta,
    string? Retomar = null,
    string? Modelo = null,
    string? Modo = null,
    IReadOnlyList<string>? Diretorios = null,
    bool Isolada = false,
    string? Mcp = null,
    string? PromptExtra = null,
    IReadOnlyList<string>? Permitidas = null,
    bool Bifurcar = false,
    string? Esforco = null,
    string? Configuracoes = null,
    IReadOnlyDictionary<string, string>? Ambiente = null,
    ILancadorDeAgente? Lancador = null)
{
    public void Aplicar(ProcessStartInfo info)
    {
        foreach (var (chave, valor) in Ambiente ?? new Dictionary<string, string>()) info.Environment[chave] = valor;
    }
}

public sealed class SessaoClaude(OpcoesSessao opcoes, string? executavel = null) : ISessaoAgente
{
    readonly object _escrita = new();
    readonly System.Threading.Channels.Channel<string> _fila = System.Threading.Channels.Channel.CreateUnbounded<string>(new System.Threading.Channels.UnboundedChannelOptions { SingleReader = true });
    Task? _escritor;
    readonly StringBuilder _erros = new();
    readonly ConcurrentDictionary<string, TaskCompletionSource<ControleRespondido>> _esperando = new();
    static readonly ConcurrentDictionary<int, byte> Nossos = new();
    IProcessoDoAgente? _processo;
    int _pedidos;

    public event Action<EventoClaude>? Evento;
    public event Action<int, string>? Encerrou;

    public OpcoesSessao Opcoes { get; } = opcoes;
    public string? SessaoId { get; private set; } = opcoes.Retomar;
    public bool Ativa => _processo is { HasExited: false };
    public int? Pid => _processo is { HasExited: false } p ? p.Id : null;
    public static IReadOnlyCollection<int> PidsDoNexora => [.. Nossos.Keys];
    public string Agente => Agentes.Claude;
    public bool TemRemoto => true;
    public bool TemControles => true;
    public static void Registrar(int pid) => Nossos[pid] = 0;
    public static void Esquecer(int pid) => Nossos.TryRemove(pid, out _);

    public static IReadOnlyList<string> Argumentos(OpcoesSessao o)
    {
        List<string> args =
        [
            "-p", "--input-format", "stream-json", "--output-format", "stream-json", "--verbose",
            "--include-partial-messages", "--permission-prompt-tool", "stdio",
        ];
        if (!string.IsNullOrEmpty(o.Retomar)) args.AddRange(["--resume", o.Retomar]);
        if (o.Bifurcar && !string.IsNullOrEmpty(o.Retomar)) args.Add("--fork-session");
        if (!string.IsNullOrEmpty(o.Modelo)) args.AddRange(["--model", o.Modelo]);
        if (!string.IsNullOrEmpty(o.Esforco)) args.AddRange(["--effort", o.Esforco]);
        if (o.Modo == "bypassPermissions") args.Add("--allow-dangerously-skip-permissions");
        if (!string.IsNullOrEmpty(o.Modo)) args.AddRange(["--permission-mode", o.Modo]);
        foreach (var dir in o.Diretorios ?? []) args.AddRange(["--add-dir", dir]);
        if (o.Isolada) args.AddRange(["--setting-sources", "project,local"]);
        if (!string.IsNullOrEmpty(o.Mcp)) args.AddRange(["--mcp-config", o.Mcp]);
        if (!string.IsNullOrEmpty(o.PromptExtra)) args.AddRange(["--append-system-prompt", o.PromptExtra]);
        if (o.Permitidas is { Count: > 0 } livres) args.AddRange(["--allowedTools", string.Join(',', livres)]);
        if (!string.IsNullOrEmpty(o.Configuracoes)) args.AddRange(["--settings", o.Configuracoes]);
        return args;
    }

    public static string? Simulado { get; set; }

    public void Iniciar()
    {
        var remoto = Opcoes.Lancador is { Remoto: true };
        var info = new ProcessStartInfo(executavel ?? (remoto ? "claude" : Simulado is null ? LocalizadorClaude.Encontrar() : "dotnet"))
        {
            WorkingDirectory = Opcoes.Pasta,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            StandardInputEncoding = new UTF8Encoding(false),
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        Opcoes.Aplicar(info);
        if (executavel is null && !remoto && Simulado is not null) info.ArgumentList.Add(Simulado);
        foreach (var arg in Argumentos(Opcoes)) info.ArgumentList.Add(arg);
        _processo = (Opcoes.Lancador ?? LancadorLocal.Instancia).Abrir(info);
        if (_processo.Id > 0) Nossos[_processo.Id] = 0;
        var iniciado = _processo;
        _escritor = Task.Run(() => EscreverFilaAsync(iniciado));
        _ = LerErrosAsync(_processo);
        _ = LerSaidaAsync(_processo);
        _ = AnunciarComandosAsync();
    }

    async Task AnunciarComandosAsync()
    {
        if (await PedirAsync(Protocolo.Inicializar) is { Sucesso: true, Dados: { } dados }) Evento?.Invoke(Protocolo.LerInicio(dados));
    }

    public async Task<ControleRespondido> PedirAsync(Func<string, string> montar, TimeSpan? limite = null)
    {
        var id = NovoId();
        var espera = new TaskCompletionSource<ControleRespondido>(TaskCreationOptions.RunContinuationsAsynchronously);
        _esperando[id] = espera;
        Escrever(montar(id));
        try
        {
            return await espera.Task.WaitAsync(limite ?? TimeSpan.FromSeconds(45));
        }
        catch (TimeoutException)
        {
            _esperando.TryRemove(id, out _);
            return new ControleRespondido(id, false, Idioma.T("O claude não respondeu a tempo."), null);
        }
    }

    public Task<ControleRespondido> LigarRemotoAsync(bool ligado) => PedirAsync(id => Protocolo.ControleRemoto(id, ligado));

    public void Enviar(string texto, IReadOnlyList<Anexo>? imagens = null) => Escrever(Protocolo.Mensagem(texto, imagens));

    public void Permitir(string pedidoId, JsonNode? entrada, JsonNode? regras = null) =>
        Escrever(Protocolo.Permitir(pedidoId, entrada, regras));

    public void Negar(string pedidoId, string motivo) => Escrever(Protocolo.Negar(pedidoId, motivo));

    public void LiberarSubagente(string pedidoId, JsonNode? entrada) => Escrever(Protocolo.LiberarSubagente(pedidoId, entrada));

    public void BarrarSubagente(string pedidoId, string motivo) => Escrever(Protocolo.BarrarSubagente(pedidoId, motivo));

    public void Interromper() => Escrever(Protocolo.Interromper(NovoId()));

    public void MudarModo(string modo) => Escrever(Protocolo.MudarModo(NovoId(), modo));

    public void MudarModelo(string? modelo) => Escrever(Protocolo.MudarModelo(NovoId(), modelo));

    public void AplicarConfiguracoes(System.Text.Json.Nodes.JsonObject configuracoes) => Escrever(Protocolo.AplicarConfiguracoes(NovoId(), configuracoes));

    public void Encerrar()
    {
        if (_processo is not { } processo) return;
        try
        {
            _fila.Writer.TryComplete();
            _escritor?.Wait(TimeSpan.FromSeconds(1));
            lock (_escrita) processo.StandardInput.Close();
            if (!processo.WaitForExit(1500)) processo.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        var processo = _processo;
        if (processo is null) return;
        try
        {
            _fila.Writer.TryComplete();
            if (_escritor is { } escritor) await escritor.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            lock (_escrita) processo.StandardInput.Close();
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await processo.WaitForExitAsync(limite.Token);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or OperationCanceledException or TimeoutException)
        {
        }
        try
        {
            if (!processo.HasExited) processo.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
        processo.Dispose();
    }

    void Escrever(string linha)
    {
        if (_processo is { HasExited: false }) _fila.Writer.TryWrite(linha);
    }

    async Task EscreverFilaAsync(IProcessoDoAgente processo)
    {
        try
        {
            await foreach (var linha in _fila.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                if (processo.HasExited) break;
                await processo.StandardInput.WriteAsync((linha + "\n").AsMemory()).ConfigureAwait(false);
                await processo.StandardInput.FlushAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
    }

    async Task LerSaidaAsync(IProcessoDoAgente processo)
    {
        try
        {
            while (await processo.StandardOutput.ReadLineAsync() is { } linha)
                foreach (var evento in Protocolo.Ler(linha))
                {
                    if (evento is ControleRespondido resposta && _esperando.TryRemove(resposta.PedidoId, out var espera))
                    {
                        espera.TrySetResult(resposta);
                        continue;
                    }
                    switch (evento)
                    {
                        case SessaoIniciada iniciada:
                            SessaoId = iniciada.SessaoId;
                            break;
                        case ControleNaoSuportado pedido:
                            Escrever(Protocolo.RecusarControle(pedido.PedidoId, Idioma.F("O Nexora ainda não responde a {0}.", pedido.Subtipo)));
                            break;
                    }
                    Evento?.Invoke(evento);
                }
            await processo.WaitForExitAsync();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
        foreach (var id in _esperando.Keys.ToList())
            if (_esperando.TryRemove(id, out var pendente)) pendente.TrySetResult(new ControleRespondido(id, false, Idioma.T("O claude fechou."), null));
        Nossos.TryRemove(processo.Id, out _);
        string erros;
        lock (_erros) erros = _erros.ToString().Trim();
        Encerrou?.Invoke(processo.HasExited ? processo.ExitCode : -1, erros);
    }

    async Task LerErrosAsync(IProcessoDoAgente processo)
    {
        try
        {
            while (await processo.StandardError.ReadLineAsync() is { } linha)
                lock (_erros)
                {
                    _erros.AppendLine(linha);
                    if (_erros.Length > 8000) _erros.Remove(0, _erros.Length - 8000);
                }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
    }

    string NovoId() => "nexora-" + Interlocked.Increment(ref _pedidos);
}
