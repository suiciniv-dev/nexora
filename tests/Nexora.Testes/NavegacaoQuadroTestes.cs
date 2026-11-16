using Nexora.ViewModels;

namespace Nexora.Testes;

public sealed class NavegacaoQuadroTestes
{
    static readonly IReadOnlyList<IReadOnlyList<string>> Colunas =
    [
        ["a1", "a2"],
        ["b1", "b2", "b3"],
        [],
        ["d1"],
    ];

    [Fact]
    public void SemNadaSelecionadoVaiParaOPrimeiro() => Assert.Equal("a1", NavegacaoQuadro.Vizinho(Colunas, null, 0, 1));

    [Fact]
    public void ParaBaixoEParaCimaFicamNaColuna()
    {
        Assert.Equal("b2", NavegacaoQuadro.Vizinho(Colunas, "b1", 0, 1));
        Assert.Equal("b3", NavegacaoQuadro.Vizinho(Colunas, "b3", 0, 1));
        Assert.Equal("a1", NavegacaoQuadro.Vizinho(Colunas, "a1", 0, -1));
    }

    [Fact]
    public void ParaOLadoPulaColunaVaziaEMantemALinhaQuandoDa()
    {
        Assert.Equal("b2", NavegacaoQuadro.Vizinho(Colunas, "a2", 1, 0));
        Assert.Equal("d1", NavegacaoQuadro.Vizinho(Colunas, "b3", 1, 0));
        Assert.Equal("a2", NavegacaoQuadro.Vizinho(Colunas, "b3", -1, 0));
        Assert.Equal("d1", NavegacaoQuadro.Vizinho(Colunas, "d1", 1, 0));
    }

    [Fact]
    public void NaJanelaRapidaAndaPelaOrdemSemPassarDasPontas()
    {
        string[] ordem = ["a1", "a2", "b1"];
        Assert.Equal("a2", NavegacaoQuadro.Passo(ordem, "a1", 1));
        Assert.Equal("b1", NavegacaoQuadro.Passo(ordem, "b1", 1));
        Assert.Equal("a1", NavegacaoQuadro.Passo(ordem, "a1", -1));
        Assert.Equal("a1", NavegacaoQuadro.Passo(ordem, "zz", 1));
    }
}
