using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class CliqueEmTudoTestes
{
    const int MaximoPorTela = 90;

    static readonly (string Nome, Action<JanelaVM> Abrir)[] Telas =
    [
        ("Conversa", vm => vm.Mostrar(vm.Abertas[0])),
        ("Ao vivo", vm => vm.MostrarVisaoGeralCommand.Execute(null)),
        ("Projetos", vm => vm.MostrarProjetosCommand.Execute(null)),
        ("Issues", vm => vm.MostrarIssuesCommand.Execute(null)),
        ("Agendados", vm => vm.MostrarAgendadosCommand.Execute(null)),
        ("Recursos", vm => vm.MostrarRecursosCommand.Execute(null)),
        ("Uso", vm => vm.MostrarUsoCommand.Execute(null)),
        ("Configurações", vm => vm.MostrarConfiguracoesCommand.Execute(null)),
        ("Orquestrador", vm => vm.MostrarOrquestraCommand.Execute(null)),
    ];

    [Fact]
    public Task NenhumBotaoDasTelasDerrubaOApp() => Sessao.Rodar(async () =>
    {
        var falhas = new List<string>();
        await Percorrer(falhas, moldura: false);
        Assert.True(falhas.Count == 0, "Cliques que derrubariam o Nexora:\n" + string.Join("\n", falhas));
    });

    [Fact]
    public Task NenhumBotaoDaMolduraDerrubaOApp() => Sessao.Rodar(async () =>
    {
        var falhas = new List<string>();
        await Percorrer(falhas, moldura: true);
        Assert.True(falhas.Count == 0, "Cliques que derrubariam o Nexora:\n" + string.Join("\n", falhas));
    });

    static async Task Percorrer(List<string> falhas, bool moldura)
    {
        var estilo = new Style(x => x.OfType<Popup>()) { Setters = { new Setter(Popup.ShouldUseOverlayLayerProperty, true) } };
        Application.Current!.Styles.Add(estilo);
        var demoAntes = JanelaVM.Demo;
        var isoladaAntes = JanelaVM.Isolada;
        var atual = "";
        void ErroSolto(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            falhas.Add($"{atual}: {e.Exception.GetType().Name}: {e.Exception.Message}");
            e.Handled = true;
        }
        var configAntes = System.Text.Json.JsonSerializer.Serialize(App.Config);
        Sistema.SoAnotar = _ => { };
        Dispatcher.UIThread.UnhandledException += ErroSolto;
        try
        {
            foreach (var (nome, abrir) in moldura ? Telas[..2] : Telas)
            {
                var (vm, janela) = Montar();
                try
                {
                    abrir(vm);
                    await Assentar();
                    var total = Botoes(janela, moldura).Count;
                    for (var i = 0; i < Math.Min(total, MaximoPorTela); i++)
                    {
                        Fechar(janela);
                        abrir(vm);
                        await Assentar();
                        var botoes = Botoes(janela, moldura);
                        if (i >= botoes.Count) break;
                        var botao = botoes[i];
                        atual = $"{nome} › {Rotulo(botao)}";
                        try
                        {
                            Clicar(janela, botao);
                            await Assentar();
                        }
                        catch (Exception ex)
                        {
                            falhas.Add($"{atual}: {ex.GetType().Name}: {ex.Message}");
                        }
                    }
                }
                finally
                {
                    Fechar(janela);
                    janela.Close();
                }
            }
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= ErroSolto;
            Sistema.SoAnotar = null;
            Application.Current.Styles.Remove(estilo);
            JanelaVM.Demo = demoAntes;
            JanelaVM.Isolada = isoladaAntes;
            Restaurar(configAntes);
            Core.Texto.Idioma.Definir("pt");
        }
    }

    static void Restaurar(string json)
    {
        var antes = System.Text.Json.JsonSerializer.Deserialize<Config>(json)!;
        foreach (var p in typeof(Config).GetProperties().Where(p => p.CanRead && p.CanWrite))
            p.SetValue(App.Config, p.GetValue(antes));
        Temas.Catalogo.Modo = App.Config.ModoFundo == "parado" ? "vidro" : App.Config.ModoFundo;
        Temas.Catalogo.Escolher(App.Config.Tema);
    }

    static (JanelaVM, Janela) Montar()
    {
        JanelaVM.Isolada = true;
        JanelaVM.Demo = true;
        var vm = new JanelaVM();
        var janela = new Janela { DataContext = vm, Width = 1440, Height = 900 };
        ToolTip.SetServiceEnabled(janela, false);
        vm.PreencherDemo();
        vm.Novidade = null;
        janela.Show();
        return (vm, janela);
    }

    static List<Button> Botoes(Janela janela, bool moldura) =>
    [
        .. janela.GetVisualDescendants().OfType<Button>()
            .Where(b => b.IsEffectivelyVisible && b.IsEffectivelyEnabled && b.Bounds.Width > 2 && b.Bounds.Height > 2)
            .Where(b => b.FindAncestorOfType<AreaView>() is null == moldura)
            .Where(b => Centro(janela, b) is { } p && p.X >= 0 && p.Y >= 0 && p.X < janela.Bounds.Width && p.Y < janela.Bounds.Height),
    ];

    static Point? Centro(Janela janela, Button botao) =>
        botao.TranslatePoint(new Point(botao.Bounds.Width / 2, botao.Bounds.Height / 2), janela);

    static void Clicar(Janela janela, Button botao)
    {
        var ponto = Centro(janela, botao)!.Value;
        janela.MouseDown(ponto, MouseButton.Left);
        janela.MouseUp(ponto, MouseButton.Left);
    }

    static void Fechar(Janela janela)
    {
        janela.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        janela.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    static string Rotulo(Button botao)
    {
        if (AutomationProperties.GetName(botao) is { Length: > 0 } nome) return nome;
        if (ToolTip.GetTip(botao) is string dica && dica.Length > 0) return dica;
        var texto = botao.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
        return texto ?? botao.Name ?? botao.GetType().Name;
    }

    static async Task Assentar()
    {
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            await Task.Delay(10);
        }
    }
}
