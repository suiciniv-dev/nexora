using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.Core.Ambientes;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class AvatarDoOrquestradorTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-polvo-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    static void Esperar(Func<bool> condicao)
    {
        var limite = DateTime.Now.AddSeconds(10);
        while (!condicao() && DateTime.Now < limite)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(10);
        }
        Dispatcher.UIThread.RunJobs();
        Assert.True(condicao(), "a condição não aconteceu a tempo");
    }

    static void Foto(Window janela, string nome)
    {
        if (Environment.GetEnvironmentVariable("NEXORA_FOTOS") is not { Length: > 0 } pasta) return;
        for (var i = 0; i < 6; i++)
        {
            Thread.Sleep(20);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        janela.CaptureRenderedFrame()?.Save(Path.Combine(pasta, nome));
    }

    [Fact]
    public Task OrquestradorUsaOPolvo() => Sessao.Rodar(() =>
    {
        var escolha = CatalogoDeAvatares.Agente(CatalogoDeAvatares.IdOrquestrador);
        Assert.NotNull(escolha);
        Assert.Equal("Octopus", escolha.Definicao.Nome);
        Assert.Same(CatalogoDeAvatares.Orquestrador!.Definicao, escolha.Definicao);

        var janela = new JanelaVM();
        janela.AbrirSessao(new Local("Loja", _pasta, []));
        var chat = janela.Abertas.Last();
        Assert.NotEqual(CatalogoDeAvatares.IdOrquestrador, chat.AgenteDoCartao);
        var mudou = new List<string?>();
        chat.PropertyChanged += (_, e) => mudou.Add(e.PropertyName);

        chat.Mcp = "{}";

        Assert.Equal(CatalogoDeAvatares.IdOrquestrador, chat.AgenteDoCartao);
        Assert.Equal("Orquestrador", chat.NomeDoAgente);
        Assert.Contains(nameof(ChatVM.AgenteDoCartao), mudou);
    });

    [Fact]
    public Task PolvoDoOrquestradorSoMexeQuandoEleTrabalha() => Sessao.Rodar(() =>
    {
        var demoAntes = JanelaVM.Demo;
        try
        {
            JanelaVM.Demo = true;
            Nexora.Temas.Catalogo.Aplicar(Nexora.Temas.Catalogo.Atual);
            var vm = new JanelaVM();
            var janela = new Janela { DataContext = vm, Width = 1440, Height = 900 };
            janela.Show();
            vm.MostrarOrquestraCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            var view = janela.GetVisualDescendants().OfType<OrquestraView>().First();
            var espera = view.GetVisualDescendants().OfType<AvatarVivo>().First(a => a.Name == "AvatarDeEspera");
            var topo = view.GetVisualDescendants().OfType<AvatarVivo>().First(a => a.Name == "AvatarDoTopo");
            Assert.True(vm.Orquestra.SemChefe);
            Assert.True(espera.IsEffectivelyVisible);
            Assert.True(topo.Parado);
            Esperar(() => espera.UltimoQuadro is not null);
            Assert.Equal("Polvo, o avatar do orquestrador", Avalonia.Automation.AutomationProperties.GetName(espera));
            Foto(janela, "orquestrador-vazio.png");

            vm.AbrirSessao(new Local("Loja", _pasta, []));
            var chefe = vm.Abertas.Last();
            vm.Orquestra.Demo(chefe, []);
            vm.MostrarOrquestraCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            view = janela.GetVisualDescendants().OfType<OrquestraView>().First(v => v.IsEffectivelyVisible);
            topo = view.GetVisualDescendants().OfType<AvatarVivo>().First(a => a.Name == "AvatarDoTopo");
            Assert.Same(chefe, vm.Orquestra.Chefe);
            Assert.True(topo.Parado);
            chefe.Estado = EstadoChat.Trabalhando;
            Dispatcher.UIThread.RunJobs();
            Assert.False(topo.Parado);
            var doNo = view.GetVisualDescendants().OfType<AvatarVivo>().First(a => a.Name == "AvatarDoNo" && a.IsEffectivelyVisible);
            Assert.False(doNo.Parado);
            Esperar(() => topo.UltimoQuadro is not null && doNo.UltimoQuadro is not null);
            Foto(janela, "orquestrador-trabalhando.png");

            chefe.Estado = EstadoChat.Pronta;
            Dispatcher.UIThread.RunJobs();
            Assert.True(topo.Parado);
            Assert.True(doNo.Parado);
            janela.Close();
        }
        finally
        {
            JanelaVM.Demo = demoAntes;
        }
    });
}
