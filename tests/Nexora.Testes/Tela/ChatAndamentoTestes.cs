using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.Core.Ambientes;
using Nexora.Core.Claude;
using Nexora.Core.Historico;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class ChatAndamentoTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-andamento-").FullName;

    public void Dispose()
    {
        try
        {
            if (!Directory.Exists(_pasta)) return;
            foreach (var arquivo in Directory.EnumerateFiles(_pasta, "*", SearchOption.AllDirectories)) File.SetAttributes(arquivo, FileAttributes.Normal);
            Directory.Delete(_pasta, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    ChatVM NovaConversa() => ChatVM.Nova(new Local("Loja", _pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });

    static void Esperar(Func<bool> condicao, int segundos = 10)
    {
        var limite = DateTime.Now.AddSeconds(segundos);
        while (!condicao() && DateTime.Now < limite)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
        Dispatcher.UIThread.RunJobs();
    }

    static IEnumerable<string> TextosVisiveis(Window janela) =>
        janela.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Text is { Length: > 0 }).Select(t => t.Text!);

    [Fact]
    public Task TurnoMostraPensamentoFasesERodapeSemCusto() => Sessao.Rodar(() =>
    {
        var chat = NovaConversa();
        var janela = new Window { Width = 1100, Height = 800, Content = new ChatView { DataContext = chat } };
        janela.Show();
        chat.TurnoDesde = DateTime.Now.AddSeconds(-3);
        chat.Estado = EstadoChat.Trabalhando;

        chat.Aplicar(new PensamentoComecou());
        Dispatcher.UIThread.RunJobs();
        chat.Aplicar(new Pensando(155));
        Dispatcher.UIThread.RunJobs();

        var pensamento = Assert.Single(chat.Itens.OfType<ItemPensamento>());
        Assert.True(pensamento.Rodando);
        Assert.Equal(155, pensamento.Tokens);
        Assert.StartsWith("Pensando…", chat.AndamentoTexto);
        Assert.Contains(TextosVisiveis(janela), t => t.StartsWith("Pensando…  ·  ") && t.EndsWith(" s"));

        var contador = Assert.Single(janela.GetVisualDescendants().OfType<Contador>());
        Assert.InRange(contador.Exibido, 0, 154);
        var vistos = new HashSet<int>();
        for (var i = 0; i < 120 && contador.Exibido < 155; i++)
        {
            vistos.Add(contador.Exibido);
            contador.Avancar(TimeSpan.FromMilliseconds(16));
        }
        Assert.True(vistos.Count > 20, $"os tokens pularam em vez de contar: {string.Join(", ", vistos)}");
        Assert.Equal("155 tokens", contador.Text);

        chat.Aplicar(new BlocoEncerrado(0));
        Assert.True(pensamento.Concluido);
        Assert.StartsWith("Pensou por", pensamento.Rotulo);

        chat.Aplicar(new TextoParcial("18h"));
        Assert.StartsWith("Escrevendo…", chat.AndamentoTexto);
        chat.Aplicar(new FerramentaComecou("Write"));
        Assert.StartsWith("Preparando Write…", chat.AndamentoTexto);

        chat.Aplicar(new ContextoUsado(12_000, true, "claude-opus-5-5"));
        chat.Aplicar(new TurnoEncerrado(false, "18h", 15.87, 2457,
            new Dictionary<string, long> { ["claude-haiku-5-5"] = 1_000_000, ["claude-sonnet-5-5"] = 1_000_000, ["claude-opus-5-5"] = 200_000 }));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(EstadoChat.Pronta, chat.Estado);
        Assert.DoesNotContain(chat.Trecho, i => i is ItemPensamento);
        Assert.Equal("2 s  ·  Opus 5.5", chat.Itens.OfType<ItemAviso>().Last(a => a.EhRodape).Texto);
        Assert.Equal("Opus 5.5", chat.Modelo);
        Assert.Equal(200_000, chat.JanelaContexto);
        Assert.DoesNotContain(TextosVisiveis(janela), t => t.Contains("US$") || t.StartsWith("Pensando…"));
    });

    [Fact]
    public Task ConversaDoHistoricoVemComOModeloQueEstavaUsando() => Sessao.Rodar(() =>
    {
        ModelosDeTeste.Usar();
        var arquivo = Path.Combine(_pasta, "s-hist.jsonl");
        File.WriteAllLines(arquivo,
        [
            """{"type":"user","message":{"role":"user","content":"Oi"},"permissionMode":"default","timestamp":"2026-11-05T10:00:00Z","sessionId":"s-hist"}""",
            """{"type":"assistant","message":{"model":"claude-fable-5-1","content":[{"type":"text","text":"Olá"}],"usage":{"input_tokens":10,"output_tokens":5}},"timestamp":"2026-11-05T10:00:05Z","sessionId":"s-hist"}""",
            """{"type":"user","message":{"role":"user","content":"Segue"},"permissionMode":"auto","timestamp":"2026-11-05T10:01:00Z","sessionId":"s-hist"}""",
            """{"type":"assistant","message":{"model":"claude-fable-5-1","content":[{"type":"text","text":"Feito"}],"usage":{"input_tokens":10,"output_tokens":5}},"timestamp":"2026-11-05T10:01:05Z","sessionId":"s-hist"}""",
        ]);
        var chat = ChatVM.DeConversa(new ConversaSalva("s-hist", arquivo, "Teste", _pasta, null, DateTime.Now), new Local("Loja", _pasta, []),
            new AnfitriaoFalso(), _ => { }, _ => { });

        Esperar(() => chat.Carregamento.IsCompleted);

        Assert.Equal("claude-fable-5-1", chat.ModeloValor);
        Assert.Equal("Fable 5.1", chat.ModeloRotulo);
        Assert.Equal(3, chat.ModoIndice);
        Assert.Equal("Automático", chat.ModoRotulo);
    });

    [Fact]
    public Task MenuDoModeloMostraPadraoResolvidoEEsforcoDoModelo() => Sessao.Rodar(() =>
    {
        ModelosDeTeste.Usar();
        var chat = NovaConversa();
        Assert.Equal("Opus 5.5", chat.ModeloRotulo);

        chat.AbrirMenuModeloCommand.Execute(null);

        Assert.True(chat.MenuModeloAberto);
        var padrao = chat.OpcoesModelo[0];
        Assert.Equal("Padrão", padrao.Nome);
        Assert.True(padrao.Selecionada);
        Assert.StartsWith("Opus 5.5", padrao.Descricao);
        Assert.Contains(chat.OpcoesModelo, o => o.Nome == "Fable 5.1" && o.Descricao == "Para os desafios mais difíceis");
        Assert.Equal(["Baixo", "Médio", "Alto", "Extra alto", "Máximo"], chat.OpcoesEsforco.Select(e => e.Rotulo));

        chat.OpcoesModelo.First(o => o.Nome == "Haiku 4.5").EscolherCommand.Execute(null);
        Assert.Equal("haiku", chat.ModeloValor);
        Assert.False(chat.MenuModeloAberto);
        Assert.False(chat.TemEsforco);
        Assert.Empty(chat.OpcoesEsforco);

        Assert.Empty(chat.Itens);
        chat.Aplicar(new TextoCompleto("olá"));
        chat.AbrirMenuModeloCommand.Execute(null);
        chat.OpcoesModelo.First(o => o.Nome == "Opus 5.5").EscolherCommand.Execute(null);
        Assert.Equal("Modelo trocado para Opus 5.5", Assert.Single(chat.Itens.OfType<ItemAviso>(), a => a.EhTroca).Texto);
        chat.AbrirMenuModeloCommand.Execute(null);
        chat.OpcoesModelo.First(o => o.Nome == "Opus 5.5").EscolherCommand.Execute(null);
        Assert.Single(chat.Itens.OfType<ItemAviso>(), a => a.EhTroca);
        chat.OpcoesEsforco.First(e => e.Rotulo == "Máximo").EscolherCommand.Execute(null);
        Assert.Equal("max", chat.EsforcoValor);
        Assert.Equal("Máximo", chat.EsforcoRotulo);
        Assert.True(Assert.Single(chat.OpcoesEsforco, e => e.Ativa).Rotulo == "Máximo");
    });

    [Fact]
    public Task MensagensEsperamNaFilaEnquantoOClaudeTrabalha() => Sessao.Rodar(() =>
    {
        var pasta = Directory.CreateTempSubdirectory("nexora-fila-").FullName;
        var chat = ChatVM.Nova(new Local("Loja", pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });
        chat.Estado = EstadoChat.Trabalhando;

        chat.Rascunho = "primeira na fila";
        chat.EnviarCommand.Execute(null);
        chat.Rascunho = "segunda";
        chat.EnviarCommand.Execute(null);

        Assert.Equal(["primeira na fila", "segunda"], chat.Fila.Select(f => f.Texto));
        Assert.Equal("", chat.Rascunho);
        Assert.Empty(chat.Itens.OfType<ItemUsuario>());

        chat.Fila[1].EditarCommand.Execute(null);
        Assert.Single(chat.Fila);
        Assert.Equal("segunda", chat.Rascunho);
        chat.Rascunho = "";

        chat.Aplicar(new TurnoEncerrado(true, "parou", null, 1000));
        for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs();
        Assert.Single(chat.Fila);

        Directory.Delete(pasta);
        chat.Estado = EstadoChat.Trabalhando;
        chat.Aplicar(new TurnoEncerrado(false, "ok", null, 1000));
        Esperar(() => chat.Fila.Count == 0);

        Assert.Empty(chat.Fila);
        Assert.Equal("primeira na fila", Assert.Single(chat.Itens.OfType<ItemUsuario>()).Texto);
    });

    [Fact]
    public Task ContextoCheioSugereCompactarEDispensarSegura() => Sessao.Rodar(() =>
    {
        var chat = NovaConversa();
        chat.Aplicar(new TextoCompleto("olá"));
        chat.Estado = EstadoChat.Pronta;
        chat.JanelaContexto = 1_000_000;
        chat.ContextoTokens = 700_000;
        Assert.False(chat.SugerirCompactar);

        chat.ContextoTokens = 820_000;
        Assert.True(chat.SugerirCompactar);
        Assert.Contains("82%", chat.TextoCompactar);

        chat.Estado = EstadoChat.Trabalhando;
        Assert.False(chat.SugerirCompactar);
        chat.Estado = EstadoChat.Pronta;

        chat.DispensarCompactarCommand.Execute(null);
        Assert.False(chat.SugerirCompactar);
        chat.ContextoTokens = 860_000;
        Assert.False(chat.SugerirCompactar);
        chat.ContextoTokens = 910_000;
        Assert.True(chat.SugerirCompactar);

        chat.Aplicar(new ConversaCompactada(false, 910_000, 60_000));
        Assert.False(chat.SugerirCompactar);
        Assert.Equal(60_000, chat.ContextoTokens);
        Assert.StartsWith("Conversa compactada", chat.Itens.OfType<ItemAviso>().Last(a => a.EhTroca).Texto);
    });

    [Fact]
    public Task RapidoSoValeEmModeloQueSuportaEUltracodeVaiNaSessao() => Sessao.Rodar(() =>
    {
        ModelosDeTeste.Usar();
        var chat = NovaConversa();
        chat.ModeloValor = "opus";
        Assert.Null(chat.ConfiguracoesDaSessao);

        chat.AlternarRapidoCommand.Execute(null);
        chat.AlternarUltracodeCommand.Execute(null);
        Assert.True(chat.MostraRapido);
        Assert.True(chat.MostraUltracode);
        Assert.Equal("""{"fastMode":true,"ultracode":true}""", chat.ConfiguracoesDaSessao);

        chat.ModeloValor = "claude-fable-5-1";
        Assert.False(chat.SuportaRapido);
        Assert.False(chat.MostraRapido);
        Assert.Equal("""{"fastMode":false,"ultracode":true}""", chat.ConfiguracoesDaSessao);

        chat.ModeloValor = "haiku";
        Assert.False(chat.SuportaUltracode);
        Assert.Null(chat.ConfiguracoesDaSessao);
    });

    [Fact]
    public Task UsoDoPlanoApareceNoTopoComAlerta() => Sessao.Rodar(() =>
    {
        var janela = new JanelaVM();
        Assert.False(janela.TemPlano);
        janela.AbrirSessao(new Local("Loja", _pasta, []));
        var chat = janela.Abertas.Last();

        chat.Aplicar(new UsoDoPlano(0.84, 0.97, DateTimeOffset.Now.AddHours(2)));

        Assert.True(janela.TemPlano);
        Assert.Equal("5h  84%", janela.TextoPlanoSessao);
        Assert.True(janela.SessaoEmAlerta);
        Assert.False(janela.SessaoCritica);
        Assert.True(janela.SemanaCritica);
        Assert.Contains("renova às", janela.DicaPlano);

        chat.Aplicar(new UsoDoPlano(null, 0.5, null));
        Assert.Equal(0.84, janela.PlanoSessao);
        Assert.False(janela.SemanaCritica);
    });

    [Fact]
    public Task QuandoAJanelaDoPlanoRenovaORodapeZeraSemReabrir() => Sessao.Rodar(() =>
    {
        var janela = new JanelaVM();
        janela.AbrirSessao(new Local("Loja", _pasta, []));
        var chat = janela.Abertas.Last();
        var renova = DateTimeOffset.Now.AddMinutes(2);
        chat.Aplicar(new UsoDoPlano(0.55, 0.32, renova, DateTimeOffset.Now.AddDays(3)));
        Assert.StartsWith("55% 1m", janela.TextoLimites);

        janela.VirarJanelas(renova.AddSeconds(1));

        Assert.Equal(0, janela.PlanoSessao);
        Assert.Null(janela.PlanoRenova);
        Assert.Equal(0.32, janela.PlanoSemana);
        Assert.StartsWith("0%", janela.TextoLimites);
        Assert.False(janela.TemAvisoDoPlano);

        chat.Aplicar(new UsoDoPlano(0.02, 0.33, DateTimeOffset.Now.AddHours(5)));
        Assert.StartsWith("2% 4h", janela.TextoLimites);
    });

    [Fact]
    public Task XFechaAConversaEEscondeASessaoDeFora() => Sessao.Rodar(() =>
    {
        var janela = new JanelaVM();
        janela.AbrirSessao(new Local("Loja", _pasta, []));
        var chat = janela.Abertas.Last();
        var geral = janela.VisaoGeral;

        chat.Estado = EstadoChat.Trabalhando;
        Assert.False(chat.PodeTirar);
        geral.TirarCommand.Execute(chat);
        Assert.Contains(chat, janela.Abertas);

        chat.Estado = EstadoChat.Pronta;
        Assert.True(chat.PodeTirar);
        geral.TirarCommand.Execute(chat);
        Assert.DoesNotContain(chat, janela.Abertas);

        var fora = new SessaoForaVM((_, _) => { }, (_, _, _) => Task.FromResult<ChatVM?>(null))
        {
            Dados = new SessaoAtiva(4242, "vs-x", _pasta, "loja", "claude-vscode", false, DateTimeOffset.Now.AddHours(-5)),
            AtividadeEm = DateTime.Now.AddHours(-5),
        };
        Assert.Contains("VS Code", fora.DicaTirar);
        Assert.False(VisaoGeralVM.Escondida(fora));
        geral.TirarCommand.Execute(fora);
        Assert.True(VisaoGeralVM.Escondida(fora));

        fora.AtividadeEm = DateTime.Now.AddMinutes(1);
        Assert.False(VisaoGeralVM.Escondida(fora));
        App.Config.SessoesEscondidas.Remove("vs-x");
    });

    [Fact]
    public Task FormularioDeAgendadoValidaEGuarda() => Sessao.Rodar(() =>
    {
        var pagina = new AgendadosVM(new JanelaVM());
        var antes = App.Config.Agendamentos.Count;

        pagina.SalvarCommand.Execute(null);
        Assert.Equal("Escolha o projeto.", pagina.Erro);

        pagina.NovoProjeto = new Local("Loja", _pasta, []);
        pagina.SalvarCommand.Execute(null);
        Assert.StartsWith("Escreva o pedido", pagina.Erro);

        pagina.NovoTexto = "liste os PRs abertos";
        pagina.NovaHora = "25:90";
        pagina.SalvarCommand.Execute(null);
        Assert.StartsWith("Use a hora", pagina.Erro);

        pagina.NovaHora = "8:30";
        pagina.SalvarCommand.Execute(null);
        try
        {
            Assert.Null(pagina.Erro);
            var novo = Assert.Single(pagina.Itens, i => i.Dados.Pasta == _pasta);
            Assert.Equal("liste os PRs abertos", novo.Nome);
            Assert.Equal("de segunda a sexta às 08:30", novo.Quando);
            Assert.Equal(antes + 1, App.Config.Agendamentos.Count);
        }
        finally
        {
            App.Config.Agendamentos.RemoveAll(a => a.Pasta == _pasta);
        }
    });

    [Fact]
    public Task TerminalDaSessaoRodaNaPastaDoProjeto() => Sessao.Rodar(() =>
    {
        if (!ChatVM.TemTerminal) return;
        var chat = NovaConversa();
        Assert.Null(chat.TerminalExibido);

        chat.AlternarTerminalCommand.Execute(null);
        Assert.True(chat.TerminalAberto);
        var terminal = Assert.IsType<TerminalVM>(chat.TerminalExibido);
        var saida = new System.Text.StringBuilder();
        terminal.Recebeu += texto => saida.Append(texto);

        terminal.Garantir(100, 30);
        terminal.Escrever("Write-Output ('nexora' + '-ok')\r");
        Esperar(() => saida.ToString().Contains("nexora-ok"), 45);

        Assert.Contains("nexora-ok", saida.ToString());
        Assert.Contains(Path.GetFileName(_pasta), saida.ToString());
        Assert.True(terminal.Rodando);

        chat.AlternarTerminalCommand.Execute(null);
        Assert.False(chat.TerminalAberto);
        Assert.True(terminal.Rodando);
        chat.Encerrar(esperar: true);
        Assert.False(terminal.Rodando);
    });

    [Fact]
    public Task CliqueNoLinkDeArquivoAvisaQuemMostra() => Sessao.Rodar(() =>
    {
        var texto = new MarkdownTexto { Texto = "Veja [o serviço](src/Calc/CalcAppService.cs#L12) antes." };
        var janela = new Window { Width = 600, Height = 200, Content = texto };
        janela.Show();
        Dispatcher.UIThread.RunJobs();
        var bloco = janela.GetVisualDescendants().OfType<SelectableTextBlock>().Single();
        var inicio = (bloco.Inlines!.Text ?? "").IndexOf("o serviço", StringComparison.Ordinal);
        var retangulo = bloco.TextLayout.HitTestTextPosition(inicio + 2);
        var ponto = bloco.TranslatePoint(retangulo.Center + new Avalonia.Point(bloco.Padding.Left, bloco.Padding.Top), janela)!.Value;
        string? clicado = null;
        void Pegar(MarkdownTexto _, string destino) => clicado = destino;
        MarkdownTexto.LinkClicado += Pegar;
        try
        {
            janela.MouseDown(ponto, Avalonia.Input.MouseButton.Left);
            janela.MouseUp(ponto, Avalonia.Input.MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("src/Calc/CalcAppService.cs#L12", clicado);

            clicado = null;
            var fora = bloco.TranslatePoint(new Avalonia.Point(4, retangulo.Center.Y + bloco.Padding.Top), janela)!.Value;
            janela.MouseDown(fora, Avalonia.Input.MouseButton.Left);
            janela.MouseUp(fora, Avalonia.Input.MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.Null(clicado);
        }
        finally
        {
            MarkdownTexto.LinkClicado -= Pegar;
        }
    });

    [Fact]
    public Task ConversaParadaAvisaQueOCacheExpirouERetomaPassagem() => Sessao.Rodar(() =>
    {
        var chat = NovaConversa();
        chat.Aplicar(new TextoCompleto("feito"));
        chat.Estado = EstadoChat.Pronta;
        chat.ContextoTokens = 684_000;
        chat.CacheLonga = true;
        chat.UltimaResposta = DateTimeOffset.Now.AddHours(-16).AddMinutes(-4);
        chat.AtualizarTempo();

        Assert.True(chat.AvisoCacheVisivel);
        Assert.Equal("Parada há 16 h 04 min. O cache do prompt provavelmente expirou: a próxima mensagem relê cerca de 684 mil tokens.", chat.AvisoCache);

        chat.UltimaResposta = DateTimeOffset.Now.AddMinutes(-20);
        chat.AtualizarTempo();
        Assert.False(chat.AvisoCacheVisivel);

        chat.Rascunho = "antes";
        new PassagemVM(new Core.Senna.Passagem("Handoff: afastamento-20261105", "Loja", DateTimeOffset.Now), chat).RetomarCommand.Execute(null);
        Assert.Equal("antes\nRetome o handoff \"Handoff: afastamento-20261105\".", chat.Rascunho);
    });

    static void Git(string pasta, params string[] argumentos)
    {
        var inicio = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = pasta, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in argumentos) inicio.ArgumentList.Add(a);
        using var processo = System.Diagnostics.Process.Start(inicio)!;
        processo.WaitForExit(20_000);
    }

    [Fact]
    public Task BranchDoPainelTrocaAntesDeComecar() => Sessao.Rodar(() =>
    {
        Git(_pasta, "init", "-b", "main");
        Git(_pasta, "config", "user.email", "teste@exemplo.com");
        Git(_pasta, "config", "user.name", "Teste");
        File.WriteAllText(Path.Combine(_pasta, "a.txt"), "a");
        Git(_pasta, "add", ".");
        Git(_pasta, "commit", "-m", "primeiro");
        Git(_pasta, "branch", "integracoes-api-20261104");
        var chat = NovaConversa();
        chat.AtualizarBranch();
        Assert.Equal("main", chat.Branch);
        var janela = new Window { Width = 1100, Height = 800, Content = new ChatView { DataContext = chat } };
        janela.Show();
        Dispatcher.UIThread.RunJobs();
        var seletor = janela.GetVisualDescendants().OfType<ComboBox>().First(c => ReferenceEquals(c.ItemsSource, chat.Locais));
        string[] TextosDoSeletor() => seletor.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToArray();
        Assert.Contains("main", TextosDoSeletor());

        chat.AbrirMenuBranchCommand.Execute(null);
        Esperar(() => chat.OpcoesBranch.Count == 2);
        Assert.True(chat.MenuBranchAberto);
        Assert.True(chat.OpcoesBranch.Single(o => o.Nome == "main").Selecionada);

        chat.FiltroBranch = "integra";
        Assert.Equal("integracoes-api-20261104", Assert.Single(chat.OpcoesBranch).Nome);

        chat.Estado = EstadoChat.Trabalhando;
        chat.OpcoesBranch[0].EscolherCommand.Execute(null);
        Esperar(() => chat.TemAvisoBranch);
        Assert.StartsWith("Tem uma sessão trabalhando", chat.AvisoBranch);
        Assert.Equal("main", chat.Branch);

        chat.Estado = EstadoChat.Pronta;
        chat.OpcoesBranch[0].EscolherCommand.Execute(null);
        Esperar(() => chat.Branch == "integracoes-api-20261104");
        Assert.Equal("integracoes-api-20261104", chat.Branch);
        Assert.False(chat.MenuBranchAberto);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("integracoes-api-20261104", TextosDoSeletor());
        Assert.DoesNotContain("main", TextosDoSeletor());
        Assert.Same(chat.Locais, seletor.ItemsSource);
        janela.Close();
    });

    [Fact]
    public Task BranchCriadaPeloClaudeApareceSemEsperarOFimDoTurno() => Sessao.Rodar(() =>
    {
        Git(_pasta, "init", "-b", "dev");
        Git(_pasta, "config", "user.email", "teste@exemplo.com");
        Git(_pasta, "config", "user.name", "Teste");
        File.WriteAllText(Path.Combine(_pasta, "a.txt"), "a");
        Git(_pasta, "add", ".");
        Git(_pasta, "commit", "-m", "primeiro");
        var chat = NovaConversa();
        chat.AtualizarBranch();
        Assert.Equal("dev", chat.Branch);
        chat.Estado = EstadoChat.Trabalhando;

        using var entrada = System.Text.Json.JsonDocument.Parse("{\"command\":\"git checkout -b views-personalizadas\"}");
        chat.Aplicar(new FerramentaChamada("t1", "Bash", entrada.RootElement.Clone()));
        Git(_pasta, "checkout", "-b", "views-personalizadas");
        Assert.Equal("dev", chat.Branch);
        chat.Aplicar(new FerramentaRespondeu("t1", "Switched to a new branch 'views-personalizadas'", false));

        Assert.Equal("views-personalizadas", chat.Branch);
        Assert.Equal(EstadoChat.Trabalhando, chat.Estado);
    });

    [Fact]
    public Task MenuDoModoMostraOPadraoResolvido() => Sessao.Rodar(() =>
    {
        var chat = NovaConversa();
        Assert.Equal("Perguntar sempre", chat.ModoRotulo);

        chat.AbrirMenuModoCommand.Execute(null);
        chat.OpcoesModo.First(o => o.Nome == "Planejar").EscolherCommand.Execute(null);

        Assert.Equal(4, chat.ModoIndice);
        Assert.Equal("Planejar", chat.ModoRotulo);
        Assert.False(chat.MenuModoAberto);
    });
}
