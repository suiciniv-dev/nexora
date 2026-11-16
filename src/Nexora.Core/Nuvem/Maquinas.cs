using System.Diagnostics;
using System.Text;

namespace Nexora.Core.Nuvem;

public static class TiposDeMaquina
{
    public const string Ssh = "ssh";
    public const string InstaBox = "instabox";
    public const string Freestyle = "freestyle";
    public const string Teste = "teste";
}

public sealed record MaquinaNaNuvem(
    string Id,
    string Nome,
    string Tipo,
    string Host = "",
    string Usuario = "",
    int Porta = 22,
    string? Home = null,
    DateTime? TestadaEm = null,
    string? Resumo = null,
    string? IdNoProvedor = null,
    string? Modelo = null)
{
    public string Endereco => Tipo switch
    {
        TiposDeMaquina.Teste => Nome,
        TiposDeMaquina.InstaBox => "InstaBox" + (string.IsNullOrEmpty(Modelo) ? "" : " · " + Modelo) + " · " + IdNoProvedor,
        _ => (string.IsNullOrEmpty(Usuario) ? Host : Usuario + "@" + Host) + (Porta is 22 or 0 ? "" : ":" + Porta),
    };
    public bool Testada => TestadaEm is not null && !string.IsNullOrEmpty(Home);

    public static string NovoId() => Guid.NewGuid().ToString("N")[..10];
}

public sealed record RelatorioDaMaquina(bool Conectou, string? Erro, string? Sistema, string? Home, IReadOnlyDictionary<string, string> Ferramentas)
{
    public string? Versao(string ferramenta) => Ferramentas.TryGetValue(ferramenta, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;
    public bool Tem(string ferramenta) => Versao(ferramenta) is not null;

    public static RelatorioDaMaquina Ler(string saida)
    {
        var valores = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var linha in saida.Replace("\r", "").Split('\n'))
        {
            var igual = linha.IndexOf('=');
            if (igual > 0) valores[linha[..igual].Trim()] = linha[(igual + 1)..].Trim();
        }
        valores.Remove("sistema", out var sistema);
        valores.Remove("home", out var home);
        return new RelatorioDaMaquina(!string.IsNullOrEmpty(home), null, sistema, home, valores);
    }

    public static RelatorioDaMaquina Falhou(string erro) => new(false, erro, null, null, new Dictionary<string, string>());
}

public sealed record ResultadoRemoto(int Codigo, string Saida, string Erros)
{
    public bool Ok => Codigo == 0;
    public bool CaiuAConexao => Codigo == 255;
}

public sealed record CotaDaMaquina(string Texto, double? Usado);

public interface IMaquinaRemota
{
    MaquinaNaNuvem Dados { get; }
    Task<ProcessStartInfo> PrepararAsync(string comando, CancellationToken cancelamento = default);
    Task<RelatorioDaMaquina> TestarAsync(CancellationToken cancelamento = default);
    Task<ResultadoRemoto> RodarAsync(string comando, string? entrada = null, CancellationToken cancelamento = default);
    Task<ResultadoRemoto> EnviarAsync(string comando, Func<Stream, Task> escrever, CancellationToken cancelamento = default);
    Task<ResultadoRemoto> ReceberAsync(string comando, Func<Stream, Task> ler, CancellationToken cancelamento = default);
    Task GarantirAjudanteAsync(CancellationToken cancelamento = default);
    void AvisarQueda();
    Task<ProcessStartInfo?> TunelAsync(int portaLocal, int portaRemota, CancellationToken cancelamento = default);
}

public interface IProvedorDeMaquinas
{
    string Tipo { get; }
    bool CriaMaquinas { get; }
    bool Pausa { get; }
    bool PublicaPortas { get; }
    IMaquinaRemota Abrir(MaquinaNaNuvem dados);
    Task<MaquinaNaNuvem> CriarAsync(string nome, CancellationToken cancelamento = default);
    Task RetomarAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default);
    Task PausarAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default);
    Task<string> PublicarPortaAsync(MaquinaNaNuvem maquina, int porta, CancellationToken cancelamento = default);
    Task<CotaDaMaquina?> CotaAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default);
}

public interface ITransporte
{
    ProcessStartInfo Comando(string comandoRemoto);
    Task AntesAsync(CancellationToken cancelamento) => Task.CompletedTask;
    ProcessStartInfo? Tunel(int portaLocal, int portaRemota) => null;
    void Caiu()
    {
    }
}

public sealed class MaquinaRemota(MaquinaNaNuvem dados, ITransporte transporte) : IMaquinaRemota
{
    int _ajudante;

    public MaquinaNaNuvem Dados { get; } = dados;
    public ITransporte Transporte { get; } = transporte;

    public async Task<ProcessStartInfo> PrepararAsync(string comando, CancellationToken cancelamento = default)
    {
        await Transporte.AntesAsync(cancelamento).ConfigureAwait(false);
        var info = Transporte.Comando(comando);
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardInput = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        return info;
    }

    public async Task<RelatorioDaMaquina> TestarAsync(CancellationToken cancelamento = default)
    {
        try
        {
            Interlocked.Exchange(ref _ajudante, 0);
            await GarantirAjudanteAsync(cancelamento).ConfigureAwait(false);
            var resultado = await RodarAsync(ScriptRemoto.Chamar("sobre"), null, cancelamento).ConfigureAwait(false);
            if (!resultado.Ok) return RelatorioDaMaquina.Falhou(ExplicarFalha(resultado));
            return RelatorioDaMaquina.Ler(resultado.Saida);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return RelatorioDaMaquina.Falhou(ex.Message);
        }
    }

    public static string ExplicarFalha(ResultadoRemoto resultado)
    {
        var erros = resultado.Erros.Trim();
        if (erros.Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
            return Texto.Idioma.T("A máquina recusou a chave do Nexora. Confira se a linha pública está no ~/.ssh/authorized_keys do usuário.");
        if (erros.Contains("Could not resolve", StringComparison.OrdinalIgnoreCase))
            return Texto.Idioma.T("Não achei esse endereço. Confira o host.");
        if (erros.Contains("timed out", StringComparison.OrdinalIgnoreCase) || erros.Contains("Connection refused", StringComparison.OrdinalIgnoreCase))
            return Texto.Idioma.T("A máquina não respondeu. Ela está ligada e com o SSH aberto nessa porta?");
        if (erros.Contains("Host key verification failed", StringComparison.OrdinalIgnoreCase) || erros.Contains("REMOTE HOST IDENTIFICATION HAS CHANGED", StringComparison.OrdinalIgnoreCase))
            return Texto.Idioma.T("A identidade da máquina mudou desde a última conexão. Se foi recriada, apague-a e conecte de novo.");
        return erros.Length > 0 ? Claude.Resumos.UmaLinha(erros, 300) : Texto.Idioma.F("A conexão falhou (código {0}).", resultado.Codigo);
    }

    public void AvisarQueda() => Transporte.Caiu();

    public async Task<ProcessStartInfo?> TunelAsync(int portaLocal, int portaRemota, CancellationToken cancelamento = default)
    {
        await Transporte.AntesAsync(cancelamento).ConfigureAwait(false);
        if (Transporte.Tunel(portaLocal, portaRemota) is not { } info) return null;
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardInput = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        return info;
    }

    public async Task GarantirAjudanteAsync(CancellationToken cancelamento = default)
    {
        if (Volatile.Read(ref _ajudante) == 1) return;
        var conferido = await RodarAsync(ScriptRemoto.Conferir, null, cancelamento).ConfigureAwait(false);
        if (conferido.Ok && conferido.Saida.Trim() == "pronto")
        {
            Volatile.Write(ref _ajudante, 1);
            return;
        }
        if (conferido.CaiuAConexao) throw new ConexaoCaiuException(ExplicarFalha(conferido));
        var instalado = await RodarAsync(ScriptRemoto.Instalar, ScriptRemoto.Conteudo, cancelamento).ConfigureAwait(false);
        if (instalado.CaiuAConexao) throw new ConexaoCaiuException(ExplicarFalha(instalado));
        if (!instalado.Ok) throw new IOException(ExplicarFalha(instalado));
        Volatile.Write(ref _ajudante, 1);
    }

    public Task<ResultadoRemoto> RodarAsync(string comando, string? entrada = null, CancellationToken cancelamento = default) =>
        EnviarAsync(comando, async fluxo =>
        {
            if (entrada is null) return;
            var bytes = new UTF8Encoding(false).GetBytes(entrada);
            await fluxo.WriteAsync(bytes, cancelamento).ConfigureAwait(false);
        }, cancelamento);

    public async Task<ResultadoRemoto> EnviarAsync(string comando, Func<Stream, Task> escrever, CancellationToken cancelamento = default)
    {
        using var processo = await IniciarAsync(comando, cancelamento).ConfigureAwait(false);
        var saida = processo.StandardOutput.ReadToEndAsync(cancelamento);
        var erros = processo.StandardError.ReadToEndAsync(cancelamento);
        try
        {
            await escrever(processo.StandardInput.BaseStream).ConfigureAwait(false);
            await processo.StandardInput.BaseStream.FlushAsync(cancelamento).ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
        finally
        {
            Fechar(processo);
        }
        return await TerminarAsync(processo, saida, erros, cancelamento).ConfigureAwait(false);
    }

    public async Task<ResultadoRemoto> ReceberAsync(string comando, Func<Stream, Task> ler, CancellationToken cancelamento = default)
    {
        using var processo = await IniciarAsync(comando, cancelamento).ConfigureAwait(false);
        Fechar(processo);
        var erros = processo.StandardError.ReadToEndAsync(cancelamento);
        await ler(processo.StandardOutput.BaseStream).ConfigureAwait(false);
        return await TerminarAsync(processo, Task.FromResult(""), erros, cancelamento).ConfigureAwait(false);
    }

    async Task<Process> IniciarAsync(string comando, CancellationToken cancelamento)
    {
        var info = await PrepararAsync(comando, cancelamento).ConfigureAwait(false);
        info.StandardOutputEncoding = Encoding.UTF8;
        info.StandardErrorEncoding = Encoding.UTF8;
        var processo = Process.Start(info) ?? throw new InvalidOperationException(Texto.Idioma.T("O ssh não abriu."));
        Claude.GrupoDeProcessos.Adicionar(processo);
        return processo;
    }

    static void Fechar(Process processo)
    {
        try
        {
            processo.StandardInput.Close();
        }
        catch (IOException)
        {
        }
    }

    async Task<ResultadoRemoto> TerminarAsync(Process processo, Task<string> saida, Task<string> erros, CancellationToken cancelamento)
    {
        try
        {
            await processo.WaitForExitAsync(cancelamento).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { processo.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw;
        }
        var resultado = new ResultadoRemoto(processo.ExitCode, await saida.ConfigureAwait(false), await erros.ConfigureAwait(false));
        if (resultado.CaiuAConexao) AvisarQueda();
        return resultado;
    }
}
