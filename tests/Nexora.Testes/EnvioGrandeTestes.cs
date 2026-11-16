using System.Diagnostics;
using Nexora.Core.Claude;

namespace Nexora.Testes;

public sealed class EnvioGrandeTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-envio-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_pasta, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    string ClaudeQueNaoLe()
    {
        var falso = Path.Combine(_pasta, "claude-falso.cmd");
        File.WriteAllText(falso, "@ping -n 30 127.0.0.1 >nul\r\n");
        return falso;
    }

    [Fact]
    public async Task MensagemGrandeNaoTravaQuemEnviaMesmoComOClaudeSemLer()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var sessao = new SessaoClaude(new OpcoesSessao(_pasta), ClaudeQueNaoLe());
        sessao.Iniciar();
        var imagem = new Anexo("print.png", "image/png", new byte[3_000_000]);
        Assert.True(EmThreadPropria(() => sessao.Enviar("mensagem com print", [imagem])), "Enviar ficou preso esperando o Claude ler a entrada.");
        await Task.CompletedTask;
    }

    static bool EmThreadPropria(Action acao)
    {
        using var feito = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            acao();
            feito.Set();
        }) { IsBackground = true };
        thread.Start();
        return feito.Wait(Limite);
    }

    static readonly TimeSpan Limite = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task VariosEnviosSeguidosTambemNaoTravam()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var sessao = new SessaoClaude(new OpcoesSessao(_pasta), ClaudeQueNaoLe());
        sessao.Iniciar();
        var texto = new string('x', 400_000);
        Assert.True(EmThreadPropria(() =>
        {
            for (var i = 0; i < 10; i++) sessao.Enviar(texto);
            sessao.Interromper();
        }), "Envios seguidos travaram.");
        await Task.CompletedTask;
    }
}
