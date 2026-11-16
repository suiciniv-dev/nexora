using System.Text.Json;
using Nexora.Core.Claude;
using Nexora.Core.Historico;
using Nexora.ViewModels;

namespace Nexora.Testes;

static class ModelosDeTeste
{
    public static IReadOnlyList<ModeloDisponivel> Lista { get; } =
    [
        new("default", "Default (recommended)", "Opus 5.5 · Best for everyday, complex tasks", "claude-opus-5-5", ["low", "medium", "high", "xhigh", "max"]),
        new("opus", "Opus 5.5", "For complex work and everyday tasks", "claude-opus-5-5", ["low", "medium", "high", "xhigh", "max"], Rapido: true),
        new("claude-fable-5-1", "Fable 5.1", "For your toughest challenges", "claude-fable-5-1", ["low", "medium", "high", "xhigh", "max"]),
        new("haiku", "Haiku 4.5", "Fastest for quick answers", "claude-haiku-4-5-20251001", []),
    ];

    public static void Usar() => CatalogoModelos.Guardar(Lista);
}

public sealed class AndamentoEModelosTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-padroes-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_pasta, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void LeInicioDoPensamentoDaFerramentaEFimDoBloco()
    {
        Assert.IsType<PensamentoComecou>(Assert.Single(Protocolo.Ler(
            """{"type":"stream_event","event":{"type":"content_block_start","index":0,"content_block":{"type":"thinking","thinking":"","signature":""}},"parent_tool_use_id":null}""")));
        Assert.Equal(new FerramentaComecou("Write"), Assert.Single(Protocolo.Ler(
            """{"type":"stream_event","event":{"type":"content_block_start","index":1,"content_block":{"type":"tool_use","id":"t1","name":"Write","input":{}}},"parent_tool_use_id":null}""")));
        Assert.Equal(new BlocoEncerrado(0), Assert.Single(Protocolo.Ler(
            """{"type":"stream_event","event":{"type":"content_block_stop","index":0},"parent_tool_use_id":null}""")));
        Assert.Empty(Protocolo.Ler(
            """{"type":"stream_event","event":{"type":"content_block_start","index":1,"content_block":{"type":"text","text":""}},"parent_tool_use_id":null}"""));
    }

    [Fact]
    public void LeContagemETextoDoPensamento()
    {
        Assert.Equal(new Pensando(155), Assert.Single(Protocolo.Ler(
            """{"type":"system","subtype":"thinking_tokens","estimated_tokens":155,"estimated_tokens_delta":105,"session_id":"abc"}""")));
        Assert.Equal(new PensamentoParcial("hmm"), Assert.Single(Protocolo.Ler(
            """{"type":"stream_event","event":{"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"hmm"}},"parent_tool_use_id":null}""")));
    }

    [Fact]
    public void ConfiguracoesVaoNaAberturaENaSessaoAberta()
    {
        var args = SessaoClaude.Argumentos(new OpcoesSessao(@"D:\Proj", Configuracoes: """{"fastMode":true,"ultracode":false}"""));
        var i = args.ToList().IndexOf("--settings");
        Assert.True(i >= 0);
        Assert.Equal("""{"fastMode":true,"ultracode":false}""", args[i + 1]);
        Assert.DoesNotContain("--settings", SessaoClaude.Argumentos(new OpcoesSessao(@"D:\Proj")));

        using var doc = JsonDocument.Parse(Protocolo.AplicarConfiguracoes("x", new System.Text.Json.Nodes.JsonObject { ["ultracode"] = true }));
        var pedido = doc.RootElement.GetProperty("request");
        Assert.Equal("apply_flag_settings", pedido.GetProperty("subtype").GetString());
        Assert.True(pedido.GetProperty("settings").GetProperty("ultracode").GetBoolean());
    }

    [Fact]
    public void LeAConversaCompactada() =>
        Assert.Equal(new ConversaCompactada(true, 820_000, 55_000), Assert.Single(Protocolo.Ler(
            """{"type":"system","subtype":"compact_boundary","compact_metadata":{"trigger":"auto","pre_tokens":820000,"post_tokens":55000},"session_id":"abc"}""")));

    [Fact]
    public void PensamentoDeSubagenteFicaDeFora() =>
        Assert.Empty(Protocolo.Ler(
            """{"type":"stream_event","event":{"type":"content_block_start","index":0,"content_block":{"type":"thinking","thinking":""}},"parent_tool_use_id":"toolu_1"}"""));

    [Fact]
    public void InicioTrazOsModelosDoClaude()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("""
            {"commands":[],"remote_control_available":true,"models":[
              {"value":"default","resolvedModel":"claude-opus-5-5","displayName":"Default (recommended)","description":"Opus 5.5 · Best","supportsEffort":true,"supportsFastMode":true,"supportedEffortLevels":["low","high","xhigh"]},
              {"value":"haiku","resolvedModel":"claude-haiku-4-5-20251001","displayName":"Haiku 4.5","description":"Fastest for quick answers"},
              {"value":"","displayName":"sem valor"}
            ]}
            """);
        var modelos = Protocolo.LerInicio(doc.RootElement).Modelos!;

        Assert.Equal(2, modelos.Count);
        Assert.Equal(new[] { "low", "high", "xhigh" }, modelos[0].Esforcos);
        Assert.Equal("claude-opus-5-5", modelos[0].Resolvido);
        Assert.Empty(modelos[1].Esforcos);
        Assert.True(modelos[0].Rapido);
        Assert.False(modelos[1].Rapido);
        Assert.Equal("Haiku 4.5", modelos[1].Nome);
    }

    [Fact]
    public void CatalogoDaNomeAoPadraoEAcheOAliasPeloId()
    {
        ModelosDeTeste.Usar();

        Assert.Equal("Opus 5.5", CatalogoModelos.Nome("opus"));
        Assert.Equal("Opus 5.5", CatalogoModelos.Nome(CatalogoModelos.Padrao));
        Assert.Equal("opus", CatalogoModelos.ValorPara("claude-opus-5-5"));
        Assert.Equal("claude-fable-5-1", CatalogoModelos.ValorPara("claude-fable-5-1"));
        Assert.Equal("claude-opus-4-8", CatalogoModelos.ValorPara("claude-opus-4-8"));
        Assert.Equal("Opus 4.8", CatalogoModelos.Nome("claude-opus-4-8"));
        Assert.Equal("Haiku 4.5", CatalogoModelos.NomeDoId("claude-haiku-4-5-20251001"));
    }

    [Fact]
    public void PadroesSeguemAOrdemDoClaudeEIsoladaIgnoraOUsuario()
    {
        var usuario = Path.Combine(_pasta, "usuario");
        var projeto = Path.Combine(_pasta, "projeto");
        Directory.CreateDirectory(Path.Combine(usuario, ".claude"));
        Directory.CreateDirectory(Path.Combine(projeto, ".claude"));
        File.WriteAllText(Path.Combine(usuario, ".claude", "settings.json"),
            """{"model":"opus","effortLevel":"xhigh","permissions":{"defaultMode":"acceptEdits"}}""");
        File.WriteAllText(Path.Combine(projeto, ".claude", "settings.json"), """{"model":"haiku"}""");
        File.WriteAllText(Path.Combine(projeto, ".claude", "settings.local.json"), """
            {"model":"sonnet", // escolha local
            }
            """);

        Assert.Equal(new PadroesDoClaude("sonnet", "xhigh", "acceptEdits"), PadroesDoClaude.Ler(projeto, isolada: false, usuario));
        Assert.Equal(new PadroesDoClaude("sonnet", null, null), PadroesDoClaude.Ler(projeto, isolada: true, usuario));
        Assert.Equal(new PadroesDoClaude(null, null, null), PadroesDoClaude.Ler(Path.Combine(_pasta, "nada"), isolada: true, usuario));
    }

    [Fact]
    public void PensamentoMostraTokensETempo()
    {
        var item = new ItemPensamento(new DateTime(2026, 11, 5, 10, 0, 0)) { Tokens = 2500 };
        Assert.True(item.Rodando);
        Assert.Equal("Pensando…  ·  2,5 mil tokens", item.Rotulo);

        item.Encerrar(new DateTime(2026, 11, 5, 10, 0, 5));
        Assert.False(item.Rodando);
        Assert.Equal("Pensou por 5 s  ·  2,5 mil tokens", item.Rotulo);

        item.Encerrar(new DateTime(2026, 11, 5, 10, 0, 50));
        Assert.Equal(TimeSpan.FromSeconds(5), item.Duracao);
        Assert.Equal("Pensou por 1 s", new ItemPensamento(DateTime.Now) { Duracao = TimeSpan.Zero }.Rotulo);
        Assert.Equal("155 tokens", ItemPensamento.TextoTokens(155));
    }

    [Fact]
    public void RodapeDoTurnoNaoMostraCusto()
    {
        var rodape = ItemAviso.Rodape(new TurnoEncerrado(false, "ok", 15.87, 12_000));
        Assert.Equal("12 s", rodape.Texto);
        Assert.DoesNotContain("US$", rodape.Texto);
    }

    [Fact]
    public void OriginalDoVsCodeSomeEnquantoACopiaEstaAbertaEParada()
    {
        var copiadas = new HashSet<string> { "a" };
        SessaoAtiva Sessao(string id, bool ocupada = false, bool esperando = false) =>
            new(1, id, @"D:\Proj", null, "vscode", ocupada, null, esperando);

        Assert.True(VisaoGeralVM.EstaCopiadaNoNexora(Sessao("a"), copiadas));
        Assert.False(VisaoGeralVM.EstaCopiadaNoNexora(Sessao("a", ocupada: true), copiadas));
        Assert.False(VisaoGeralVM.EstaCopiadaNoNexora(Sessao("a", esperando: true), copiadas));
        Assert.False(VisaoGeralVM.EstaCopiadaNoNexora(Sessao("b"), copiadas));
    }
}
