using Nexora.Core.Senna;
using Xunit;

namespace Nexora.Testes;

public class PassagensBuscaTestes
{
    static readonly IReadOnlyList<Passagem> Lista =
    [
        new("Handoff: escala-colaborador-20261105", "Acme.Shop", DateTimeOffset.Now),
        new("Handoff: importacao-planilhas-claude-20261106", "Acme.Shop", DateTimeOffset.Now),
        new("Handoff: visual-tour-fixes", "Nexora", DateTimeOffset.Now),
        new("Handoff: Migração das férias", "Toca", DateTimeOffset.Now),
    ];

    [Fact]
    public void BuscaVaziaDevolveTudo() => Assert.Equal(4, PassagensDoSenna.Filtrar(Lista, "  ").Count);

    [Fact]
    public void TodasAsPalavrasPrecisamBater()
    {
        var achadas = PassagensDoSenna.Filtrar(Lista, "acme planilhas");
        Assert.Single(achadas);
        Assert.Contains("importacao-planilhas", achadas[0].Titulo);
    }

    [Fact]
    public void IgnoraAcentoMaiusculaEHifen()
    {
        Assert.Single(PassagensDoSenna.Filtrar(Lista, "MIGRACAO ferias"));
        Assert.Single(PassagensDoSenna.Filtrar(Lista, "tour fixes"));
        Assert.Single(PassagensDoSenna.Filtrar(Lista, "escala-colaborador"));
    }

    [Fact]
    public void BuscaPeloProjeto() => Assert.Equal(2, PassagensDoSenna.Filtrar(Lista, "shop").Count);

    [Fact]
    public void SemResultado() => Assert.Empty(PassagensDoSenna.Filtrar(Lista, "inexistente"));
}
