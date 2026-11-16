using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.Core.Claude;
using Nexora.Core.Historico;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class SubagentesTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-subagentes-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_pasta, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    ChatVM NovaConversa() => ChatVM.Nova(new Local("Loja", _pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });

    static JsonElement J(string json) => JsonDocument.Parse(json).RootElement.Clone();

    static FerramentaChamada Agent(string id, string descricao, string tipo = "Explore", string extra = "") =>
        new(id, "Agent", J($$"""{"description":"{{descricao}}","subagent_type":"{{tipo}}","prompt":"Faça: {{descricao}}"{{extra}}}"""));

    static EventoDeSubagente Dentro(string pai, EventoClaude evento) => new(pai, evento);

    static IEnumerable<string> TextosVisiveis(Window janela) =>
        janela.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Text is { Length: > 0 }).Select(t => t.Text!);

    static void Esperar(Func<bool> condicao)
    {
        var limite = DateTime.Now.AddSeconds(10);
        while (!condicao() && DateTime.Now < limite)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task SubagentGanhaCartaoESuasFerramentasFicamDentroDele() => Sessao.Rodar(() =>
    {
        var chat = NovaConversa();
        var janela = new Window { Width = 1100, Height = 800, Content = new ChatView { DataContext = chat } };
        janela.Show();
        chat.TurnoDesde = DateTime.Now.AddSeconds(-5);
        chat.Estado = EstadoChat.Trabalhando;

        chat.Aplicar(new FerramentaComecou("Agent"));
        chat.Aplicar(Agent("toolu_a", "Mapear o cupom", extra: ""","model":"haiku" """));
        Dispatcher.UIThread.RunJobs();

        var grupo = Assert.Single(chat.Itens.OfType<ItemSubagentes>());
        var agente = Assert.Single(grupo.Agentes);
        Assert.Equal(("Explore", "Mapear o cupom", "Haiku"), (agente.Tipo, agente.Descricao, agente.ModeloPedido));
        Assert.Equal("Faça: Mapear o cupom", agente.Pedido);
        Assert.Equal(EstadoSubagente.Rodando, agente.Estado);
        Assert.StartsWith("Rodando subagent…", chat.AndamentoTexto);
        Assert.True(chat.TemSubagentesRodando);
        Assert.Equal("1 subagent rodando", chat.TextoSubagentes);

        chat.Aplicar(Dentro("toolu_a", new TextoCompleto("Vou procurar onde o cupom é validado.")));
        Assert.Equal("Vou procurar onde o cupom é validado.", agente.Atividade);
        chat.Aplicar(Dentro("toolu_a", new FerramentaChamada("in1", "Grep", J("""{"pattern":"Cupom","path":"src"}"""))));
        chat.Aplicar(Dentro("toolu_a", new FerramentaChamada("in2", "Read", J($$"""{"file_path":"{{JsonEncodedText.Encode(Path.Combine(_pasta, "Cupom.cs"))}}"}"""))));
        chat.Aplicar(Dentro("toolu_a", new FerramentaRespondeu("in1", "Cupom.cs", false)));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(chat.Itens.OfType<ItemFerramenta>());
        Assert.Equal(["Grep", "Read"], agente.Ferramentas.Select(f => f.Nome));
        Assert.True(agente.Ferramentas[0].Ok);
        Assert.True(agente.Ferramentas[1].Rodando);
        Assert.Equal("Cupom.cs", agente.Ferramentas[1].Resumo);
        Assert.Equal("2 ferramentas", agente.TextoFerramentas);
        Assert.Equal("Read  Cupom.cs", agente.Atividade);
        Assert.Equal("Subagent Explore: Read  Cupom.cs", chat.UltimaAtividade);
        Assert.StartsWith("Rodando subagent…", chat.AndamentoTexto);
        Assert.Contains("Mapear o cupom", TextosVisiveis(janela));
        Assert.DoesNotContain("FERRAMENTAS", TextosVisiveis(janela));

        chat.Aplicar(new FerramentaRespondeu("toolu_a",
            "Achei **três pontos**.\n\n- CheckoutController\n- CupomService\nagentId: a1 (para continuar)\n<usage>total_tokens: 900\ntool_uses: 6\nduration_ms: 41000</usage>",
            false, new ResumoDeSubagente("completed", "a1", "Explore", 6, 41000, 900)));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(EstadoSubagente.Terminou, agente.Estado);
        Assert.Equal("Achei **três pontos**.\n\n- CheckoutController\n- CupomService", agente.Resultado);
        Assert.Equal("Achei três pontos.", agente.Previa);
        Assert.Equal(6, agente.QuantasFerramentas);
        Assert.Equal("41 s", agente.TempoTexto);
        Assert.True(agente.Ferramentas.All(f => f.Concluida));
        Assert.False(chat.TemSubagentesRodando);
        Assert.Equal(FaseTurno.Pensando, chat.Fase);

        agente.AlternarCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var visiveis = TextosVisiveis(janela).ToList();
        Assert.Contains("FERRAMENTAS", visiveis);
        Assert.Contains("RESULTADO", visiveis);
        Assert.Contains("Grep", visiveis);
        Assert.Contains("terminou", visiveis);
        janela.Close();
    });

    [Fact]
    public Task SubagentsEmParaleloFicamEmpilhadosNoMesmoGrupo() => Sessao.Rodar(() =>
    {
        var chat = NovaConversa();
        chat.Estado = EstadoChat.Trabalhando;

        chat.Aplicar(Agent("p1", "Ler o checkout"));
        chat.Aplicar(Agent("p2", "Ler os testes", "general-purpose"));
        chat.Aplicar(Agent("p3", "Ler a API", "Plan"));

        var grupo = Assert.Single(chat.Itens.OfType<ItemSubagentes>());
        Assert.True(grupo.Varios);
        Assert.Equal("3 subagents em paralelo", grupo.Titulo);
        Assert.Equal("3 rodando", grupo.Resumo);
        Assert.StartsWith("Rodando 3 subagents…", chat.AndamentoTexto);
        Assert.Equal("3 subagents rodando", chat.TextoSubagentes);

        chat.Aplicar(Dentro("p2", new FerramentaChamada("x", "Bash", J("""{"command":"dotnet test"}"""))));
        Assert.Single(grupo.Agentes[1].Ferramentas);
        Assert.Empty(grupo.Agentes[0].Ferramentas);

        chat.Aplicar(new FerramentaRespondeu("p1", "ok", false));
        chat.Aplicar(new FerramentaRespondeu("p3", "Tool permission denied", true));
        Assert.Equal("1 rodando  ·  1 terminou  ·  1 deu erro", grupo.Resumo);
        Assert.StartsWith("Rodando subagent…", chat.AndamentoTexto);
        Assert.True(grupo.Agentes[2].Falhou);

        chat.Aplicar(new FerramentaRespondeu("p2", "[Request interrupted by user for tool use]", true));
        Assert.True(grupo.Agentes[1].Parou);
        Assert.Equal(FaseTurno.Pensando, chat.Fase);

        chat.Aplicar(Agent("p4", "Revisar"));
        Assert.Equal(2, chat.Itens.OfType<ItemSubagentes>().Count());
        Assert.False(chat.Itens.OfType<ItemSubagentes>().Last().Varios);
    });

    [Fact]
    public Task SubagentEmSegundoPlanoMantemATrabalhandoAteOAvisoDeTarefa() => Sessao.Rodar(async () =>
    {
        var antes = ChatVM.EsperaDepoisDoFundo;
        ChatVM.EsperaDepoisDoFundo = TimeSpan.FromMilliseconds(80);
        try
        {
            var chat = NovaConversa();
            chat.TurnoDesde = DateTime.Now.AddSeconds(-5);
            chat.Estado = EstadoChat.Trabalhando;
            chat.Aplicar(Agent("bg", "Rodar a suíte", "general-purpose", ""","run_in_background":true"""));
            chat.Aplicar(new FerramentaRespondeu("bg", "Async agent launched successfully.", false, new ResumoDeSubagente("async_launched", "a9", null, null, null, null)));
            var agente = Assert.Single(chat.Subagentes);

            Assert.Equal(EstadoSubagente.SegundoPlano, agente.Estado);
            Assert.Equal("em segundo plano", agente.EstadoTexto);
            Assert.Null(agente.Resultado);

            chat.Aplicar(new TurnoEncerrado(false, "Deixei os testes rodando.", null, 3000));
            Assert.Equal(EstadoChat.Trabalhando, chat.Estado);
            Assert.Null(chat.TurnoDesde);
            Assert.Equal("Trabalhando", chat.EstadoTexto);
            Assert.Equal("Esperando 1 subagent em segundo plano…", chat.AndamentoTexto);
            Assert.Equal("Esperando 1 subagent em segundo plano: Rodar a suíte", chat.UltimaAtividade);
            Assert.Equal(EstadoSubagente.SegundoPlano, agente.Estado);
            Assert.Equal("1 subagent rodando", chat.TextoSubagentes);

            chat.Aplicar(new TarefaAtualizada(FaseTarefa.Andou, "bg", Ferramentas: 4, UltimaFerramenta: "Bash", Resumo: "Rodando os testes de integração"));
            Assert.Equal(EstadoChat.Trabalhando, chat.Estado);
            Assert.Equal(4, agente.QuantasFerramentas);
            Assert.True(agente.SoContagem);
            Assert.Equal("Rodando os testes de integração", agente.Atividade);

            chat.Aplicar(new TarefaAtualizada(FaseTarefa.Terminou, "bg", "completed", 5, 61000, Resultado: "Todos os 230 testes passaram."));
            Assert.Equal(EstadoSubagente.Terminou, agente.Estado);
            Assert.Equal("Todos os 230 testes passaram.", agente.Resultado);
            Assert.Equal("1 min 01 s", agente.TempoTexto);
            Assert.False(chat.TemSubagentesRodando);
            Assert.Equal(EstadoChat.Trabalhando, chat.Estado);

            for (var i = 0; i < 60 && chat.Estado != EstadoChat.Pronta; i++) await Task.Delay(50);
            Assert.Equal(EstadoChat.Pronta, chat.Estado);
        }
        finally
        {
            ChatVM.EsperaDepoisDoFundo = antes;
        }
    });

    [Fact]
    public Task AvisoDoSubagentSeguidoDeNovoTurnoNaoSoltaAPronta() => Sessao.Rodar(() =>
    {
        var antes = ChatVM.EsperaDepoisDoFundo;
        ChatVM.EsperaDepoisDoFundo = TimeSpan.FromMilliseconds(80);
        try
        {
            var chat = NovaConversa();
            chat.TurnoDesde = DateTime.Now;
            chat.Estado = EstadoChat.Trabalhando;
            chat.Aplicar(Agent("b1", "Primeiro", "general-purpose", ""","run_in_background":true"""));
            chat.Aplicar(new FerramentaRespondeu("b1", "Async agent launched successfully.", false, new ResumoDeSubagente("async_launched", "x1", null, null, null, null)));
            chat.Aplicar(Agent("b2", "Segundo", "general-purpose", ""","run_in_background":true"""));
            chat.Aplicar(new FerramentaRespondeu("b2", "Async agent launched successfully.", false, new ResumoDeSubagente("async_launched", "x2", null, null, null, null)));
            chat.Aplicar(new TurnoEncerrado(false, "Lancei dois.", null, 1000));
            Assert.Equal("Esperando 2 subagents em segundo plano…", chat.AndamentoTexto);

            chat.Aplicar(new TarefaAtualizada(FaseTarefa.Terminou, "b1", "completed", 2, 5000, Resultado: "Primeiro pronto."));
            Assert.Equal(EstadoChat.Trabalhando, chat.Estado);
            Assert.Equal("Esperando 1 subagent em segundo plano…", chat.AndamentoTexto);

            chat.Aplicar(Dentro("b2", new FerramentaChamada("z", "Read", J("""{"file_path":"a.cs"}"""))));
            Assert.Null(chat.TurnoDesde);

            chat.Aplicar(new PensamentoComecou());
            Assert.NotNull(chat.TurnoDesde);
            Assert.StartsWith("Pensando…", chat.AndamentoTexto);
            chat.Aplicar(new TextoCompleto("Vi que o primeiro terminou."));
            chat.Aplicar(new TurnoEncerrado(false, null, null, 2000));
            Assert.Equal(EstadoChat.Trabalhando, chat.Estado);
            Assert.Null(chat.TurnoDesde);

            chat.Aplicar(new TarefaAtualizada(FaseTarefa.Terminou, "b2", "completed", 3, 9000, Resultado: "Segundo pronto."));
            chat.Aplicar(new TextoCompleto("Os dois terminaram."));
            Assert.NotNull(chat.TurnoDesde);
            chat.Aplicar(new TurnoEncerrado(false, null, null, 2000));
            Assert.Equal(EstadoChat.Pronta, chat.Estado);
            Assert.Equal(["Primeiro pronto.", "Segundo pronto."], chat.Subagentes.Select(a => a.Resultado));
        }
        finally
        {
            ChatVM.EsperaDepoisDoFundo = antes;
        }
    });

    static ChatVM ConversaComSubagentNoFundo(ChatVM chat)
    {
        chat.TurnoDesde = DateTime.Now;
        chat.Estado = EstadoChat.Trabalhando;
        chat.Aplicar(Agent("bg", "Rodar a suíte", "general-purpose", ""","run_in_background":true"""));
        chat.Aplicar(new FerramentaRespondeu("bg", "Async agent launched successfully.", false, new ResumoDeSubagente("async_launched", "a9", null, null, null, null)));
        chat.Aplicar(new TurnoEncerrado(false, "Ok.", null, 1000));
        Assert.Equal(EstadoChat.Trabalhando, chat.Estado);
        Assert.Null(chat.TurnoDesde);
        return chat;
    }

    [Fact]
    public Task EsperandoSubagentEmSegundoPlanoAMensagemSeguePeloCaminhoNormal() => Sessao.Rodar(() =>
    {
        var pasta = Directory.CreateTempSubdirectory("nexora-fundo-").FullName;
        var chat = ConversaComSubagentNoFundo(ChatVM.Nova(new Local("Loja", pasta, []), new AnfitriaoFalso(), _ => { }, _ => { }));
        Directory.Delete(pasta);

        chat.Rascunho = "Enquanto isso, olhe o README";
        chat.EnviarCommand.Execute(null);

        Assert.Empty(chat.Fila);
        Assert.Equal("Enquanto isso, olhe o README", Assert.Single(chat.Itens.OfType<ItemUsuario>()).Texto);
    });

    [Fact]
    public Task PararEnquantoEsperaOSubagentEmSegundoPlanoSoltaAPronta() => Sessao.Rodar(() =>
    {
        var chat = ConversaComSubagentNoFundo(NovaConversa());

        chat.PararCommand.Execute(null);

        Assert.Equal(EstadoChat.Pronta, chat.Estado);
        Assert.Equal(EstadoSubagente.Parado, Assert.Single(chat.Subagentes).Estado);
        Assert.False(chat.TemSubagentesRodando);
    });

    [Fact]
    public Task SubagentsDeDoisPaisIntercaladosNoStreamCaemCadaUmNoSeuCartao() => Sessao.Rodar(() =>
    {
        var chat = NovaConversa();
        chat.TurnoDesde = DateTime.Now;
        chat.Estado = EstadoChat.Trabalhando;
        string[] linhas =
        [
            """{"type":"assistant","message":{"model":"claude-sonnet-4-5","content":[{"type":"tool_use","id":"toolu_A","name":"Agent","input":{"description":"Ler o checkout","subagent_type":"Explore","prompt":"a"}},{"type":"tool_use","id":"toolu_B","name":"Agent","input":{"description":"Ler os testes","subagent_type":"general-purpose","prompt":"b"}}],"usage":{"input_tokens":10,"output_tokens":5}},"parent_tool_use_id":null}""",
            """{"type":"assistant","message":{"content":[{"type":"tool_use","id":"a1","name":"Grep","input":{"pattern":"Cupom"}}]},"parent_tool_use_id":"toolu_A"}""",
            """{"type":"assistant","message":{"content":[{"type":"tool_use","id":"b1","name":"Glob","input":{"pattern":"tests/**"}}]},"parent_tool_use_id":"toolu_B"}""",
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"b1","content":"x.cs"}]},"parent_tool_use_id":"toolu_B"}""",
            """{"type":"assistant","message":{"content":[{"type":"tool_use","id":"a2","name":"Read","input":{"file_path":"a.cs"}},{"type":"tool_use","id":"a3","name":"Bash","input":{"command":"ls"}}]},"parent_tool_use_id":"toolu_A"}""",
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"a1","content":"a.cs"}]},"parent_tool_use_id":"toolu_A"}""",
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"toolu_B","content":"Testes lidos."}]},"parent_tool_use_id":null,"tool_use_result":{"status":"completed","agentId":"ag-b","agentType":"general-purpose","totalToolUseCount":1,"totalDurationMs":4000,"totalTokens":50}}""",
        ];
        foreach (var linha in linhas)
            foreach (var evento in Protocolo.Ler(linha)) chat.Aplicar(evento);

        var grupo = Assert.Single(chat.Itens.OfType<ItemSubagentes>());
        var (a, b) = (grupo.Agentes[0], grupo.Agentes[1]);
        Assert.Equal(["Ler o checkout", "Ler os testes"], [a.Descricao, b.Descricao]);
        Assert.Equal(["Grep", "Read", "Bash"], a.Ferramentas.Select(f => f.Nome));
        Assert.Equal(["Glob"], b.Ferramentas.Select(f => f.Nome));
        Assert.True(a.Ferramentas[0].Concluida);
        Assert.False(a.Ferramentas[1].Concluida);
        Assert.True(b.Ferramentas[0].Ok);
        Assert.Equal(EstadoSubagente.Rodando, a.Estado);
        Assert.Equal(EstadoSubagente.Terminou, b.Estado);
        Assert.Equal("Testes lidos.", b.Resultado);
        Assert.Empty(chat.Itens.OfType<ItemFerramenta>());
    });

    [Fact]
    public Task TurnoQueAcabaComSubagentAbertoMarcaQueEleParou() => Sessao.Rodar(() =>
    {
        var chat = NovaConversa();
        chat.Estado = EstadoChat.Trabalhando;
        chat.Aplicar(Agent("s1", "Explorar"));
        chat.Aplicar(Dentro("s1", new FerramentaChamada("r", "Read", J("""{"file_path":"a.cs"}"""))));

        chat.Aplicar(new TurnoEncerrado(true, "Interrompido", null, 1000));

        var agente = Assert.Single(chat.Subagentes);
        Assert.Equal(EstadoSubagente.Parado, agente.Estado);
        Assert.True(agente.Ferramentas.Single().Concluida);
        Assert.False(chat.TemSubagentesRodando);
    });

    [Fact]
    public Task HistoricoMontaOCartaoComAsFerramentasGravadasDoSubagent() => Sessao.Rodar(() =>
    {
        var arquivo = Path.Combine(_pasta, "s-sub.jsonl");
        File.WriteAllLines(arquivo,
        [
            """{"type":"user","message":{"role":"user","content":"Revise o cupom"},"timestamp":"2026-11-06T10:00:00Z","sessionId":"s-sub"}""",
            """{"type":"assistant","message":{"content":[{"type":"tool_use","id":"ag1","name":"Agent","input":{"description":"Mapear o cupom","subagent_type":"Explore","prompt":"Ache"}},{"type":"tool_use","id":"ag2","name":"Agent","input":{"description":"Rodar testes","subagent_type":"general-purpose","prompt":"Rode","run_in_background":true}}]},"sessionId":"s-sub"}""",
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"ag2","content":"Async agent launched successfully."}]},"toolUseResult":{"isAsync":true,"status":"async_launched","agentId":"bg2"},"sessionId":"s-sub"}""",
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"ag1","content":[{"type":"text","text":"Três pontos.\n<usage>tool_uses: 2\nduration_ms: 9000</usage>"}]}]},"toolUseResult":{"status":"completed","agentId":"ex1","agentType":"Explore","totalToolUseCount":2,"totalDurationMs":9000,"totalTokens":100},"sessionId":"s-sub"}""",
            """{"type":"assistant","message":{"content":[{"type":"text","text":"Mapeado. Os testes seguem rodando."}]},"sessionId":"s-sub"}""",
            """{"type":"user","message":{"role":"user","content":"<task-notification>\n<tool-use-id>ag2</tool-use-id>\n<status>completed</status>\n<result>Passou tudo.</result>\n<usage><tool_uses>3</tool_uses><duration_ms>70000</duration_ms></usage>\n</task-notification>"},"sessionId":"s-sub"}""",
        ]);
        var internos = Directory.CreateDirectory(Path.Combine(_pasta, "s-sub", "subagents")).FullName;
        File.WriteAllLines(Path.Combine(internos, "agent-ex1.jsonl"),
        [
            """{"type":"assistant","isSidechain":true,"agentId":"ex1","message":{"content":[{"type":"tool_use","id":"g1","name":"Grep","input":{"pattern":"Cupom"}}]}}""",
            """{"type":"user","isSidechain":true,"agentId":"ex1","message":{"content":[{"type":"tool_result","tool_use_id":"g1","content":"a.cs"}]}}""",
            """{"type":"assistant","isSidechain":true,"agentId":"ex1","message":{"content":[{"type":"tool_use","id":"r1","name":"Read","input":{"file_path":"a.cs"}}]}}""",
        ]);

        var chat = ChatVM.DeConversa(new ConversaSalva("s-sub", arquivo, "Cupom", _pasta, null, DateTime.Now), new Local("Loja", _pasta, []),
            new AnfitriaoFalso(), _ => { }, _ => { });
        Esperar(() => chat.Carregamento.IsCompleted);

        var grupo = Assert.Single(chat.Itens.OfType<ItemSubagentes>());
        Assert.Equal(2, grupo.Agentes.Count);
        var (explore, testes) = (grupo.Agentes[0], grupo.Agentes[1]);
        Assert.Equal(EstadoSubagente.Terminou, explore.Estado);
        Assert.Equal(["Grep", "Read"], explore.Ferramentas.Select(f => f.Nome));
        Assert.True(explore.Ferramentas.All(f => f.Concluida));
        Assert.Equal("Três pontos.", explore.Resultado);
        Assert.Equal("9 s", explore.TempoTexto);
        Assert.Equal(EstadoSubagente.Terminou, testes.Estado);
        Assert.Equal("Passou tudo.", testes.Resultado);
        Assert.Equal(3, testes.QuantasFerramentas);
        Assert.Empty(chat.Itens.OfType<ItemFerramenta>());
        Assert.False(chat.TemSubagentesRodando);
    });

    [Fact]
    public Task DemoMostraSubagentsEmParaleloEOQuadroContaOsQueRodam() => Sessao.Rodar(() =>
    {
        var demoAntes = JanelaVM.Demo;
        JanelaVM.Demo = true;
        try
        {
            var janela = new JanelaVM();
            janela.PreencherDemo();
            var chat = Assert.IsType<ChatVM>(janela.Atual);

            var grupo = Assert.Single(chat.Itens.OfType<ItemSubagentes>());
            Assert.Equal(2, grupo.Agentes.Count);
            Assert.Equal(EstadoSubagente.Terminou, grupo.Agentes[0].Estado);
            Assert.Equal(7, grupo.Agentes[0].QuantasFerramentas);
            Assert.Equal(EstadoSubagente.SegundoPlano, grupo.Agentes[1].Estado);
            Assert.Equal("cupom-checkout", chat.Branch);
            Assert.Equal("1 subagent rodando", chat.TextoSubagentes);

            var tela = new Window { Width = 1440, Height = 900, Content = new VisaoGeralView { DataContext = janela.VisaoGeral } };
            tela.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("1 subagent rodando", TextosVisiveis(tela));
            tela.Close();
            foreach (var aberta in janela.Abertas) aberta.Encerrar();
        }
        finally
        {
            JanelaVM.Demo = demoAntes;
        }
    });

    [Fact]
    public Task PainelListaTodosOsSubagentsComAvatar() => Sessao.Rodar(() =>
    {
        Avalonia.Application.Current!.Resources["FundoSolido"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FBF7EC"));
        var chat = NovaConversa();
        var vista = new ChatView { DataContext = chat };
        var janela = new Window { Width = 1100, Height = 800, Content = vista };
        janela.Show();
        Dispatcher.UIThread.RunJobs();
        var botao = vista.FindControl<Button>("BotaoSubagentes")!;
        Assert.False(botao.IsEffectivelyVisible);

        chat.TurnoDesde = DateTime.Now.AddSeconds(-5);
        chat.Estado = EstadoChat.Trabalhando;
        chat.Aplicar(new FerramentaComecou("Agent"));
        chat.Aplicar(Agent("a", "Pesquisar fontes", "Explore", extra: ""","model":"opus" """));
        chat.Aplicar(Agent("b", "Planejar a mudança", "Plan", extra: ""","model":"sonnet" """));
        chat.Aplicar(Agent("c", "Organizar o resumo", "general-purpose", extra: ""","model":"haiku" """));
        chat.Aplicar(Agent("d", "Conferir os dados", "code-reviewer"));
        chat.Aplicar(new FerramentaRespondeu("b", "Plano pronto", false, new ResumoDeSubagente("completed", "x", "Plan", 7, 61000, 32000, "Plano pronto")));
        chat.Aplicar(new FerramentaRespondeu("c", "falhou", true));
        Dispatcher.UIThread.RunJobs();

        Assert.True(botao.IsEffectivelyVisible);
        Assert.Equal(4, chat.PainelSubagentes.Count);
        Assert.Equal("2", chat.TextoBotaoSubagentes);

        chat.AbrirSubagentesCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(vista.FindControl<Avalonia.Controls.Primitives.Popup>("MenuSubagentes")!.IsOpen);
        var avatares = vista.GetVisualDescendants().Concat(((Avalonia.Controls.Primitives.Popup)vista.FindControl<Avalonia.Controls.Primitives.Popup>("MenuSubagentes")!).Child!.GetVisualDescendants()).OfType<AvatarVivo>().Where(a => a.Subagente is not null).ToList();
        Assert.Equal(4, avatares.Count);
        Assert.Equal(["Explore", "Plan", "general-purpose", "code-reviewer"], avatares.Select(a => a.Subagente));
        Assert.Equal(Nexora.Core.Avatares.HumorAvatar.Pensando, avatares[0].Humor);
        Assert.True(avatares[2].Desbotado);

        var pasta = Environment.GetEnvironmentVariable("NEXORA_FOTOS");
        if (pasta is not null)
        {
            Directory.CreateDirectory(pasta);
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            janela.CaptureRenderedFrame()?.Save(Path.Combine(pasta, "painel-subagents.png"));
        }
    });

    [Fact]
    public Task FolhaDeAvatares() => Sessao.Rodar(() =>
    {
        var pasta = Environment.GetEnvironmentVariable("NEXORA_FOTOS");
        if (pasta is null) return;
        Directory.CreateDirectory(pasta);
        var grade = new Grid { Background = Avalonia.Media.Brushes.White, Margin = new Avalonia.Thickness(16) };
        string[] tipos = ["Explore", "Plan", "general-purpose", "code-reviewer"];
        for (var c = 0; c < 4; c++) grade.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var l = 0; l < tipos.Length; l++)
        {
            grade.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var c = 0; c < 4; c++)
            {
                var humor = c switch { 1 => Nexora.Core.Avatares.HumorAvatar.Pensando, 2 => Nexora.Core.Avatares.HumorAvatar.Feliz, _ => Nexora.Core.Avatares.HumorAvatar.Parado };
                var m = new AvatarVivo { Subagente = tipos[l], Humor = humor, Desbotado = c == 3, Width = 120, Height = 120, Margin = new Avalonia.Thickness(8) };
                Grid.SetRow(m, l);
                Grid.SetColumn(m, c);
                grade.Children.Add(m);
            }
        }
        var janela = new Window { Width = 620, Height = 620, Content = grade };
        janela.Show();
        Esperar(() => grade.Children.OfType<AvatarVivo>().All(a => a.Preparado is not null));
        for (var n = 0; n < 3; n++)
        {
            Thread.Sleep(n == 0 ? 50 : 170);
            foreach (var m in grade.Children.OfType<AvatarVivo>()) m.InvalidateVisual();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            janela.CaptureRenderedFrame()?.Save(Path.Combine(pasta, $"avatares-{n}.png"));
        }
    });
}
