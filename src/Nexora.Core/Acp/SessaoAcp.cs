using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.Core.Acp;

public sealed class SessaoAcp : ISessaoAgente, IHistoricoDoAgente
{
    public const string Antigravity = "antigravity";
    const string LoginDoAntigravity = "oauth-personal";

    readonly System.Threading.Channels.Channel<string> _fila = System.Threading.Channels.Channel.CreateUnbounded<string>(new System.Threading.Channels.UnboundedChannelOptions { SingleReader = true });
    readonly ConcurrentDictionary<long, TaskCompletionSource<RespostaAcp>> _esperando = new();
    readonly ConcurrentDictionary<string, PermissaoDoAgente> _permissoes = new();
    readonly List<(string Texto, IReadOnlyList<Anexo> Imagens)> _antesDaSessao = [];
    readonly Queue<(string Texto, IReadOnlyList<Anexo> Imagens)> _proximos = new();
    readonly ProtocoloAcp _protocolo = new();
    readonly StringBuilder _erros = new();
    readonly object _trava = new();
    readonly TaskCompletionSource<bool> _pronta = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly List<EventoClaude> _historico = [];
    volatile bool _gravandoHistorico;
    readonly string _agente;
    readonly string _nome;
    readonly string _arquivo;
    readonly IReadOnlyList<string> _argumentos;
    readonly IReadOnlyDictionary<string, string?>? _ambiente;
    readonly string _versaoDoApp;
    IProcessoDoAgente? _processo;
    Task? _escritor;
    Task? _turno;
    TaskCompletionSource<AgenteParou>? _fimDoTurno;
    CapacidadesAcp? _capacidades;
    long _ids;
    long _ultimaAtividade;
    string? _extra;
    string? _modelo;
    string? _modo;
    bool _sessaoPronta;
    volatile bool _falhou;
    volatile bool _fechadoPorFalha;
    bool _rodando;

    static SessaoAcp() => Idioma.Registrar(TextosAcp.Pares);

    public SessaoAcp(OpcoesSessao opcoes, string agente, string nome, string arquivo, IReadOnlyList<string> argumentos, IReadOnlyDictionary<string, string?>? ambiente = null, string versaoDoApp = "0.0.0")
    {
        Opcoes = opcoes;
        _agente = agente;
        _nome = nome;
        _arquivo = arquivo;
        _argumentos = argumentos;
        _ambiente = ambiente;
        _versaoDoApp = versaoDoApp;
        _extra = opcoes.PromptExtra;
        _modelo = opcoes.Modelo;
        _modo = opcoes.Modo;
    }

    public event Action<EventoClaude>? Evento;
    public event Action<int, string>? Encerrou;

    public OpcoesSessao Opcoes { get; }
    public string Agente => _agente;
    public string Nome => _nome;
    public string? SessaoId => _protocolo.SessaoId;
    public bool Ativa => !_falhou && _processo is { HasExited: false };
    public int? Pid => _processo is { HasExited: false } p ? p.Id : null;
    public bool TemRemoto => false;
    public bool TemControles => false;
    public CapacidadesAcp? Capacidades => _capacidades;
    public string? ModoDoAgente => _protocolo.Modo;
    public string? ModeloDoAgente => _protocolo.Modelo;

    public static (string Arquivo, IReadOnlyList<string> Argumentos) Comando(string arquivo, IReadOnlyList<string> argumentos)
    {
        var extensao = Path.GetExtension(arquivo);
        if (OperatingSystem.IsWindows() && (extensao.Equals(".cmd", StringComparison.OrdinalIgnoreCase) || extensao.Equals(".bat", StringComparison.OrdinalIgnoreCase)))
            return ("cmd.exe", ["/c", arquivo, .. argumentos]);
        return (arquivo, argumentos);
    }

    public void Iniciar()
    {
        var (arquivo, argumentos) = Opcoes.Lancador is { Remoto: true } ? (_arquivo, _argumentos) : Comando(_arquivo, _argumentos);
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
        Opcoes.Aplicar(info);
        if (_ambiente is not null)
            foreach (var (chave, valor) in _ambiente)
                if (valor is null) info.Environment.Remove(chave);
                else info.Environment[chave] = valor;
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
        var inicio = await PedirAcpAsync("initialize", ProtocoloAcp.Inicializar(_versaoDoApp), TimeSpan.FromSeconds(60)).ConfigureAwait(false);
        if (inicio.Erro is { } falha)
        {
            Falhar(falha);
            return;
        }
        var capacidades = ProtocoloAcp.Capacidades(inicio.Resultado ?? default);
        _capacidades = capacidades;
        lock (_protocolo) _protocolo.Geracao = capacidades.Geracao;
        if (_agente == Antigravity && capacidades.Logins.Any(m => m.Id == LoginDoAntigravity))
        {
            var login = await AutenticarAsync(LoginDoAntigravity).ConfigureAwait(false);
            if (login.Erro is { } erroDeLogin)
            {
                Falhar(erroDeLogin);
                return;
            }
        }
        var mcp = ProtocoloAcp.ServidoresMcp(LerMcp(), capacidades);
        var resposta = await AbrirAsync(capacidades, mcp).ConfigureAwait(false);
        if (resposta.Erro is { } erro)
        {
            Falhar(erro);
            return;
        }
        IReadOnlyList<EventoClaude> eventos;
        lock (_protocolo) eventos = _protocolo.LerSessao(resposta.Resultado ?? default);
        if (_protocolo.SessaoId is not { } sessao)
        {
            Falhar(Idioma.T(TextosAcp.SemConversa));
            return;
        }
        await AplicarModoAsync(_modo).ConfigureAwait(false);
        await AplicarModeloAsync(_modelo).ConfigureAwait(false);
        Evento?.Invoke(new SessaoIniciada(sessao, _protocolo.Modelo, Opcoes.Pasta, _modo));
        Emitir(eventos);
        List<(string, IReadOnlyList<Anexo>)> pendentes;
        lock (_trava)
        {
            _sessaoPronta = true;
            pendentes = [.. _antesDaSessao];
            _antesDaSessao.Clear();
        }
        _pronta.TrySetResult(true);
        foreach (var (texto, imagens) in pendentes) Enviar(texto, imagens);
    }

    async Task<RespostaAcp> AbrirAsync(CapacidadesAcp capacidades, JsonArray mcp)
    {
        var nova = () => PedirComLoginAsync("session/new", ProtocoloAcp.NovaSessao(Opcoes.Pasta, mcp.DeepClone().AsArray(), Opcoes.Diretorios, capacidades));
        if (string.IsNullOrEmpty(Opcoes.Retomar)) return await nova().ConfigureAwait(false);
        RespostaAcp? retomada = null;
        if (capacidades.CarregarSessao)
            retomada = await CarregarAsync(ProtocoloAcp.CarregarSessao(Opcoes.Retomar, Opcoes.Pasta, mcp.DeepClone().AsArray(), Opcoes.Diretorios, capacidades)).ConfigureAwait(false);
        else if (capacidades.RetomarSessao)
            retomada = await PedirComLoginAsync("session/resume", ProtocoloAcp.RetomarSessao(Opcoes.Retomar, Opcoes.Pasta, mcp.DeepClone().AsArray(), Opcoes.Diretorios, capacidades)).ConfigureAwait(false);
        if (retomada is { Erro: null })
        {
            lock (_protocolo) _protocolo.SessaoId ??= Opcoes.Retomar;
            return retomada;
        }
        lock (_protocolo) _protocolo.SessaoId = null;
        Evento?.Invoke(new AvisoDoAgente(Idioma.F(TextosAcp.NaoReabriu, _nome)));
        return await nova().ConfigureAwait(false);
    }

    async Task<RespostaAcp> CarregarAsync(JsonObject parametros)
    {
        _gravandoHistorico = true;
        lock (_protocolo)
        {
            _protocolo.SessaoId = Opcoes.Retomar;
            _protocolo.Repetindo = true;
        }
        var desde = Environment.TickCount64;
        Interlocked.Exchange(ref _ultimaAtividade, 0);
        var pedido = PedirComLoginAsync(ProtocoloAcp.Metodo("session/load", _protocolo.Geracao), parametros, TimeSpan.FromSeconds(90));
        while (!pedido.IsCompleted && Environment.TickCount64 - desde < 90_000)
        {
            await Task.WhenAny(pedido, Task.Delay(200)).ConfigureAwait(false);
            var ultima = Interlocked.Read(ref _ultimaAtividade);
            if (ultima > 0 && Environment.TickCount64 - ultima > 2000) break;
        }
        IReadOnlyList<EventoClaude> restantes;
        lock (_protocolo)
        {
            _protocolo.Repetindo = false;
            restantes = [.. _protocolo.Descarregar()];
        }
        Emitir(restantes);
        _gravandoHistorico = false;
        return pedido.IsCompleted ? await pedido.ConfigureAwait(false) : new RespostaAcp(0, null, null);
    }

    async Task<RespostaAcp> PedirComLoginAsync(string metodo, JsonObject parametros, TimeSpan? limite = null)
    {
        var resposta = await PedirAcpAsync(metodo, parametros.DeepClone().AsObject(), limite ?? TimeSpan.FromMinutes(2)).ConfigureAwait(false);
        if (resposta.Codigo != ProtocoloAcp.ErroDeLogin) return resposta;
        if (_capacidades is { } capacidades && ProtocoloAcp.LoginDoAgente(capacidades) is { } login)
        {
            var entrou = await AutenticarAsync(login.Id).ConfigureAwait(false);
            if (entrou.Erro is null) resposta = await PedirAcpAsync(metodo, parametros.DeepClone().AsObject(), limite ?? TimeSpan.FromMinutes(2)).ConfigureAwait(false);
        }
        return resposta.Codigo == ProtocoloAcp.ErroDeLogin ? resposta with { Erro = ProtocoloAcp.ErroDeLoginComDetalhe(_nome, resposta.Erro) } : resposta;
    }

    Task<RespostaAcp> AutenticarAsync(string metodo) =>
        PedirAcpAsync(ProtocoloAcp.Metodo("authenticate", _protocolo.Geracao), new JsonObject { ["methodId"] = metodo }, TimeSpan.FromMinutes(5));

    string? LerMcp()
    {
        var mcp = Opcoes.Mcp;
        if (string.IsNullOrWhiteSpace(mcp)) return null;
        if (mcp.TrimStart().StartsWith('{')) return mcp;
        try
        {
            return File.Exists(mcp) ? File.ReadAllText(mcp) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    void Falhar(string mensagem)
    {
        _falhou = true;
        _pronta.TrySetResult(false);
        lock (_trava) _antesDaSessao.Clear();
        Evento?.Invoke(new TurnoEncerrado(true, mensagem, null, 0));
        if (_processo is not { HasExited: false }) return;
        _fechadoPorFalha = true;
        _ = Task.Run(Encerrar);
    }

    void Emitir(IEnumerable<EventoClaude> eventos)
    {
        foreach (var e in eventos) Soltar(e);
    }

    void Soltar(EventoClaude evento)
    {
        if (_gravandoHistorico)
        {
            lock (_historico) _historico.Add(evento);
            return;
        }
        Evento?.Invoke(evento);
    }

    public async Task<IReadOnlyList<EventoClaude>> LerHistoricoAsync(TimeSpan? espera = null)
    {
        try
        {
            await _pronta.Task.WaitAsync(espera ?? TimeSpan.FromSeconds(120)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }
        lock (_historico) return [.. _historico];
    }

    public void Enviar(string texto, IReadOnlyList<Anexo>? imagens = null)
    {
        lock (_trava)
        {
            if (!_sessaoPronta)
            {
                _antesDaSessao.Add((texto, imagens ?? []));
                return;
            }
            if (_rodando)
            {
                _proximos.Enqueue((texto, imagens ?? []));
                return;
            }
            _rodando = true;
            _turno = TurnoAsync(texto, imagens ?? []);
        }
    }

    async Task TurnoAsync(string texto, IReadOnlyList<Anexo> imagens)
    {
        await Task.Yield();
        try
        {
            if (_protocolo.SessaoId is not { } sessao) return;
            var aceita = _capacidades?.Imagem == true;
            if (imagens.Count > 0 && !aceita) Evento?.Invoke(new AvisoDoAgente(Idioma.F(TextosAcp.SemImagens, _nome)));
            var extra = Interlocked.Exchange(ref _extra, null);
            var fim = new TaskCompletionSource<AgenteParou>(TaskCreationOptions.RunContinuationsAsynchronously);
            _fimDoTurno = fim;
            var relogio = Stopwatch.StartNew();
            var resposta = await PedirAcpAsync("session/prompt", ProtocoloAcp.Prompt(sessao, extra, texto, imagens, aceita), Timeout.InfiniteTimeSpan).ConfigureAwait(false);
            string? motivo = null;
            var erro = resposta.Erro;
            if (erro is null)
            {
                if (resposta.Resultado is { ValueKind: JsonValueKind.Object } r && r.TryGetProperty("stopReason", out var s) && s.ValueKind == JsonValueKind.String) motivo = s.GetString();
                else if (_protocolo.Geracao >= 2)
                {
                    var parou = await fim.Task.ConfigureAwait(false);
                    motivo = parou.Motivo;
                    erro = parou.Erro;
                }
            }
            _fimDoTurno = null;
            IReadOnlyList<EventoClaude> eventos;
            lock (_protocolo) eventos = _protocolo.Fim(motivo, erro, relogio.ElapsedMilliseconds);
            Emitir(eventos);
        }
        finally
        {
            lock (_trava)
            {
                if (_proximos.TryDequeue(out var proximo)) _turno = TurnoAsync(proximo.Texto, proximo.Imagens);
                else _rodando = false;
            }
        }
    }

    public void Permitir(string pedidoId, JsonNode? entrada, JsonNode? regras = null) => Responder(pedidoId, true, regras is not null);

    public void Negar(string pedidoId, string motivo) => Responder(pedidoId, false, false);

    void Responder(string pedidoId, bool permitir, bool sempre)
    {
        if (!_permissoes.TryRemove(pedidoId, out var pedido)) return;
        Escrever(ProtocoloAcp.Resposta(pedido.Id, ProtocoloAcp.Escolha(pedido.Opcoes, permitir, sempre)));
    }

    public void Interromper()
    {
        if (_protocolo.SessaoId is { } sessao) Escrever(ProtocoloAcp.Aviso("session/cancel", new JsonObject { ["sessionId"] = sessao }));
        foreach (var id in _permissoes.Keys.ToList())
            if (_permissoes.TryRemove(id, out var pedido)) Escrever(ProtocoloAcp.Resposta(pedido.Id, ProtocoloAcp.Cancelado()));
    }

    public void MudarModo(string modo)
    {
        _modo = modo;
        if (_sessaoPronta) _ = AplicarModoAsync(modo);
    }

    public void MudarModelo(string? modelo)
    {
        _modelo = modelo;
        if (_sessaoPronta) _ = AplicarModeloAsync(modelo);
    }

    async Task AplicarModoAsync(string? modo)
    {
        (string Metodo, JsonObject Parametros)? pedido;
        lock (_protocolo) pedido = _protocolo.PedidoDeModo(modo);
        if (pedido is not { } p) return;
        var escolhido = p.Parametros["modeId"]?.GetValue<string>() ?? p.Parametros["value"]?.GetValue<string>();
        var resposta = await PedirAcpAsync(p.Metodo, p.Parametros, TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        if (resposta.Erro is not null) return;
        lock (_protocolo)
        {
            _protocolo.Modo = escolhido;
            if (resposta.Resultado is { ValueKind: JsonValueKind.Object } r && r.TryGetProperty("configOptions", out var opcoes)) _protocolo.LerConfiguracoes(opcoes);
        }
    }

    async Task AplicarModeloAsync(string? modelo)
    {
        (string Metodo, JsonObject Parametros)? pedido;
        lock (_protocolo) pedido = _protocolo.PedidoDeModelo(modelo);
        if (pedido is not { } p) return;
        var resposta = await PedirAcpAsync(p.Metodo, p.Parametros, TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        if (resposta.Erro is not null) return;
        lock (_protocolo)
        {
            _protocolo.Modelo = modelo;
            if (resposta.Resultado is { ValueKind: JsonValueKind.Object } r && r.TryGetProperty("configOptions", out var opcoes)) _protocolo.LerConfiguracoes(opcoes);
        }
    }

    public void AplicarConfiguracoes(JsonObject configuracoes)
    {
    }

    public Task<ControleRespondido> PedirAsync(Func<string, string> montar, TimeSpan? limite = null) =>
        Task.FromResult(new ControleRespondido("", false, Idioma.T(TextosAcp.SemComando), null));

    public Task<ControleRespondido> LigarRemotoAsync(bool ligado) =>
        Task.FromResult(new ControleRespondido("", false, Idioma.T(TextosAcp.SoClaude), null));

    async Task<RespostaAcp> PedirAcpAsync(string metodo, JsonObject? parametros, TimeSpan? limite = null)
    {
        var id = Interlocked.Increment(ref _ids);
        var espera = new TaskCompletionSource<RespostaAcp>(TaskCreationOptions.RunContinuationsAsynchronously);
        _esperando[id] = espera;
        if (_processo is not { HasExited: false })
        {
            _esperando.TryRemove(id, out _);
            return new RespostaAcp(id, null, Idioma.T(TextosAcp.Fechou));
        }
        Escrever(ProtocoloAcp.Pedido(id, metodo, parametros));
        try
        {
            return await espera.Task.WaitAsync(limite ?? TimeSpan.FromSeconds(45)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _esperando.TryRemove(id, out _);
            return new RespostaAcp(id, null, Idioma.T(TextosAcp.NaoRespondeu));
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
            while (await processo.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } linha)
            {
                Interlocked.Exchange(ref _ultimaAtividade, Environment.TickCount64);
                IReadOnlyList<MensagemAcp> mensagens;
                lock (_protocolo) mensagens = _protocolo.Ler(linha);
                foreach (var mensagem in mensagens) Tratar(mensagem);
            }
            await processo.WaitForExitAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
        foreach (var id in _esperando.Keys.ToList())
            if (_esperando.TryRemove(id, out var pendente)) pendente.TrySetResult(new RespostaAcp(id, null, Idioma.T(TextosAcp.Fechou)));
        _fimDoTurno?.TrySetResult(new AgenteParou(null, Idioma.T(TextosAcp.Fechou)));
        _permissoes.Clear();
        SessaoClaude.Esquecer(processo.Id);
        _pronta.TrySetResult(false);
        Task? turno;
        lock (_trava)
        {
            _proximos.Clear();
            turno = _turno;
        }
        if (turno is not null)
            try
            {
                await turno.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
        string erros;
        lock (_erros) erros = _erros.ToString().Trim();
        if (_fechadoPorFalha) return;
        Encerrou?.Invoke(processo.HasExited ? processo.ExitCode : -1, ProtocoloAcp.ExplicarErros(erros, _nome));
    }

    void Tratar(MensagemAcp mensagem)
    {
        switch (mensagem)
        {
            case RespostaAcp resposta:
                if (_esperando.TryRemove(resposta.Id, out var espera)) espera.TrySetResult(resposta);
                break;
            case PermissaoDoAgente permissao:
                _permissoes[permissao.PedidoId] = permissao;
                break;
            case PedidoDoAgente { Metodo: "fs/read_text_file" } leitura:
            {
                var (resultado, erro) = ProtocoloAcp.LerArquivo(leitura.Parametros, [Opcoes.Pasta, .. Opcoes.Diretorios ?? []]);
                Escrever(resultado is not null ? ProtocoloAcp.Resposta(leitura.Id, resultado) : ProtocoloAcp.Recusa(leitura.Id, erro ?? "", -32603));
                break;
            }
            case PedidoDoAgente pedido:
                Escrever(ProtocoloAcp.Recusa(pedido.Id, Idioma.F(TextosAcp.NaoResponde, pedido.Metodo)));
                break;
            case EventoDoAcp evento:
                Soltar(evento.Evento);
                break;
            case AgenteParou parou:
                _fimDoTurno?.TrySetResult(parou);
                break;
            case LinkDeLogin link:
                Evento?.Invoke(new AvisoDoAgente(Idioma.F(TextosAcp.AbraOLink, _nome, link.Url)));
                break;
        }
    }

    async Task LerErrosAsync(IProcessoDoAgente processo)
    {
        try
        {
            while (await processo.StandardError.ReadLineAsync().ConfigureAwait(false) is { } linha)
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
