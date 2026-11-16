using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.Core.Claude;
using Nexora.Core.Codex;
using Nexora.Core.Historico;
using Nexora.Core.Texto;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

sealed class SessaoFalsa(OpcoesSessao opcoes, string agente) : ISessaoAgente
{
    public OpcoesSessao Opcoes { get; } = opcoes;
    public List<string> Enviados { get; } = [];
    public List<string> Permitidos { get; } = [];
    public bool Descartada { get; private set; }

    public event Action<EventoClaude>? Evento;
    public event Action<int, string>? Encerrou;

    public string Agente { get; } = agente;
    public string? SessaoId => Opcoes.Retomar;
    public bool Ativa => !Descartada;
    public int? Pid => null;
    public bool TemRemoto => false;
    public bool TemControles => true;

    public void Emitir(EventoClaude evento) => Evento?.Invoke(evento);
    public void Iniciar() { }
    public void Enviar(string texto, IReadOnlyList<Anexo>? imagens = null) => Enviados.Add(texto);
    public void Permitir(string pedidoId, JsonNode? entrada, JsonNode? regras = null) => Permitidos.Add(pedidoId);
    public void Negar(string pedidoId, string motivo) { }
    public List<(string Id, JsonNode? Entrada)> Liberados { get; } = [];
    public void LiberarSubagente(string pedidoId, JsonNode? entrada) => Liberados.Add((pedidoId, entrada));
    public void Interromper() { }
    public void MudarModo(string modo) { }
    public void MudarModelo(string? modelo) { }
    public void AplicarConfiguracoes(JsonObject configuracoes) { }
    public Task<ControleRespondido> PedirAsync(Func<string, string> montar, TimeSpan? limite = null) => Task.FromResult(new ControleRespondido("", false, null, null));
    public Task<ControleRespondido> LigarRemotoAsync(bool ligado) => PedirAsync(_ => "");

    public void Encerrar()
    {
        Descartada = true;
        Encerrou?.Invoke(0, "");
    }

    public ValueTask DisposeAsync()
    {
        Descartada = true;
        return ValueTask.CompletedTask;
    }
}

[Collection("Idioma")]
public sealed class MelhoriasDoChatTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-melhorias-").FullName;
    readonly List<SessaoFalsa> _sessoes = [];
    readonly string _modeloDosSubagentes = App.Config.ModeloDosSubagentes;
    readonly int _subagentesAoMesmoTempo = App.Config.SubagentesAoMesmoTempo;

    public MelhoriasDoChatTestes()
    {
        ChatVM.SessaoDeTeste = (opcoes, agente) =>
        {
            var sessao = new SessaoFalsa(opcoes, agente);
            _sessoes.Add(sessao);
            return sessao;
        };
    }

    public void Dispose()
    {
        ChatVM.SessaoDeTeste = null;
        App.Config.ModeloDosSubagentes = _modeloDosSubagentes;
        App.Config.SubagentesAoMesmoTempo = _subagentesAoMesmoTempo;
        VagasDeSubagentes.Zerar();
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

    static string Conteudo(TextBlock bloco) => bloco.Text ?? bloco.Inlines?.Text ?? "";

    static async Task EsperarAsync(Func<bool> condicao, int milissegundos = 5000)
    {
        var limite = DateTime.Now.AddMilliseconds(milissegundos);
        while (!condicao() && DateTime.Now < limite) await Task.Delay(15);
        Dispatcher.UIThread.RunJobs();
    }

    static IEnumerable<ItemAviso> Trocas(ChatVM chat) => chat.Itens.OfType<ItemAviso>().Where(a => a.EhTroca);

    [Fact]
    public void CitacaoViraBlocoMarkdownEVolta()
    {
        var montado = Citacoes.Montar("Linha A\n\nLinha B", "Por quê?");
        Assert.Equal("> Linha A\n>\n> Linha B\n\nPor quê?", montado);
        Assert.Equal(("Linha A\n\nLinha B", "Por quê?"), Citacoes.Separar(montado));
        Assert.Equal((null, "> sem linha em branco\ncontinua"), Citacoes.Separar("> sem linha em branco\ncontinua"));
        Assert.Equal((null, "texto comum"), Citacoes.Separar("texto comum"));
        Assert.Equal("só texto", Citacoes.Montar(null, "só texto"));
    }

    [Fact]
    public Task TrocaDeModeloEEsforcoAparecemNaConversaSemRepetir() => Sessao.Rodar(() =>
    {
        ModelosDeTeste.Usar();
        var chat = NovaConversa();
        chat.Aplicar(new TextoCompleto("olá"));

        chat.AbrirMenuModeloCommand.Execute(null);
        chat.OpcoesModelo.First(o => o.Nome == "Opus 5.5").EscolherCommand.Execute(null);
        Assert.Equal("Modelo trocado para Opus 5.5", Assert.Single(Trocas(chat)).Texto);

        chat.AbrirMenuModeloCommand.Execute(null);
        chat.OpcoesEsforco.First(e => e.Rotulo == "Alto").EscolherCommand.Execute(null);
        chat.OpcoesEsforco.First(e => e.Rotulo == "Máximo").EscolherCommand.Execute(null);
        Assert.Equal(["Modelo trocado para Opus 5.5", "Esforço: Máximo"], Trocas(chat).Select(t => t.Texto));

        chat.Aplicar(new TextoCompleto("respondi"));
        chat.OpcoesEsforco.First(e => e.Rotulo == "Baixo").EscolherCommand.Execute(null);
        Assert.Equal("Esforço: Baixo", Trocas(chat).Last().Texto);
        Assert.Equal(3, Trocas(chat).Count());
    });

    [Fact]
    public Task MenuDeModeloAvisaDoCustoQuandoAConversaEstaGrande() => Sessao.Rodar(() =>
    {
        ModelosDeTeste.Usar();
        var chat = NovaConversa();
        chat.Aplicar(new TextoCompleto("olá"));
        chat.ContextoTokens = 12_000;
        chat.AbrirMenuModeloCommand.Execute(null);
        Assert.False(chat.TemAvisoCustoTroca);

        chat.ContextoTokens = 850_000;
        (chat.CacheLonga, chat.UltimaResposta) = (true, DateTimeOffset.Now.AddMinutes(-2));
        chat.AbrirMenuModeloCommand.Execute(null);
        Assert.True(chat.TemAvisoCustoTroca);
        Assert.Contains("~850 mil tokens sem cache", chat.AvisoCustoTroca);

        chat.UltimaResposta = DateTimeOffset.Now.AddHours(-3);
        Assert.False(chat.TemAvisoCustoTroca);
    });

    [Fact]
    public Task ResponderPoeACitacaoNoCampoEOAgenteRecebeEmMarkdown() => Sessao.Rodar(() =>
    {
        var chat = NovaConversa();
        chat.Aplicar(new TextoCompleto("Primeira linha da resposta.\nSegunda linha."));

        chat.ResponderA("Segunda linha.");
        Assert.True(chat.TemCitacao);
        chat.Rascunho = "Por que a segunda?";
        chat.EnviarCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(chat.TemCitacao);
        var sessao = Assert.Single(_sessoes);
        Assert.Equal(["> Segunda linha.\n\nPor que a segunda?"], sessao.Enviados);
        var enviada = chat.Itens.OfType<ItemUsuario>().Last();
        Assert.Equal("Por que a segunda?", enviada.Texto);
        Assert.True(enviada.TemCitacao);
        Assert.Equal("Segunda linha.", enviada.Citacao!.Texto);
        Assert.Equal("Por que a segunda?", chat.Titulo);

        var doHistorico = new ItemUsuario(sessao.Enviados[0]);
        Assert.Equal(("Segunda linha.", "Por que a segunda?"), (doHistorico.Citacao?.Texto, doHistorico.Texto));
    });

    [Fact]
    public Task BotaoResponderCitaSoOTrechoSelecionado() => Sessao.Rodar(() =>
    {
        var chat = NovaConversa();
        chat.Aplicar(new TextoCompleto("Use o cupom NATAL10 no checkout."));
        var janela = new Window { Width = 1100, Height = 800, Content = new ChatView { DataContext = chat } };
        janela.Show();
        Dispatcher.UIThread.RunJobs();

        var botao = janela.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("responder-msg"));
        var bloco = janela.GetVisualDescendants().OfType<SelectableTextBlock>().First(b => Conteudo(b).Contains("NATAL10"));
        var inicio = Conteudo(bloco).IndexOf("NATAL10", StringComparison.Ordinal);
        bloco.SelectionStart = inicio;
        bloco.SelectionEnd = inicio + "NATAL10".Length;
        botao.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("NATAL10", chat.Citacao?.Texto);

        chat.Citacao!.RemoverCommand.Execute(null);
        Assert.False(chat.TemCitacao);
        bloco.SelectionEnd = bloco.SelectionStart;
        botao.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("Use o cupom NATAL10 no checkout.", chat.Citacao?.Texto);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(janela.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "Use o cupom NATAL10 no checkout." && t.FindAncestorOfType<ChatView>() is not null && t is not SelectableTextBlock);
        janela.Close();
    });

    [Fact]
    public Task ClearComecaSessaoNovaNaMesmaConversa() => Sessao.Rodar(() =>
    {
        var chat = NovaConversa();
        chat.EnviarTexto("primeira pergunta");
        Dispatcher.UIThread.RunJobs();
        var primeira = Assert.Single(_sessoes);
        primeira.Emitir(new SessaoIniciada("sessao-1", "claude-opus-5-5", _pasta, null));
        primeira.Emitir(new ContextoUsado(90_000, true, "claude-opus-5-5"));
        primeira.Emitir(new TextoCompleto("resposta antiga"));
        primeira.Emitir(new TurnoEncerrado(false, null, null, 1000));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("sessao-1", chat.SessaoId);

        chat.EnviarTexto("/clear");
        Dispatcher.UIThread.RunJobs();

        Assert.True(primeira.Descartada);
        Assert.DoesNotContain("/clear", primeira.Enviados);
        Assert.Null(chat.SessaoId);
        Assert.Null(chat.ContextoTokens);
        Assert.Equal(EstadoChat.Pronta, chat.Estado);
        var divisor = Assert.Single(chat.Itens.OfType<ItemAviso>(), a => a.EhNovaSessao);
        Assert.Equal("Nova sessão — o agente não lembra do que veio antes", divisor.Texto);
        Assert.Contains(chat.Itens.OfType<ItemTexto>(), t => t.Texto == "resposta antiga");
        Assert.True(chat.Itens.IndexOf(divisor) > chat.Itens.IndexOf(chat.Itens.OfType<ItemTexto>().First()));

        chat.EnviarTexto("pergunta nova");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, _sessoes.Count);
        Assert.Null(_sessoes[1].Opcoes.Retomar);
        Assert.Equal(["pergunta nova"], _sessoes[1].Enviados);
    });

    [Fact]
    public Task ClearApareceComoComandoParaQualquerAgente() => Sessao.Rodar(async () =>
    {
        var chat = NovaConversa();
        chat.EscolherAgente(Agentes.Codex);
        chat.Aplicar(new ComandosDisponiveis([new ComandoBarra("review", null, null)], false));
        await chat.BuscarComandosAsync("cle");
        Assert.Contains(chat.ComandosSugeridos, c => c.Nome == "clear");
    });

    [Fact]
    public Task AgenteCaladoPorMuitoTempoContinuaTrabalhandoSemAviso() => Sessao.Rodar(async () =>
    {
        var chat = NovaConversa();
        chat.EnviarTexto("pense bastante");
        Dispatcher.UIThread.RunJobs();
        await EsperarAsync(() => false, 400);
        Assert.Equal(EstadoChat.Trabalhando, chat.Estado);
        Assert.DoesNotContain(chat.Itens, i => i is ItemAviso { EhErro: true });
        Assert.Single(_sessoes);
        _sessoes[0].Emitir(new TurnoEncerrado(false, null, null, 1000));
        await EsperarAsync(() => chat.Estado != EstadoChat.Trabalhando);
    });

    [Fact]
    public Task PedidoDeSubagentApareceNaConversaPaiComONome() => Sessao.Rodar(() =>
    {
        var chat = NovaConversa();
        chat.EnviarTexto("revise o cupom");
        Dispatcher.UIThread.RunJobs();
        var sessao = _sessoes[0];
        sessao.Emitir(new FerramentaChamada("toolu_a", "Agent", J("""{"description":"Mapear o cupom","subagent_type":"Explore","prompt":"x"}""")));
        sessao.Emitir(new EventoDeSubagente("toolu_a", new FerramentaChamada("in1", "Bash", J("""{"command":"git log"}"""))));
        sessao.Emitir(new PermissaoPedida("p1", "Bash", J("""{"command":"git log"}"""), null, null, null, "agente-1", "in1"));
        Dispatcher.UIThread.RunJobs();

        var pedido = Assert.Single(chat.Itens.OfType<ItemPermissao>());
        Assert.Equal("Subagent Explore: Mapear o cupom", pedido.Subagente);
        Assert.True(pedido.TemSubagente);
        Assert.Equal(EstadoChat.PrecisaDeVoce, chat.Estado);
        Assert.StartsWith("Esperando você permitir", chat.Subagentes.Single().Atividade);

        var janela = new Window { Width = 1100, Height = 800, Content = new ChatView { DataContext = chat } };
        janela.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(janela.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "Subagent Explore: Mapear o cupom");
        janela.Close();

        pedido.PermitirCommand.Execute(null);
        Assert.Equal(["p1"], sessao.Permitidos);
        Assert.False(pedido.Pendente);

        sessao.Emitir(new PermissaoPedida("p2", "Bash", J("""{"command":"ls"}"""), null, null, null));
        Dispatcher.UIThread.RunJobs();
        Assert.Null(chat.Itens.OfType<ItemPermissao>().Last().Subagente);

        sessao.Emitir(new PermissaoPedida("p3", "Bash", J("""{"command":"pwd"}"""), null, null, null, "agente-1", "desconhecida"));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Subagent Explore: Mapear o cupom", chat.Itens.OfType<ItemPermissao>().Last().Subagente);
    });

    [Fact]
    public void ProtocoloPedeOGanchoDeSubagentELeOCallback()
    {
        var inicio = JsonNode.Parse(Protocolo.Inicializar("i1"))!;
        var gancho = inicio["request"]!["hooks"]!["PreToolUse"]![0]!;
        Assert.Equal("Agent|Task", (string?)gancho["matcher"]);
        Assert.Equal(Protocolo.GanchoDeSubagente, (string?)gancho["hookCallbackIds"]![0]);

        var pedido = Assert.IsType<SubagentePedido>(Assert.Single(Protocolo.Ler("""{"type":"control_request","request_id":"h1","request":{"subtype":"hook_callback","callback_id":"nexora_subagente","input":{"hook_event_name":"PreToolUse","tool_name":"Agent","tool_input":{"description":"x","prompt":"y","model":"opus"},"tool_use_id":"toolu_7"}}}""")));
        Assert.Equal(("h1", "toolu_7"), (pedido.PedidoId, pedido.FerramentaId));
        Assert.Equal("opus", pedido.Entrada.GetProperty("model").GetString());

        Assert.IsType<ControleNaoSuportado>(Assert.Single(Protocolo.Ler("""{"type":"control_request","request_id":"h2","request":{"subtype":"hook_callback","callback_id":"outro","input":{}}}""")));

        var liberado = JsonNode.Parse(Protocolo.LiberarSubagente("h1", new JsonObject { ["model"] = "sonnet" }))!;
        var saida = liberado["response"]!["response"]!["hookSpecificOutput"]!;
        Assert.Equal("allow", (string?)saida["permissionDecision"]);
        Assert.Equal("sonnet", (string?)saida["updatedInput"]!["model"]);
        Assert.Null(JsonNode.Parse(Protocolo.LiberarSubagente("h1", null))!["response"]!["response"]!["hookSpecificOutput"]);
    }

    [Fact]
    public void EntradaDoSubagenteTrocaOuTiraOModelo()
    {
        var entrada = J("""{"prompt":"y","model":"opus"}""");
        Assert.Equal("sonnet", (string?)ChatVM.EntradaDoSubagente(entrada, VagasDeSubagentes.Sonnet)!["model"]);
        Assert.Equal("y", (string?)ChatVM.EntradaDoSubagente(entrada, VagasDeSubagentes.Haiku)!["prompt"]);
        Assert.False(ChatVM.EntradaDoSubagente(entrada, VagasDeSubagentes.Conversa)!.AsObject().ContainsKey("model"));
        Assert.Null(ChatVM.EntradaDoSubagente(entrada, "desconhecido"));
    }

    void ChamarSubagente(SessaoFalsa sessao, string id, string? modelo = null)
    {
        var extra = modelo is null ? "" : $$""","model":"{{modelo}}" """;
        sessao.Emitir(new FerramentaChamada(id, "Agent", J($$"""{"description":"Tarefa {{id}}","subagent_type":"Explore","prompt":"x"{{extra}}}""")));
        sessao.Emitir(new SubagentePedido("h_" + id, id, J($$"""{"description":"Tarefa {{id}}","prompt":"x"{{extra}}}""")));
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task SubagentSaiEmSonnetPorPadraoEOTerceiroEsperaVaga() => Sessao.Rodar(() =>
    {
        App.Config.ModeloDosSubagentes = VagasDeSubagentes.Sonnet;
        App.Config.SubagentesAoMesmoTempo = 2;
        VagasDeSubagentes.Zerar();
        var chat = NovaConversa();
        chat.EnviarTexto("explore tudo");
        Dispatcher.UIThread.RunJobs();
        var sessao = _sessoes[0];

        ChamarSubagente(sessao, "t1", "opus");
        ChamarSubagente(sessao, "t2");
        ChamarSubagente(sessao, "t3");

        Assert.Equal(["h_t1", "h_t2"], sessao.Liberados.Select(l => l.Id));
        Assert.All(sessao.Liberados, l => Assert.Equal("sonnet", (string?)l.Entrada!["model"]));
        var terceiro = chat.Subagentes.Single(a => a.Id == "t3");
        Assert.True(terceiro.NaFila);
        Assert.Equal("na fila", terceiro.EstadoTexto);
        Assert.Equal((2, 1), (VagasDeSubagentes.EmUso, VagasDeSubagentes.Esperando));

        sessao.Emitir(new FerramentaRespondeu("t1", "pronto", false, new ResumoDeSubagente("completed", "a1", "Explore", 3, 1000, 500)));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["h_t1", "h_t2", "h_t3"], sessao.Liberados.Select(l => l.Id));
        Assert.False(terceiro.NaFila);
        Assert.Equal((2, 0), (VagasDeSubagentes.EmUso, VagasDeSubagentes.Esperando));

        chat.Encerrar();
        Assert.Equal(0, VagasDeSubagentes.EmUso);
    });

    [Fact]
    public Task ModoPerguntarEsperaAEscolhaDoModelo() => Sessao.Rodar(() =>
    {
        App.Config.ModeloDosSubagentes = VagasDeSubagentes.Perguntar;
        App.Config.SubagentesAoMesmoTempo = 2;
        VagasDeSubagentes.Zerar();
        var chat = NovaConversa();
        chat.EnviarTexto("explore");
        Dispatcher.UIThread.RunJobs();
        var sessao = _sessoes[0];

        ChamarSubagente(sessao, "t1", "opus");
        var agente = chat.Subagentes.Single();
        Assert.True(agente.EscolhendoModelo);
        Assert.Empty(sessao.Liberados);

        agente.EscolherModeloCommand.Execute("haiku");
        Dispatcher.UIThread.RunJobs();

        var liberado = Assert.Single(sessao.Liberados);
        Assert.Equal("haiku", (string?)liberado.Entrada!["model"]);
        Assert.False(agente.EscolhendoModelo);
        Assert.Equal(1, VagasDeSubagentes.EmUso);

        sessao.Emitir(new SubagentePedido("h_solto", "sem-cartao", J("""{"prompt":"x"}""")));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("h_solto", sessao.Liberados.Last().Id);
        Assert.Equal(1, VagasDeSubagentes.EmUso);
        chat.Encerrar();
    });

    [Fact]
    public void ProtocoloLeOSubagentDoPedido()
    {
        var p = Assert.IsType<PermissaoPedida>(Assert.Single(Protocolo.Ler("""{"type":"control_request","request_id":"r1","request":{"subtype":"can_use_tool","tool_name":"Bash","input":{"command":"ls"},"tool_use_id":"toolu_9","agent_id":"a1b2"}}""")));
        Assert.Equal(("a1b2", "toolu_9"), (p.AgenteId, p.FerramentaId));
        var principal = Assert.IsType<PermissaoPedida>(Assert.Single(Protocolo.Ler("""{"type":"control_request","request_id":"r2","request":{"subtype":"can_use_tool","tool_name":"Bash","input":{"command":"ls"},"tool_use_id":"toolu_8"}}""")));
        Assert.Null(principal.AgenteId);

        var codex = new ProtocoloCodex();
        codex.Ler("""{"method":"thread/started","params":{"thread":{"id":"pai"}}}""");
        var doPai = Assert.IsType<PermissaoPedida>(Assert.IsType<EventoDoCodex>(codex.Ler("""{"id":1,"method":"item/commandExecution/requestApproval","params":{"threadId":"pai","command":"ls"}}""")[1]).Evento);
        Assert.Null(doPai.AgenteId);
        var doFilho = Assert.IsType<PermissaoPedida>(Assert.IsType<EventoDoCodex>(codex.Ler("""{"id":2,"method":"item/commandExecution/requestApproval","params":{"threadId":"filho","command":"ls"}}""")[1]).Evento);
        Assert.Equal("filho", doFilho.AgenteId);
    }
}
