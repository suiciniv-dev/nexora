using Nexora.ViewModels;

namespace Nexora.Testes.Tela;

public sealed class ReinicioTestes
{
    [Fact]
    public Task TrocarOModoDoFundoNaoPedeReinicioPorqueODesenhoEPelaPlacaDeVideo() => Sessao.Rodar(() =>
    {
        var modoAntes = App.Config.ModoFundo;
        try
        {
            var janela = new JanelaVM();
            foreach (var modo in new[] { "solido", "vidro", "animado", "solido" })
            {
                janela.MudarModoFundo(modo);
                Assert.False(janela.PedindoReinicio);
            }
        }
        finally
        {
            App.Config.ModoFundo = modoAntes;
            Temas.Catalogo.Modo = modoAntes == "parado" ? "vidro" : modoAntes;
            Temas.Catalogo.Aplicar(Temas.Catalogo.Atual);
        }
    });
}
