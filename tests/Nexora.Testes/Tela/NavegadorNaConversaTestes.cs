using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class NavegadorNaConversaTestes
{
    static async Task NoDemo(Func<JanelaVM, Task> teste)
    {
        var (demo, isolada) = (JanelaVM.Demo, JanelaVM.Isolada);
        var flutuar = NavegadorVM.Flutuar;
        JanelaVM.Isolada = true;
        JanelaVM.Demo = true;
        try
        {
            var vm = new JanelaVM();
            vm.PreencherDemo();
            await teste(vm);
        }
        finally
        {
            (JanelaVM.Demo, JanelaVM.Isolada) = (demo, isolada);
            NavegadorVM.Flutuar = flutuar;
        }
    }

    [Fact]
    public Task AbaDoNavegadorAlargaOPainelEDestacaParaJanela() => Sessao.Rodar(() => NoDemo(async vm =>
    {
        var chat = vm.Abertas[0];
        var flutuantes = new List<NavegadorVM>();
        NavegadorVM.Flutuar = flutuantes.Add;
        Assert.Null(chat.NavegadorNoPainel);
        Assert.Equal(ChatVM.LarguraDetalhesNormal, chat.LarguraDetalhes);

        chat.MostrarAbaCommand.Execute("navegador");
        Assert.True(chat.AbaNavegador);
        Assert.True(chat.DetalhesLigados);
        Assert.Same(chat.Navegador, chat.NavegadorNoPainel);
        Assert.Equal(ChatVM.LarguraDetalhesNavegador, chat.LarguraDetalhes);

        var janela = new Window { Width = 1440, Height = 900, Content = new DetalhesSessaoView { DataContext = chat } };
        janela.Show();
        Dispatcher.UIThread.RunJobs();
        var vista = janela.GetVisualDescendants().OfType<NavegadorView>().Single();
        Assert.True(vista.IsEffectivelyVisible);
        Assert.Contains(janela.GetVisualDescendants().OfType<Border>(), b => b.Bounds.Width == ChatVM.LarguraDetalhesNavegador);

        chat.AbrirNavegador("http://localhost:5173/");
        Assert.Equal("http://localhost:5173/", chat.Navegador.Endereco);
        Assert.True(chat.Navegador.MostraPagina);

        chat.Navegador.DestacarCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Same(chat.Navegador, Assert.Single(flutuantes));
        Assert.Null(chat.NavegadorNoPainel);
        Assert.True(chat.NavegadorDestacado);
        Assert.Equal(ChatVM.LarguraDetalhesNormal, chat.LarguraDetalhes);
        Assert.Empty(janela.GetVisualDescendants().OfType<NavegadorView>());

        chat.AbrirNavegador(null);
        Assert.Equal(2, flutuantes.Count);

        chat.Navegador.EncaixarCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Same(chat.Navegador, chat.NavegadorNoPainel);
        Assert.Single(janela.GetVisualDescendants().OfType<NavegadorView>());

        chat.MostrarAbaCommand.Execute("sessao");
        Assert.Equal(ChatVM.LarguraDetalhesNormal, chat.LarguraDetalhes);
        janela.Close();
        await Task.CompletedTask;
    }));
}
