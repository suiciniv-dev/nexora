using System.Text.Json;
using Nexora.Core.Senna;
using Nexora.ViewModels;

namespace Nexora.Testes;

public sealed class PassagensTestes
{
    [Fact]
    public void LeSoAsPassagensDaMaisNovaParaAMaisVelha()
    {
        using var doc = JsonDocument.Parse("""
            {"total":4,"itens":[
              {"id":1,"titulo":"Handoff: cdn-storage-reserva-20261105","tipo":"handoff","projeto":"Acme.Shop","atualizado_em":1793884259.847},
              {"id":2,"titulo":"Token permanente do Cloudflare","tipo":"note","projeto":"dev","atualizado_em":1793900000},
              {"id":3,"titulo":"Handoff: afastamento-reimportacao-mes-certo-20261105","tipo":"handoff","projeto":"Acme.Shop","atualizado_em":1793896483.503},
              {"id":4,"titulo":"Handoff: integracoes-api-20261104","tipo":"handoff","projeto":"Acme.Shop-melhorias","atualizado_em":1793822987.976}
            ]}
            """);

        var passagens = PassagensDoSenna.Ler(doc.RootElement);

        Assert.Equal(3, passagens.Count);
        Assert.Equal("afastamento-reimportacao-mes-certo-20261105", passagens[0].Rotulo);
        Assert.Equal("Handoff: afastamento-reimportacao-mes-certo-20261105", passagens[0].Titulo);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1793896483503), passagens[0].Quando);
        Assert.Equal(3, PassagensDoSenna.DoProjeto(passagens, @"D:\Repositorios\Acme.Shop").Count);
        Assert.Equal(["integracoes-api-20261104"], PassagensDoSenna.DoProjeto(passagens, @"D:\Repositorios\Acme.Shop-melhorias").Select(p => p.Rotulo).Take(1));
        Assert.Empty(PassagensDoSenna.DoProjeto(passagens, @"D:\Nexora"));
    }

    [Fact]
    public void MesmoProjetoIncluiAsWorktrees()
    {
        Assert.True(PassagensDoSenna.MesmoProjeto("Acme.Shop", "acme.shop"));
        Assert.True(PassagensDoSenna.MesmoProjeto("Acme.Shop-melhorias", "Acme.Shop"));
        Assert.True(PassagensDoSenna.MesmoProjeto("Acme.Shop", "Acme.Shop-3361-erros-impressao-espelho"));
        Assert.False(PassagensDoSenna.MesmoProjeto("Acme", "Acme.Shop"));
        Assert.False(PassagensDoSenna.MesmoProjeto("", "Nexora"));
    }

    [Fact]
    public void TokensPorExtenso()
    {
        Assert.Equal("684 mil", CartaoVM.TokensPorExtenso(684_300));
        Assert.Equal("950", CartaoVM.TokensPorExtenso(950));
        Assert.Contains("milhão", CartaoVM.TokensPorExtenso(1_200_000));
        Assert.Contains("milhões", CartaoVM.TokensPorExtenso(2_500_000));
    }
}
