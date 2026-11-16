using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.Core.Git;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class AjustesVisuaisTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-ajustes-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    ChatVM Conversa() => ChatVM.Nova(new Local("Loja", _pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });

    static Window Mostrar(Control conteudo, double largura)
    {
        var janela = new Window { Width = largura, Height = 800, Content = conteudo };
        FecharPopups.Ligar(janela);
        janela.Show();
        Dispatcher.UIThread.RunJobs();
        janela.UpdateLayout();
        return janela;
    }

    [Fact]
    public Task EscFechaOMenuDeModeloMesmoComFocoNoBotao() => Sessao.Rodar(() =>
    {
        var chat = Conversa();
        var view = new ChatView { DataContext = chat };
        var janela = Mostrar(view, 1200);
        chat.AbrirMenuModeloCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(chat.MenuModeloAberto);
        view.FindControl<Button>("BotaoModelo")!.Focus();

        janela.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

        Assert.False(chat.MenuModeloAberto);
        janela.Close();
    });

    [Fact]
    public Task EscSemNadaAbertoSegueParaATela() => Sessao.Rodar(() =>
    {
        var view = new ChatView { DataContext = Conversa() };
        var janela = Mostrar(view, 1200);

        Assert.False(FecharPopups.FecharUm(janela));
        janela.Close();
    });

    [Fact]
    public Task ConversaEstreitaEscondeOsDetalhesEVoltaQuandoAlarga() => Sessao.Rodar(() =>
    {
        var chat = Conversa();
        chat.DetalhesVisivel = true;
        var view = new ChatView { DataContext = chat };
        var janela = Mostrar(view, 440);
        var detalhes = view.GetVisualDescendants().OfType<DetalhesSessaoView>().Single();

        Assert.False(detalhes.IsVisible);

        janela.Width = 1300;
        Dispatcher.UIThread.RunJobs();
        janela.UpdateLayout();

        Assert.True(detalhes.IsVisible);
        Assert.True(chat.DetalhesVisivel);
        janela.Close();
    });

    [Fact]
    public Task ImagemAbreComoImagemEMostraOTamanho() => Sessao.Rodar(() =>
    {
        var caminho = Path.Combine(_pasta, "print.png");
        using (var desenho = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(120, 80)))
            desenho.Save(caminho, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        string? citado = null;
        var arquivo = new ArquivoVM(caminho, _pasta, _ => { }, _ => { }, t => citado = t);

        Assert.True(arquivo.EhImagem);
        Assert.False(arquivo.MostraTexto);
        Assert.Null(arquivo.Problema);
        Assert.StartsWith("120 × 80", arquivo.InfoImagem);

        var view = new ArquivoView { DataContext = arquivo };
        var janela = Mostrar(view, 900);
        Assert.True(view.FindControl<Image>("Figura")!.IsEffectivelyVisible);

        arquivo.CitarImagem();
        Assert.Equal("@print.png ", citado);
        janela.Close();
        arquivo.Descartar();
    });

    [Theory]
    [InlineData(".claude/worktrees/agent-a090a8bb/", true)]
    [InlineData(".claude\\worktrees\\agent-a090a8bb\\x.cs", true)]
    [InlineData(".claude/settings.json", false)]
    [InlineData("src/Loja/Cupom.cs", false)]
    public void WorktreeDeAgenteNaoContaComoAlteracao(string caminho, bool deAgente) =>
        Assert.Equal(deAgente, Alteracoes.DeAgente(caminho));

    [Fact]
    public async Task GitEmPastaQueNaoExisteDizIssoEmPortugues()
    {
        var r = await ComandoGit.ExecutarGhAsync(Path.Combine(_pasta, "sumiu"), ["project", "list"]);

        Assert.False(r.Ok);
        Assert.Contains("não existe neste computador", r.Erro);
    }
}
