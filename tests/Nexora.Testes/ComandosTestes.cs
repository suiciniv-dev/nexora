using System.Text.Json;
using Nexora.Core.Claude;

namespace Nexora.Testes;

public class ComandosTestes
{
    [Fact]
    public void RespostaDoInitializeViraListaDeComandos()
    {
        var linha = """{"type":"control_response","response":{"subtype":"success","request_id":"nexora-1","response":{"commands":[{"name":"compact","description":"Free up context","argumentHint":"<instruções>"},{"name":"senna-handoff","description":"Grava no Senna"},{"name":""}],"remote_control_available":false}}}""";
        var resposta = Assert.IsType<ControleRespondido>(Assert.Single(Protocolo.Ler(linha)));
        var inicio = Protocolo.LerInicio(resposta.Dados!.Value);

        Assert.Equal([new ComandoBarra("compact", "Free up context", "<instruções>"), new ComandoBarra("senna-handoff", "Grava no Senna", null)], inicio.Comandos);
        Assert.False(inicio.RemotoDisponivel);
        Assert.True(Protocolo.LerInicio(JsonDocument.Parse("{}").RootElement).RemotoDisponivel);
    }

    [Fact]
    public void FiltroPoeQuemComecaComOTermoNaFrente()
    {
        ComandoBarra[] todos =
        [
            new("context", "Show current context usage", null),
            new("compact", "Free up context by summarizing", null),
            new("senna-handoff", "Grava a passagem desta sessão", null),
            new("model", "Set the AI model", null),
        ];
        Assert.Equal(["compact", "context"], CatalogoComandos.Filtrar(todos, "/co").Select(c => c.Nome));
        Assert.Equal(["senna-handoff"], CatalogoComandos.Filtrar(todos, "/hand").Select(c => c.Nome));
        Assert.Equal(["senna-handoff"], CatalogoComandos.Filtrar(todos, "/passagem").Select(c => c.Nome));
        Assert.Equal(4, CatalogoComandos.Filtrar(todos, "/").Count);
    }

    [Fact]
    public void SoABarraMostraTudoNaOrdemDoClaude()
    {
        ComandoBarra[] todos =
        [
            new("senna-handoff", null, null),
            new("ui-ux-pro-max", null, null),
            .. Enumerable.Range(0, 40).Select(i => new ComandoBarra($"c{i}", null, null)),
        ];
        var lista = CatalogoComandos.Filtrar(todos, "/");
        Assert.Equal(42, lista.Count);
        Assert.Equal(["senna-handoff", "ui-ux-pro-max"], lista.Take(2).Select(c => c.Nome));
    }

    [Fact]
    public void PedidoDeRemoteControl()
    {
        var pedido = JsonDocument.Parse(Protocolo.ControleRemoto("nexora-7", ligado: true)).RootElement;
        Assert.Equal("control_request", pedido.GetProperty("type").GetString());
        Assert.Equal("nexora-7", pedido.GetProperty("request_id").GetString());
        Assert.Equal("remote_control", pedido.GetProperty("request").GetProperty("subtype").GetString());
        Assert.True(pedido.GetProperty("request").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task ClaudeDeVerdadeListaOsComandos()
    {
        if (Environment.GetEnvironmentVariable("NEXORA_INTEGRACAO") != "1") return;

        var pasta = Directory.CreateTempSubdirectory("nexora-comandos-").FullName;
        var comandos = await CatalogoComandos.ObterAsync(pasta, isolada: true);
        Directory.Delete(pasta, recursive: true);

        Assert.Contains(comandos.Comandos, c => c.Nome == "compact");
        Assert.Contains(comandos.Comandos, c => c.Nome == "context");
        Assert.Empty(SessaoClaude.PidsDoNexora);
    }
}
