using System.Text.Json;
using Nexora.Core.Claude;
using Nexora.Core.Git;

namespace Nexora.Testes;

public sealed class FluxoDaIssueTestes
{
    static readonly IReadOnlyList<OpcaoStatus> Acme =
    [
        new("a", "🆕 New"), new("b", "📋 Backlog"), new("c", "📌 Ready"), new("d", "🏗 In progress"), new("e", "👀 In review"), new("f", "✅ Done"),
    ];

    [Fact]
    public void AchaAColunaDeAndamentoEDeRevisaoMesmoComEmoji()
    {
        Assert.Equal("d", FluxoDaIssue.Opcao(Acme, Etapa.Andamento)?.Id);
        Assert.Equal("e", FluxoDaIssue.Opcao(Acme, Etapa.Revisao)?.Id);
    }

    [Fact]
    public void EntendeNomesEmPortugues()
    {
        IReadOnlyList<OpcaoStatus> quadro = [new("1", "A fazer"), new("2", "Em andamento"), new("3", "Em revisão"), new("4", "Concluído")];
        Assert.Equal("2", FluxoDaIssue.Opcao(quadro, Etapa.Andamento)?.Id);
        Assert.Equal("3", FluxoDaIssue.Opcao(quadro, Etapa.Revisao)?.Id);
    }

    [Fact]
    public void QuadroSemAColunaNaoMexe()
    {
        IReadOnlyList<OpcaoStatus> quadro = [new("1", "Todo"), new("2", "Done")];
        Assert.Null(FluxoDaIssue.Opcao(quadro, Etapa.Revisao));
    }

    [Fact]
    public void SoAvancaNuncaVolta()
    {
        Assert.True(FluxoDaIssue.Avanca(Acme, "c", "d"));
        Assert.True(FluxoDaIssue.Avanca(Acme, null, "d"));
        Assert.False(FluxoDaIssue.Avanca(Acme, "f", "e"));
        Assert.False(FluxoDaIssue.Avanca(Acme, "d", "d"));
    }

    [Fact]
    public void LeONumstatComRenomeEBinario()
    {
        var linhas = Alteracoes.LerNumstat("12\t3\tsrc/a.cs\n-\t-\tlogo.png\n5\t0\tsrc/{velho => novo}/b.cs\n2\t1\tc.txt => d.txt\n");
        Assert.Equal(4, linhas.Count);
        Assert.Equal(("src/a.cs", 12, 3), (linhas[0].Caminho, linhas[0].Mais, linhas[0].Menos));
        Assert.True(linhas[1].Binario);
        Assert.Equal("src/novo/b.cs", linhas[2].Caminho);
        Assert.Equal("d.txt", linhas[3].Caminho);
    }

    [Fact]
    public void ChaveDePermissaoUsaOComandoOuOArquivo()
    {
        var bash = JsonDocument.Parse("""{"command":"  dotnet test  ","description":"roda"}""").RootElement;
        var outra = JsonDocument.Parse("""{"command":"dotnet test","description":"outra descrição"}""").RootElement;
        Assert.Equal(ChaveDePermissao.De("Bash", bash), ChaveDePermissao.De("Bash", outra));
        var edit = JsonDocument.Parse("""{"file_path":"D:\\x\\a.cs","old_string":"1","new_string":"2"}""").RootElement;
        Assert.Equal("Edit|D:\\x\\a.cs", ChaveDePermissao.De("Edit", edit));
        Assert.False(ChaveDePermissao.Liberavel("AskUserQuestion"));
    }

    [Fact]
    public void BuscaAchaPorNumeroTituloEtiquetaSemAcento()
    {
        var item = new ItemDoQuadro("i", "Issue", 3312, "Opção de criar cópia do banco", "u", "OPEN", "acme-corp/Acme.Shop",
            ["acme-dev"], [new Etiqueta("enhancement", "")], new Dictionary<string, ValorDeCampo>(), null, [], null);
        Assert.True(item.Combina("#3312"));
        Assert.True(item.Combina("copia banco"));
        Assert.True(item.Combina("enhancement"));
        Assert.True(item.Combina("acme-dev"));
        Assert.False(item.Combina("fotos"));
    }

    [Fact]
    public void LeOsComentariosDaIssue()
    {
        var detalhe = GitHub.LerIssue("""
            {"number":7,"title":"t","body":"b","url":"u","author":{"login":"ana"},"labels":[],"createdAt":"2026-11-01T10:00:00Z",
             "comments":[{"author":{"login":"bia"},"body":"Feito no PR #9","createdAt":"2026-11-02T10:00:00Z"}]}
            """);
        Assert.NotNull(detalhe);
        Assert.Equal(1, detalhe!.Comentarios);
        Assert.Equal(("bia", "Feito no PR #9"), (detalhe.Lista[0].Autor, detalhe.Lista[0].Corpo));
    }
}
