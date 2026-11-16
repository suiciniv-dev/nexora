using System.Text.Json;
using Nexora.Core.Claude;

namespace Nexora.Testes;

public class ProtocoloTestes
{
    [Fact]
    public void LeInicioDaSessao()
    {
        var e = Assert.Single(Protocolo.Ler("""{"type":"system","subtype":"init","cwd":"D:\\Proj","session_id":"abc","model":"claude-haiku-4-5","permissionMode":"default","tools":["Bash"]}"""));
        Assert.Equal(new SessaoIniciada("abc", "claude-haiku-4-5", @"D:\Proj", "default"), e);
    }

    [Fact]
    public void IgnoraHooksEStatus()
    {
        Assert.Empty(Protocolo.Ler("""{"type":"system","subtype":"hook_started","hook_id":"x","session_id":"abc"}"""));
        Assert.Empty(Protocolo.Ler("""{"type":"system","subtype":"status","status":"requesting","session_id":"abc"}"""));
        Assert.Empty(Protocolo.Ler("não é json"));
        Assert.Empty(Protocolo.Ler(""));
    }

    [Fact]
    public void LeTextoParcial()
    {
        var e = Assert.Single(Protocolo.Ler("""{"type":"stream_event","event":{"type":"content_block_delta","index":1,"delta":{"type":"text_delta","text":"Olá"}},"session_id":"abc","parent_tool_use_id":null}"""));
        Assert.Equal(new TextoParcial("Olá"), e);
    }

    [Fact]
    public void IgnoraPensamentoParcialEMensagensDeSubagente()
    {
        Assert.Empty(Protocolo.Ler("""{"type":"stream_event","event":{"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":""}},"parent_tool_use_id":null}"""));
        Assert.Empty(Protocolo.Ler("""{"type":"stream_event","event":{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"x"}},"parent_tool_use_id":"toolu_1"}"""));
    }

    [Fact]
    public void LeTextoEFerramentaDoAssistente()
    {
        var texto = Assert.Single(Protocolo.Ler("""{"type":"assistant","message":{"id":"msg_1","role":"assistant","content":[{"type":"text","text":"nexora"}]},"parent_tool_use_id":null,"session_id":"abc"}"""));
        Assert.Equal(new TextoCompleto("nexora"), texto);

        var uso = Assert.IsType<FerramentaChamada>(Assert.Single(Protocolo.Ler("""{"type":"assistant","message":{"content":[{"type":"tool_use","id":"toolu_01","name":"Bash","input":{"command":"echo nexora","description":"Echo"}}]},"parent_tool_use_id":null}""")));
        Assert.Equal("toolu_01", uso.Id);
        Assert.Equal("Bash", uso.Nome);
        Assert.Equal("echo nexora", uso.Entrada.GetProperty("command").GetString());
    }

    [Fact]
    public void LeResultadoDeFerramentaEmTextoOuEmBlocos()
    {
        var simples = Assert.IsType<FerramentaRespondeu>(Assert.Single(Protocolo.Ler("""{"type":"user","message":{"role":"user","content":[{"tool_use_id":"toolu_01","type":"tool_result","content":"nexora","is_error":false}]},"parent_tool_use_id":null}""")));
        Assert.Equal(new FerramentaRespondeu("toolu_01", "nexora", false), simples);

        var blocos = Assert.IsType<FerramentaRespondeu>(Assert.Single(Protocolo.Ler("""{"type":"user","message":{"content":[{"tool_use_id":"t2","type":"tool_result","content":[{"type":"text","text":"a"},{"type":"text","text":"b"}],"is_error":true}]}}""")));
        Assert.Equal("a\nb", blocos.Conteudo);
        Assert.True(blocos.Erro);
    }

    [Fact]
    public void LePedidoDePermissao()
    {
        var p = Assert.IsType<PermissaoPedida>(Assert.Single(Protocolo.Ler("""{"type":"control_request","request_id":"req-1","request":{"subtype":"can_use_tool","tool_name":"Write","display_name":"Write","input":{"file_path":"C:\\x\\nota.txt","content":"nexora"},"description":"nota.txt","permission_suggestions":[{"type":"setMode","mode":"acceptEdits","destination":"session"}],"decision_reason":"Claude requested permissions to write"}}""")));
        Assert.Equal("req-1", p.PedidoId);
        Assert.Equal("Write", p.Ferramenta);
        Assert.Equal("nota.txt", p.Descricao);
        Assert.Equal("Claude requested permissions to write", p.Motivo);
        Assert.NotNull(p.Sugestoes);
        Assert.Equal("nexora", p.Entrada.GetProperty("content").GetString());
    }

    [Fact]
    public void PedidoDeControleDesconhecidoViraNaoSuportado()
    {
        var e = Assert.Single(Protocolo.Ler("""{"type":"control_request","request_id":"r9","request":{"subtype":"hook_callback","callback_id":"x"}}"""));
        Assert.Equal(new ControleNaoSuportado("r9", "hook_callback"), e);
    }

    [Fact]
    public void LeFimDoTurno()
    {
        var ok = Assert.IsType<TurnoEncerrado>(Assert.Single(Protocolo.Ler("""{"type":"result","subtype":"success","is_error":false,"duration_ms":13400,"result":"nexora","total_cost_usd":0.08,"session_id":"abc"}""")));
        Assert.False(ok.Erro);
        Assert.Equal(13400, ok.DuracaoMs);
        Assert.Equal(0.08, ok.CustoUsd);

        var falhou = Assert.IsType<TurnoEncerrado>(Assert.Single(Protocolo.Ler("""{"type":"result","subtype":"error_during_execution","is_error":false,"duration_ms":10}""")));
        Assert.True(falhou.Erro);
    }

    [Fact]
    public void LeContextoUsadoECacheDeUmaHora()
    {
        var eventos = Protocolo.Ler("""{"type":"assistant","message":{"model":"claude-haiku-4-5-20251001","content":[{"type":"text","text":"ok"}],"usage":{"input_tokens":10,"cache_creation_input_tokens":34620,"cache_read_input_tokens":1000,"cache_creation":{"ephemeral_5m_input_tokens":0,"ephemeral_1h_input_tokens":34620},"output_tokens":4}},"parent_tool_use_id":null}""");
        Assert.Equal(2, eventos.Length);
        Assert.Equal(new ContextoUsado(35634, true, "claude-haiku-4-5-20251001"), eventos[1]);

        var cinco = Assert.IsType<ContextoUsado>(Protocolo.LerUso(System.Text.Json.JsonDocument.Parse("""{"usage":{"input_tokens":5,"cache_read_input_tokens":10,"cache_creation":{"ephemeral_5m_input_tokens":3}}}""").RootElement, null)[0]);
        Assert.False(cinco.CacheLonga);
    }

    [Fact]
    public void FimDoTurnoTrazJanelaDeContextoEModelo()
    {
        var fim = Assert.IsType<TurnoEncerrado>(Assert.Single(Protocolo.Ler("""{"type":"result","subtype":"success","is_error":false,"duration_ms":10,"total_cost_usd":0.07,"modelUsage":{"claude-haiku-4-5-20251001":{"contextWindow":200000,"canonicalModel":"claude-haiku-4-5"},"claude-opus-5-5":{"contextWindow":1000000,"canonicalModel":"claude-opus-5-5"}}}""")));
        Assert.Equal(1_000_000, fim.JanelaDe("claude-opus-5-5"));
        Assert.Equal(200_000, fim.JanelaDe("claude-haiku-4-5-20251001"));
        Assert.Null(fim.JanelaDe("claude-sonnet-5-5"));
    }

    [Fact]
    public void LeUsoDoPlano()
    {
        var u = Assert.IsType<UsoDoPlano>(Assert.Single(Protocolo.Ler("""{"type":"rate_limit_event","rate_limit_info":{"status":"allowed","unifiedWindows":{"five_hour":{"utilization":0.04,"resetsAt":1793320800},"seven_day":{"utilization":0.35,"resetsAt":1793743200}}}}""")));
        Assert.Equal(0.04, u.Sessao);
        Assert.Equal(0.35, u.Semana);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1793320800), u.SessaoReinicia);
    }

    [Fact]
    public void LeRespostaDeControle()
    {
        var ok = Assert.IsType<ControleRespondido>(Assert.Single(Protocolo.Ler("""{"type":"control_response","response":{"subtype":"success","request_id":"nexora-2","response":{"mode":"acceptEdits"}}}""")));
        Assert.True(ok.Sucesso);
        Assert.Equal("acceptEdits", ok.Dados!.Value.GetProperty("mode").GetString());

        var erro = Assert.IsType<ControleRespondido>(Assert.Single(Protocolo.Ler("""{"type":"control_response","response":{"subtype":"error","request_id":"nexora-3","error":"modo inválido"}}""")));
        Assert.False(erro.Sucesso);
        Assert.Equal("modo inválido", erro.Erro);
    }

    [Fact]
    public void MontaMensagemDoUsuario()
    {
        using var doc = JsonDocument.Parse(Protocolo.Mensagem("Olá, \"Claude\"\nlinha 2"));
        var raiz = doc.RootElement;
        Assert.Equal("user", raiz.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, raiz.GetProperty("parent_tool_use_id").ValueKind);
        var bloco = raiz.GetProperty("message").GetProperty("content")[0];
        Assert.Equal("Olá, \"Claude\"\nlinha 2", bloco.GetProperty("text").GetString());
    }

    [Fact]
    public void MontaPermissaoComEntradaERegras()
    {
        var pedido = Assert.IsType<PermissaoPedida>(Protocolo.Ler("""{"type":"control_request","request_id":"req-1","request":{"subtype":"can_use_tool","tool_name":"Bash","input":{"command":"ls"},"permission_suggestions":[{"type":"addRules","rules":[{"toolName":"Bash"}]}]}}""")[0]);
        using var doc = JsonDocument.Parse(Protocolo.Permitir(pedido.PedidoId, Protocolo.ParaNo(pedido.Entrada), Protocolo.ParaNo(pedido.Sugestoes)));
        var r = doc.RootElement.GetProperty("response");
        Assert.Equal("success", r.GetProperty("subtype").GetString());
        Assert.Equal("req-1", r.GetProperty("request_id").GetString());
        Assert.Equal("allow", r.GetProperty("response").GetProperty("behavior").GetString());
        Assert.Equal("ls", r.GetProperty("response").GetProperty("updatedInput").GetProperty("command").GetString());
        Assert.Equal("addRules", r.GetProperty("response").GetProperty("updatedPermissions")[0].GetProperty("type").GetString());
    }

    [Fact]
    public void MontaNegacaoEControles()
    {
        using var negar = JsonDocument.Parse(Protocolo.Negar("req-2", "não"));
        Assert.Equal("deny", negar.RootElement.GetProperty("response").GetProperty("response").GetProperty("behavior").GetString());

        using var modo = JsonDocument.Parse(Protocolo.MudarModo("c1", "plan"));
        Assert.Equal("control_request", modo.RootElement.GetProperty("type").GetString());
        Assert.Equal("set_permission_mode", modo.RootElement.GetProperty("request").GetProperty("subtype").GetString());
        Assert.Equal("plan", modo.RootElement.GetProperty("request").GetProperty("mode").GetString());

        using var parar = JsonDocument.Parse(Protocolo.Interromper("c2"));
        Assert.Equal("interrupt", parar.RootElement.GetProperty("request").GetProperty("subtype").GetString());
    }

    [Fact]
    public void ArgumentosRetomamEPassamModoEModelo()
    {
        var args = SessaoClaude.Argumentos(new OpcoesSessao(@"D:\x", Retomar: "abc", Modelo: "opus", Modo: "plan"));
        Assert.Contains("--permission-prompt-tool", args);
        Assert.Equal("abc", args[args.ToList().IndexOf("--resume") + 1]);
        Assert.Equal("opus", args[args.ToList().IndexOf("--model") + 1]);
        Assert.Equal("plan", args[args.ToList().IndexOf("--permission-mode") + 1]);
        Assert.DoesNotContain("--allow-dangerously-skip-permissions", args);
        Assert.Contains("--allow-dangerously-skip-permissions", SessaoClaude.Argumentos(new OpcoesSessao(@"D:\x", Modo: "bypassPermissions")));
        Assert.DoesNotContain("--setting-sources", args);
        Assert.DoesNotContain("--fork-session", args);
        Assert.Contains("--fork-session", SessaoClaude.Argumentos(new OpcoesSessao(@"D:\x", Retomar: "abc", Bifurcar: true)));
        Assert.DoesNotContain("--fork-session", SessaoClaude.Argumentos(new OpcoesSessao(@"D:\x", Bifurcar: true)));
    }

    [Fact]
    public void ArgumentosLevamPastasVinculadasEModoIsolado()
    {
        var args = SessaoClaude.Argumentos(new OpcoesSessao(@"D:\Cloud", Diretorios: [@"D:\Calc", @"D:\App"], Isolada: true)).ToList();
        Assert.Equal(2, args.Count(a => a == "--add-dir"));
        Assert.Equal(@"D:\Calc", args[args.IndexOf("--add-dir") + 1]);
        Assert.Equal("project,local", args[args.IndexOf("--setting-sources") + 1]);
    }
}
