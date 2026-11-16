using System.Diagnostics;
using System.Runtime.CompilerServices;
using Nexora.Core.Desempenho;
using Xunit.Abstractions;

namespace Nexora.Testes;

public sealed class TravadasTestes(ITestOutputHelper saida)
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void SegurarDeProposito(ManualResetEventSlim pronto, ManualResetEventSlim soltar)
    {
        pronto.Set();
        soltar.Wait();
    }

    [Fact]
    public void FotografaAPilhaDeUmaThreadPresa()
    {
        if (!FotoDaThread.Disponivel) return;
        using var pronto = new ManualResetEventSlim();
        using var soltar = new ManualResetEventSlim();
        var presa = new Thread(() => SegurarDeProposito(pronto, soltar)) { IsBackground = true };
        presa.Start();
        Assert.True(pronto.Wait(5000));
        try
        {
            var relogio = Stopwatch.StartNew();
            var quadros = FotoDaThread.Tirar(presa.ManagedThreadId);
            var primeira = relogio.ElapsedMilliseconds;
            relogio.Restart();
            FotoDaThread.Tirar(presa.ManagedThreadId);
            saida.WriteLine($"Primeira foto {primeira} ms, segunda {relogio.ElapsedMilliseconds} ms");
            Assert.NotNull(quadros);
            saida.WriteLine(string.Join("\n", quadros));
            Assert.Contains("Nexora.Testes.TravadasTestes.SegurarDeProposito", quadros);
            Assert.Equal("Nexora.Testes.TravadasTestes.SegurarDeProposito", Pilha.Causa(quadros));
            Assert.StartsWith("System.Threading.", Pilha.Ponta(quadros));
        }
        finally
        {
            soltar.Set();
        }
    }

    [Fact]
    public void ThreadQueNaoExisteNaoTemFoto()
    {
        if (!FotoDaThread.Disponivel) return;
        Assert.Null(FotoDaThread.Tirar(int.MaxValue));
    }

    [Theory]
    [InlineData("Nexora.ViewModels.ChatVM", "Aplicar", "Nexora.ViewModels.ChatVM.Aplicar")]
    [InlineData("Nexora.ViewModels.ChatVM+<CarregarAsync>d__12", "MoveNext", "Nexora.ViewModels.ChatVM.CarregarAsync")]
    [InlineData("Nexora.ViewModels.ChatVM+<>c__DisplayClass5_0", "<Abrir>b__0", "Nexora.ViewModels.ChatVM.Abrir")]
    [InlineData("Nexora.ViewModels.ChatVM+<>c", "<Abrir>b__5_0", "Nexora.ViewModels.ChatVM.Abrir")]
    [InlineData("Avalonia.Headless.HeadlessUnitTestSession+<>c__DisplayClass12_0`1<System.Object>", "<DispatchCore>b__0", "Avalonia.Headless.HeadlessUnitTestSession.DispatchCore")]
    [InlineData("Nexora.Lista`1+<>c<System.String>", "<Ordenar>b__3_0", "Nexora.Lista.Ordenar")]
    [InlineData("Nexora.Views.DiffView", "<Montar>g__Linha|3_0", "Nexora.Views.DiffView.Montar.Linha")]
    [InlineData("System.Collections.Generic.List`1<System.String>", "Sort", "System.Collections.Generic.List.Sort")]
    [InlineData("System.Collections.Generic.Dictionary<System.String, System.Collections.Generic.List<System.Int32>>", "TryInsert", "System.Collections.Generic.Dictionary.TryInsert")]
    [InlineData("Nexora.ViewModels.ChatVM", ".ctor", "Nexora.ViewModels.ChatVM.ctor")]
    [InlineData("Interop+Kernel32", "WaitForSingleObject", "Interop.Kernel32.WaitForSingleObject")]
    public void LimpaONomeDoQuadro(string tipo, string metodo, string esperado)
    {
        var nome = Pilha.Limpar(tipo, metodo);
        Assert.Equal(esperado, nome);
        Assert.True(Pilha.EhNomeDeCodigo(nome));
    }

    [Fact]
    public void CausaEOPrimeiroQuadroDoNexoraEPontaOPrimeiroComNome()
    {
        string[] pilha = ["[InlinedCallFrame]", "System.IO.RandomAccess.ReadAtOffset", "System.IO.File.ReadAllText", "Nexora.Core.Git.Repositorio.Status", "Nexora.ViewModels.AmbienteVM.Atualizar", "Avalonia.Threading.Dispatcher.InvokeAsync"];
        Assert.Equal("Nexora.Core.Git.Repositorio.Status", Pilha.Causa(pilha));
        Assert.Equal("System.IO.RandomAccess.ReadAtOffset", Pilha.Ponta(pilha));

        string[] soDoAvalonia = ["SkiaSharp.SKFont.MeasureText", "Avalonia.Media.TextFormatting.TextLayout.ctor", "System.Threading.ExecutionContext.Run"];
        Assert.Equal("SkiaSharp.SKFont.MeasureText", Pilha.Causa(soDoAvalonia));

        Assert.Equal("System.Threading.Monitor.Wait", Pilha.Causa(["[HelperMethodFrame]", "System.Threading.Monitor.Wait"]));
        Assert.Null(Pilha.Causa(["[InlinedCallFrame]"]));
        Assert.False(Pilha.EhNomeDeCodigo(@"D:\Projetos\Loja.txt"));
        Assert.False(Pilha.EhNomeDeCodigo("SemPonto"));
        Assert.False(Pilha.EhNomeDeCodigo("Nexora." + new string('a', Pilha.MaximoDoNome)));
    }

    static Travada Travada(DateTime hora, string causa, int duracao, string ponta = "System.IO.File.ReadAllText") =>
        new() { Hora = hora, Duracao = duracao, FotoEm = 300, Causa = causa, Ponta = ponta, Tela = "Chat", Abas = 3, Trabalhando = 1, Pilha = [ponta, causa] };

    [Fact]
    public void AgrupaAsCausasPelaMaisFrequente()
    {
        var hora = new DateTime(2026, 11, 11, 9, 0, 0);
        var causas = Agregacao.Causas(
        [
            Travada(hora, "Nexora.A.Lenta", 900),
            Travada(hora, "Nexora.A.Lenta", 3496, "System.Threading.Monitor.Wait"),
            Travada(hora, "Nexora.A.Lenta", 400, "System.Threading.Monitor.Wait"),
            Travada(hora, "Nexora.B.Rara", 5000),
            Travada(hora, "", 700),
        ]);
        Assert.Equal(
        [
            new CausaDeTravada("Nexora.A.Lenta", "System.Threading.Monitor.Wait", 3, 3496),
            new CausaDeTravada("Nexora.B.Rara", "System.IO.File.ReadAllText", 1, 5000),
        ], causas);
        Assert.Equal(Agregacao.CausasPorDia, Agregacao.Causas(Enumerable.Range(0, 9).Select(i => Travada(hora, $"Nexora.C.M{i}", 400))).Count);
    }

    [Fact]
    public void GuardaAsTravadasPorDiaELevaAsCausasParaOResumo()
    {
        var pasta = Directory.CreateTempSubdirectory("nexora-travadas-").FullName;
        try
        {
            var gravacao = new Gravacao(pasta);
            var dia = new DateOnly(2026, 11, 10);
            var hora = dia.ToDateTime(new TimeOnly(8, 16));
            gravacao.Acrescentar(new Amostra { Hora = hora, AtrasoMaximo = 3496, Travadas = 1 });
            var travada = Travada(hora, "Nexora.ViewModels.ChatVM.Aplicar", 3496);
            Assert.True(gravacao.AcrescentarTravada(travada));
            gravacao.AcrescentarTravada(Travada(hora.AddMinutes(5), "Nexora.ViewModels.ChatVM.Aplicar", 500));
            File.AppendAllText(gravacao.ArquivoDeTravadas(dia), "{quebrada\n");

            var lidas = gravacao.LerTravadas(dia);
            Assert.Equal(2, lidas.Count);
            Assert.Equal(travada.Pilha, lidas[0].Pilha);
            Assert.Equal(travada with { Pilha = lidas[0].Pilha }, lidas[0]);

            gravacao.Arrumar(dia.AddDays(1));
            Assert.True(File.Exists(gravacao.ArquivoDeTravadas(dia)));
            var resumo = gravacao.LerDia(dia);
            Assert.NotNull(resumo);
            Assert.Equal([new CausaDeTravada("Nexora.ViewModels.ChatVM.Aplicar", "System.IO.File.ReadAllText", 2, 3496)], resumo.Causas);

            gravacao.Limpar(dia.AddDays(Gravacao.DiasPadrao));
            Assert.False(File.Exists(gravacao.ArquivoDeTravadas(dia)));
        }
        finally
        {
            Directory.Delete(pasta, true);
        }
    }

    [Fact]
    public void ArquivoDeTravadasParaDeCrescerNoLimite()
    {
        var pasta = Directory.CreateTempSubdirectory("nexora-travadas-").FullName;
        try
        {
            var gravacao = new Gravacao(pasta);
            var hora = new DateTime(2026, 11, 10, 9, 0, 0);
            var cheia = Travada(hora, "Nexora.A.B", 400) with { Pilha = [.. Enumerable.Range(0, FotoDaThread.MaximoDeQuadros).Select(i => $"Nexora.Algum.Lugar.Bem.Comprido.Metodo{i}")] };
            Assert.True(gravacao.AcrescentarTravada(cheia));
            var umaLinha = new FileInfo(gravacao.ArquivoDeTravadas(DateOnly.FromDateTime(hora))).Length;
            saida.WriteLine($"Uma travada com {FotoDaThread.MaximoDeQuadros} quadros ocupa {umaLinha} bytes; o limite guarda {Gravacao.LimiteDeTravadas / umaLinha} por dia.");
            Assert.True(Gravacao.LimiteDeTravadas / umaLinha >= 200);
            using (var aberto = new FileStream(gravacao.ArquivoDeTravadas(DateOnly.FromDateTime(hora)), FileMode.Append)) aberto.SetLength(Gravacao.LimiteDeTravadas);
            Assert.False(gravacao.AcrescentarTravada(cheia));
        }
        finally
        {
            Directory.Delete(pasta, true);
        }
    }
}
