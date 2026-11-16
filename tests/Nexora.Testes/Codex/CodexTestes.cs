using System.Collections.Concurrent;
using System.Text.Json;
using Nexora.Core.Claude;
using Nexora.Core.Codex;

namespace Nexora.Testes.Codex;

public sealed class CodexTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-codex-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    static List<EventoClaude> Eventos(ProtocoloCodex protocolo, params string[] linhas) =>
        [.. linhas.SelectMany(protocolo.Ler).OfType<EventoDoCodex>().Select(e => e.Evento)];

    [Fact]
    public void Pedidos_saem_sem_jsonrpc_e_com_os_campos_do_app_server()
    {
        var turno = ProtocoloCodex.Pedido(7, "turn/start", ProtocoloCodex.Turno("thr", "oi", [@"C:\a.png"], null, "plan", "xhigh"));
        using var doc = JsonDocument.Parse(turno);
        var r = doc.RootElement;

        Assert.False(r.TryGetProperty("jsonrpc", out _));
        Assert.Equal(7, r.GetProperty("id").GetInt64());
        var p = r.GetProperty("params");
        Assert.Equal("thr", p.GetProperty("threadId").GetString());
        Assert.Equal("text", p.GetProperty("input")[0].GetProperty("type").GetString());
        Assert.Equal("localImage", p.GetProperty("input")[1].GetProperty("type").GetString());
        Assert.Equal("xhigh", p.GetProperty("effort").GetString());
        Assert.Equal("on-request", p.GetProperty("approvalPolicy").GetString());
        Assert.Equal("readOnly", p.GetProperty("sandboxPolicy").GetProperty("type").GetString());
        Assert.Equal("detailed", p.GetProperty("summary").GetString());
        Assert.Equal(("never", "danger-full-access"), ProtocoloCodex.Politica("bypassPermissions"));
        Assert.Equal(("untrusted", "workspace-write"), ProtocoloCodex.Politica("manual"));
        Assert.Equal("acceptForSession", ProtocoloCodex.Decisao(true, true)["decision"]!.GetValue<string>());
        Assert.Equal("decline", ProtocoloCodex.Decisao(false, true)["decision"]!.GetValue<string>());
    }

    [Fact]
    public void Turno_com_modelo_leva_o_modo_plano_e_o_direcionamento_vai_para_o_turno_ativo()
    {
        var turno = ProtocoloCodex.Turno("thr", "oi", [], "gpt-5.5", "plan", "max", @"C:\proj");
        Assert.Equal("plan", turno["collaborationMode"]!["mode"]!.GetValue<string>());
        Assert.Equal("xhigh", turno["collaborationMode"]!["settings"]!["reasoning_effort"]!.GetValue<string>());
        Assert.Equal(@"C:\proj", turno["cwd"]!.GetValue<string>());
        Assert.Equal("default", ProtocoloCodex.Turno("thr", "oi", [], "gpt-5.5", "acceptEdits", null)["collaborationMode"]!["mode"]!.GetValue<string>());

        var direcao = ProtocoloCodex.Direcionar("thr", "t9", "e os testes?", []);
        Assert.Equal("t9", direcao["expectedTurnId"]!.GetValue<string>());
        Assert.Equal("e os testes?", direcao["input"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public void Inicio_pede_api_experimental_e_a_thread_leva_mcp_e_instrucoes()
    {
        Assert.True(ProtocoloCodex.Inicializar("1.0")["capabilities"]!["experimentalApi"]!.GetValue<bool>());
        var mcp = """{"mcpServers":{"banco":{"type":"stdio","command":"node","args":["srv.js"],"env":{"A":"1"}},"web":{"type":"http","url":"http://localhost:9/mcp","headers":{"X":"y"}},"ruim":{"type":"sse"}}}""";
        var thread = ProtocoloCodex.NovaThread(@"C:\proj", null, null, mcp, "Responda curto.");
        var config = thread["config"]!.AsObject();

        Assert.True(config["features.concurrent_reasoning_summaries"]!.GetValue<bool>());
        Assert.Equal("node", config["mcp_servers.banco"]!["command"]!.GetValue<string>());
        Assert.Equal("1", config["mcp_servers.banco"]!["env"]!["A"]!.GetValue<string>());
        Assert.Equal("http://localhost:9/mcp", config["mcp_servers.web"]!["url"]!.GetValue<string>());
        Assert.Equal("y", config["mcp_servers.web"]!["http_headers"]!["X"]!.GetValue<string>());
        Assert.False(config.ContainsKey("mcp_servers.ruim"));
        Assert.Equal("Responda curto.", thread["developerInstructions"]!.GetValue<string>());
        Assert.Equal(@"C:\proj", ProtocoloCodex.Retomar("thr", @"C:\proj")["cwd"]!.GetValue<string>());
        Assert.True(ProtocoloCodex.Arquivada("session thr_1 is archived"));
    }

    [Fact]
    public void Pergunta_do_codex_vira_pergunta_e_a_resposta_volta_pelo_id()
    {
        var protocolo = new ProtocoloCodex();
        var mensagens = protocolo.Ler("""{"id":5,"method":"item/tool/requestUserInput","params":{"threadId":"x","turnId":"t","itemId":"i","isBlocking":true,"questions":[{"id":"q1","header":"Banco","question":"Qual banco?","options":[{"label":"SQLite","description":"local"},{"label":"Postgres","description":"servidor"}]}]}}""");

        var permissao = Assert.IsType<PermissaoPedida>(Assert.IsType<EventoDoCodex>(mensagens[1]).Evento);
        Assert.Equal("AskUserQuestion", permissao.Ferramenta);
        var pergunta = permissao.Entrada.GetProperty("questions")[0];
        Assert.Equal("Qual banco?", pergunta.GetProperty("question").GetString());
        Assert.Equal("Postgres", pergunta.GetProperty("options")[1].GetProperty("label").GetString());

        var resposta = protocolo.RespostasDasPerguntas("5", System.Text.Json.Nodes.JsonNode.Parse("""{"Qual banco?":"SQLite"}"""));
        Assert.Equal("""{"answers":{"q1":{"answers":["SQLite"]}}}""", resposta.ToJsonString());
    }

    [Fact]
    public void Pedido_de_acesso_volta_as_permissoes_pedidas_so_quando_permitido()
    {
        var protocolo = new ProtocoloCodex();
        protocolo.Ler("""{"id":6,"method":"item/permissions/requestApproval","params":{"threadId":"x","turnId":"t","itemId":"i","reason":"rede","permissions":{"network":{"enabled":true}}}}""");
        var pedidas = protocolo.PermissoesPedidas("6");

        Assert.Equal("""{"permissions":{"network":{"enabled":true}},"scope":"session"}""", ProtocoloCodex.DecisaoDePermissoes(true, true, pedidas).ToJsonString());
        Assert.Equal("""{"permissions":{},"scope":"turn"}""", ProtocoloCodex.DecisaoDePermissoes(false, false, pedidas).ToJsonString());
        Assert.Equal("approved_for_session", ProtocoloCodex.DecisaoAntiga(true, true)["decision"]!.GetValue<string>());
    }

    [Fact]
    public void Plano_erro_com_nova_tentativa_e_limites_parciais()
    {
        var protocolo = new ProtocoloCodex();
        var eventos = Eventos(protocolo,
            """{"method":"turn/started","params":{"turn":{"id":"t1"}}}""",
            """{"method":"turn/plan/updated","params":{"threadId":"x","turnId":"t1","plan":[{"step":"Ler","status":"completed"},{"step":"Testar","status":"inProgress"}]}}""",
            """{"method":"error","params":{"threadId":"x","turnId":"t1","willRetry":true,"error":{"message":"rede caiu"}}}""",
            """{"method":"error","params":{"threadId":"x","turnId":"t1","willRetry":false,"error":{"message":"limite","additionalDetails":"volte amanhã"}}}""",
            """{"method":"account/rateLimits/updated","params":{"rateLimits":{"primary":{"usedPercent":10}}}}""",
            """{"method":"account/rateLimits/updated","params":{"rateLimits":{"secondary":{"usedPercent":70}}}}""",
            """{"method":"turn/completed","params":{"turn":{"id":"t1","status":"failed"}}}""");

        var plano = Assert.IsType<FerramentaChamada>(eventos[0]);
        Assert.Equal("TodoWrite", plano.Nome);
        Assert.Equal("in_progress", plano.Entrada.GetProperty("todos")[1].GetProperty("status").GetString());
        Assert.Contains("rede caiu", Assert.IsType<AvisoDoAgente>(eventos[2]).Texto);
        var parcial = Assert.IsType<UsoDoCodex>(eventos[4]);
        Assert.Equal(0.1, parcial.Sessao);
        Assert.Equal(0.7, parcial.Semana);
        var fim = Assert.IsType<TurnoEncerrado>(eventos[5]);
        Assert.True(fim.Erro);
        Assert.Equal("limite\nvolte amanhã", fim.Texto);
    }

    [Fact]
    public void Turno_que_ja_terminou_nao_volta_a_ficar_ativo()
    {
        var protocolo = new ProtocoloCodex();
        Eventos(protocolo, """{"method":"turn/completed","params":{"turn":{"id":"t1","status":"completed"}}}""");
        protocolo.TurnoComecou("t1");
        Assert.Null(protocolo.TurnoAtual);
        protocolo.TurnoComecou("t2");
        Assert.Equal("t2", protocolo.TurnoAtual);
        Assert.True(protocolo.Abandonar("caiu")!.Erro);
        Assert.Null(protocolo.TurnoAtual);
    }

    [Fact]
    public void Turno_vira_texto_ferramenta_e_fim()
    {
        var protocolo = new ProtocoloCodex { Modelo = "gpt-falso" };
        var eventos = Eventos(protocolo,
            """{"method":"turn/started","params":{"turn":{"id":"t1"}}}""",
            """{"method":"item/agentMessage/delta","params":{"delta":"Ol","itemId":"m","threadId":"x","turnId":"t1"}}""",
            """{"method":"item/completed","params":{"item":{"type":"agentMessage","id":"m","text":"Olá"},"threadId":"x","turnId":"t1","completedAtMs":1}}""",
            """{"method":"item/started","params":{"item":{"type":"commandExecution","id":"c","command":"ls","cwd":".","status":"inProgress","commandActions":[]},"threadId":"x","turnId":"t1","startedAtMs":1}}""",
            """{"method":"item/completed","params":{"item":{"type":"commandExecution","id":"c","command":"ls","cwd":".","status":"completed","commandActions":[],"aggregatedOutput":"a.txt","exitCode":2},"threadId":"x","turnId":"t1","completedAtMs":1}}""",
            """{"method":"item/started","params":{"item":{"type":"fileChange","id":"f","status":"inProgress","changes":[{"path":"a.cs","kind":{"type":"update","move_path":null},"diff":"@@ -1 +1 @@"}]},"threadId":"x","turnId":"t1","startedAtMs":1}}""",
            """{"method":"item/started","params":{"item":{"type":"mcpToolCall","id":"p","server":"github","tool":"search","status":"inProgress","arguments":{"q":"x"}},"threadId":"x","turnId":"t1","startedAtMs":1}}""",
            """{"method":"thread/tokenUsage/updated","params":{"tokenUsage":{"last":{"inputTokens":900,"totalTokens":1000},"modelContextWindow":272000}}}""",
            """{"method":"turn/completed","params":{"turn":{"id":"t1","status":"completed","items":[]}}}""");

        Assert.Equal("Ol", Assert.IsType<TextoParcial>(eventos[0]).Texto);
        Assert.Equal("Olá", Assert.IsType<TextoCompleto>(eventos[1]).Texto);
        Assert.Equal("Bash", Assert.IsType<FerramentaChamada>(eventos[2]).Nome);
        var saida = Assert.IsType<FerramentaRespondeu>(eventos[3]);
        Assert.True(saida.Erro);
        Assert.Equal("a.txt", saida.Conteudo);
        var edicao = Assert.IsType<FerramentaChamada>(eventos[4]);
        Assert.Equal("Edit", edicao.Nome);
        Assert.Equal("a.cs", edicao.Entrada.GetProperty("file_path").GetString());
        Assert.Equal("mcp__github__search", Assert.IsType<FerramentaChamada>(eventos[5]).Nome);
        Assert.Equal(900, Assert.IsType<ContextoUsado>(eventos[6]).Tokens);
        var fim = Assert.IsType<TurnoEncerrado>(eventos[7]);
        Assert.False(fim.Erro);
        Assert.Equal(272000, fim.JanelaDe("gpt-falso"));
    }

    [Fact]
    public void Turno_interrompido_ou_com_falha_vira_erro()
    {
        var protocolo = new ProtocoloCodex();
        var falhou = Assert.IsType<TurnoEncerrado>(Eventos(protocolo,
            """{"method":"turn/completed","params":{"turn":{"id":"t","status":"failed","error":{"message":"sem créditos"}}}}""").Single());
        Assert.True(falhou.Erro);
        Assert.Equal("sem créditos", falhou.Texto);
        Assert.True(Assert.IsType<TurnoEncerrado>(Eventos(protocolo,
            """{"method":"turn/completed","params":{"turn":{"id":"t","status":"interrupted"}}}""").Single()).Erro);
    }

    [Fact]
    public void Pedido_de_aprovacao_guarda_o_id_e_vira_permissao()
    {
        var protocolo = new ProtocoloCodex();
        var mensagens = protocolo.Ler("""{"id":42,"method":"item/commandExecution/requestApproval","params":{"itemId":"c","threadId":"x","turnId":"t","startedAtMs":1,"command":"rm -rf bin","reason":"limpar"}}""");

        var pedido = Assert.IsType<PedidoDoCodex>(mensagens[0]);
        Assert.Equal("42", pedido.Id.ToJsonString());
        var permissao = Assert.IsType<PermissaoPedida>(Assert.IsType<EventoDoCodex>(mensagens[1]).Evento);
        Assert.Equal("42", permissao.PedidoId);
        Assert.Equal("Bash", permissao.Ferramenta);
        Assert.Equal("rm -rf bin", permissao.Entrada.GetProperty("command").GetString());
        Assert.NotNull(permissao.Sugestoes);
        Assert.Equal("""{"id":42,"result":{"decision":"accept"}}""", ProtocoloCodex.Resposta(pedido.Id, ProtocoloCodex.Decisao(true, false)));
    }

    [Fact]
    public void Limites_viram_uso_do_codex()
    {
        using var doc = JsonDocument.Parse("""{"primary":{"usedPercent":25,"resetsAt":1893456000},"secondary":{"usedPercent":60}}""");
        var uso = ProtocoloCodex.Limites(doc.RootElement)!;

        Assert.Equal(0.25, uso.Sessao);
        Assert.Equal(0.6, uso.Semana);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1893456000), uso.SessaoReinicia);
    }

    [Fact]
    public void Localizador_acha_o_codex_do_npm_e_da_extensao()
    {
        var npm = Path.Combine(_pasta, "npm");
        var vendor = Path.Combine(npm, "node_modules", "@openai", "codex", "vendor", "x86_64-pc-windows-msvc", "codex");
        Directory.CreateDirectory(vendor);
        File.WriteAllText(Path.Combine(npm, "codex.cmd"), "");
        File.WriteAllText(Path.Combine(vendor, "codex.exe"), "");
        Assert.Equal(Path.Combine(vendor, "codex.exe"), LocalizadorCodex.Procurar(_pasta, npm));

        var bin = Path.Combine(_pasta, ".vscode", "extensions", "openai.chatgpt-1.2.3", "bin", "windows-x86_64");
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, "codex.exe"), "");
        Assert.Equal(Path.Combine(bin, "codex.exe"), LocalizadorCodex.DaExtensao(_pasta));
    }

    [Fact]
    public async Task Sessao_conversa_com_um_codex_falso_do_inicio_ao_fim()
    {
        var script = Path.Combine(AppContext.BaseDirectory, "Codex", "codex-falso.ps1");
        var eventos = new BlockingCollection<EventoClaude>();
        await using var sessao = new SessaoCodex(new OpcoesSessao(_pasta), "9.9.9", "powershell.exe",
            ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script]);
        sessao.Evento += eventos.Add;
        sessao.Iniciar();
        sessao.Enviar("rodar testes");

        T Esperar<T>() where T : EventoClaude
        {
            var limite = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < limite)
                if (eventos.TryTake(out var e, TimeSpan.FromSeconds(1)) && e is T certo) return certo;
            throw new TimeoutException(typeof(T).Name);
        }

        var iniciada = Esperar<SessaoIniciada>();
        Assert.Equal("thr_falso", iniciada.SessaoId);
        Assert.Equal("thr_falso", sessao.SessaoId);
        Assert.Equal("Recebi: rodar testes", Esperar<TextoCompleto>().Texto);
        var pedido = Esperar<PermissaoPedida>();
        sessao.Permitir(pedido.PedidoId, null, System.Text.Json.Nodes.JsonNode.Parse(ProtocoloCodex.MarcaDaSessao));
        Assert.Equal("decisao=acceptForSession", Esperar<FerramentaRespondeu>().Conteudo);
        Assert.False(Esperar<TurnoEncerrado>().Erro);
        Assert.Equal("turn_1", sessao.UltimoTurno);
        Assert.Null(await sessao.VoltarAntesDeAsync("turn_1"));
        Assert.Contains("turno desconhecido", await sessao.VoltarAntesDeAsync("turn_9"));
    }

    [Fact]
    public void Conversa_nova_troca_de_agente_so_antes_da_primeira_mensagem()
    {
        var janela = new Nexora.ViewModels.JanelaVM();
        janela.AbrirSessao(new Nexora.ViewModels.Local("Loja", _pasta, []));
        var chat = janela.Abertas[0];
        Assert.True(chat.PodeTrocarAgente);
        chat.TrocarAgenteCommand.Execute(null);
        Assert.True(chat.MenuAgenteAberto);
        Assert.Contains(chat.OpcoesAgente, o => o.Selecionada);

        chat.EscolherAgente("gemini");
        Assert.False(chat.MenuAgenteAberto);
        Assert.Equal("gemini", chat.Agente);
        Assert.False(chat.EhClaude);
        Assert.Contains("Gemini CLI", chat.DicaDoCampo);
        Assert.False(chat.SuportaUltracode);

        chat.EscolherAgente(Agentes.Codex);
        Assert.True(chat.EhCodex);
        Assert.Contains("Codex", chat.DicaDoCampo);

        chat.Itens.Add(new Nexora.ViewModels.ItemUsuario("oi"));
        Assert.False(chat.PodeTrocarAgente);
        chat.EscolherAgente(Agentes.Claude);
        Assert.True(chat.EhCodex);
    }
}
