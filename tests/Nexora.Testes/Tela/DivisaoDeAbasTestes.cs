using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.Core.Ambientes;
using Nexora.Core.Claude;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class DivisaoDeAbasTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-divisao-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    (JanelaVM Janela, AreaVM Area, ChatVM A, ChatVM B) Duas()
    {
        var janela = new JanelaVM();
        var local = new Local("Loja", _pasta, []);
        janela.AbrirSessao(local);
        janela.AbrirSessao(local);
        var area = janela.Principal;
        var a = (ChatVM)area.Guias[0].Conteudo;
        var b = (ChatVM)area.Guias[1].Conteudo;
        return (janela, area, a, b);
    }

    [Fact]
    public Task DividirMostraDuasAbasLadoALadoEFocaANova() => Sessao.Rodar(() =>
    {
        var (_, area, a, b) = Duas();
        area.Selecionada = area.Achar(a);
        Assert.False(area.Dividido);
        Assert.Single(area.Conteudos);

        area.Dividir(area.Achar(b)!);

        Assert.True(area.Dividido);
        Assert.Equal([a, b], area.Conteudos.Cast<ChatVM>());
        Assert.Equal(area.Achar(b), area.Selecionada);
        Assert.True(area.Achar(a)!.NaDivisao);
    });

    [Fact]
    public Task AbaForaDoSplitMostraAAbaComoHojeEOBotaoVoltaAoSplit() => Sessao.Rodar(() =>
    {
        var (janela, area, a, b) = Duas();
        var geral = area.Abrir(janela.VisaoGeral);
        area.Selecionada = area.Achar(a);
        area.Dividir(area.Achar(b)!);

        area.Selecionada = geral;

        Assert.False(area.Dividido);
        Assert.Equal([janela.VisaoGeral], area.Conteudos);
        Assert.True(area.TemSplitGuardado);

        area.AlternarSplitCommand.Execute(null);
        Assert.True(area.Dividido);
        Assert.Equal([a, b], area.Conteudos.Cast<ChatVM>());

        area.AlternarSplitCommand.Execute(null);
        Assert.False(area.Dividido);
        Assert.Single(area.Conteudos);
    });

    [Fact]
    public Task AbrirEmSplitForaDoSplitAcrescentaAoGuardado() => Sessao.Rodar(() =>
    {
        var (janela, area, a, b) = Duas();
        janela.AbrirSessao(new Local("Loja", _pasta, []));
        var c = (ChatVM)area.Guias[2].Conteudo;
        area.Selecionada = area.Achar(a);
        area.Dividir(area.Achar(b)!);
        area.Selecionada = area.Abrir(janela.VisaoGeral);

        c.AbrirEmSplitCommand.Execute(null);

        Assert.True(area.Dividido);
        Assert.Equal([a, b, c], area.Conteudos.Cast<ChatVM>());
        Assert.Same(c, area.Conteudo);
    });

    [Fact]
    public Task FecharPainelMantemAAbaEFecharAAbaTiraOPainel() => Sessao.Rodar(() =>
    {
        var (janela, area, a, b) = Duas();
        area.Selecionada = area.Achar(a);
        area.Dividir(area.Achar(b)!);

        area.Desdividir(area.Achar(b)!);
        Assert.False(area.Dividido);
        Assert.Equal(2, area.Guias.Count);
        Assert.Single(area.Conteudos);

        area.Dividir(area.Achar(b)!);
        Assert.True(area.Dividido);
        janela.FecharGuia(area.Achar(b)!);
        Assert.False(area.Dividido);
        Assert.Equal([a], area.Conteudos.Cast<ChatVM>());
        Assert.Equal(area.Achar(a), area.Selecionada);
    });

    [Fact]
    public Task NovaSessaoAoLadoAcrescentaPainelAteOLimite() => Sessao.Rodar(() =>
    {
        var janela = new JanelaVM();
        janela.AbrirSessao(new Local("Loja", _pasta, []));
        var area = janela.Principal;
        var primeira = area.Selecionada!.Conteudo;

        for (var i = 1; i < AreaVM.MaxPaineis; i++) area.NovaSessaoAoLadoCommand.Execute(null);

        Assert.Equal(AreaVM.MaxPaineis, area.Conteudos.Count);
        Assert.Same(primeira, area.Conteudos[0]);
        Assert.False(area.PodeDividir);

        area.NovaSessaoAoLadoCommand.Execute(null);
        Assert.Equal(AreaVM.MaxPaineis, area.Conteudos.Count);
    });

    [Fact]
    public Task AbaSozinhaVoltaAoNormalDepoisDeDesfazerADivisao() => Sessao.Rodar(() =>
    {
        var (_, area, a, b) = Duas();
        area.Selecionada = area.Achar(a);
        area.Dividir(area.Achar(b)!);
        area.Desdividir(area.Achar(a)!);

        Assert.False(area.Dividido);
        Assert.Equal(area.Achar(b), area.Selecionada);
        Assert.Equal([b], area.Conteudos.Cast<ChatVM>());
    });

    [Theory]
    [InlineData("/btw", true, null)]
    [InlineData("/btw  o que é isso? ", true, "o que é isso?")]
    [InlineData("/BTW x", true, "x")]
    [InlineData("/btwx", false, null)]
    [InlineData("oi /btw", false, null)]
    public void ReconheceOComandoLateral(string texto, bool esperado, string? pergunta)
    {
        Assert.Equal(esperado, ChatVM.EhComandoLateral(texto, out var lida));
        Assert.Equal(pergunta, lida);
    }

    [Fact]
    public Task PerguntaLateralAbreAoLadoDaOrigemEDigitarBtwTambem() => Sessao.Rodar(() =>
    {
        var janela = new JanelaVM();
        janela.AbrirSessao(new Local("Loja", _pasta, []));
        var origem = (ChatVM)janela.Principal.Selecionada!.Conteudo;

        origem.Rascunho = "/btw";
        origem.EnviarCommand.Execute(null);

        var area = janela.Principal;
        Assert.True(area.Dividido);
        var lateral = Assert.IsType<ChatVM>(area.Conteudos[1]);
        Assert.Same(origem, area.Conteudos[0]);
        Assert.True(lateral.EhLateral);
        Assert.Equal("", origem.Rascunho);
        Assert.DoesNotContain(origem.Itens, i => i is ItemUsuario);
        Assert.Contains(lateral, janela.Abertas);

        lateral.ManterLateralCommand.Execute(null);
        Assert.False(lateral.EhLateral);
    });

    [Fact]
    public Task AreaMostraOsPaineisComLarguraDividida() => Sessao.Rodar(() =>
    {
        var (janela, area, a, b) = Duas();
        area.Selecionada = area.Achar(a);
        area.Dividir(area.Achar(b)!);
        a.Titulo = "Loja: pedidos";
        b.Titulo = "Loja: relatórios";
        var vista = new AreaView { DataContext = area };
        var tela = new Window { Width = 1500, Height = 800, Content = vista };
        tela.Show();
        Dispatcher.UIThread.RunJobs();

        var vistas = vista.GetVisualDescendants().OfType<ChatView>().Where(c => c.IsVisible).ToList();
        Assert.Equal(2, vistas.Count);
        Assert.All(vistas, v => Assert.InRange(v.Bounds.Width, 600, 800));
        Assert.True(vistas.Max(v => v.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("painel")).Bounds.X) > 600);

        var quadro = tela.CaptureRenderedFrame();
        quadro?.Save(Path.Combine(Path.GetTempPath(), "nexora-divisao.png"));
    });

    [Theory]
    [InlineData(2, new[] { 2 })]
    [InlineData(3, new[] { 3 })]
    [InlineData(4, new[] { 2, 2 })]
    [InlineData(5, new[] { 3, 2 })]
    [InlineData(6, new[] { 3, 3 })]
    [InlineData(7, new[] { 3, 2, 2 })]
    [InlineData(8, new[] { 3, 3, 2 })]
    [InlineData(9, new[] { 3, 3, 3 })]
    [InlineData(10, new[] { 4, 3, 3 })]
    [InlineData(12, new[] { 4, 4, 4 })]
    public void GradeOrganizaOsPaineisEmLinhas(int paineis, int[] linhas) => Assert.Equal(linhas, AbasVivas.Linhas(paineis));

    [Fact]
    public Task AtivasEmSplitAbreAsSessoesComQuemPrecisaPrimeiro() => Sessao.Rodar(() =>
    {
        var janela = new JanelaVM();
        for (var i = 0; i < 7; i++) janela.AbrirSessao(new Local("Loja", _pasta, []));
        var precisa = janela.Abertas[3];
        precisa.Estado = EstadoChat.PrecisaDeVoce;

        janela.VisaoGeral.AbrirAtivasEmSplitCommand.Execute(null);

        var area = janela.Principal;
        Assert.Equal(7, area.Conteudos.Count);
        Assert.Same(precisa, area.Conteudos[0]);
        Assert.Same(precisa, area.Conteudo);
    });

    [Fact]
    public Task AbrirEmSplitPeloMenuAcrescentaEComCheioTrocaOFoco() => Sessao.Rodar(() =>
    {
        var janela = new JanelaVM();
        var maximo = AreaVM.MaxPaineis;
        for (var i = 0; i < maximo + 2; i++) janela.AbrirSessao(new Local("Loja", _pasta, []));
        var area = janela.Principal;
        var chats = area.Guias.Select(g => (ChatVM)g.Conteudo).ToList();
        area.Selecionada = area.Achar(chats[0]);

        for (var i = 1; i < maximo; i++) chats[i].AbrirEmSplitCommand.Execute(null);
        Assert.Equal(chats.Take(maximo), area.Conteudos.Cast<ChatVM>());

        chats[maximo + 1].AbrirEmSplitCommand.Execute(null);
        Assert.Equal(maximo, area.Conteudos.Count);
        Assert.Contains(chats[maximo + 1], area.Conteudos);
        Assert.DoesNotContain(chats[maximo - 1], area.Conteudos);

        area.Achar(chats[2])!.SoEstaCommand.Execute(null);
        Assert.False(area.Dividido);
        Assert.Same(chats[2], area.Conteudo);
    });

    [Fact]
    public Task SeisPaineisEmGradeComCabecalhoEnxuto() => Sessao.Rodar(() =>
    {
        var janela = new JanelaVM();
        string[] titulos = ["Relatório semanal", "Calendário Q4", "Proposta Brightline", "Triagem do inbox", "Pesquisa de vídeos", "Ideias de isca"];
        foreach (var t in titulos)
        {
            janela.AbrirSessao(new Local("Loja", _pasta, []));
            janela.Abertas[0].Titulo = t;
        }
        janela.AbrirAtivasEmSplit();
        var area = janela.Principal;
        var vista = new AreaView { DataContext = area };
        var tela = new Window { Width = 1500, Height = 900, Content = vista };
        tela.Show();
        Dispatcher.UIThread.RunJobs();

        var chats = vista.GetVisualDescendants().OfType<ChatView>().Where(c => c.IsEffectivelyVisible).ToList();
        Assert.Equal(6, chats.Count);
        Assert.All(chats, c => Assert.False(c.FindControl<Border>("Cabecalho")!.IsVisible));
        Assert.All(chats, c => Assert.InRange(c.Bounds.Width, 450, 520));
        Assert.All(chats, c => Assert.InRange(c.Bounds.Height, 380, 450));
        var titulosVistos = vista.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.All(titulos, t => Assert.Contains(t, titulosVistos));

        tela.CaptureRenderedFrame()?.Save(Path.Combine(Path.GetTempPath(), "nexora-grade.png"));

        area.Achar(area.Conteudos[1])!.SoEstaCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var sozinho = Assert.Single(vista.GetVisualDescendants().OfType<ChatView>(), c => c.IsEffectivelyVisible);
        Assert.True(sozinho.FindControl<Border>("Cabecalho")!.IsVisible);
    });
}
