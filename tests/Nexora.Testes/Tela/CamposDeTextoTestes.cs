using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using Nexora.Core.Ambientes;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class CamposDeTextoTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-tela-").FullName;

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

    static Window Mostrar(Control conteudo)
    {
        var janela = new Window { Width = 1200, Height = 800, Content = conteudo };
        janela.Show();
        return janela;
    }

    [Fact]
    public Task PaginaDoProjetoShiftEnterQuebraLinhaEEnterComeca() => Sessao.Rodar(() =>
    {
        var anfitriao = new AnfitriaoFalso();
        var projeto = new AmbienteVM(new Ambiente("Loja", _pasta, [], null), anfitriao);
        var view = new AmbienteView { DataContext = projeto };
        var janela = Mostrar(view);
        var campo = view.FindControl<TextBox>("Composer")!;
        campo.Focus();

        janela.KeyTextInput("primeira");
        janela.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Shift);
        janela.KeyTextInput("segunda");

        Assert.Empty(anfitriao.Abertas);
        Assert.Contains('\n', projeto.Rascunho);

        janela.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);

        var aberta = Assert.Single(anfitriao.Abertas);
        Assert.Contains("primeira", aberta.Mensagem);
        Assert.Contains("segunda", aberta.Mensagem);
        Assert.Contains('\n', aberta.Mensagem);
    });

    [Fact]
    public Task ConversaShiftEnterQuebraLinhaSemEnviar() => Sessao.Rodar(() =>
    {
        var chat = ChatVM.Nova(new Local("Loja", _pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });
        var view = new ChatView { DataContext = chat };
        var janela = Mostrar(view);
        view.FindControl<TextBox>("Composer")!.Focus();

        janela.KeyTextInput("linha um");
        janela.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Shift);
        janela.KeyTextInput("linha dois");

        Assert.Contains('\n', chat.Rascunho);
        Assert.Empty(chat.Itens.OfType<ItemUsuario>());
    });

    [Fact]
    public Task ArquivoCitadoApareceDestacadoEPodeSerTirado() => Sessao.Rodar(() =>
    {
        var chat = ChatVM.Nova(new Local("Loja", _pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });
        var view = new ChatView { DataContext = chat };
        var janela = Mostrar(view);

        chat.Rascunho = "Veja @Acme.Shop/src/Tarefas/TarefaGerarEspelhos.cs , e @README.md por favor";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        janela.UpdateLayout();

        Assert.Equal(["Acme.Shop/src/Tarefas/TarefaGerarEspelhos.cs", "README.md"], chat.Citados.Select(c => c.Caminho));
        var etiquetas = view.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("citado") && b.IsEffectivelyVisible).ToList();
        Assert.Equal(2, etiquetas.Count);

        chat.Citados[0].RemoverCommand.Execute(null);

        Assert.DoesNotContain("TarefaGerarEspelhos", chat.Rascunho);
        Assert.Contains("@README.md", chat.Rascunho);
        Assert.Single(chat.Citados);
    });

    [Fact]
    public Task ListasDeArquivosEComandosTemFundoSolido() => Sessao.Rodar(() =>
    {
        Avalonia.Application.Current!.Resources["FundoSolido"] = Avalonia.Media.Brushes.Black;
        var chat = ChatVM.Nova(new Local("Loja", _pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });
        var view = new ChatView { DataContext = chat };
        Mostrar(view);
        chat.MenuModeloAberto = true;
        chat.MenuModoAberto = true;
        foreach (var nome in new[] { "Mencoes", "Comandos", "MenuModelo", "MenuModo" })
        {
            var popup = view.FindControl<Popup>(nome)!;
            var filho = popup.Child is Adiado adiado ? adiado.Child : popup.Child;
            Assert.True(filho is Border { Background: Avalonia.Media.ISolidColorBrush { Color.A: 255 } }, $"{nome} está sem fundo sólido");
        }
    });

    [Fact]
    public Task ComecarPelaIssueSoPreencheORascunho() => Sessao.Rodar(() =>
    {
        var janela = new JanelaVM();
        var issue = new Core.Git.IssueDaSessao(3361, "acme-corp/Acme.Shop", "https://github.com/acme-corp/Acme.Shop/issues/3361", "Erros impressão espelho");

        janela.AbrirSessaoDaIssue(new Local("Loja", _pasta, []), "Issue: https://github.com/acme-corp/Acme.Shop/issues/3361\n", issue);

        var chat = Assert.Single(janela.Abertas);
        Assert.StartsWith("Issue: https://github.com/", chat.Rascunho);
        Assert.Empty(chat.Itens.OfType<ItemUsuario>());
        Assert.False(chat.Trabalhando);
        Assert.Equal(3361, chat.Issue?.Numero);
    });

    static async Task Esperar(Func<bool> condicao)
    {
        var limite = DateTime.Now.AddSeconds(10);
        while (!condicao() && DateTime.Now < limite)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(condicao(), "a condição não aconteceu a tempo");
    }

    [Fact]
    public Task ColarTextoNaConversaPodeSerDesfeitoComCtrlZ() => Sessao.Rodar(async () =>
    {
        var chat = ChatVM.Nova(new Local("Loja", _pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });
        var view = new ChatView { DataContext = chat };
        var janela = Mostrar(view);
        view.FindControl<TextBox>("Composer")!.Focus();
        janela.KeyTextInput("antes ");
        await janela.Clipboard!.SetTextAsync("colado sem querer");

        janela.KeyPressQwerty(PhysicalKey.V, RawInputModifiers.Control);
        await Esperar(() => chat.Rascunho == "antes colado sem querer");
        janela.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);

        Assert.Equal("antes ", chat.Rascunho);
    });

    [Fact]
    public Task AltMTrocaOModoEShiftTabSaiDoCampo() => Sessao.Rodar(() =>
    {
        var chat = ChatVM.Nova(new Local("Loja", _pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });
        var view = new ChatView { DataContext = chat };
        var janela = Mostrar(view);
        var campo = view.FindControl<TextBox>("Composer")!;
        campo.Focus();
        var modo = chat.ModoIndice;

        janela.KeyPressQwerty(PhysicalKey.M, RawInputModifiers.Alt);
        Assert.NotEqual(modo, chat.ModoIndice);

        janela.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
        Assert.False(campo.IsFocused);
    });
}
