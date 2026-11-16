using System.Text;
using Nexora.Core.Cli;

namespace Nexora;

static class Cli
{
    public static int Rodar(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        var canal = Environment.GetEnvironmentVariable(CanalLocal.VariavelDoCanal) is { Length: > 0 } nome ? nome : CanalLocal.Nome(ControleDoApp.PastaDeDados);
        var linha = new LinhaDeComando((pedido, cancelar) => CanalLocal.PedirAsync(canal, pedido, TimeSpan.FromSeconds(2), cancelar), Console.Out, Console.Error, Console.In, Environment.CurrentDirectory);
        using var cancelar = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancelar.Cancel();
        };
        try
        {
            return linha.RodarAsync(args, cancelar.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
    }
}
