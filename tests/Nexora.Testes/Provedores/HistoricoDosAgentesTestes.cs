using System.Collections.Concurrent;
using System.Text.Json;
using Nexora.Core.Claude;
using Nexora.Core.Codex;
using Nexora.Core.Historico;

namespace Nexora.Testes.Provedores;

public sealed class HistoricoDosAgentesTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-historico-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    [Fact]
    public void Indice_guarda_atualiza_mantem_a_primeira_mensagem_e_remove_por_pasta()
    {
        var arquivo = Path.Combine(_pasta, "conversas.json");
        var projeto = Path.Combine(_pasta, "loja");
        ConversasDosAgentes.Guardar(new ConversaDeAgente("gemini", "s1", projeto, "Primeira", "main", new DateTime(2026, 11, 1), "oi", "olá"), arquivo);
        ConversasDosAgentes.Guardar(new ConversaDeAgente("codex", "s2", Path.Combine(_pasta, "outro"), "Outra", null, new DateTime(2026, 11, 2)), arquivo);
        ConversasDosAgentes.Guardar(new ConversaDeAgente("gemini", "s1", projeto, "Renomeada", "main", new DateTime(2026, 11, 3), "não é a primeira", "tchau"), arquivo);

        var lista = ConversasDosAgentes.Listar(arquivo);
        Assert.Equal(["s1", "s2"], lista.Select(c => c.SessaoId));
        Assert.Equal("Renomeada", lista[0].Titulo);
        Assert.Equal("oi", lista[0].PrimeiraMensagem);
        var salva = lista[0].ParaSalva();
        Assert.Equal("gemini", salva.Agente);
        Assert.Equal("tchau", salva.UltimaMensagem);

        ConversasDosAgentes.RemoverDaPasta(projeto + Path.DirectorySeparatorChar, arquivo);
        Assert.Equal(["s2"], ConversasDosAgentes.Listar(arquivo).Select(c => c.SessaoId));
        ConversasDosAgentes.Remover("s2", arquivo);
        Assert.Empty(ConversasDosAgentes.Listar(arquivo));
    }

    [Fact]
    public void Thread_do_codex_vira_eventos_do_historico()
    {
        using var doc = JsonDocument.Parse("""
            {"id":"t","turns":[{"id":"x","items":[
              {"type":"userMessage","id":"u","content":[{"type":"text","text":"rode os testes"},{"type":"image","url":"data:"}]},
              {"type":"reasoning","id":"r","summary":[]},
              {"type":"commandExecution","id":"c","command":"dotnet test","cwd":".","status":"completed","commandActions":[],"aggregatedOutput":"ok","exitCode":0},
              {"type":"agentMessage","id":"a","text":"Passou."}
            ]}]}
            """);
        var eventos = ProtocoloCodex.Historico(doc.RootElement);

        var usuario = Assert.IsType<MensagemDoUsuario>(eventos[0]);
        Assert.Equal("rode os testes", usuario.Texto);
        Assert.Equal(1, usuario.Imagens);
        Assert.Equal("Bash", Assert.IsType<FerramentaChamada>(eventos[1]).Nome);
        Assert.Equal("ok", Assert.IsType<FerramentaRespondeu>(eventos[2]).Conteudo);
        Assert.Equal("Passou.", Assert.IsType<TextoCompleto>(eventos[3]).Texto);
    }

    [Fact]
    public async Task Codex_falso_retoma_a_conversa_lista_modelos_e_le_o_historico()
    {
        var script = Path.Combine(AppContext.BaseDirectory, "Codex", "codex-falso.ps1");
        var eventos = new BlockingCollection<EventoClaude>();
        await using var sessao = new SessaoCodex(new OpcoesSessao(_pasta, Retomar: "thr_antiga"), "9.9.9", "powershell.exe",
            ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script]);
        sessao.Evento += eventos.Add;
        sessao.Iniciar();

        var historico = await sessao.LerHistoricoAsync(TimeSpan.FromSeconds(30));
        Assert.Equal("thr_antiga", sessao.SessaoId);
        Assert.Equal("oi de antes", Assert.IsType<MensagemDoUsuario>(historico[0]).Texto);
        Assert.Equal("resposta de antes", Assert.IsType<TextoCompleto>(historico[^1]).Texto);

        var limite = DateTime.UtcNow.AddSeconds(30);
        ComandosDisponiveis? comandos = null;
        while (comandos is null && DateTime.UtcNow < limite)
            if (eventos.TryTake(out var e, TimeSpan.FromSeconds(1)) && e is ComandosDisponiveis c) comandos = c;
        Assert.NotNull(comandos);
        Assert.Equal("gpt-falso", Assert.Single(comandos.Modelos!).Valor);
    }
}
