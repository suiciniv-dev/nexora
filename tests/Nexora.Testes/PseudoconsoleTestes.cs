using System.Text;
using Nexora.Core.Terminal;

namespace Nexora.Testes;

public sealed class PseudoconsoleTestes
{
    [Fact]
    public async Task RodaUmComandoNaPastaEDevolveASaida()
    {
        if (!OperatingSystem.IsWindows()) return;
        var pasta = Directory.CreateTempSubdirectory("nexora-terminal-").FullName;
        var saida = new StringBuilder();
        var fim = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var terminal = Pseudoconsole.Abrir("cmd.exe /c cd && echo nexora-ok", pasta, 100, 30);
        terminal.Saida += texto =>
        {
            lock (saida) saida.Append(texto);
        };
        terminal.Encerrou += codigo => fim.TrySetResult(codigo);
        terminal.Redimensionar(120, 40);

        var codigo = await fim.Task.WaitAsync(TimeSpan.FromSeconds(20));

        Assert.Equal(0, codigo);
        string texto;
        lock (saida) texto = saida.ToString();
        Assert.Contains("nexora-ok", texto);
        Assert.Contains(Path.GetFileName(pasta), texto);
        Assert.True(terminal.Pid > 0);
    }

    [Fact]
    public void ShellPadraoEhPowerShell() =>
        Assert.Contains("powershell", Pseudoconsole.ShellPadrao().Replace("pwsh", "powershell"), StringComparison.OrdinalIgnoreCase);
}
