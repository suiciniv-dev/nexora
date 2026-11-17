using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Nexora.Core.Texto;
using Nexora.ViewModels;

namespace Nexora.Testes.Tela;

public sealed class DashboardTestes
{
    static Task EmDemo(Action acao) => Sessao.Rodar(() =>
    {
        var antes = JanelaVM.Demo;
        JanelaVM.Demo = true;
        try
        {
            acao();
        }
        finally
        {
            JanelaVM.Demo = antes;
        }
    });

    static void Assentar(Window janela)
    {
        for (var i = 0; i < 40; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(10);
        }
        janela.UpdateLayout();
    }

    static (Janela Janela, JanelaVM Vm) Abrir()
    {
        var vm = new JanelaVM();
        vm.PreencherDemo();
        vm.Novidade = null;
        var janela = new Janela { DataContext = vm, Width = 1440, Height = 900 };
        janela.Show();
        Assentar(janela);
        return (janela, vm);
    }

    [Fact]
    public Task DashboardMostraPlanoAtividadeProjetoEPendencias() => EmDemo(() =>
    {
        var (janela, vm) = Abrir();
        vm.MostrarDashboardCommand.Execute(null);
        Assentar(janela);

        Assert.True(vm.NaDashboard);
        var painel = vm.PaginaDashboard;
        Assert.Equal("Loja", painel.NomeDoProjeto);
        Assert.Equal(5, painel.MeusPrs.Count);
        Assert.Contains(painel.MeusPrs, p => p.Mergeado);
        Assert.Contains(painel.MeusPrs, p => p.Estado == "draft");
        Assert.True(painel.TemParaRevisar);
        Assert.Equal(2, painel.ParaRevisar.Count);
        Assert.Equal(5, painel.Commits.Count);
        Assert.Equal(4, painel.Noticias.Count);
        Assert.True(painel.Contribuicoes.Count > 360);
        Assert.Equal(DayOfWeek.Sunday, painel.InicioDasContribuicoes.DayOfWeek);
        Assert.Contains(painel.Contribuicoes, n => n == 4);
        Assert.Equal(["Claude", "Codex"], painel.Planos.Select(p => p.Nome));
        Assert.Equal(2, painel.ColunasDosPlanos);
        Assert.Equal(["Claude", "Codex"], painel.SeriesDeUso.Select(s => s.Nome));
        Assert.All(painel.SeriesDeUso, s => Assert.Equal(30, s.Valores.Count));
        Assert.Equal(7, painel.Semana.Count);
        Assert.True(painel.TemFila);
        Assert.NotEmpty(painel.EmAndamento);
        Assert.Equal(painel.EmAndamento.OrderBy(c => c.Precisa ? 0 : c.Trabalhando ? 1 : c.Pronta ? 2 : 3).ToList(), painel.EmAndamento.ToList());
        Assert.NotEmpty(painel.Recentes);
        Assert.Contains("%", painel.Planos[0].Numero);
        Assert.Matches("^\\+", painel.Mais);
        janela.Close();
    });

    [Fact]
    public Task CampoDoDashboardSoComecaComTexto() => EmDemo(() =>
    {
        var (janela, vm) = Abrir();
        vm.MostrarDashboardCommand.Execute(null);
        Assentar(janela);
        var painel = vm.PaginaDashboard;
        Assert.False(painel.ComecarCommand.CanExecute(null));
        painel.Pedido = "   ";
        Assert.False(painel.ComecarCommand.CanExecute(null));
        painel.Pedido = "Corrigir o cupom";
        Assert.True(painel.ComecarCommand.CanExecute(null));
        janela.Close();
    });

    [Fact]
    public Task CtrlZeroAbreODashboardECtrlShiftZeroOAoVivo() => EmDemo(() =>
    {
        var (janela, vm) = Abrir();
        janela.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.Control);
        janela.KeyReleaseQwerty(PhysicalKey.Digit0, RawInputModifiers.Control);
        Assentar(janela);
        Assert.True(vm.NaDashboard);
        janela.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.Control | RawInputModifiers.Shift);
        janela.KeyReleaseQwerty(PhysicalKey.Digit0, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assentar(janela);
        Assert.True(vm.NaVisaoGeral);
        janela.Close();
    });

    [Theory]
    [InlineData(0, "0 min")]
    [InlineData(59, "0 min")]
    [InlineData(25 * 60, "25 min")]
    [InlineData(3 * 3600 + 7 * 60, "3 h 07 min")]
    public void HorasDoTempoNoNexora(int segundos, string esperado) =>
        Assert.Equal(esperado, DashboardVM.Horas(TimeSpan.FromSeconds(segundos)));

    [Fact]
    public void NumerosGrandesFicamCurtos()
    {
        Idioma.Definir("pt");
        Assert.Equal("2,3 bi", DashboardVM.Grande(2.3e9));
        Assert.Equal("542 mi", DashboardVM.Grande(5.42e8));
        Assert.Equal("12 mil", DashboardVM.Grande(12_000));
        Assert.Equal("950", DashboardVM.Grande(950));
    }

    [Fact]
    public Task FixarOProjetoFazODashboardAbrirNele() => EmDemo(() =>
    {
        var antes = App.Config.ProjetoDoDashboard;
        try
        {
            var (janela, vm) = Abrir();
            vm.MostrarDashboardCommand.Execute(null);
            Assentar(janela);
            var painel = vm.PaginaDashboard;
            var outro = vm.Ambientes.First(a => a != painel.Projeto);
            painel.Projeto = outro;
            Assert.False(painel.ProjetoFixo);
            painel.AlternarFixoCommand.Execute(null);
            Assert.True(painel.ProjetoFixo);
            Assert.Equal(outro.Principal, App.Config.ProjetoDoDashboard);
            janela.Close();

            var (janela2, vm2) = Abrir();
            vm2.MostrarDashboardCommand.Execute(null);
            Assentar(janela2);
            Assert.Equal(outro.Nome, vm2.PaginaDashboard.NomeDoProjeto);
            vm2.PaginaDashboard.AlternarFixoCommand.Execute(null);
            Assert.Null(App.Config.ProjetoDoDashboard);
            janela2.Close();
        }
        finally
        {
            App.Config.ProjetoDoDashboard = antes;
        }
    });
}
