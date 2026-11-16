using Nexora.ViewModels;

namespace Nexora.Testes.Tela;

public sealed class NovaConversaTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-nova-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    [Fact]
    public Task NovaConversaComCopiaDoProjetoNaoQuebra() => Sessao.Rodar(() =>
    {
        var anfitriao = new AnfitriaoFalso();
        anfitriao.ListaDeLocais.Add(new Local("Loja", _pasta, ["/outro"]));
        var copia = new Local("Loja", _pasta, ["/outro"]);
        var chat = ChatVM.Nova(copia, anfitriao, _ => { }, _ => { });
        Assert.Same(anfitriao.ListaDeLocais[0], chat.LocalAtual);
        Assert.Same(chat.LocalAtual, chat.LocalEscolhido);
    });
}
