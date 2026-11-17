using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.Core.Git;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class LayoutVerticalTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-vertical-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    ChatVM Conversa() => ChatVM.Nova(new Local("Loja", _pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });

    static Window Mostrar(Control conteudo, double largura, double altura = 800, bool vertical = false)
    {
        var janela = new Window { Width = largura, Height = altura, Content = conteudo };
        Disposicao.SetVertical(janela, vertical);
        FecharPopups.Ligar(janela);
        janela.Show();
        Atualizar(janela);
        return janela;
    }

    static void Atualizar(Window janela)
    {
        Dispatcher.UIThread.RunJobs();
        janela.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        janela.UpdateLayout();
    }

    static DetalhesSessaoView Detalhes(ChatView view) => view.GetVisualDescendants().OfType<DetalhesSessaoView>().Single();

    static Button BotaoDetalhes(ChatView view) =>
        view.GetVisualDescendants().OfType<Button>().Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Detalhes da sessão");

    [Theory]
    [InlineData(1080, 1920, true)]
    [InlineData(900, 1600, true)]
    [InlineData(640, 600, true)]
    [InlineData(1440, 900, false)]
    [InlineData(1920, 1080, false)]
    [InlineData(960, 600, false)]
    public void SoOModoVerticalEhVertical(double largura, double altura, bool vertical) =>
        Assert.Equal(vertical, Disposicao.EhVertical(new Size(largura, altura)));

    [Fact]
    public Task NoModoVerticalOPainelDeDetalhesFicaEscondidoEAbrePorCima() => Sessao.Rodar(() =>
    {
        var chat = Conversa();
        chat.DetalhesVisivel = true;
        var view = new ChatView { DataContext = chat };
        var janela = Mostrar(view, 1300, 1800, vertical: true);
        var detalhes = Detalhes(view);

        Assert.True(chat.DetalhesSobrepostos);
        Assert.False(detalhes.IsVisible);
        Assert.True(chat.DetalhesVisivel);
        Assert.True(BotaoDetalhes(view).IsVisible);
        Assert.True(Math.Abs(view.FindControl<Grid>("Divisao")!.ColumnDefinitions[3].ActualWidth) < 1);

        BotaoDetalhes(view).Command!.Execute(null);
        Atualizar(janela);

        Assert.True(detalhes.IsVisible);
        Assert.True(chat.DetalhesAbertos);
        Assert.True(chat.DetalhesVisivel);
        Assert.True(view.FindControl<DockPanel>("Conversa")!.Bounds.Width > 1200);
        Assert.True(detalhes.Bounds.Width > 0 && detalhes.TranslatePoint(new Point(0, 0), view)!.Value.X > 900);
        janela.Close();
    });

    [Fact]
    public Task DetalhesPorCimaFechamComEscCliqueForaEMesmoBotao() => Sessao.Rodar(() =>
    {
        var chat = Conversa();
        var view = new ChatView { DataContext = chat };
        var janela = Mostrar(view, 1100, 1800, vertical: true);

        chat.AlternarDetalhesAquiCommand.Execute(null);
        Atualizar(janela);
        Assert.True(Detalhes(view).IsVisible);
        janela.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Atualizar(janela);
        Assert.False(chat.DetalhesAbertos);
        Assert.False(Detalhes(view).IsVisible);

        chat.AlternarDetalhesAquiCommand.Execute(null);
        Atualizar(janela);
        var veu = view.GetVisualDescendants().OfType<Button>().Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Fechar os detalhes da sessão");
        Assert.True(veu.IsEffectivelyVisible);
        janela.MouseDown(new Point(200, 900), MouseButton.Left);
        janela.MouseUp(new Point(200, 900), MouseButton.Left);
        Atualizar(janela);
        Assert.False(chat.DetalhesAbertos);

        chat.AlternarDetalhesAquiCommand.Execute(null);
        Assert.True(chat.DetalhesAbertos);
        chat.AlternarDetalhesAquiCommand.Execute(null);
        Assert.False(chat.DetalhesAbertos);
        Assert.False(FecharPopups.FecharUm(janela));
        janela.Close();
    });

    [Fact]
    public Task AtalhosDeAbaAbremODetalheNoModoVertical() => Sessao.Rodar(() =>
    {
        var chat = Conversa();
        chat.DetalhesVisivel = false;
        var view = new ChatView { DataContext = chat };
        var janela = Mostrar(view, 1100, 1800, vertical: true);

        chat.MostrarAbaCommand.Execute("git");
        Atualizar(janela);

        Assert.True(chat.DetalhesAbertos);
        Assert.False(chat.DetalhesVisivel);
        Assert.Equal("git", chat.AbaDetalhe);
        Assert.True(Detalhes(view).IsVisible);
        janela.Close();
    });

    [Fact]
    public Task NoModoDeitadoOPainelContinuaAoLadoEOBotaoAlterna() => Sessao.Rodar(() =>
    {
        var chat = Conversa();
        chat.DetalhesVisivel = true;
        var view = new ChatView { DataContext = chat };
        var janela = Mostrar(view, 1300, 800);
        var detalhes = Detalhes(view);

        Assert.False(chat.DetalhesSobrepostos);
        Assert.True(detalhes.IsVisible);
        Assert.True(view.FindControl<Grid>("Divisao")!.ColumnDefinitions[3].ActualWidth > 300);
        Assert.False(view.GetVisualDescendants().OfType<Button>().Any(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Fechar os detalhes da sessão" && b.IsEffectivelyVisible));

        BotaoDetalhes(view).Command!.Execute(null);
        Atualizar(janela);
        Assert.False(chat.DetalhesVisivel);
        Assert.False(detalhes.IsVisible);
        Assert.False(chat.DetalhesAbertos);

        BotaoDetalhes(view).Command!.Execute(null);
        Atualizar(janela);
        Assert.True(chat.DetalhesVisivel);
        Assert.True(detalhes.IsVisible);
        janela.Close();
    });

    [Fact]
    public Task VoltarAoModoDeitadoDevolveOPainelAoLado() => Sessao.Rodar(() =>
    {
        var chat = Conversa();
        chat.DetalhesVisivel = true;
        var view = new ChatView { DataContext = chat };
        var janela = Mostrar(view, 1300, 1800, vertical: true);
        chat.AlternarDetalhesAquiCommand.Execute(null);
        Atualizar(janela);
        Assert.True(chat.DetalhesAbertos);

        Disposicao.SetVertical(janela, false);
        Atualizar(janela);

        Assert.False(chat.DetalhesSobrepostos);
        Assert.False(chat.DetalhesAbertos);
        Assert.True(Detalhes(view).IsVisible);
        Assert.True(view.FindControl<Grid>("Divisao")!.ColumnDefinitions[3].ActualWidth > 300);
        janela.Close();
    });

    [Fact]
    public Task CabecalhoEstreitoNoModoVerticalPassaOsBotoesParaASegundaLinha() => Sessao.Rodar(() =>
    {
        var chat = Conversa();
        var view = new ChatView { DataContext = chat };
        var janela = Mostrar(view, 560, 1400, vertical: true);
        var botoes = view.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Classes.Contains("botoes-chat"));

        Assert.Equal(1, Grid.GetRow(botoes));
        Assert.Equal(0, Grid.GetColumn(botoes));

        Disposicao.SetVertical(janela, false);
        janela.Width = 1300;
        Atualizar(janela);

        Assert.Equal(0, Grid.GetRow(botoes));
        Assert.Equal(1, Grid.GetColumn(botoes));
        janela.Close();
    });

    [Fact]
    public Task NoModoVerticalOEditorFicaEmbaixoDaConversa() => Sessao.Rodar(() =>
    {
        var arquivo = Path.Combine(_pasta, "Cupom.cs");
        File.WriteAllText(arquivo, "class Cupom { }\n");
        var chat = Conversa();
        var view = new ChatView { DataContext = chat };
        var janela = Mostrar(view, 1000, 1800, vertical: true);
        var divisao = view.FindControl<Grid>("Divisao")!;
        var editor = view.FindControl<EditorView>("PainelEditor")!;

        chat.AlternarDetalhesAquiCommand.Execute(null);
        Assert.True(chat.DetalhesAbertos);

        chat.Editor.Abrir(arquivo);
        Atualizar(janela);

        Assert.False(chat.DetalhesAbertos);
        Assert.Equal(3, divisao.RowDefinitions.Count);
        Assert.Equal(2, Grid.GetRow(editor));
        Assert.Equal(0, Grid.GetColumn(editor));
        Assert.True(editor.Bounds.Width > 900);
        Assert.True(editor.Bounds.Y > view.FindControl<DockPanel>("Conversa")!.Bounds.Y + 300);

        Disposicao.SetVertical(janela, false);
        Atualizar(janela);

        Assert.Empty(divisao.RowDefinitions);
        Assert.Equal(2, Grid.GetColumn(editor));
        Assert.True(editor.Bounds.X > 300);
        janela.Close();
        chat.Editor.Abas.OfType<ArquivoVM>().ToList().ForEach(a => a.Descartar());
    });

    [Fact]
    public void LateralFicaFixaNaJanelaLargaEViraGavetaNaEstreita()
    {
        var larga = new JanelaVM { LateralVisivel = true, Vertical = false };
        Assert.True(larga.LateralFixa);
        Assert.True(larga.LateralNaTela);
        larga.Navegar(larga.VisaoGeral);
        Assert.True(larga.LateralNaTela);
        larga.AlternarLateralCommand.Execute(null);
        Assert.False(larga.LateralVisivel);
        Assert.False(larga.LateralNaTela);
        larga.MostrarLateral();
        Assert.True(larga.LateralFixa);

        var estreita = new JanelaVM { LateralVisivel = true, Vertical = true };
        Assert.False(estreita.LateralFixa);
        Assert.False(estreita.LateralNaTela);
        estreita.AlternarLateralCommand.Execute(null);
        Assert.True(estreita.LateralSobreposta);
        estreita.FecharLateralSobrepostaCommand.Execute(null);
        Assert.False(estreita.LateralNaTela);
        estreita.MostrarLateral();
        Assert.True(estreita.LateralNaTela);
        estreita.Navegar(estreita.VisaoGeral);
        Assert.False(estreita.LateralNaTela);
    }

    [Fact]
    public void ConversasAbremPorCimaEOPinFixaALateral()
    {
        var vm = new JanelaVM { LateralVisivel = false, Vertical = false };
        vm.AlternarLateralCommand.Execute(null);
        Assert.True(vm.LateralSobreposta);
        Assert.True(vm.VeuDaLateral);
        vm.FecharLateralSobrepostaCommand.Execute(null);
        Assert.False(vm.LateralNaTela);

        vm.AlternarLateralCommand.Execute(null);
        vm.Navegar(vm.VisaoGeral);
        Assert.False(vm.LateralNaTela);

        vm.AlternarLateralCommand.Execute(null);
        vm.FixarLateralCommand.Execute(null);
        Assert.True(vm.LateralFixa);
        Assert.False(vm.VeuDaLateral);
        vm.Navegar(vm.VisaoGeral);
        Assert.True(vm.LateralFixa);

        vm.FixarLateralCommand.Execute(null);
        Assert.False(vm.LateralNaTela);

        vm.FixarLateralCommand.Execute(null);
        vm.AlternarLateralCommand.Execute(null);
        Assert.False(vm.LateralNaTela);
    }

    [Fact]
    public Task EscFechaALateralSobreposta() => Sessao.Rodar(() =>
    {
        var vm = new JanelaVM { LateralVisivel = true, Vertical = true };
        var janela = new Janela { DataContext = vm, Width = 900, Height = 1600 };
        janela.Show();
        Atualizar(janela);
        Assert.True(Disposicao.GetVertical(janela));

        vm.AlternarLateralCommand.Execute(null);
        Atualizar(janela);
        Assert.True(vm.LateralSobreposta);

        janela.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Atualizar(janela);

        Assert.False(vm.LateralAberta);
        janela.Close();
    });

    [Fact]
    public void GradeAdaptativaAjustaAsColunasALarguraDisponivel()
    {
        Assert.Equal(4, GradeAdaptativa.ContarColunas(900, 0, 0, 4));
        Assert.Equal(2, GradeAdaptativa.ContarColunas(788, 300, 12, 4));
        Assert.Equal(3, GradeAdaptativa.ContarColunas(1032, 300, 12, 4));
        Assert.Equal(1, GradeAdaptativa.ContarColunas(200, 300, 12, 4));
    }

    [Fact]
    public Task GradeAdaptativaDistribuiOsFilhosEmLinhasDeAlturaPropria() => Sessao.Rodar(() =>
    {
        var grade = new GradeAdaptativa { Colunas = 2, Espaco = 10, EspacoVertical = 6 };
        grade.Children.Add(new Border { MinHeight = 40 });
        grade.Children.Add(new Border { MinHeight = 60 });
        grade.Children.Add(new Border { MinHeight = 30 });
        grade.Measure(new Size(410, double.PositiveInfinity));
        grade.Arrange(new Rect(0, 0, 410, grade.DesiredSize.Height));

        Assert.Equal(60 + 6 + 30, grade.DesiredSize.Height);
        Assert.Equal(new Rect(0, 0, 200, 60), grade.Children[0].Bounds);
        Assert.Equal(new Rect(210, 0, 200, 60), grade.Children[1].Bounds);
        Assert.Equal(new Rect(0, 66, 200, 30), grade.Children[2].Bounds);
    });

    [Fact]
    public Task Sem_editor_nao_ha_alca_para_puxar_e_a_conversa_vai_ate_o_painel() => Sessao.Rodar(() =>
    {
        var arquivo = Path.Combine(_pasta, "Cupom.cs");
        File.WriteAllText(arquivo, "class Cupom { }\n");
        var chat = Conversa();
        chat.DetalhesVisivel = true;
        var view = new ChatView { DataContext = chat };
        var janela = Mostrar(view, 1600, 900);
        var divisao = view.FindControl<Grid>("Divisao")!;
        var alca = view.FindControl<GridSplitter>("Alca")!;

        Assert.False(alca.IsVisible);
        Assert.True(Math.Abs(divisao.ColumnDefinitions[0].ActualWidth + divisao.ColumnDefinitions[3].ActualWidth - divisao.Bounds.Width) < 1);

        chat.Editor.Abrir(arquivo);
        Atualizar(janela);
        Assert.True(alca.IsVisible);

        chat.Editor.Abas.ToList().ForEach(chat.Editor.Fechar);
        Atualizar(janela);
        Assert.False(alca.IsVisible);
        janela.Close();
    });
}
