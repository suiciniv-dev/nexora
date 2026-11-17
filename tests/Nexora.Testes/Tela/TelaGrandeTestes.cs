using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class TelaGrandeTestes
{
    static JanelaVM Demo()
    {
        var vm = new JanelaVM();
        vm.PreencherDemo();
        vm.Novidade = null;
        return vm;
    }

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
        var vm = Demo();
        var janela = new Janela { DataContext = vm, Width = 1440, Height = 900 };
        janela.Show();
        Assentar(janela);
        return (janela, vm);
    }

    static void Tecla(Window janela, PhysicalKey tecla, RawInputModifiers mods = RawInputModifiers.None)
    {
        janela.KeyPressQwerty(tecla, mods);
        janela.KeyReleaseQwerty(tecla, mods);
        Assentar(janela);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 60)]
    [InlineData(2, 160)]
    [InlineData(4, 200)]
    public void PrateleiraDeslizaAteOFocoSemPassarDoFim(int foco, double esperado) =>
        Assert.Equal(esperado, Prateleira.Alvo([100, 100, 100, 100, 100], foco, 300, 40));

    [Fact]
    public void PrateleiraQueCabeInteiraNaoDesliza()
    {
        Assert.Equal(0, Prateleira.Alvo([100, 100], 1, 300, 40));
        Assert.Equal(0, Prateleira.Alvo([100, 100], 1, 300, 0, ancorada: true));
    }

    [Fact]
    public void PrateleiraAncoradaPoeOFocoNoComecoAteNoUltimo() => Assert.Equal(400, Prateleira.Alvo([100, 100, 100, 100, 100], 4, 300, 0, ancorada: true));

    [Fact]
    public Task AbreNaFileiraMaisUrgenteEAndaPelasSetas() => EmDemo(() =>
    {
        var vm = Demo();
        var tela = vm.TelaGrande;
        vm.TelaCheia = true;

        Assert.True(tela.Aberta);
        Assert.False(tela.NoMenu);
        Assert.True(tela.Fileiras.Count >= 2);
        Assert.True(tela.Fileiras[0].Precisa);
        Assert.Same(tela.Fileiras[0].Cartoes[0], tela.Focado);

        tela.Mover(1, 0);
        Assert.Same(tela.Fileiras[0].Cartoes[1], tela.Focado);
        tela.Mover(1, 0);
        tela.Mover(1, 0);
        Assert.Same(tela.Fileiras[0].Cartoes[^1], tela.Focado);

        tela.Mover(0, 1);
        Assert.Same(tela.Fileiras[1].Cartoes[0], tela.Focado);
        Assert.True(tela.Fileiras[1].Ativa);
        Assert.False(tela.Fileiras[0].Ativa);

        tela.Mover(0, -1);
        Assert.Same(tela.Fileiras[0].Cartoes[^1], tela.Focado);

        tela.Mover(0, -1);
        Assert.True(tela.NoMenu);
        Assert.DoesNotContain(tela.Fileiras, f => f.Ativa);
        tela.Mover(1, 0);
        Assert.Equal(1, tela.Atalho);
        tela.Mover(0, 1);
        Assert.False(tela.NoMenu);
        vm.TelaCheia = false;
        Assert.False(tela.Aberta);
    });

    [Fact]
    public Task SemSessoesOFocoFicaNoMenu() => EmDemo(() =>
    {
        var tela = new JanelaVM().TelaGrande;
        tela.Abrir();

        Assert.True(tela.NoMenu);
        Assert.True(tela.Vazia);
        Assert.Empty(tela.Fileiras);
        tela.Mover(0, 1);
        Assert.True(tela.NoMenu);
        tela.Rodar(1);
        Assert.True(tela.NoMenu);
        Assert.False(tela.MostraAcoes);
    });

    [Theory]
    [InlineData("issues")]
    [InlineData("agendados")]
    [InlineData("orquestra")]
    [InlineData("configuracoes")]
    public Task AsSetasAndamPelaPaginaESobemAteOMenu(string pagina) => EmDemo(() =>
    {
        var (janela, vm) = Abrir();
        Tecla(janela, PhysicalKey.F11);
        (pagina switch
        {
            "issues" => vm.MostrarIssuesCommand,
            "agendados" => vm.MostrarAgendadosCommand,
            "orquestra" => vm.MostrarOrquestraCommand,
            _ => vm.MostrarConfiguracoesCommand,
        }).Execute(null);
        Assentar(janela);
        var cartao = janela.FindControl<Border>("CartaoConteudo")!;
        var visitados = new HashSet<object>();
        for (var i = 0; i < 8; i++)
        {
            Tecla(janela, i < 4 ? PhysicalKey.ArrowDown : PhysicalKey.ArrowUp);
            if (i == 0) Assert.True(janela.FindControl<Border>("Realce")!.Opacity > 0);
            if (vm.TelaGrande.NoMenu) break;
            var foco = janela.FocusManager?.GetFocusedElement() as Control;
            Assert.NotNull(foco);
            Assert.True(cartao.IsVisualAncestorOf(foco), $"{pagina}: foco fora da página em {foco.GetType().Name}");
            visitados.Add(foco);
        }
        Assert.True(visitados.Count >= 2, $"{pagina}: as setas não saíram de {visitados.First().GetType().Name}");

        for (var i = 0; i < 20 && !vm.TelaGrande.NoMenu; i++) Tecla(janela, PhysicalKey.ArrowUp);
        Assert.True(vm.TelaGrande.NoMenu, $"{pagina}: a seta para cima não chegou ao menu");
        Assert.Equal(0, janela.FindControl<Border>("Realce")!.Opacity);
        janela.Close();
    });

    [Fact]
    public Task CliqueNaPaginaTiraOFocoDoMenu() => EmDemo(() =>
    {
        var (janela, vm) = Abrir();
        Tecla(janela, PhysicalKey.F11);
        vm.TelaGrande.Escolher(vm.TelaGrande.Atalhos.First(a => a.Icone == "painel-geral"));
        Assentar(janela);
        vm.TelaGrande.IrAoMenu();
        Assentar(janela);
        Assert.True(vm.TelaGrande.NoMenu);

        janela.GetVisualDescendants().OfType<TextBox>().First(t => t.Classes.Contains("busca-sessoes") && t.IsEffectivelyVisible).Focus();
        Assentar(janela);

        Assert.False(vm.TelaGrande.NoMenu);
        janela.Close();
    });

    [Fact]
    public Task TabRodaPelasFileirasEVoltaDoFim() => EmDemo(() =>
    {
        var vm = Demo();
        var tela = vm.TelaGrande;
        tela.Abrir();
        var total = tela.Fileiras.Count;

        for (var i = 1; i < total; i++)
        {
            tela.Rodar(1);
            Assert.Equal(i, tela.Linha);
        }
        tela.Rodar(1);
        Assert.Equal(0, tela.Linha);
        tela.Rodar(-1);
        Assert.Equal(total - 1, tela.Linha);
    });

    [Fact]
    public Task EnterAbreAConversaEFechaOInicio() => EmDemo(() =>
    {
        var vm = Demo();
        var tela = vm.TelaGrande;
        vm.TelaCheia = true;
        var chat = Assert.IsType<ChatVM>(tela.Focado);

        tela.Confirmar();

        Assert.False(tela.Aberta);
        Assert.True(vm.TelaCheia);
        Assert.Same(chat, vm.Conteudo);
    });

    sealed class CartaoFalso(string nome) : CartaoVM
    {
        public override DateTime? Recencia => null;
        public override bool Trabalhando => false;
        public override bool Precisa => true;
        public override string TempoTexto => "";
        public override string DicaTirar => "";
        public override string NomeProjeto => nome;
    }

    [Fact]
    public void QuandoOCartaoEmFocoSaiDaFileiraOFocoFicaNoLugar()
    {
        CartaoVM a = new CartaoFalso("a"), b = new CartaoFalso("b"), c = new CartaoFalso("c");
        var fileira = new FileiraVM("Precisa de você", "precisa", _ => true);
        Assert.True(fileira.Atualizar([a, b, c]));
        Assert.Same(a, fileira.Focado);
        fileira.Ir(1);
        Assert.Same(b, fileira.Focado);

        fileira.Atualizar([a, c]);
        Assert.Same(c, fileira.Focado);
        Assert.Equal(1, fileira.Foco);

        fileira.Atualizar([c, a]);
        Assert.Same(c, fileira.Focado);
        Assert.Equal(0, fileira.Foco);

        Assert.False(fileira.Atualizar([]));
        Assert.Null(fileira.Focado);
    }

    [Fact]
    public Task OPedidoDoCartaoEmFocoApareceNoInicioESomeQuandoRespondido() => EmDemo(() =>
    {
        var vm = Demo();
        var tela = vm.TelaGrande;
        tela.Abrir();
        var comPedido = tela.Fileiras[0].Cartoes.OfType<ChatVM>().First(c => c.PermissaoPendente is { EhPergunta: false });
        tela.Selecionar(comPedido);
        Assert.Same(comPedido, tela.Focado);
        Assert.True(tela.MostraPedido);
        Assert.True(tela.PodePermitir);
        var mudou = new List<string?>();
        tela.PropertyChanged += (_, e) => mudou.Add(e.PropertyName);

        comPedido.PermissaoPendente = null;

        Assert.Null(tela.Pedido);
        Assert.False(tela.MostraPedido);
        Assert.Contains(nameof(TelaGrandeVM.Pedido), mudou);

        tela.Mover(0, -1);
        tela.Mover(0, -1);
        Assert.True(tela.NoMenu);
        Assert.False(tela.MostraAcoes);
    });

    [Fact]
    public Task MenuAbreAPaginaEEscVoltaParaOInicio() => EmDemo(() =>
    {
        var (janela, vm) = Abrir();
        Tecla(janela, PhysicalKey.F11);
        Assert.True(vm.TelaGrande.Aberta);
        var inicio = janela.GetVisualDescendants().OfType<TelaGrandeView>().Single();
        Assert.True(inicio.IsEffectivelyVisible);

        Tecla(janela, PhysicalKey.ArrowUp);
        Assert.True(vm.TelaGrande.NoMenu);
        var projetos = vm.TelaGrande.Atalhos.ToList().FindIndex(a => a.Icone == "camadas");
        for (var i = 0; i < projetos; i++) Tecla(janela, PhysicalKey.ArrowRight);
        Tecla(janela, PhysicalKey.Enter);

        Assert.False(vm.TelaGrande.Aberta);
        Assert.True(vm.NaProjetos);
        Assert.False(inicio.IsEffectivelyVisible);
        var topo = janela.GetVisualDescendants().OfType<TopoTelaGrandeView>().Single();
        Assert.True(topo.IsEffectivelyVisible);
        Assert.All(janela.GetVisualDescendants().OfType<TrilhoView>(), t => Assert.False(t.IsEffectivelyVisible));
        Assert.False(janela.GetVisualDescendants().OfType<AbasView>().First().IsEffectivelyVisible);
        Assert.True(vm.TelaGrande.Atalhos[projetos].Ativo);
        Assert.Single(vm.TelaGrande.Atalhos, a => a.Ativo);

        Tecla(janela, PhysicalKey.Escape);
        Assert.False(vm.TelaGrande.Aberta);
        Assert.True(vm.TelaGrande.NoMenu);
        Assert.Equal(projetos, vm.TelaGrande.Atalho);
        Assert.Same(topo, janela.FocusManager?.GetFocusedElement());

        Tecla(janela, PhysicalKey.Escape);
        Assert.True(vm.TelaGrande.Aberta);
        Assert.True(vm.TelaGrande.Atalhos[0].Ativo);

        Tecla(janela, PhysicalKey.F11);
        Assert.False(vm.TelaCheia);
        Assert.False(vm.TelaGrande.Aberta);
        janela.Close();
    });

    [Fact]
    public Task NoAoVivoEscLevaAoMenuESetaParaBaixoVoltaParaAPagina() => EmDemo(() =>
    {
        var (janela, vm) = Abrir();
        Tecla(janela, PhysicalKey.F11);
        Tecla(janela, PhysicalKey.ArrowUp);
        Tecla(janela, PhysicalKey.ArrowRight);
        Tecla(janela, PhysicalKey.ArrowRight);
        Assert.Equal("painel-geral", vm.TelaGrande.Atalhos[vm.TelaGrande.Atalho].Icone);
        Tecla(janela, PhysicalKey.Enter);
        Assert.True(vm.NaVisaoGeral);
        Assert.False(vm.TelaGrande.Aberta);
        var busca = janela.GetVisualDescendants().OfType<TextBox>().First(t => t.Classes.Contains("busca-sessoes") && t.IsEffectivelyVisible);
        busca.Focus();
        Assentar(janela);

        Tecla(janela, PhysicalKey.Escape);
        Assert.True(vm.TelaGrande.NoMenu);
        Assert.False(vm.TelaGrande.Aberta);

        Tecla(janela, PhysicalKey.ArrowDown);
        Assert.False(vm.TelaGrande.NoMenu);
        var depois = janela.FocusManager?.GetFocusedElement();
        Tecla(janela, PhysicalKey.ArrowRight);
        var agora = janela.FocusManager?.GetFocusedElement();
        Assert.True(agora is Button b && b.Classes.Contains("card"), $"depois do baixo: {depois?.GetType().Name} {(depois as Control)?.Classes}; depois da direita: {agora?.GetType().Name} {(agora as Control)?.Classes}");

        Tecla(janela, PhysicalKey.Escape);
        Tecla(janela, PhysicalKey.ArrowRight);
        Assert.Equal("conversa", vm.TelaGrande.Atalhos[vm.TelaGrande.Atalho].Icone);
        Tecla(janela, PhysicalKey.Escape);
        Assert.True(vm.TelaGrande.Aberta);

        Tecla(janela, PhysicalKey.F11);
        Assert.False(vm.TelaGrande.Ligada);
        Assert.True(vm.LateralFixa || janela.GetVisualDescendants().OfType<TrilhoView>().Any(t => t.IsEffectivelyVisible));
        Assert.False(janela.GetVisualDescendants().OfType<TopoTelaGrandeView>().Single().IsEffectivelyVisible);
        janela.Close();
    });

    [Fact]
    public Task OCartaoEmFocoGanhaDestaqueEAFileiraDesliza() => EmDemo(() =>
    {
        var (janela, vm) = Abrir();
        Tecla(janela, PhysicalKey.F11);
        var inicio = janela.GetVisualDescendants().OfType<TelaGrandeView>().Single();
        Prateleira[] Deitadas() => [.. inicio.GetVisualDescendants().OfType<Prateleira>().Where(p => p.Orientacao == Orientation.Horizontal && p.Children.Count > 0 && p.Children[0].DataContext is CartaoVM)];
        var empilhada = inicio.GetVisualDescendants().OfType<Prateleira>().Single(p => p.Orientacao == Orientation.Vertical);

        var primeira = Deitadas()[0];
        Assert.True(primeira.Children[0].Classes.Contains("foco"));
        var cartao = primeira.Children[0].GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("cartao-g"));
        Assert.Equal(2, cartao.BorderThickness.Left);

        Tecla(janela, PhysicalKey.ArrowRight);
        Assert.False(primeira.Children[0].Classes.Contains("foco"));
        Assert.True(primeira.Children[1].Classes.Contains("foco"));
        Assert.Equal(1, cartao.BorderThickness.Left);

        var ultima = vm.TelaGrande.Fileiras.Count - 1;
        for (var i = 0; i < ultima; i++) Tecla(janela, PhysicalKey.ArrowDown);
        Assert.Equal(ultima, vm.TelaGrande.Linha);
        Assert.True(empilhada.Deslocamento > 0);
        Assert.InRange(empilhada.Children[ultima].Bounds.Top, -1, 1);
        Assert.DoesNotContain(primeira.Children, c => c.Classes.Contains("foco"));
        janela.Close();
    });

    [Fact]
    public Task NoAoVivoAsSetasComecamPeloPrimeiroCartaoMesmoSemFoco() => EmDemo(() =>
    {
        var (janela, vm) = Abrir();
        vm.VisaoGeral.MostrarMesaCommand.Execute(null);
        vm.MostrarVisaoGeralCommand.Execute(null);
        Assentar(janela);
        Assentar(janela);

        Tecla(janela, PhysicalKey.ArrowRight);

        var foco = janela.FocusManager?.GetFocusedElement() as Button;
        Assert.NotNull(foco);
        Assert.Contains("card", foco.Classes);
        Assert.Same(vm.VisaoGeral.Mesa[0], foco.DataContext);

        Tecla(janela, PhysicalKey.ArrowRight);
        foco = janela.FocusManager?.GetFocusedElement() as Button;
        Assert.Same(vm.VisaoGeral.Mesa[1], foco?.DataContext);

        Tecla(janela, PhysicalKey.Enter);
        Assert.Same(vm.VisaoGeral.Mesa[1], vm.VisaoGeral.Espiado);
        janela.Close();
    });
}
