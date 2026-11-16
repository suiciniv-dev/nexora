using Nexora.ViewModels;

namespace Nexora.Testes.Tela;

public sealed class ReinicioTestes
{
    [Fact]
    public Task TrocarParaSolidoPedeReinicioEVoltarAoVidroDesiste() => Sessao.Rodar(() =>
    {
        var modoAntes = App.Config.ModoFundo;
        try
        {
            var janela = new JanelaVM();

            janela.MudarModoFundo("solido");
            Assert.True(janela.PedindoReinicio);
            Assert.Contains("processador", janela.TextoReinicio);

            janela.ReiniciarDepoisCommand.Execute(null);
            Assert.False(janela.PedindoReinicio);

            janela.MudarModoFundo("solido");
            janela.MudarModoFundo("vidro");
            Assert.False(janela.PedindoReinicio);

            janela.MudarModoFundo("animado");
            Assert.False(janela.PedindoReinicio);
        }
        finally
        {
            App.Config.ModoFundo = modoAntes;
            Temas.Catalogo.Modo = modoAntes == "parado" ? "vidro" : modoAntes;
            Temas.Catalogo.Aplicar(Temas.Catalogo.Atual);
        }
    });
}
