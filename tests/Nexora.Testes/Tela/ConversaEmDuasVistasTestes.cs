using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Threading;
using Nexora.Core.Ambientes;
using Nexora.Core.Claude;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class ConversaEmDuasVistasTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-duas-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    (ChatVM Chat, ChatView Aba, ChatView Espiada, Window Janela) Montar()
    {
        var chat = ChatVM.Nova(new Local("Loja", _pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });
        var aba = new ChatView { DataContext = chat, IsVisible = false };
        var espiada = new ChatView { DataContext = chat, Compacto = true };
        var grade = new Grid();
        grade.Children.Add(aba);
        grade.Children.Add(espiada);
        var janela = new Window { Width = 1200, Height = 800, Content = grade };
        janela.Show();
        Dispatcher.UIThread.RunJobs();
        espiada.FindControl<TextBox>("Composer")!.Focus();
        Dispatcher.UIThread.RunJobs();
        return (chat, aba, espiada, janela);
    }

    [Fact]
    public Task MenuAbreSoNaVistaEmUso() => Sessao.Rodar(() =>
    {
        var (chat, aba, espiada, _) = Montar();

        chat.AbrirMenuModoCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(espiada.FindControl<Popup>("MenuModo")!.IsOpen);
        Assert.False(aba.FindControl<Popup>("MenuModo")!.IsOpen);

        espiada.FindControl<Popup>("MenuModo")!.IsOpen = false;
        Dispatcher.UIThread.RunJobs();
        Assert.False(chat.MenuModoAberto);
    });

    [Fact]
    public Task BarraListaOsComandosNaVistaEmUso() => Sessao.Rodar(async () =>
    {
        CatalogoComandos.Guardar(_pasta, new ComandosDisponiveis([new ComandoBarra("revisar", "Revisa o diff", null), new ComandoBarra("handoff", null, null)], RemotoDisponivel: true));
        var (_, aba, espiada, janela) = Montar();

        janela.KeyTextInput("/");
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.True(espiada.FindControl<Popup>("Comandos")!.IsOpen);
        Assert.False(aba.FindControl<Popup>("Comandos")!.IsOpen);
        Assert.Equal(4, espiada.FindControl<ListBox>("ListaComandos")!.ItemCount);
    });

    [Fact]
    public Task SegundoComandoNoMeioDaMensagem() => Sessao.Rodar(async () =>
    {
        CatalogoComandos.Guardar(_pasta, new ComandosDisponiveis([new ComandoBarra("revisar", "Revisa o diff", null), new ComandoBarra("handoff", null, null)], RemotoDisponivel: true));
        var (chat, _, espiada, janela) = Montar();
        async Task Assentar()
        {
            for (var i = 0; i < 5; i++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
        }

        janela.KeyTextInput("/rev");
        await Assentar();
        janela.KeyPressQwerty(Avalonia.Input.PhysicalKey.Tab, Avalonia.Input.RawInputModifiers.None);
        janela.KeyTextInput("o diff e depois /han");
        await Assentar();

        var comandos = espiada.FindControl<Popup>("Comandos")!;
        Assert.True(comandos.IsOpen);
        Assert.Equal(1, espiada.FindControl<ListBox>("ListaComandos")!.ItemCount);

        janela.KeyPressQwerty(Avalonia.Input.PhysicalKey.Tab, Avalonia.Input.RawInputModifiers.None);
        await Assentar();

        Assert.Equal("/revisar o diff e depois /handoff ", chat.Rascunho);
        Assert.False(comandos.IsOpen);

        janela.KeyTextInput("src/pasta");
        await Assentar();
        Assert.False(comandos.IsOpen);
    });
}
