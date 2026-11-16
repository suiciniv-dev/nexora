using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Threading.Channels;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.Core.Nuvem;

public enum EstadoDaNuvem
{
    Conectando,
    Sincronizando,
    Iniciando,
    Rodando,
    Desconectado,
    Terminou,
    Falhou,
}

public sealed record MudancaDaNuvem(EstadoDaNuvem Estado, string? Detalhe = null);

public sealed record EstadoDaExecucao(string Situacao, int Codigo, long Tamanho)
{
    public bool Rodando => Situacao == "rodando";
    public bool Terminou => Situacao == "fim";
    public bool Existe => Situacao != "nenhum";

    public static EstadoDaExecucao Ler(string saida)
    {
        var partes = saida.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length < 3) return new EstadoDaExecucao("nenhum", 0, 0);
        _ = int.TryParse(partes[1], out var codigo);
        _ = long.TryParse(partes[2], out var tamanho);
        return new EstadoDaExecucao(partes[0], codigo, tamanho);
    }
}

public sealed record PlanoDaExecucao(
    string Id,
    string Pasta,
    IReadOnlyDictionary<string, string> Ambiente,
    Func<Action<MudancaDaNuvem>, CancellationToken, Task>? Preparar = null,
    bool ContinuarDoTurno = true,
    long? Desde = null);

public sealed class LancadorRemoto(IMaquinaRemota maquina, PlanoDaExecucao plano) : ILancadorDeAgente
{
    public bool Remoto => true;
    public IMaquinaRemota Maquina { get; } = maquina;
    public PlanoDaExecucao Plano { get; set; } = plano;
    public ProcessoRemoto? Ultimo { get; private set; }
    public event Action<MudancaDaNuvem>? Mudou;

    public IProcessoDoAgente Abrir(ProcessStartInfo info)
    {
        var processo = new ProcessoRemoto(Maquina, Plano, Comando(info));
        processo.Mudou += m => Mudou?.Invoke(m);
        Ultimo = processo;
        processo.Comecar();
        return processo;
    }

    public static string Comando(ProcessStartInfo info) => Comando(info.FileName, [.. info.ArgumentList]);

    public static string Comando(string programa, IReadOnlyList<string> lista)
    {
        var argumentos = lista.ToList();
        var nome = Path.GetFileName(programa);
        if ((nome.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase) || nome.Equals("cmd", StringComparison.OrdinalIgnoreCase)) && argumentos.Count > 1 && argumentos[0] == "/c")
        {
            programa = argumentos[1];
            argumentos = argumentos[2..];
        }
        var simples = Path.GetFileName(programa.Replace('\\', '/'));
        foreach (var extensao in new[] { ".exe", ".cmd", ".bat" })
            if (simples.EndsWith(extensao, StringComparison.OrdinalIgnoreCase)) simples = simples[..^extensao.Length];
        return "exec " + Shell.Comando(simples, argumentos) + "\n";
    }
}

public sealed class ProcessoRemoto : IProcessoDoAgente
{
    static int _ids;

    readonly IMaquinaRemota _maquina;
    readonly PlanoDaExecucao _plano;
    readonly string _comando;
    readonly AnonymousPipeServerStream _saida = new(PipeDirection.Out);
    readonly AnonymousPipeServerStream _erros = new(PipeDirection.Out);
    readonly Channel<byte[]> _linhas = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    readonly CancellationTokenSource _fim = new();
    readonly TaskCompletionSource _saiu = new(TaskCreationOptions.RunContinuationsAsynchronously);
    long _lidos;
    CancellationTokenSource? _queda;
    CancellationTokenSource? _espera;
    int _codigo;
    volatile bool _terminou;

    public ProcessoRemoto(IMaquinaRemota maquina, PlanoDaExecucao plano, string comando)
    {
        _maquina = maquina;
        _plano = plano;
        _comando = comando;
        Id = Interlocked.Decrement(ref _ids);
        StandardOutput = new StreamReader(new AnonymousPipeClientStream(PipeDirection.In, _saida.ClientSafePipeHandle), Encoding.UTF8);
        StandardError = new StreamReader(new AnonymousPipeClientStream(PipeDirection.In, _erros.ClientSafePipeHandle), Encoding.UTF8);
        StandardInput = new StreamWriter(new FluxoDeLinhas(l => _linhas.Writer.TryWrite(l)), new UTF8Encoding(false));
    }

    public event Action<MudancaDaNuvem>? Mudou;

    public static TimeSpan[] Esperas { get; set; } = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30)];

    public string Execucao => _plano.Id;
    public long Lidos => Interlocked.Read(ref _lidos);
    public EstadoDaNuvem Estado { get; private set; } = EstadoDaNuvem.Conectando;
    public int Id { get; }
    public bool HasExited => _terminou;
    public int ExitCode => _codigo;
    public StreamWriter StandardInput { get; }
    public StreamReader StandardOutput { get; }
    public StreamReader StandardError { get; }

    public void Comecar() => _ = Task.Run(RodarAsync);

    public Task WaitForExitAsync(CancellationToken cancelamento = default) => _saiu.Task.WaitAsync(cancelamento);

    public bool WaitForExit(int milissegundos) => _saiu.Task.Wait(milissegundos);

    public void Kill(bool entireProcessTree) => Soltar(0, null);

    public void Dispose() => Soltar(0, null);

    public Task<ResultadoRemoto> PararNaMaquinaAsync(CancellationToken cancelamento = default) =>
        _maquina.RodarAsync(ScriptRemoto.Chamar("parar", _plano.Id), null, cancelamento);

    void Avisar(EstadoDaNuvem estado, string? detalhe = null)
    {
        Estado = estado;
        Mudou?.Invoke(new MudancaDaNuvem(estado, detalhe));
    }

    async Task RodarAsync()
    {
        var token = _fim.Token;
        try
        {
            var inicio = await PrepararAsync(token).ConfigureAwait(false);
            if (inicio is null) return;
            Interlocked.Exchange(ref _lidos, inicio.Value);
            _ = Task.Run(() => EscreverAsync(token), CancellationToken.None);
            await LerAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or ObjectDisposedException)
        {
            Falhar(ex.Message);
        }
    }

    async Task<long?> PrepararAsync(CancellationToken token)
    {
        var tentativa = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            Avisar(EstadoDaNuvem.Conectando);
            try
            {
                await _maquina.GarantirAjudanteAsync(token).ConfigureAwait(false);
                var estado = await EstadoAsync(token).ConfigureAwait(false);
                if (estado.Rodando)
                {
                    if (_plano.Desde is { } desde) return desde;
                    if (!_plano.ContinuarDoTurno) return estado.Tamanho;
                    var turno = await _maquina.RodarAsync(ScriptRemoto.Chamar("inicio-do-turno", _plano.Id), null, token).ConfigureAwait(false);
                    return turno.Ok && long.TryParse(turno.Saida.Trim(), out var posicao) ? posicao : estado.Tamanho;
                }
                if (_plano.Preparar is { } preparar)
                {
                    Avisar(EstadoDaNuvem.Sincronizando);
                    await preparar(m => Avisar(m.Estado, m.Detalhe), token).ConfigureAwait(false);
                }
                Avisar(EstadoDaNuvem.Iniciando);
                var entrada = new StringBuilder();
                foreach (var (nome, valor) in _plano.Ambiente)
                    if (!valor.Contains('\n') && !valor.Contains('\r')) entrada.Append(nome).Append('=').Append(valor).Append('\n');
                entrada.Append('\n').Append(_comando);
                var iniciado = await _maquina.RodarAsync(ScriptRemoto.Chamar("iniciar", _plano.Id, _plano.Pasta), entrada.ToString(), token).ConfigureAwait(false);
                entrada.Clear();
                if (iniciado.CaiuAConexao) throw new ConexaoCaiuException(MaquinaRemota.ExplicarFalha(iniciado));
                if (!iniciado.Ok) throw new InvalidOperationException(MaquinaRemota.ExplicarFalha(iniciado));
                return 0;
            }
            catch (ConexaoCaiuException ex)
            {
                Avisar(EstadoDaNuvem.Desconectado, ex.Message);
                await EsperarAsync(tentativa++, token).ConfigureAwait(false);
            }
        }
    }

    async Task<EstadoDaExecucao> EstadoAsync(CancellationToken token)
    {
        var resultado = await _maquina.RodarAsync(ScriptRemoto.Chamar("estado", _plano.Id), null, token).ConfigureAwait(false);
        if (resultado.CaiuAConexao) throw new ConexaoCaiuException(MaquinaRemota.ExplicarFalha(resultado));
        if (!resultado.Ok) throw new InvalidOperationException(MaquinaRemota.ExplicarFalha(resultado));
        return EstadoDaExecucao.Ler(resultado.Saida);
    }

    async Task LerAsync(CancellationToken token)
    {
        var tentativa = 0;
        while (!token.IsCancellationRequested)
        {
            var viuOFim = false;
            var conectou = false;
            ProcessStartInfo info;
            try
            {
                info = await _maquina.PrepararAsync(ScriptRemoto.Chamar("ler", _plano.Id, Lidos.ToString(System.Globalization.CultureInfo.InvariantCulture)), token).ConfigureAwait(false);
            }
            catch (ConexaoCaiuException ex)
            {
                Avisar(EstadoDaNuvem.Desconectado, ex.Message);
                await EsperarAsync(tentativa++, token).ConfigureAwait(false);
                continue;
            }
            using (var processo = Process.Start(info)
                ?? throw new InvalidOperationException(Idioma.T("O ssh não abriu.")))
            {
                GrupoDeProcessos.Adicionar(processo);
                processo.StandardInput.Close();
                using var queda = CancellationTokenSource.CreateLinkedTokenSource(token);
                _queda = queda;
                using var registro = queda.Token.Register(() => Matar(processo));
                var ultimoSinal = Environment.TickCount64;
                var avisos = Task.Run(async () =>
                {
                    try
                    {
                        while (await processo.StandardError.ReadLineAsync(queda.Token).ConfigureAwait(false) is { } linha)
                        {
                            Interlocked.Exchange(ref ultimoSinal, Environment.TickCount64);
                            if (linha == "nexora-lendo")
                            {
                                conectou = true;
                                tentativa = 0;
                                Avisar(EstadoDaNuvem.Rodando);
                            }
                            else if (linha == "nexora-fim") viuOFim = true;
                        }
                    }
                    catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
                    {
                    }
                }, CancellationToken.None);
                var vigia = Task.Run(async () =>
                {
                    try
                    {
                        while (!queda.IsCancellationRequested)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(5), queda.Token).ConfigureAwait(false);
                            if (Environment.TickCount64 - Interlocked.Read(ref ultimoSinal) > Silencio.TotalMilliseconds) queda.Cancel();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }, CancellationToken.None);
                var buffer = new byte[16384];
                var origem = processo.StandardOutput.BaseStream;
                int lidos;
                try
                {
                    while ((lidos = await origem.ReadAsync(buffer).AsTask().WaitAsync(queda.Token).ConfigureAwait(false)) > 0)
                    {
                        Interlocked.Exchange(ref ultimoSinal, Environment.TickCount64);
                        await _saida.WriteAsync(buffer.AsMemory(0, lidos), CancellationToken.None).ConfigureAwait(false);
                        await _saida.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                        Interlocked.Add(ref _lidos, lidos);
                    }
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
                {
                }
                if (!processo.HasExited) queda.Cancel();
                try
                {
                    await processo.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                    await avisos.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                }
                queda.Cancel();
                await vigia.ConfigureAwait(false);
                _queda = null;
            }
            token.ThrowIfCancellationRequested();
            EstadoDaExecucao? estado = null;
            try
            {
                estado = await EstadoAsync(token).ConfigureAwait(false);
            }
            catch (ConexaoCaiuException)
            {
            }
            if (estado is { Terminou: true } && Lidos >= estado.Tamanho)
            {
                await TerminarAsync(estado.Codigo, token).ConfigureAwait(false);
                return;
            }
            if (estado is { Existe: true, Rodando: false, Terminou: false } || estado is { Existe: false })
            {
                Soltar(-1, Idioma.T("O agente sumiu da máquina (ela reiniciou?). Mande a mensagem de novo para recomeçar com o histórico."));
                return;
            }
            if (viuOFim && estado is { Terminou: true }) continue;
            _maquina.AvisarQueda();
            Avisar(EstadoDaNuvem.Desconectado, conectou ? null : Idioma.T("Tentando de novo…"));
            await EsperarAsync(tentativa++, token).ConfigureAwait(false);
        }
    }

    async Task EscreverAsync(CancellationToken token)
    {
        var tentativa = 0;
        try
        {
            while (await _linhas.Reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                using var lote = new MemoryStream();
                while (_linhas.Reader.TryRead(out var linha)) lote.Write(linha);
                var bytes = lote.ToArray();
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    ResultadoRemoto resultado;
                    try
                    {
                        resultado = await _maquina.EnviarAsync(ScriptRemoto.Chamar("escrever", _plano.Id, bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                            f => f.WriteAsync(bytes, token).AsTask(), token).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
                    {
                        resultado = new ResultadoRemoto(255, "", ex.Message);
                    }
                    if (resultado.Ok)
                    {
                        tentativa = 0;
                        break;
                    }
                    if (resultado.Codigo == 3) return;
                    await EsperarAsync(tentativa++, token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    async Task TerminarAsync(int codigo, CancellationToken token)
    {
        try
        {
            var erros = await _maquina.RodarAsync(ScriptRemoto.Chamar("erros", _plano.Id), null, token).ConfigureAwait(false);
            if (erros.Ok && erros.Saida.Length > 0)
            {
                var bytes = Encoding.UTF8.GetBytes(erros.Saida);
                await _erros.WriteAsync(bytes, token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
        Avisar(EstadoDaNuvem.Terminou);
        Soltar(codigo, null);
    }

    void Falhar(string mensagem)
    {
        Avisar(EstadoDaNuvem.Falhou, mensagem);
        Soltar(1, mensagem);
    }

    void Soltar(int codigo, string? erro)
    {
        lock (_saiu)
        {
            if (_terminou) return;
            _codigo = codigo;
            _terminou = true;
        }
        if (erro is not null)
            try
            {
                var bytes = Encoding.UTF8.GetBytes(erro + "\n");
                _erros.Write(bytes);
            }
            catch (IOException)
            {
            }
        _linhas.Writer.TryComplete();
        _fim.Cancel();
        Fechar(_erros);
        Fechar(_saida);
        _saiu.TrySetResult();
    }

    static void Fechar(Stream fluxo)
    {
        try
        {
            fluxo.Dispose();
        }
        catch (IOException)
        {
        }
    }

    static void Matar(Process processo)
    {
        try
        {
            if (!processo.HasExited) processo.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }

    public void TentarAgora()
    {
        try
        {
            _espera?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    async Task EsperarAsync(int tentativa, CancellationToken token)
    {
        using var espera = CancellationTokenSource.CreateLinkedTokenSource(token);
        _espera = espera;
        try
        {
            await Task.Delay(Esperas[Math.Min(tentativa, Esperas.Length - 1)], espera.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
        }
        finally
        {
            _espera = null;
        }
    }

    public static TimeSpan Silencio { get; set; } = TimeSpan.FromSeconds(45);

    public void DerrubarConexao()
    {
        try
        {
            _queda?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}

public sealed class ConexaoCaiuException(string mensagem) : IOException(mensagem);

sealed class FluxoDeLinhas(Action<byte[]> linha) : Stream
{
    readonly MemoryStream _atual = new();

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        lock (_atual)
            foreach (var b in buffer)
            {
                _atual.WriteByte(b);
                if (b != (byte)'\n') continue;
                linha(_atual.ToArray());
                _atual.SetLength(0);
            }
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Write(buffer.AsSpan(offset, count));
        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }
}
