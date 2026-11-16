using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Nexora.Core.Claude;
using Nexora.Core.Executar;

namespace Nexora.Core.Nuvem;

public sealed class TunelSsh : IDisposable
{
    readonly IMaquinaRemota _maquina;
    readonly CancellationTokenSource _fim = new();
    readonly TaskCompletionSource<bool> _aberto = new(TaskCreationOptions.RunContinuationsAsynchronously);
    Process? _processo;

    public TunelSsh(IMaquinaRemota maquina, int portaLocal, int portaRemota)
    {
        _maquina = maquina;
        PortaLocal = portaLocal;
        PortaRemota = portaRemota;
        _ = Task.Run(ManterAsync);
    }

    public int PortaLocal { get; }
    public int PortaRemota { get; }
    public Task<bool> Aberto => _aberto.Task;

    async Task ManterAsync()
    {
        var tentativa = 0;
        while (!_fim.IsCancellationRequested)
        {
            try
            {
                var info = await _maquina.TunelAsync(PortaLocal, PortaRemota, _fim.Token).ConfigureAwait(false);
                if (info is null)
                {
                    _aberto.TrySetResult(false);
                    return;
                }
                using var processo = Process.Start(info) ?? throw new InvalidOperationException(Texto.Idioma.T("O ssh não abriu."));
                GrupoDeProcessos.Adicionar(processo);
                _processo = processo;
                processo.StandardInput.Close();
                _ = processo.StandardOutput.ReadToEndAsync();
                _ = processo.StandardError.ReadToEndAsync();
                _aberto.TrySetResult(true);
                var inicio = Stopwatch.StartNew();
                await processo.WaitForExitAsync(_fim.Token).ConfigureAwait(false);
                if (inicio.Elapsed > TimeSpan.FromMinutes(1)) tentativa = 0;
                if (processo.ExitCode == 255) _maquina.AvisarQueda();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
            try
            {
                var esperas = ProcessoRemoto.Esperas;
                await Task.Delay(esperas[Math.Min(tentativa++, esperas.Length - 1)], _fim.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public void Dispose()
    {
        if (_fim.IsCancellationRequested) return;
        _fim.Cancel();
        _aberto.TrySetResult(false);
        try
        {
            if (_processo is { HasExited: false } processo) processo.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }
}

public sealed partial class ExecucaoNaNuvem(
    ServicoDaNuvem nuvem,
    MaquinaNaNuvem maquina,
    Alvo alvo,
    int? porta,
    string pastaLocal,
    string pastaRemota,
    IReadOnlyDictionary<string, string>? ambiente = null) : IExecucaoDeAlvo
{
    readonly Dictionary<int, TunelSsh> _tuneis = [];
    readonly object _trava = new();
    IMaquinaRemota? _remota;
    ProcessoRemoto? _processo;
    AvisoDeTrabalho? _acordada;
    bool _encerrada;

    public event Action<string, bool>? Linha;
    public event Action<string>? Ouvindo;
    public event Action<int>? Terminou;
    public event Action<MudancaDaNuvem>? Mudou;

    public string Id { get; } = "executar-" + ServicoDaNuvem.NovaExecucao();
    public MaquinaNaNuvem Maquina { get; } = maquina;
    public string PastaRemota { get; } = pastaRemota;
    public IReadOnlyCollection<TunelSsh> Tuneis
    {
        get
        {
            lock (_trava) return [.. _tuneis.Values];
        }
    }

    public static string PastaDoAlvo(string pastaRemota, string pastaLocal, string pastaDoAlvo)
    {
        var relativa = Path.GetRelativePath(pastaLocal, pastaDoAlvo);
        if (Path.IsPathRooted(relativa) || relativa == ".." || relativa.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || relativa.StartsWith("../", StringComparison.Ordinal))
            throw new InvalidOperationException(Texto.Idioma.T("Na nuvem só roda o que está dentro da pasta do projeto."));
        return relativa == "." ? pastaRemota : pastaRemota.TrimEnd('/') + "/" + relativa.Replace('\\', '/');
    }

    public static string Comando(Alvo alvo, int? porta) =>
        "exec 2>&1\n" + LancadorRemoto.Comando(alvo.Comando, alvo.ArgumentosCom(porta));

    public static int? PortaDoEndereco(string linha) =>
        Endereco().Match(linha) is { Success: true } achou && int.TryParse(achou.Groups[2].Value, out var p) && p is > 0 and < 65536 ? p : null;

    public void Iniciar()
    {
        var pastaDoAlvo = PastaDoAlvo(PastaRemota, pastaLocal, alvo.Pasta);
        _remota = nuvem.Abrir(Maquina);
        var variaveis = new Dictionary<string, string>(ambiente ?? new Dictionary<string, string>(), StringComparer.Ordinal);
        foreach (var (chave, valor) in alvo.VariaveisCom(porta)) variaveis[chave] = valor;
        var plano = new PlanoDaExecucao(Id, pastaDoAlvo, variaveis, PrepararAsync, ContinuarDoTurno: false);
        var processo = new ProcessoRemoto(_remota, plano, Comando(alvo, porta));
        processo.Mudou += m => Mudou?.Invoke(m);
        _processo = processo;
        _acordada = nuvem.ManterAcordada(Maquina, Id);
        processo.Comecar();
        _ = LerAsync(processo);
    }

    async Task PrepararAsync(Action<MudancaDaNuvem> avisar, CancellationToken token)
    {
        var remota = _remota!;
        var existe = await remota.RodarAsync("test -d " + Shell.Caminho(PastaRemota.TrimEnd('/') + "/.git") + " && echo sim || echo nao", null, token).ConfigureAwait(false);
        if (existe.CaiuAConexao) throw new ConexaoCaiuException(MaquinaRemota.ExplicarFalha(existe));
        if (existe.Saida.Trim() == "sim")
        {
            Linha?.Invoke(Texto.Idioma.F("Rodando o código que já está na máquina, em {0}.", PastaRemota), false);
            return;
        }
        Linha?.Invoke(Texto.Idioma.T("Enviando o código deste PC para a máquina…"), false);
        await CodigoNaNuvem.EnviarAsync(remota, pastaLocal, PastaRemota, t => avisar(new MudancaDaNuvem(EstadoDaNuvem.Sincronizando, t)), token).ConfigureAwait(false);
    }

    async Task LerAsync(ProcessoRemoto processo)
    {
        try
        {
            while (await processo.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } bruta)
            {
                var linha = Alvos.Ansi().Replace(bruta, "");
                Linha?.Invoke(linha, false);
                if (PortaDoEndereco(linha) is { } portaRemota) await AbrirTunelAsync(linha, portaRemota).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
        try
        {
            await processo.WaitForExitAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }
        if (processo.ExitCode != 0)
            try
            {
                var erros = await _remota!.RodarAsync(ScriptRemoto.Chamar("erros", Id)).ConfigureAwait(false);
                foreach (var linha in erros.Saida.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries)) Linha?.Invoke(linha, true);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
        Terminou?.Invoke(processo.ExitCode);
        await EncerrarAsync(esquecer: true).ConfigureAwait(false);
    }

    async Task AbrirTunelAsync(string linha, int portaRemota)
    {
        TunelSsh? tunel;
        lock (_trava)
        {
            if (_encerrada || _tuneis.ContainsKey(portaRemota)) return;
            var local = Execucao.PortaOcupada(portaRemota) ? PortaLivre() : portaRemota;
            _tuneis[portaRemota] = tunel = new TunelSsh(_remota!, local, portaRemota);
        }
        var url = Endereco().Match(linha).Value;
        if (await tunel.Aberto.ConfigureAwait(false))
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var original)) return;
            url = new UriBuilder(original) { Host = "localhost", Port = tunel.PortaLocal }.Uri.ToString();
        }
        Ouvindo?.Invoke(url);
    }

    static int PortaLivre()
    {
        using var ouvinte = new TcpListener(IPAddress.Loopback, 0);
        ouvinte.Start();
        return ((IPEndPoint)ouvinte.LocalEndpoint).Port;
    }

    public void Parar()
    {
        var processo = _processo;
        if (processo is null) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await processo.PararNaMaquinaAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                processo.Kill(true);
            }
        });
    }

    async Task EncerrarAsync(bool esquecer)
    {
        List<TunelSsh> tuneis;
        lock (_trava)
        {
            if (_encerrada) return;
            _encerrada = true;
            tuneis = [.. _tuneis.Values];
            _tuneis.Clear();
        }
        foreach (var tunel in tuneis) tunel.Dispose();
        if (_acordada is { } acordada) await acordada.DisposeAsync().ConfigureAwait(false);
        if (esquecer && _remota is { } remota)
            try
            {
                await remota.RodarAsync(ScriptRemoto.Chamar("esquecer", Id)).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
    }

    public void Dispose()
    {
        _processo?.Kill(true);
        _ = EncerrarAsync(esquecer: false);
    }

    [GeneratedRegex(@"https?://(localhost|127\.0\.0\.1|0\.0\.0\.0|\[::\]|\[::1\]):(\d{2,5})[^\s'"")]*", RegexOptions.IgnoreCase)]
    private static partial Regex Endereco();
}
