using Nexora.Core.Texto;

namespace Nexora.Testes;

public sealed class CitacoesEImagensTestes
{
    [Fact]
    public void AchaOsArquivosCitadosComArroba()
    {
        var texto = "Veja @Acme.Shop/src/Pages/EspelhoPontosAssistente.razor , @Acme.Shop/src/Tarefas/TarefaGerarEspelhos.cs e @README.md.";
        Assert.Equal(["Acme.Shop/src/Pages/EspelhoPontosAssistente.razor", "Acme.Shop/src/Tarefas/TarefaGerarEspelhos.cs", "README.md"], Mencoes.Arquivos(texto));
    }

    [Fact]
    public void EmailEArrobaSoltaNaoSaoArquivo()
    {
        Assert.Empty(Mencoes.Arquivos("mande para vini@acme.example, avise o @marcio e use @ no texto"));
    }

    [Fact]
    public void TirarUmaCitacaoMantemOResto()
    {
        var texto = "Issue: https://x/1\nVeja @src/a.cs , e @README.md por favor\n";
        var sem = Mencoes.Remover(texto, "src/a.cs");
        Assert.DoesNotContain("src/a.cs", sem);
        Assert.Contains("@README.md", sem);
        Assert.StartsWith("Issue: https://x/1\n", sem);
        Assert.EndsWith("\n", sem);
    }

    [Fact]
    public void SeparaAsImagensHtmlDoTextoDaIssue()
    {
        var (resto, imagens) = ImagensMd.Separar("""Demora para gerar. <img width="1608" height="295" alt="Image" src="https://github.com/user-attachments/assets/8546e9b8-a265-4627-8571-bbc81f30e79f" />""");
        Assert.Equal("Demora para gerar.", resto);
        var imagem = Assert.Single(imagens);
        Assert.Equal(1608, imagem.Largura);
        Assert.Equal("8546e9b8-a265-4627-8571-bbc81f30e79f", ImagensMd.Chave(imagem.Url));
    }

    [Fact]
    public void SeparaImagemEmMarkdownNaOrdem()
    {
        var (resto, imagens) = ImagensMd.Separar("![tela](https://a/1.png) texto ![](https://a/2.png)");
        Assert.Equal("texto", resto);
        Assert.Equal(["https://a/1.png", "https://a/2.png"], imagens.Select(i => i.Url));
    }

    [Fact]
    public void LinkAssinadoDoGitHubCasaPeloIdDoAnexo()
    {
        var html = """<a href="x"><img width="1608" alt="Image" src="https://private-user-images.githubusercontent.com/16240589/696114577-8546e9b8-a265-4627-8571-bbc81f30e79f.png?jwt=abc&amp;x=1"></a>""";
        var fonte = Assert.Single(ImagensMd.FontesDoHtml(html));
        Assert.EndsWith("jwt=abc&x=1", fonte);
        Assert.Equal("8546e9b8-a265-4627-8571-bbc81f30e79f", ImagensMd.Chave(fonte));
    }

    [Fact]
    public void TextoSemImagemFicaIgual()
    {
        var (resto, imagens) = ImagensMd.Separar("Só texto com <b>negrito</b>.");
        Assert.Equal("Só texto com <b>negrito</b>.", resto);
        Assert.Empty(imagens);
    }
}
