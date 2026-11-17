using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.Core.Codex;

public sealed class SessaoCodex(OpcoesSessao opcoes, string versaoDoApp = "0.0.0", string? executavel = null, IReadOnlyList<string>? prefixo = null) : ISessaoAgente, IHistoricoDoAgente
{
    readonly System.Threading.Channels.Channel<string> _fila = System.Threading.Channels.Channel.CreateUnbounded<string>(new System.Threading.Channels.UnboundedChannelOptions { SingleReader = true });
    readonly ConcurrentDictionary<long, TaskCompletionSource<RespostaCodex>> _esperando = new();
    readonly ConcurrentDictionary<string, (JsonNode Id, string Metodo)> _pedidos = new();
    readonly List<(string Texto, IReadOnlyList<Anexo> Imagens)> _antesDaThread = [];
    readonly ProtocoloCodex _protocolo = new();
    readonly StringBuilder _erros = new();
    readonly object _trava = new();
    readonly TaskCompletionSource<bool> _pronta = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly List<string> _temporarios = [];
    IProcessoDoAgente? _processo;
    Task? _escritor;
    long _ids;
    string? _modelo = opcoes.Modelo;
    string? _modo = opcoes.Modo;
    bool _threadPronta;

    public event Action<EventoClaude>? Evento;
    public event Action<int, string>? Encerrou;

    public static string? Simulado { get; set; }

    static readonly HashSet<string> Respondiveis =
    [
        "item/commandExecution/requestApproval",
        "item/fileChange/requestApproval",
        "item/permissions/requestApproval",
        "item/tool/requestUserInput",
        "execCommandApproval",
        "applyPatchApproval",
    ];

    public OpcoesSessao Opcoes { get; } = opcoes;
    public string Agente => Agentes.Codex;
    public string? SessaoId => _protocolo.ThreadId;
    public bool Ativa => _processo is { HasExited: false };
    public int? Pid => _processo is { HasExited: false } p ? p.Id : null;
    public bool TemRemoto => false;
    public bool TemControles => false;
    public string? UltimoTurno => _protocolo.UltimoTurno;

    public async Task<string?> VoltarAntesDeAsync(string turnoId, TimeSpan? espera = null)
    {
        try
        {
            if (!await _pronta.Task.WaitAsync(espera ?? TimeSpan.FromSeconds(60)).ConfigureAwait(false)) return Idioma.T("O Codex não abriu a conversa.");
        }
        catch (TimeoutException)
        {
            return Idioma.T("O Codex não respondeu a tempo.");
        }
        if (_protocolo.ThreadId is not { } thread) return Idioma.T("O Codex não abriu a conversa.");
        var resposta = await PedirCodexAsync("thread/revert", new JsonObject { ["threadId"] = thread, ["beforeTurnId"] = turnoId }, TimeSpan.FromMinutes(2)).ConfigureAwait(false);
        return resposta.Erro;
    }

    public async Task<IReadOnlyList<EventoClaude>> LerHistoricoAsync(TimeSpan? espera = null)
    {
        try
        {
            if (!await _pronta.Task.WaitAsync(espera ?? TimeSpan.FromSeconds(60)).ConfigureAwait(false)) return [];
        }
        catch (TimeoutException)
        {
            return [];
        }
        if (_protocolo.ThreadId is not { } thread) return [];
        var resposta = await PedirCodexAsync("thread/read", new JsonObject { ["threadId"] = thread, ["includeTurns"] = true }, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
        return resposta.Resultado is { } r && r.TryGetProperty("thread", out var t) ? ProtocoloCodex.Historico(t) : [];
    }

    public void Iniciar()
    {
        var (arquivo, argumentos) = executavel is not null ? (executavel, prefixo?.ToArray() ?? [])
            : Opcoes.Lancador is { Remoto: true } ? ("codex", [])
            : Simulado is not null ? ("dotnet", new[] { Simulado })
            : LocalizadorCodex.Comando();
        var info = new ProcessStartInfo(arquivo)
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
        foreach (var arg in argumentos) info.ArgumentList.Add(arg);
        info.ArgumentList.Add("app-server");
        Opcoes.Aplicar(info);
        _processo = (Opcoes.Lancador ?? LancadorLocal.Instancia).Abrir(info);
        if (_processo.Id > 0) SessaoClaude.Registrar(_processo.Id);
        var iniciado = _processo;
        _escritor = Task.Run(() => EscreverFilaAsync(iniciado));
        _ = LerErrosAsync(iniciado);
        _ = LerSaidaAsync(iniciado);
        _ = ComecarAsync();
    }

    async Task ComecarAsync()
    {
        var inicio = await PedirCodexAsync("initialize", ProtocoloCodex.Inicializar(versaoDoApp));
        if (inicio.Erro is { } falha)
        {
            Falhar(falha);
            return;
        }
        Escrever(ProtocoloCodex.Aviso("initialized"));
        _ = ListarModelosAsync();
        RespostaCodex resposta;
        if (string.IsNullOrEmpty(Opcoes.Retomar))
            resposta = await PedirCodexAsync("thread/start", ProtocoloCodex.NovaThread(Opcoes.Pasta, _modelo, _modo, Opcoes.Mcp, Opcoes.PromptExtra));
        else
        {
            resposta = await PedirCodexAsync("thread/resume", ProtocoloCodex.Retomar(Opcoes.Retomar, Opcoes.Pasta, _modelo, Opcoes.Mcp));
            if (ProtocoloCodex.Arquivada(resposta.Erro))
            {
                await PedirCodexAsync("thread/unarchive", new JsonObject { ["threadId"] = Opcoes.Retomar });
                resposta = await PedirCodexAsync("thread/resume", ProtocoloCodex.Retomar(Opcoes.Retomar, Opcoes.Pasta, _modelo, Opcoes.Mcp));
            }
        }
        if (resposta.Erro is { } erro)
        {
            Falhar(erro);
            return;
        }
        if (resposta.Resultado is { } r && r.TryGetProperty("thread", out var thread) && thread.TryGetProperty("id", out var id) && id.GetString() is { } threadId)
            _protocolo.ThreadId = threadId;
        if (resposta.Resultado is { } r2 && r2.TryGetProperty("model", out var m) && m.ValueKind == JsonValueKind.String) _protocolo.Modelo = m.GetString();
        if (_protocolo.ThreadId is null)
        {
            Falhar(Idioma.T("O Codex não devolveu a conversa."));
            return;
        }
        Evento?.Invoke(new SessaoIniciada(_protocolo.ThreadId, _protocolo.Modelo, Opcoes.Pasta, _modo));
        List<(string, IReadOnlyList<Anexo>)> pendentes;
        lock (_trava)
        {
            _threadPronta = true;
            pendentes = [.. _antesDaThread];
            _antesDaThread.Clear();
        }
        _pronta.TrySetResult(true);
        foreach (var (texto, imagens) in pendentes) Turno(texto, imagens);
        var limites = await PedirCodexAsync("account/rateLimits/read", null, TimeSpan.FromSeconds(20));
        if (limites.Resultado is { } l && l.TryGetProperty("rateLimits", out var rl) && ProtocoloCodex.Limites(rl) is { } uso) Evento?.Invoke(uso);
    }

    async Task ListarModelosAsync()
    {
        var modelos = new List<ModeloDisponivel>();
        string? cursor = null;
        for (var pagina = 0; pagina < 10; pagina++)
        {
            var parametros = new JsonObject();
            if (cursor is not null) parametros["cursor"] = cursor;
            var resposta = await PedirCodexAsync("model/list", parametros, TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            if (resposta.Resultado is not { } r) break;
            var (lidos, proximo) = ProtocoloCodex.Modelos(r);
            modelos.AddRange(lidos);
            if (string.IsNullOrEmpty(proximo)) break;
            cursor = proximo;
        }
        if (modelos.Count > 0) Evento?.Invoke(new ComandosDisponiveis([], false, modelos));
    }

    void Falhar(string mensagem)
    {
        _pronta.TrySetResult(false);
        Evento?.Invoke(new TurnoEncerrado(true, mensagem, null, 0));
    }

    public void Enviar(string texto, IReadOnlyList<Anexo>? imagens = null)
    {
        lock (_trava)
        {
            if (!_threadPronta)
            {
                _antesDaThread.Add((texto, imagens ?? []));
                return;
            }
        }
        Turno(texto, imagens ?? []);
    }

    void Turno(string texto, IReadOnlyList<Anexo> imagens)
    {
        if (_protocolo.ThreadId is not { } thread) return;
        var caminhos = new List<string>();
        foreach (var imagem in imagens)
        {
            var caminho = Path.Combine(Path.GetTempPath(), $"nexora-codex-{Guid.NewGuid():N}{Extensao(imagem.TipoMidia)}");
            File.WriteAllBytes(caminho, imagem.Dados);
            caminhos.Add(caminho);
            lock (_temporarios) _temporarios.Add(caminho);
        }
        if (_protocolo.TurnoAtual is { } ativo)
        {
            _ = DirecionarAsync(thread, ativo, texto, caminhos);
            return;
        }
        _ = ComecarTurnoAsync(thread, texto, caminhos);
    }

    async Task ComecarTurnoAsync(string thread, string texto, IReadOnlyList<string> caminhos)
    {
        var resposta = await PedirCodexAsync("turn/start", ProtocoloCodex.Turno(thread, texto, caminhos, _modelo, _modo, Opcoes.Esforco, Opcoes.Pasta), TimeSpan.FromMinutes(2)).ConfigureAwait(false);
        if (resposta.Erro is { } erro)
        {
            Falhar(erro);
            return;
        }
        if (resposta.Resultado is { } r && r.TryGetProperty("turn", out var turno) && turno.ValueKind == JsonValueKind.Object
            && turno.TryGetProperty("id", out var id) && id.GetString() is { } turnoId)
            _protocolo.TurnoComecou(turnoId);
    }

    async Task DirecionarAsync(string thread, string turno, string texto, IReadOnlyList<string> caminhos)
    {
        var resposta = await PedirCodexAsync("turn/steer", ProtocoloCodex.Direcionar(thread, turno, texto, caminhos), TimeSpan.FromMinutes(1)).ConfigureAwait(false);
        if (resposta.Erro is null) return;
        if (_protocolo.TurnoAtual is null) await ComecarTurnoAsync(thread, texto, caminhos).ConfigureAwait(false);
        else Evento?.Invoke(new AvisoDoAgente(Idioma.F("O Codex não aceitou a mensagem no meio do turno: {0}", resposta.Erro)));
    }

    static string Extensao(string tipo) => tipo switch
    {
        "image/jpeg" => ".jpg",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        _ => ".png",
    };

    public void Permitir(string pedidoId, JsonNode? entrada, JsonNode? regras = null) => Responder(pedidoId, true, regras is not null, entrada);

    public void Negar(string pedidoId, string motivo) => Responder(pedidoId, false, false, null);

    void Responder(string pedidoId, bool permitir, bool naSessao, JsonNode? entrada)
    {
        if (!_pedidos.TryRemove(pedidoId, out var pedido)) return;
        var resultado = pedido.Metodo switch
        {
            "execCommandApproval" or "applyPatchApproval" => ProtocoloCodex.DecisaoAntiga(permitir, naSessao),
            "item/permissions/requestApproval" => ProtocoloCodex.DecisaoDePermissoes(permitir, naSessao, _protocolo.PermissoesPedidas(pedidoId)),
            "item/tool/requestUserInput" => _protocolo.RespostasDasPerguntas(pedidoId, permitir ? entrada?["answers"] : null),
            _ => ProtocoloCodex.Decisao(permitir, naSessao),
        };
        Escrever(ProtocoloCodex.Resposta(pedido.Id, resultado));
    }

    public void Interromper()
    {
        foreach (var pendente in _pedidos.Keys.ToList()) Responder(pendente, false, false, null);
        if (_protocolo.ThreadId is { } thread && _protocolo.TurnoAtual is { } turno)
            _ = PedirCodexAsync("turn/interrupt", new JsonObject { ["threadId"] = thread, ["turnId"] = turno });
    }

    public void MudarModo(string modo) => _modo = modo;

    public void MudarModelo(string? modelo) => _modelo = modelo;

    public void AplicarConfiguracoes(JsonObject configuracoes)
    {
    }

    public Task<ControleRespondido> PedirAsync(JsonObject pedido, TimeSpan? limite = null) =>
        Task.FromResult(new ControleRespondido("", false, Idioma.T("O Codex não tem esse comando."), null));

    public Task<ControleRespondido> LigarRemotoAsync(bool ligado) =>
        Task.FromResult(new ControleRespondido("", false, Idioma.T("O Remote Control é só do Claude Code."), null));

    async Task<RespostaCodex> PedirCodexAsync(string metodo, JsonObject? parametros, TimeSpan? limite = null)
    {
        var id = Interlocked.Increment(ref _ids);
        var espera = new TaskCompletionSource<RespostaCodex>(TaskCreationOptions.RunContinuationsAsynchronously);
        _esperando[id] = espera;
        Escrever(ProtocoloCodex.Pedido(id, metodo, parametros));
        try
        {
            return await espera.Task.WaitAsync(limite ?? TimeSpan.FromSeconds(45)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _esperando.TryRemove(id, out _);
            return new RespostaCodex(id, null, Idioma.T("O Codex não respondeu a tempo."));
        }
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
                foreach (var mensagem in _protocolo.Ler(linha))
                    switch (mensagem)
                    {
                        case RespostaCodex resposta:
                            if (_esperando.TryRemove(resposta.Id, out var espera)) espera.TrySetResult(resposta);
                            break;
                        case PedidoDoCodex pedido when Respondiveis.Contains(pedido.Metodo):
                            _pedidos[pedido.Id.ToJsonString()] = (pedido.Id, pedido.Metodo);
                            break;
                        case PedidoDoCodex pedido when pedido.Metodo == "mcpServer/elicitation/request":
                            Escrever(ProtocoloCodex.Resposta(pedido.Id, new JsonObject { ["action"] = "decline" }));
                            break;
                        case PedidoDoCodex pedido:
                            Escrever(ProtocoloCodex.Recusa(pedido.Id, Idioma.F("O Nexora ainda não responde a {0}.", pedido.Metodo)));
                            break;
                        case EventoDoCodex evento:
                            Evento?.Invoke(evento.Evento);
                            break;
                    }
            await processo.WaitForExitAsync();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
        foreach (var id in _esperando.Keys.ToList())
            if (_esperando.TryRemove(id, out var pendente)) pendente.TrySetResult(new RespostaCodex(id, null, Idioma.T("O Codex fechou.")));
        _pedidos.Clear();
        if (_protocolo.Abandonar(Idioma.T("O Codex fechou no meio do turno.")) is { } abandonado) Evento?.Invoke(abandonado);
        SessaoClaude.Esquecer(processo.Id);
        _pronta.TrySetResult(false);
        LimparTemporarios();
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

    void LimparTemporarios()
    {
        lock (_temporarios)
        {
            foreach (var arquivo in _temporarios)
                try { File.Delete(arquivo); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            _temporarios.Clear();
        }
    }

    public void Encerrar()
    {
        if (_processo is not { } processo) return;
        try
        {
            _fila.Writer.TryComplete();
            _escritor?.Wait(TimeSpan.FromSeconds(1));
            processo.StandardInput.Close();
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
            processo.StandardInput.Close();
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
}
