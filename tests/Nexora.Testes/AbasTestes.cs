using Nexora.Views;

namespace Nexora.Testes;

public sealed class AbasTestes
{
    static double[] Minimos(int n, int selecionada = -1) =>
        [.. Enumerable.Range(0, n).Select(i => i == selecionada ? PainelAbas.MinimoSelecionada : PainelAbas.Minimo)];

    [Fact]
    public void ComEspacoSobrandoCadaAbaFicaComALarguraNatural()
    {
        Assert.Equal(new double[] { 120, 200, 90 }, PainelAbas.Distribuir([120, 200, 90], Minimos(3), 600));
    }

    [Fact]
    public void SemEspacoAsMaioresEncolhemPrimeiroEAsPequenasFicamIguais()
    {
        var larguras = PainelAbas.Distribuir([80, 240, 240], Minimos(3), 400);
        Assert.Equal(80, larguras[0]);
        Assert.Equal(160, larguras[1]);
        Assert.Equal(160, larguras[2]);
    }

    [Fact]
    public void MuitasAbasCabemNaFaixaSemPassarDoLimite()
    {
        var larguras = PainelAbas.Distribuir(Enumerable.Repeat(210.0, 9).ToArray(), Minimos(9), 1000);
        Assert.All(larguras, l => Assert.Equal(111, l));
        Assert.True(larguras.Sum() <= 1000);
    }

    [Fact]
    public void ApertadaASelecionadaGuardaEspacoParaOX()
    {
        var larguras = PainelAbas.Distribuir(Enumerable.Repeat(200.0, 6).ToArray(), Minimos(6, selecionada: 5), 325);
        Assert.Equal(PainelAbas.MinimoSelecionada, larguras[5]);
        Assert.All(larguras.Take(5), l => Assert.Equal(43, l));
        Assert.True(larguras.Sum() <= 325);
    }

    [Fact]
    public void LarguraInfinitaDevolveAsNaturais()
    {
        Assert.Equal(new double[] { 150, 170 }, PainelAbas.Distribuir([150, 170], Minimos(2), double.PositiveInfinity));
    }
}
