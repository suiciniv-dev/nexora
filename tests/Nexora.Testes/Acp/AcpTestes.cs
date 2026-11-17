using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Acp;
using Nexora.Core.Claude;

namespace Nexora.Testes.Acp;

public sealed class AcpTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-acp-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    static List<EventoClaude> Eventos(ProtocoloAcp protocolo, params string[] linhas) =>
        [.. linhas.SelectMany(protocolo.Ler).OfType<EventoDoAcp>().Select(e => e.Evento)];

    static string Atualizacao(string update) => """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s","update":""" + update + "}}";

    static JsonElement Json(string texto) => JsonDocument.Parse(texto).RootElement.Clone();

    static CapacidadesAcp Caps(string resposta) => ProtocoloAcp.Capacidades(Json(resposta));

    [Fact]
    public void Pedidos_saem_com_jsonrpc_e_o_initialize_anuncia_o_cliente()
    {
        using var doc = JsonDocument.Parse(ProtocoloAcp.Pedido(3, "initialize", ProtocoloAcp.Inicializar("9.9.9")));
        var r = doc.RootElement;
        Assert.Equal("2.0", r.GetProperty("jsonrpc").GetString());
        Assert.Equal(3, r.GetProperty("id").GetInt64());
        var p = r.GetProperty("params");
        Assert.Equal(1, p.GetProperty("protocolVersion").GetInt32());
        Assert.Equal("nexora", p.GetProperty("clientInfo").GetProperty("name").GetString());
        Assert.Equal("9.9.9", p.GetProperty("clientInfo").GetProperty("version").GetString());
        Assert.True(p.GetProperty("clientCapabilities").GetProperty("fs").GetProperty("readTextFile").GetBoolean());
        Assert.False(p.GetProperty("clientCapabilities").GetProperty("fs").GetProperty("writeTextFile").GetBoolean());
        Assert.False(p.GetProperty("clientCapabilities").GetProperty("terminal").GetBoolean());
        Assert.Equal("""{"jsonrpc":"2.0","method":"session/cancel","params":{"sessionId":"s"}}""", ProtocoloAcp.Aviso("session/cancel", new JsonObject { ["sessionId"] = "s" }));
        Assert.Equal("""{"jsonrpc":"2.0","id":"x","error":{"code":-32601,"message":"nao"}}""", ProtocoloAcp.Recusa(JsonValue.Create("x")!, "nao"));
    }

    [Fact]
    public void Initialize_v1_e_v2_sao_reconhecidos_pelo_formato()
    {
        var v1 = Caps("""{"protocolVersion":2,"agentInfo":{"name":"antigravity"},"agentCapabilities":{"loadSession":true,"promptCapabilities":{"image":true},"mcpCapabilities":{"http":true,"sse":false},"sessionCapabilities":{"resume":{}}},"authMethods":[{"id":"oauth-personal","name":"Google"},{"id":"chave","name":"Chave","type":"env_var"}]}""");
        Assert.Equal(1, v1.Geracao);
        Assert.Equal("antigravity", v1.Agente);
        Assert.True(v1.CarregarSessao);
        Assert.True(v1.RetomarSessao);
        Assert.True(v1.Imagem);
        Assert.True(v1.McpHttp);
        Assert.False(v1.McpSse);
        Assert.False(v1.Diretorios);
        Assert.Equal(["oauth-personal", "chave"], v1.Logins.Select(l => l.Id));
        Assert.Equal("oauth-personal", ProtocoloAcp.LoginDoAgente(v1)!.Id);
        Assert.Equal("authenticate", ProtocoloAcp.Metodo("authenticate", 1));
        Assert.Equal("session/load", ProtocoloAcp.Metodo("session/load", 1));

        var v2 = Caps("""{"protocolVersion":2,"info":{"name":"novo","title":"Novo"},"capabilities":{"session":{"prompt":{"image":{}},"mcp":{"stdio":{},"http":{}},"additionalDirectories":{}}},"authMethods":[{"methodId":"web","name":"Web","type":"agent"}]}""");
        Assert.Equal(2, v2.Geracao);
        Assert.Equal("Novo", v2.Agente);
        Assert.True(v2.CarregarSessao);
        Assert.True(v2.Imagem);
        Assert.True(v2.McpHttp);
        Assert.True(v2.Diretorios);
        Assert.Equal("web", ProtocoloAcp.LoginDoAgente(v2)!.Id);
        Assert.Equal("auth/login", ProtocoloAcp.Metodo("authenticate", 2));
        Assert.Equal("session/resume", ProtocoloAcp.Metodo("session/load", 2));
        var carregar = ProtocoloAcp.CarregarSessao("s1", @"C:\p", [], [@"C:\outra"], v2);
        Assert.Equal("start", carregar["replayFrom"]!["type"]!.GetValue<string>());
        Assert.Equal(@"C:\outra", carregar["additionalDirectories"]![0]!.GetValue<string>());
        Assert.Null(ProtocoloAcp.CarregarSessao("s1", @"C:\p", [], [@"C:\outra"], v1)["additionalDirectories"]);
        Assert.Null(ProtocoloAcp.CarregarSessao("s1", @"C:\p", [], null, v1)["replayFrom"]);
    }

    [Fact]
    public void Mcp_do_claude_vira_lista_do_acp()
    {
        const string mcp = """{"mcpServers":{"git":{"command":"node","args":["git.js","--x"],"env":{"TOKEN":"abc"}},"web":{"type":"http","url":"https://x/mcp","headers":{"Authorization":"Bearer y"}},"velho":{"type":"sse","url":"https://x/sse"}}}""";
        var v1 = Caps("""{"agentCapabilities":{"mcpCapabilities":{"http":true}}}""");
        var lista = ProtocoloAcp.ServidoresMcp(mcp, v1);
        Assert.Equal(2, lista.Count);
        var git = lista[0]!.AsObject();
        Assert.Null(git["type"]);
        Assert.Equal("git", git["name"]!.GetValue<string>());
        Assert.Equal("node", git["command"]!.GetValue<string>());
        Assert.Equal("--x", git["args"]![1]!.GetValue<string>());
        Assert.Equal("TOKEN", git["env"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("abc", git["env"]![0]!["value"]!.GetValue<string>());
        var web = lista[1]!.AsObject();
        Assert.Equal("http", web["type"]!.GetValue<string>());
        Assert.Equal("https://x/mcp", web["url"]!.GetValue<string>());
        Assert.Equal("Bearer y", web["headers"]![0]!["value"]!.GetValue<string>());

        var semHttp = ProtocoloAcp.ServidoresMcp(mcp, Caps("""{"info":{"name":"n"},"capabilities":{"session":{}}}"""));
        Assert.Equal("stdio", Assert.Single(semHttp)!["type"]!.GetValue<string>());
        Assert.Empty(ProtocoloAcp.ServidoresMcp("isto nao e json", v1));
        Assert.Empty(ProtocoloAcp.ServidoresMcp(null, v1));
    }

    [Fact]
    public void Tipos_de_ferramenta_viram_as_ferramentas_do_claude()
    {
        (string, JsonObject) F(string json) => ProtocoloAcp.Ferramenta(Json(json));

        var (bash, comando) = F("""{"kind":"execute","title":"Rodar testes","rawInput":{"command":["dotnet","test"],"timeout":5}}""");
        Assert.Equal("Bash", bash);
        Assert.Equal("dotnet test", comando["command"]!.GetValue<string>());
        Assert.Equal("Rodar testes", comando["description"]!.GetValue<string>());
        Assert.Equal(5, comando["timeout"]!.GetValue<int>());

        var (edit, edicao) = F("""{"kind":"edit","title":"Editar","content":[{"type":"diff","path":"C:\\a.cs","oldText":"x","newText":"y"}]}""");
        Assert.Equal("Edit", edit);
        Assert.Equal(@"C:\a.cs", edicao["file_path"]!.GetValue<string>());
        Assert.Equal("x", edicao["old_string"]!.GetValue<string>());
        Assert.Equal("y", edicao["new_string"]!.GetValue<string>());

        var (write, novo) = F("""{"kind":"edit","title":"Criar","content":[{"type":"diff","path":"C:\\b.cs","oldText":null,"newText":"tudo"}]}""");
        Assert.Equal("Write", write);
        Assert.Equal("tudo", novo["content"]!.GetValue<string>());

        var (semDiff, local) = F("""{"kind":"edit","title":"Editar","locations":[{"path":"C:\\c.cs","line":3}]}""");
        Assert.Equal("Edit", semDiff);
        Assert.Equal(@"C:\c.cs", local["file_path"]!.GetValue<string>());

        Assert.Equal(("Read", @"C:\d.cs"), Par(F("""{"kind":"read","title":"Ler","locations":[{"path":"C:\\d.cs"}]}"""), "file_path"));
        Assert.Equal(("Grep", "TODO"), Par(F("""{"kind":"search","title":"TODO"}"""), "pattern"));
        Assert.Equal(("WebFetch", "https://x.dev"), Par(F("""{"kind":"fetch","title":"Buscar","rawInput":{"url":"https://x.dev"}}"""), "url"));
        Assert.Equal(("Mover a.cs", "Mover a.cs"), Par(F("""{"kind":"move","title":"Mover a.cs"}"""), "description"));
    }

    static (string, string?) Par((string Nome, JsonObject Entrada) f, string campo) => (f.Nome, f.Entrada[campo]?.GetValue<string>());

    [Fact]
    public void Turno_vira_texto_pensamento_ferramenta_e_fim()
    {
        var protocolo = new ProtocoloAcp();
        protocolo.LerSessao(Json("""{"sessionId":"s","models":{"currentModelId":"m1","availableModels":[{"modelId":"m1","name":"M1"}]}}"""));
        var eventos = Eventos(protocolo,
            "lixo que nao e json",
            Atualizacao("""{"sessionUpdate":"agent_thought_chunk","content":{"type":"text","text":"hmm"}}"""),
            Atualizacao("""{"sessionUpdate":"agent_thought_chunk","content":{"type":"text","text":" sim"}}"""),
            Atualizacao("""{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"Ol"}}"""),
            Atualizacao("""{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"á"}}"""),
            Atualizacao("""{"sessionUpdate":"tool_call","toolCallId":"t","title":"ls","kind":"execute","status":"pending","rawInput":{"command":"ls"}}"""),
            Atualizacao("""{"sessionUpdate":"tool_call_update","toolCallId":"t","status":"in_progress"}"""),
            Atualizacao("""{"sessionUpdate":"tool_call_update","toolCallId":"t","status":"failed","rawOutput":{"output":"sem acesso"}}"""),
            Atualizacao("""{"sessionUpdate":"tool_call","toolCallId":"e","title":"Editar","kind":"edit","status":"pending","content":[{"type":"diff","path":"a.cs","oldText":"x","newText":"y"}]}"""),
            Atualizacao("""{"sessionUpdate":"tool_call_update","toolCallId":"e","status":"completed"}"""),
            Atualizacao("""{"sessionUpdate":"usage_update","used":900,"size":200000}"""),
            Atualizacao("""{"sessionUpdate":"session_info_update","title":"x"}"""),
            Atualizacao("""{"sessionUpdate":"desconhecido"}"""),
            Atualizacao("""{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"Fim"}}"""));
        eventos.AddRange(protocolo.Fim("end_turn", null, 10));

        Assert.IsType<PensamentoComecou>(eventos[0]);
        Assert.Equal("hmm", Assert.IsType<PensamentoParcial>(eventos[1]).Texto);
        Assert.Equal(" sim", Assert.IsType<PensamentoParcial>(eventos[2]).Texto);
        Assert.Equal("Ol", Assert.IsType<TextoParcial>(eventos[3]).Texto);
        Assert.Equal("á", Assert.IsType<TextoParcial>(eventos[4]).Texto);
        Assert.Equal("Olá", Assert.IsType<TextoCompleto>(eventos[5]).Texto);
        Assert.Equal("Bash", Assert.IsType<FerramentaChamada>(eventos[6]).Nome);
        var falhou = Assert.IsType<FerramentaRespondeu>(eventos[7]);
        Assert.True(falhou.Erro);
        Assert.Equal("sem acesso", falhou.Conteudo);
        Assert.Equal("Edit", Assert.IsType<FerramentaChamada>(eventos[8]).Nome);
        var diff = Assert.IsType<FerramentaRespondeu>(eventos[9]);
        Assert.False(diff.Erro);
        Assert.Equal("a.cs\n-x\n+y", diff.Conteudo.Replace("\r\n", "\n"));
        var uso = Assert.IsType<ContextoUsado>(eventos[10]);
        Assert.Equal(900, uso.Tokens);
        Assert.Equal("m1", uso.Modelo);
        Assert.Equal("Fim", Assert.IsType<TextoParcial>(eventos[11]).Texto);
        Assert.Equal("Fim", Assert.IsType<TextoCompleto>(eventos[12]).Texto);
        var fim = Assert.IsType<TurnoEncerrado>(eventos[13]);
        Assert.False(fim.Erro);
        Assert.Equal(200000, fim.JanelaDe("m1"));
        Assert.Equal(14, eventos.Count);
    }

    [Fact]
    public void Motivos_de_parada_viram_erro_quando_o_turno_nao_terminou_bem()
    {
        var protocolo = new ProtocoloAcp();
        Assert.False(Assert.IsType<TurnoEncerrado>(protocolo.Fim("end_turn", null, 1).Single()).Erro);
        Assert.False(Assert.IsType<TurnoEncerrado>(protocolo.Fim(null, null, 1).Single()).Erro);
        var cancelado = Assert.IsType<TurnoEncerrado>(protocolo.Fim("cancelled", null, 1).Single());
        Assert.True(cancelado.Erro);
        Assert.Equal("O turno foi interrompido.", cancelado.Texto);
        Assert.True(Assert.IsType<TurnoEncerrado>(protocolo.Fim("refusal", null, 1).Single()).Erro);
        Assert.Equal(TextosAcp.LimiteDeTokens, Assert.IsType<TurnoEncerrado>(protocolo.Fim("max_tokens", null, 1).Single()).Texto);
        Assert.Equal(TextosAcp.LimiteDePassos, Assert.IsType<TurnoEncerrado>(protocolo.Fim("max_turn_requests", null, 1).Single()).Texto);
        Assert.Equal("caiu", Assert.IsType<TurnoEncerrado>(protocolo.Fim(null, "caiu", 1).Single()).Texto);
        var parou = Assert.IsType<AgenteParou>(protocolo.Ler(Atualizacao("""{"sessionUpdate":"state_update","state":"idle","stopReason":"end_turn"}""")).Single());
        Assert.Equal("end_turn", parou.Motivo);
        Assert.Empty(protocolo.Ler(Atualizacao("""{"sessionUpdate":"state_update","state":"running"}""")));
    }

    [Fact]
    public void Plano_vira_todo_write()
    {
        var eventos = Eventos(new ProtocoloAcp(),
            Atualizacao("""{"sessionUpdate":"plan","entries":[{"content":"Ler","priority":"high","status":"completed"},{"content":"Mudar","priority":"low","status":"in_progress"}]}"""));
        var chamada = Assert.IsType<FerramentaChamada>(eventos[0]);
        Assert.Equal("TodoWrite", chamada.Nome);
        var todos = chamada.Entrada.GetProperty("todos");
        Assert.Equal(2, todos.GetArrayLength());
        Assert.Equal("Mudar", todos[1].GetProperty("activeForm").GetString());
        Assert.Equal("in_progress", todos[1].GetProperty("status").GetString());
        Assert.Equal(chamada.Id, Assert.IsType<FerramentaRespondeu>(eventos[1]).Id);
    }

    [Fact]
    public void Pedido_de_permissao_vira_permissao_e_escolhe_a_opcao_certa()
    {
        var protocolo = new ProtocoloAcp();
        var mensagens = protocolo.Ler("""{"jsonrpc":"2.0","id":42,"method":"session/request_permission","params":{"sessionId":"s","toolCall":{"toolCallId":"t","title":"rm -rf bin","kind":"execute","rawInput":{"command":"rm -rf bin"}},"options":[{"optionId":"sim","name":"Sim","kind":"allow_once"},{"optionId":"sempre","name":"Sempre","kind":"allow_always"},{"optionId":"nao","name":"Não","kind":"reject_once"}]}}""");

        var pedido = Assert.IsType<PermissaoDoAgente>(mensagens[0]);
        Assert.Equal("42", pedido.PedidoId);
        var permissao = Assert.IsType<PermissaoPedida>(Assert.IsType<EventoDoAcp>(mensagens[1]).Evento);
        Assert.Equal("42", permissao.PedidoId);
        Assert.Equal("Bash", permissao.Ferramenta);
        Assert.Equal("rm -rf bin", permissao.Entrada.GetProperty("command").GetString());
        Assert.NotNull(permissao.Sugestoes);

        string Escolhida(bool permitir, bool sempre) => ProtocoloAcp.Escolha(pedido.Opcoes, permitir, sempre)["outcome"]!["optionId"]!.GetValue<string>();
        Assert.Equal("sempre", Escolhida(true, true));
        Assert.Equal("sim", Escolhida(true, false));
        Assert.Equal("nao", Escolhida(false, false));
        Assert.Equal("""{"jsonrpc":"2.0","id":42,"result":{"outcome":{"outcome":"cancelled"}}}""", ProtocoloAcp.Resposta(pedido.Id, ProtocoloAcp.Cancelado()));

        var semSempre = protocolo.Ler("""{"jsonrpc":"2.0","id":"p2","method":"session/request_permission","params":{"sessionId":"s","toolCall":{"toolCallId":"t2","title":"Ler","kind":"read","locations":[{"path":"a.txt"}]},"options":[{"optionId":"ok","name":"Ok","kind":"allow_once"}]}}""");
        var segundo = Assert.IsType<PermissaoDoAgente>(semSempre[0]);
        Assert.Equal("\"p2\"", segundo.PedidoId);
        var leitura = Assert.IsType<PermissaoPedida>(Assert.IsType<EventoDoAcp>(semSempre[1]).Evento);
        Assert.Equal("Read", leitura.Ferramenta);
        Assert.Null(leitura.Sugestoes);
        Assert.Equal("ok", ProtocoloAcp.Escolha(segundo.Opcoes, true, true)["outcome"]!["optionId"]!.GetValue<string>());
        Assert.Equal("cancelled", ProtocoloAcp.Escolha(segundo.Opcoes, false, false)["outcome"]!["outcome"]!.GetValue<string>());
    }

    [Fact]
    public void Comandos_modelos_e_modos_do_agente()
    {
        var protocolo = new ProtocoloAcp();
        var inicio = protocolo.LerSessao(Json("""{"sessionId":"s","models":{"currentModelId":"a","availableModels":[{"modelId":"a","name":"A"},{"modelId":"b","name":"B","description":"rápido"}]},"modes":{"currentModeId":"code","availableModes":[{"id":"code","name":"Code"},{"id":"architect","name":"Architect"},{"id":"yolo","name":"YOLO"}]}}"""));
        Assert.Equal("s", protocolo.SessaoId);
        Assert.Equal("a", protocolo.Modelo);
        var modelos = Assert.IsType<ComandosDisponiveis>(Assert.Single(inicio)).Modelos!;
        Assert.Equal(new ModeloDisponivel("b", "B", "rápido", null, []).Valor, modelos[1].Valor);
        Assert.Equal("rápido", modelos[1].Descricao);

        var comandos = Assert.IsType<ComandosDisponiveis>(Eventos(protocolo,
            Atualizacao("""{"sessionUpdate":"available_commands_update","availableCommands":[{"name":"web","description":"Busca","input":{"hint":"termo"}},{"name":"/init","description":""}]}""")).Single());
        Assert.Equal(new ComandoBarra("web", "Busca", "termo"), comandos.Comandos[0]);
        Assert.Equal(new ComandoBarra("init", null, null), comandos.Comandos[1]);
        Assert.Equal(2, comandos.Modelos!.Count);

        Assert.Equal("architect", ProtocoloAcp.MelhorModo("plan", protocolo.Modos));
        Assert.Equal("yolo", ProtocoloAcp.MelhorModo("bypassPermissions", protocolo.Modos));
        Assert.Equal("code", ProtocoloAcp.MelhorModo(null, protocolo.Modos));
        Assert.Equal("code", ProtocoloAcp.MelhorModo("manual", protocolo.Modos));
        Assert.Null(ProtocoloAcp.MelhorModo("acceptEdits", protocolo.Modos));
        Assert.Null(protocolo.PedidoDeModo("default"));
        var plano = protocolo.PedidoDeModo("plan")!.Value;
        Assert.Equal("session/set_mode", plano.Metodo);
        Assert.Equal("architect", plano.Parametros["modeId"]!.GetValue<string>());
        Assert.Equal("session/set_model", protocolo.PedidoDeModelo("b")!.Value.Metodo);
        Eventos(protocolo, Atualizacao("""{"sessionUpdate":"current_mode_update","currentModeId":"architect"}"""));
        Assert.Null(protocolo.PedidoDeModo("plan"));

        var v2 = new ProtocoloAcp { Geracao = 2, SessaoId = "s2" };
        v2.LerSessao(Json("""{"configOptions":[{"configId":"modo","category":"mode","type":"select","name":"Modo","currentValue":"agent","options":[{"value":"agent","name":"Agent"},{"value":"auto-edit","name":"Auto edit"}]},{"configId":"model","type":"select","name":"Modelo","currentValue":"g1","options":[{"groupId":"x","name":"X","options":[{"value":"g1","name":"G1"},{"value":"g2","name":"G2"}]}]}]}"""));
        Assert.Equal("g1", v2.Modelo);
        Assert.Equal(["g1", "g2"], v2.Modelos.Select(m => m.Valor));
        var edicao = v2.PedidoDeModo("acceptEdits")!.Value;
        Assert.Equal("session/set_config_option", edicao.Metodo);
        Assert.Equal("modo", edicao.Parametros["configId"]!.GetValue<string>());
        Assert.Equal("auto-edit", edicao.Parametros["value"]!.GetValue<string>());
        Assert.Equal("id", edicao.Parametros["type"]!.GetValue<string>());
        var modelo = v2.PedidoDeModelo("g2")!.Value;
        Assert.Equal("session/set_config_option", modelo.Metodo);
        Assert.Equal("model", modelo.Parametros["configId"]!.GetValue<string>());
    }

    [Fact]
    public void Prompt_leva_extra_e_imagens_so_quando_o_agente_aceita()
    {
        var imagem = new Anexo("a.png", "image/png", [1, 2, 3]);
        var com = ProtocoloAcp.Prompt("s", "contexto", "oi", [imagem], true)["prompt"]!.AsArray();
        Assert.Equal(3, com.Count);
        Assert.Equal("contexto", com[0]!["text"]!.GetValue<string>());
        Assert.Equal("oi", com[1]!["text"]!.GetValue<string>());
        Assert.Equal("image", com[2]!["type"]!.GetValue<string>());
        Assert.Equal("AQID", com[2]!["data"]!.GetValue<string>());
        var sem = ProtocoloAcp.Prompt("s", null, "oi", [imagem], false)["prompt"]!.AsArray();
        Assert.Equal("text", Assert.Single(sem)!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Replay_vira_mensagens_do_usuario_e_do_agente()
    {
        var protocolo = new ProtocoloAcp { Repetindo = true };
        var eventos = Eventos(protocolo,
            Atualizacao("""{"sessionUpdate":"user_message_chunk","content":{"type":"text","text":"oi "}}"""),
            Atualizacao("""{"sessionUpdate":"user_message_chunk","content":{"type":"text","text":"lá"}}"""),
            Atualizacao("""{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"olá"}}"""),
            Atualizacao("""{"sessionUpdate":"user_message_chunk","content":{"type":"text","text":"de novo"}}"""));
        eventos.AddRange(protocolo.Descarregar());

        Assert.Equal("oi lá", Assert.IsType<MensagemDoUsuario>(eventos[0]).Texto);
        Assert.IsType<TextoParcial>(eventos[1]);
        Assert.Equal("olá", Assert.IsType<TextoCompleto>(eventos[2]).Texto);
        Assert.Equal("de novo", Assert.IsType<MensagemDoUsuario>(eventos[3]).Texto);
        Assert.Empty(Eventos(new ProtocoloAcp(), Atualizacao("""{"sessionUpdate":"user_message_chunk","content":{"type":"text","text":"eco"}}""")));
    }

    [Fact]
    public void Link_de_login_do_antigravity_sai_do_stdout()
    {
        var link = Assert.IsType<LinkDeLogin>(new ProtocoloAcp().Ler(ProtocoloAcp.PrefixoDoLink + "https://accounts.google.com/o?x=1").Single());
        Assert.Equal("https://accounts.google.com/o?x=1", link.Url);
    }

    [Fact]
    public void Leitura_de_arquivo_fica_dentro_das_pastas_da_sessao()
    {
        var arquivo = Path.Combine(_pasta, "a.txt");
        File.WriteAllText(arquivo, "um\ndois\ntres\nquatro");
        var (resultado, erro) = ProtocoloAcp.LerArquivo(Json(JsonSerializer.Serialize(new { path = arquivo, line = 2, limit = 2 })), [_pasta]);
        Assert.Null(erro);
        Assert.Equal("dois\ntres", resultado!["content"]!.GetValue<string>());
        var (fora, motivo) = ProtocoloAcp.LerArquivo(Json(JsonSerializer.Serialize(new { path = Path.Combine(Path.GetTempPath(), "outro.txt") })), [_pasta]);
        Assert.Null(fora);
        Assert.NotNull(motivo);
        var (vizinho, _) = ProtocoloAcp.LerArquivo(Json(JsonSerializer.Serialize(new { path = _pasta + "x\\a.txt" })), [_pasta]);
        Assert.Null(vizinho);
    }

    [Fact]
    public void Cmd_e_bat_rodam_pelo_cmd_no_windows()
    {
        var (arquivo, argumentos) = SessaoAcp.Comando(@"C:\npm\gemini.cmd", ["--acp"]);
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("cmd.exe", arquivo);
            Assert.Equal(["/c", @"C:\npm\gemini.cmd", "--acp"], argumentos);
        }
        Assert.Equal(("agente.exe", 1), (SessaoAcp.Comando("agente.exe", ["x"]).Arquivo, SessaoAcp.Comando("agente.exe", ["x"]).Argumentos.Count));
    }

    [Fact]
    public void Textos_novos_tem_traducao()
    {
        Assert.All(TextosAcp.Pares, p => Assert.False(string.IsNullOrWhiteSpace(p.En)));
        var constantes = typeof(TextosAcp).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToList();
        Assert.Equal(constantes.Order(), TextosAcp.Pares.Select(p => p.Pt).Order());
    }

    static string Script => Path.Combine(AppContext.BaseDirectory, "Acp", "agente-acp-falso.ps1");

    static T Esperar<T>(BlockingCollection<EventoClaude> eventos, Func<T, bool>? filtro = null) where T : EventoClaude
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < limite)
            if (eventos.TryTake(out var e, TimeSpan.FromSeconds(1)) && e is T certo && (filtro?.Invoke(certo) ?? true)) return certo;
        throw new TimeoutException(typeof(T).Name);
    }

    SessaoAcp Sessao(OpcoesSessao opcoes, BlockingCollection<EventoClaude> eventos, IReadOnlyDictionary<string, string?>? ambiente = null)
    {
        var sessao = new SessaoAcp(opcoes, "falso", "Agente Falso", "powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Script], ambiente, "9.9.9");
        sessao.Evento += eventos.Add;
        return sessao;
    }

    [Fact]
    public async Task Sessao_conversa_com_um_agente_falso_do_inicio_ao_fim()
    {
        File.WriteAllText(Path.Combine(_pasta, "leia.txt"), "conteudo");
        var eventos = new BlockingCollection<EventoClaude>();
        await using var sessao = Sessao(new OpcoesSessao(_pasta, Modo: "plan", PromptExtra: "extra"), eventos, new Dictionary<string, string?> { ["FALSO_LOGIN"] = "1" });
        sessao.Iniciar();
        sessao.Enviar("rodar testes");
        Assert.Equal("falso", sessao.Agente);
        Assert.False(sessao.TemRemoto);
        Assert.False((await sessao.PedirAsync(new System.Text.Json.Nodes.JsonObject())).Sucesso);
        Assert.False((await sessao.LigarRemotoAsync(true)).Sucesso);

        var iniciada = Esperar<SessaoIniciada>(eventos);
        Assert.Equal("ses_falso", iniciada.SessaoId);
        Assert.Equal("modelo-a", iniciada.Modelo);
        Assert.Equal("plan", sessao.ModoDoAgente);
        Assert.Equal(2, Esperar<ComandosDisponiveis>(eventos).Modelos!.Count);
        Assert.Equal("Recebi: rodar testes (modo=plan, blocos=2)", Esperar<TextoCompleto>(eventos).Texto);
        var chamada = Esperar<FerramentaChamada>(eventos);
        Assert.Equal("Bash", chamada.Nome);
        Assert.Equal("dotnet test", chamada.Entrada.GetProperty("command").GetString());
        var pedido = Esperar<PermissaoPedida>(eventos);
        Assert.NotNull(pedido.Sugestoes);
        sessao.Permitir(pedido.PedidoId, null, JsonNode.Parse(ProtocoloAcp.MarcaDaSessao));
        Assert.Equal("escolha=a1", Esperar<FerramentaRespondeu>(eventos).Conteudo);
        Assert.Equal(1234, Esperar<ContextoUsado>(eventos).Tokens);
        Assert.Equal("lido=conteudo terminal=-32601 fora=True", Esperar<TextoCompleto>(eventos).Texto);
        var fim = Esperar<TurnoEncerrado>(eventos);
        Assert.False(fim.Erro);
        Assert.Equal(200000, fim.JanelaDe("modelo-a"));

        sessao.Enviar("demorar");
        sessao.Enviar("depois");
        Assert.Equal("Vou demorar", Esperar<TextoCompleto>(eventos).Texto);
        Esperar<PermissaoPedida>(eventos);
        sessao.Interromper();
        Assert.Equal(" permissao=cancelled", Esperar<TextoCompleto>(eventos).Texto);
        var interrompido = Esperar<TurnoEncerrado>(eventos);
        Assert.True(interrompido.Erro);
        Assert.Equal("O turno foi interrompido.", interrompido.Texto);
        Assert.Equal("Recebi depois", Esperar<TextoCompleto>(eventos).Texto);
        Assert.False(Esperar<TurnoEncerrado>(eventos).Erro);
    }

    [Fact]
    public async Task Retomar_carrega_a_conversa_e_repete_o_historico()
    {
        var eventos = new BlockingCollection<EventoClaude>();
        await using var sessao = Sessao(new OpcoesSessao(_pasta, Retomar: "ses_antiga"), eventos);
        sessao.Iniciar();

        var historico = await sessao.LerHistoricoAsync(TimeSpan.FromSeconds(30));
        Assert.Equal("pergunta antiga", historico.OfType<MensagemDoUsuario>().First().Texto);
        Assert.Equal("resposta antiga", historico.OfType<TextoCompleto>().Last().Texto);
        Assert.Equal("ses_antiga", Esperar<SessaoIniciada>(eventos).SessaoId);
        Assert.Equal("ses_antiga", sessao.SessaoId);
        Assert.DoesNotContain(eventos, e => e is MensagemDoUsuario);
    }

    [Fact]
    public async Task Sem_login_o_erro_diz_para_entrar_no_agente()
    {
        var eventos = new BlockingCollection<EventoClaude>();
        await using var sessao = Sessao(new OpcoesSessao(_pasta), eventos, new Dictionary<string, string?> { ["FALSO_LOGIN"] = "2" });
        sessao.Iniciar();

        var falha = Esperar<TurnoEncerrado>(eventos);
        Assert.True(falha.Erro);
        Assert.Contains("Agente Falso", falha.Texto);
        Assert.Null(sessao.SessaoId);
        Assert.False(sessao.Ativa);
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (sessao.Ativa && DateTime.UtcNow < limite) await Task.Delay(50);
        Assert.False(sessao.Ativa);
    }

    [Fact]
    public async Task Agente_antigo_que_sai_na_abertura_explica_que_precisa_atualizar()
    {
        var eventos = new BlockingCollection<EventoClaude>();
        var encerrou = new TaskCompletionSource<(int Codigo, string Erros)>();
        await using var sessao = Sessao(new OpcoesSessao(_pasta), eventos, new Dictionary<string, string?> { ["FALSO_ANTIGO"] = "1" });
        sessao.Encerrou += (codigo, erros) => encerrou.TrySetResult((codigo, erros));
        sessao.Iniciar();

        var (codigo, erros) = await encerrou.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.NotEqual(0, codigo);
        Assert.Contains("antigo", erros);
        Assert.Contains("unexpected argument", erros);
        Assert.False(sessao.Ativa);
    }

    [Fact]
    public void Erro_de_conta_mostra_o_que_o_agente_disse_e_erro_de_versao_pede_atualizar()
    {
        Assert.Contains("entre no Gemini", ProtocoloAcp.ErroDeLoginComDetalhe("Gemini", "Authentication required"));
        Assert.Contains("entre no Gemini", ProtocoloAcp.ErroDeLoginComDetalhe("Gemini", null));
        var conta = ProtocoloAcp.ErroDeLoginComDetalhe("Gemini", "This client is no longer supported for Gemini Code Assist for individuals");
        Assert.Contains("no longer supported", conta);
        Assert.DoesNotContain("entre no", conta);

        var velho = ProtocoloAcp.ExplicarErros("error: unexpected argument \"acp\" found", "Antigravity");
        Assert.StartsWith("O Antigravity instalado é antigo", velho);
        Assert.Contains("unexpected argument", velho);
        Assert.Equal("outra coisa", ProtocoloAcp.ExplicarErros("outra coisa", "Antigravity"));
    }
}
