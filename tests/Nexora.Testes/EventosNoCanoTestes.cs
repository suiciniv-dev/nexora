using System.Text.Json;
using Nexora.Core.Claude;
using Nexora.Core.Codex;
using Nexora.Core.Servidor;

namespace Nexora.Testes;

public sealed class EventosNoCanoTestes
{
    static JsonElement J(string json) => JsonDocument.Parse(json).RootElement.Clone();

    static readonly EventoClaude[] Exemplos =
    [
        new SessaoIniciada("s1", "claude-opus-5-5", @"D:\Projetos\Loja", "acceptEdits"),
        new Pensando(1200),
        new TextoParcial("Vou olhar"),
        new PensamentoComecou(),
        new ConversaCompactada(true, 180_000, 40_000),
        new PensamentoParcial("Preciso ver o teste"),
        new FerramentaComecou("Read"),
        new BlocoEncerrado(2),
        new TextoCompleto("Pronto, **feito**."),
        new MensagemDoUsuario("Ajuste o cupom", 1),
        new FerramentaChamada("t1", "Edit", J("""{"file_path":"a.cs","old_string":"x","new_string":"y"}""")),
        new FerramentaRespondeu("t1", "ok", false, new ResumoDeSubagente("completed", "ag1", "Explore", 3, 9000, 120, "Achei três pontos.")),
        new EventoDeSubagente("t9", new FerramentaChamada("t10", "Grep", J("""{"pattern":"Cupom"}"""))),
        new TarefaAtualizada(FaseTarefa.Terminou, "", "stopped", TarefaId: "a9e2"),
        new PermissaoPedida("p1", "Bash", J("""{"command":"dotnet test"}"""), "Roda os testes", null, J("""[{"type":"addRules"}]"""), "ag1", "t3"),
        new TurnoEncerrado(false, "Feito", 0.12, 45_000, new Dictionary<string, long> { ["5h"] = 1000 }),
        new AvisoDoAgente("Limite perto"),
        new ContextoUsado(84_000, true, "opus", new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero)),
        new UsoDoPlano(0.04, 0.35, new DateTimeOffset(2026, 11, 17, 15, 0, 0, TimeSpan.Zero)),
        new ControleRespondido("c1", true, null, J("""{"modelo":"opus"}""")),
        new ComandosDisponiveis([new ComandoBarra("/compact", "Compacta", null)], true, [new ModeloDisponivel("opus", "Opus 5.5", null, "claude-opus-5-5", ["low", "high"], true)]),
        new ControleNaoSuportado("c2", "set_model"),
        new SubagentePedido("p2", "t4", J("""{"description":"Explorar"}""")),
        new UsoDoCodex(0.2, 0.5, null, null),
    ];

    [Fact]
    public void Todo_tipo_de_evento_tem_exemplo()
    {
        Assert.Equal(JsonDoNucleo.TiposDeEvento.Select(t => t.Name), Exemplos.Select(e => e.GetType().Name).Distinct().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Todo_evento_vai_e_volta_pelo_json_do_nucleo_sem_mudar()
    {
        foreach (var evento in Exemplos)
        {
            var json = JsonSerializer.Serialize<EventoClaude>(evento, JsonDoNucleo.Opcoes);
            var volta = JsonSerializer.Deserialize<EventoClaude>(json, JsonDoNucleo.Opcoes);
            Assert.NotNull(volta);
            Assert.Equal(evento.GetType(), volta.GetType());
            Assert.Equal(json, JsonSerializer.Serialize(volta, JsonDoNucleo.Opcoes));
        }
    }

    [Fact]
    public void Lista_de_eventos_mistos_vai_e_volta()
    {
        var json = JsonSerializer.Serialize<IReadOnlyList<EventoClaude>>(Exemplos, JsonDoNucleo.Opcoes);
        var volta = JsonSerializer.Deserialize<List<EventoClaude>>(json, JsonDoNucleo.Opcoes)!;
        Assert.Equal(Exemplos.Select(e => e.GetType()), volta.Select(e => e.GetType()));
        var subagente = Assert.IsType<EventoDeSubagente>(volta[12]);
        Assert.Equal("Grep", Assert.IsType<FerramentaChamada>(subagente.Evento).Nome);
    }
}
