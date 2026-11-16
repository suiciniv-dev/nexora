using Nexora.Core.Claude;
using Nexora.Core.Historico;

namespace Nexora.Testes;

public sealed class SubagentesProtocoloTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-subagentes-").FullName;

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    [Fact]
    public void MensagensDeDentroDoSubagentVoltamComOPai()
    {
        var chamada = Assert.IsType<EventoDeSubagente>(Assert.Single(Protocolo.Ler(
            """{"type":"assistant","message":{"model":"claude-haiku-4-5","content":[{"type":"tool_use","id":"toolu_in1","name":"Read","input":{"file_path":"D:\\Proj\\a.cs"}}],"usage":{"input_tokens":900,"output_tokens":40}},"parent_tool_use_id":"toolu_pai","agent_id":"a1","session_id":"s"}""")));
        Assert.Equal("toolu_pai", chamada.PaiId);
        var read = Assert.IsType<FerramentaChamada>(chamada.Evento);
        Assert.Equal(("toolu_in1", "Read"), (read.Id, read.Nome));

        var resposta = Assert.IsType<EventoDeSubagente>(Assert.Single(Protocolo.Ler(
            """{"type":"user","message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"toolu_in1","content":"linha 1"}]},"parent_tool_use_id":"toolu_pai","session_id":"s"}""")));
        Assert.Equal(new FerramentaRespondeu("toolu_in1", "linha 1", false), resposta.Evento);

        Assert.Empty(Protocolo.Ler("""{"type":"user","message":{"role":"user","content":[{"type":"text","text":"Ache os pontos do cupom"}]},"parent_tool_use_id":"toolu_pai"}"""));
        Assert.Empty(Protocolo.Ler("""{"type":"stream_event","event":{"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"x","name":"Grep","input":{}}},"parent_tool_use_id":"toolu_pai"}"""));
    }

    [Fact]
    public void ResultadoDoAgentTrazOResumoDoSubagent()
    {
        var e = Assert.IsType<FerramentaRespondeu>(Assert.Single(Protocolo.Ler(
            """{"type":"user","message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"toolu_pai","content":[{"type":"text","text":"Achei três pontos.\nagentId: a1 (para continuar)\n<usage>total_tokens: 59001\ntool_uses: 6\nduration_ms: 49559</usage>"}]}]},"parent_tool_use_id":null,"tool_use_result":{"status":"completed","agentId":"a1","agentType":"Explore","totalDurationMs":49559,"totalTokens":59001,"totalToolUseCount":6},"session_id":"s"}""")));

        Assert.Equal("toolu_pai", e.Id);
        Assert.Equal(new ResumoDeSubagente("completed", "a1", "Explore", 6, 49559, 59001), e.Subagente);
        Assert.False(e.Subagente!.EmSegundoPlano);
        Assert.Equal("Achei três pontos.", Resumos.ResultadoDeSubagente(e.Conteudo));
        Assert.Equal((6, 49559L), Resumos.UsoDoSubagente(e.Conteudo));
    }

    [Fact]
    public void AgentEmSegundoPlanoEAvisosDeTarefa()
    {
        var lancado = Assert.IsType<FerramentaRespondeu>(Assert.Single(Protocolo.Ler(
            """{"type":"user","message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"toolu_bg","content":"Async agent launched successfully."}]},"parent_tool_use_id":null,"tool_use_result":{"isAsync":true,"status":"async_launched","agentId":"a2","description":"Testes"}}""")));
        Assert.True(lancado.Subagente!.EmSegundoPlano);

        var andou = Assert.IsType<TarefaAtualizada>(Assert.Single(Protocolo.Ler(
            """{"type":"system","subtype":"task_progress","task_id":"t2","tool_use_id":"toolu_bg","description":"Testes","usage":{"total_tokens":1200,"tool_uses":3,"duration_ms":15000},"last_tool_name":"Bash","summary":"Rodando os testes"}""")));
        Assert.Equal(new TarefaAtualizada(FaseTarefa.Andou, "toolu_bg", Ferramentas: 3, DuracaoMs: 15000, UltimaFerramenta: "Bash", Resumo: "Rodando os testes"), andou);

        var terminou = Assert.IsType<TarefaAtualizada>(Assert.Single(Protocolo.Ler(
            """{"type":"system","subtype":"task_notification","task_id":"t2","tool_use_id":"toolu_bg","status":"failed","output_file":"","summary":"Agent \"Testes\" failed","usage":{"total_tokens":1500,"tool_uses":4,"duration_ms":20000}}""")));
        Assert.Equal(new TarefaAtualizada(FaseTarefa.Terminou, "toolu_bg", "failed", 4, 20000), terminou);

        Assert.Empty(Protocolo.Ler("""{"type":"system","subtype":"task_started","task_id":"t2","tool_use_id":"toolu_bg","description":"Testes"}"""));
    }

    [Fact]
    public void NotificacaoDeTarefaNoTextoDoUsuarioFechaOSubagent()
    {
        var eventos = Transcricao.LerLinha(
            """{"type":"user","message":{"role":"user","content":"<task-notification>\n<task-id>a2</task-id>\n<tool-use-id>toolu_bg</tool-use-id>\n<output-file>x</output-file>\n<status>completed</status>\n<summary>Agent \"Testes\" completed</summary>\n<result>Teste novo passou.</result>\n<usage><subagent_tokens>900</subagent_tokens><tool_uses>5</tool_uses><duration_ms>61000</duration_ms></usage>\n</task-notification>"},"timestamp":"2026-11-06T10:00:00Z"}""");

        Assert.Equal(new TarefaAtualizada(FaseTarefa.Terminou, "toolu_bg", "completed", 5, 61000, Resultado: "Teste novo passou."), Assert.Single(eventos));
    }

    [Fact]
    public void HistoricoLeAsFerramentasDoArquivoDoSubagent()
    {
        var principal = Path.Combine(_pasta, "s1.jsonl");
        File.WriteAllText(principal, "");
        var subagentes = Directory.CreateDirectory(Path.Combine(_pasta, "s1", "subagents")).FullName;
        File.WriteAllLines(Path.Combine(subagentes, "agent-a1.jsonl"),
        [
            """{"type":"user","isSidechain":true,"agentId":"a1","message":{"role":"user","content":"Ache os pontos do cupom"}}""",
            """{"type":"assistant","isSidechain":true,"agentId":"a1","message":{"content":[{"type":"text","text":"Vou procurar"},{"type":"tool_use","id":"g1","name":"Grep","input":{"pattern":"Cupom"}}]}}""",
            """{"type":"user","isSidechain":true,"agentId":"a1","message":{"content":[{"type":"tool_result","tool_use_id":"g1","content":"a.cs"}]}}""",
        ]);

        var eventos = Transcricao.LerSubagente(principal, "a1");

        Assert.Equal(3, eventos.Count);
        Assert.Equal(new TextoCompleto("Vou procurar"), eventos[0]);
        Assert.Equal("Grep", Assert.IsType<FerramentaChamada>(eventos[1]).Nome);
        Assert.Equal(new FerramentaRespondeu("g1", "a.cs", false), eventos[2]);
        Assert.Empty(Transcricao.LerSubagente(principal, "../fora"));
        Assert.Empty(Transcricao.LerSubagente(principal, "nao-existe"));
    }

    [Fact]
    public void PreviaDoResultadoSaiSemMarcasDeMarkdown()
    {
        Assert.Equal("O cupom passa por três pontos:", Resumos.SemMarcas("O cupom passa por **três pontos**:"));
        Assert.Equal("Resumo do CupomService", Resumos.SemMarcas("## Resumo do `CupomService`"));
        Assert.Equal("Primeiro item", Resumos.SemMarcas("1. Primeiro item"));
    }
}
