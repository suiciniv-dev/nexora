using Nexora.Core.Claude;
using Nexora.Core.Git;
using Nexora.ViewModels;

namespace Nexora.Testes.Tela;

public sealed class VigiaNaJanelaTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-vigia-").FullName;
    readonly (string, string, string, Dictionary<string, DateTime>, List<string>) _antes =
        (App.Config.VigiaChecks, App.Config.VigiaConflitos, App.Config.VigiaRevisoes, App.Config.VigiaVistos, App.Config.VigiasPausadas);

    public void Dispose()
    {
        (App.Config.VigiaChecks, App.Config.VigiaConflitos, App.Config.VigiaRevisoes, App.Config.VigiaVistos, App.Config.VigiasPausadas) = _antes;
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    ChatVM Conversa()
    {
        var chat = ChatVM.Nova(new Local("Loja", _pasta, []), new AnfitriaoFalso(), _ => { }, _ => { });
        chat.Estado = EstadoChat.Trabalhando;
        return chat;
    }

    static RetratoPr Retrato(string sha, params string[] falhos) => new(52, sha, true, falhos, false,
        [new ComentarioPr("x", "revisora", "Renomeie", "a.cs", 3, null, null)]);

    [Fact]
    public Task SoAvisarNaoMandaNadaAoAgente() => Sessao.Rodar(() =>
    {
        App.Config.VigiaVistos = [];
        App.Config.VigiaChecks = VigiaDePr.Avisar;
        App.Config.VigiaRevisoes = VigiaDePr.Avisar;
        var janela = new JanelaVM();
        var chat = Conversa();

        janela.Avaliar(chat, Retrato("a", "build"), "eu");

        Assert.Empty(chat.Fila);
        Assert.DoesNotContain(chat.Itens, i => i is ItemUsuario);
    });

    [Fact]
    public Task AgirMandaOPedidoUmaVezEParaDepoisDeTresPorHora() => Sessao.Rodar(() =>
    {
        App.Config.VigiaVistos = [];
        App.Config.VigiaChecks = VigiaDePr.Agir;
        App.Config.VigiaRevisoes = VigiaDePr.Desligado;
        var janela = new JanelaVM();
        var chat = Conversa();
        janela.Avaliar(chat, Retrato("a", "build"), "eu");
        janela.Avaliar(chat, Retrato("a", "build"), "eu");
        var pedido = Assert.Single(chat.Fila);
        Assert.Contains("build", pedido.Texto);
        Assert.DoesNotContain(chat.Itens, i => i is ItemUsuario);

        foreach (var sha in new[] { "b", "c", "d" }) janela.Avaliar(chat, Retrato(sha, "build"), "eu");
        Assert.Equal(3, chat.Fila.Count);
    });

    [Fact]
    public Task SessaoPausadaFicaForaDaVigia() => Sessao.Rodar(async () =>
    {
        App.Config.VigiaVistos = [];
        App.Config.VigiasPausadas = [];
        App.Config.VigiaChecks = VigiaDePr.Agir;
        var buscar = JanelaVM.BuscarRetrato;
        var buscados = new List<string>();
        JanelaVM.BuscarRetrato = (pasta, branch) =>
        {
            buscados.Add(branch);
            return Task.FromResult<RetratoPr?>(null);
        };
        try
        {
            var janela = new JanelaVM();
            var chat = Conversa();
            chat.Branch = "cupom-checkout";
            chat.SessaoId = "sessao-1";
            janela.Abertas.Add(chat);

            await janela.VigiarAsync();
            Assert.Equal(["cupom-checkout"], buscados);

            chat.AlternarVigiaCommand.Execute(null);
            Assert.True(chat.VigiaPausada);
            await janela.VigiarAsync();
            Assert.Single(buscados);
        }
        finally
        {
            JanelaVM.BuscarRetrato = buscar;
        }
    });
}
