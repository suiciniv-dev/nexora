using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using Nexora.Core.Claude;

namespace Nexora.Core.Executar;

public interface IExecucaoDeAlvo : IDisposable
{
    event Action<string, bool>? Linha;
    event Action<string>? Ouvindo;
    event Action<int>? Terminou;
    void Iniciar();
    void Parar();
}

public sealed class Execucao(Alvo alvo, int? porta, IReadOnlyDictionary<string, string>? ambiente = null) : IExecucaoDeAlvo
{
    Process? _processo;

    public event Action<string, bool>? Linha;
    public event Action<string>? Ouvindo;
    public event Action<int>? Terminou;

    public Alvo Alvo { get; } = alvo;
    public int? Porta { get; } = porta;
    public bool Ativa => _processo is { HasExited: false };

    public static bool PortaOcupada(int porta) =>
        IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(e => e.Port == porta);

    public void Iniciar()
    {
        var info = new ProcessStartInfo(Alvo.Comando)
        {
            WorkingDirectory = Alvo.Pasta,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in Alvo.ArgumentosCom(Porta)) info.ArgumentList.Add(arg);
        foreach (var (chave, valor) in ambiente ?? new Dictionary<string, string>()) info.Environment[chave] = valor;
        foreach (var (chave, valor) in Alvo.VariaveisCom(Porta)) info.Environment[chave] = valor;
        _processo = Process.Start(info) ?? throw new InvalidOperationException($"{Alvo.Comando} não abriu.");
        GrupoDeProcessos.Adicionar(_processo);
        var processo = _processo;
        _ = LerAsync(processo.StandardOutput, erro: false);
        _ = LerAsync(processo.StandardError, erro: true);
        _ = EsperarAsync(processo);
    }

    async Task LerAsync(StreamReader leitor, bool erro)
    {
        try
        {
            while (await leitor.ReadLineAsync() is { } bruta)
            {
                var linha = Alvos.Ansi().Replace(bruta, "");
                Linha?.Invoke(linha, erro);
                if (Alvos.Ouvindo().Match(linha) is { Success: true } achou) Ouvindo?.Invoke(achou.Groups[1].Value);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
    }

    async Task EsperarAsync(Process processo)
    {
        try
        {
            await processo.WaitForExitAsync();
            Terminou?.Invoke(processo.ExitCode);
        }
        catch (InvalidOperationException)
        {
            Terminou?.Invoke(-1);
        }
    }

    public void Parar()
    {
        try
        {
            if (_processo is { HasExited: false } processo) processo.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }

    public void Dispose()
    {
        Parar();
        _processo?.Dispose();
    }
}
