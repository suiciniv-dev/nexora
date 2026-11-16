using System.Diagnostics;

namespace Nexora.Core.Claude;

public interface IProcessoDoAgente : IDisposable
{
    int Id { get; }
    bool HasExited { get; }
    int ExitCode { get; }
    StreamWriter StandardInput { get; }
    StreamReader StandardOutput { get; }
    StreamReader StandardError { get; }
    Task WaitForExitAsync(CancellationToken cancelamento = default);
    bool WaitForExit(int milissegundos);
    void Kill(bool entireProcessTree);
}

public interface ILancadorDeAgente
{
    bool Remoto { get; }
    IProcessoDoAgente Abrir(ProcessStartInfo info);
}

public sealed class LancadorLocal : ILancadorDeAgente
{
    public static LancadorLocal Instancia { get; } = new();

    public bool Remoto => false;

    public IProcessoDoAgente Abrir(ProcessStartInfo info)
    {
        var processo = Process.Start(info) ?? throw new InvalidOperationException(Texto.Idioma.F("O {0} não abriu.", Path.GetFileName(info.FileName)));
        GrupoDeProcessos.Adicionar(processo);
        return new ProcessoLocal(processo);
    }
}

public sealed class ProcessoLocal(Process processo) : IProcessoDoAgente
{
    public Process Processo { get; } = processo;
    public int Id => Processo.Id;
    public bool HasExited => Processo.HasExited;
    public int ExitCode => Processo.ExitCode;
    public StreamWriter StandardInput => Processo.StandardInput;
    public StreamReader StandardOutput => Processo.StandardOutput;
    public StreamReader StandardError => Processo.StandardError;
    public Task WaitForExitAsync(CancellationToken cancelamento = default) => Processo.WaitForExitAsync(cancelamento);
    public bool WaitForExit(int milissegundos) => Processo.WaitForExit(milissegundos);
    public void Kill(bool entireProcessTree) => Processo.Kill(entireProcessTree);
    public void Dispose() => Processo.Dispose();
}
