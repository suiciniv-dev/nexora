using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using Nexora.Core.Desempenho;
using Xunit.Abstractions;

namespace Nexora.Testes.Tela;

public sealed class TravadaNaInterfaceTestes(ITestOutputHelper saida)
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void TravarDeProposito(int ms) => Thread.Sleep(ms);

    static void Girar(TimeSpan tempo)
    {
        var relogio = Stopwatch.StartNew();
        while (relogio.Elapsed < tempo)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
    }

    [Fact]
    public Task FotografaAInterfaceParadaEGuardaACausaEADuracao() => Sessao.Rodar(() =>
    {
        if (!FotoDaThread.Disponivel) return;
        using var batimento = new Batimento(TimeSpan.FromMilliseconds(50), () => ("Chat", 4, 1));
        Girar(TimeSpan.FromMilliseconds(300));
        Assert.Equal(0, batimento.Colher().Travadas);

        TravarDeProposito(1500);

        var travadas = new List<Travada>();
        var maior = 0;
        var contadas = 0;
        var relogio = Stopwatch.StartNew();
        while (travadas.Count == 0 && relogio.Elapsed < TimeSpan.FromSeconds(20))
        {
            Girar(TimeSpan.FromMilliseconds(200));
            var (m, t, fotos) = batimento.Colher();
            maior = Math.Max(maior, m);
            contadas += t;
            travadas.AddRange(fotos);
        }

        var travada = Assert.Single(travadas);
        saida.WriteLine($"Foto aos {travada.FotoEm} ms, duração {travada.Duracao} ms, causa {travada.Causa}, ponta {travada.Ponta}\n{string.Join("\n", travada.Pilha)}");
        Assert.Equal(1, contadas);
        Assert.InRange(maior, 1400, 3000);
        Assert.Equal("Nexora.Testes.Tela.TravadaNaInterfaceTestes.TravarDeProposito", travada.Causa);
        Assert.StartsWith("System.Threading.Thread.Sleep", travada.Ponta);
        Assert.InRange(travada.FotoEm, Batimento.LimiteDaFoto, 1500);
        Assert.InRange(travada.Duracao, 1400, 3000);
        Assert.Equal(("Chat", 4, 1), (travada.Tela, travada.Abas, travada.Trabalhando));
        Assert.InRange((DateTime.Now - travada.Hora).TotalSeconds, 1, 30);

        Girar(TimeSpan.FromMilliseconds(400));
        Assert.Empty(batimento.Colher().Fotos);
    });
}
