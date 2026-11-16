using Nexora.Core.Texto;

namespace Nexora.Testes;

public sealed class LinksTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-links-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_pasta, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void EnderecoSoltoViraLinkSemAPontuacaoFinal()
    {
        var trechos = Markdown.Trechos("US: https://portal.azure.com/#resource/abc/metrics. E **também** [o PR](https://github.com/x/y/pull/1)");

        Assert.Equal("US: ", trechos[0].Texto);
        Assert.Equal("https://portal.azure.com/#resource/abc/metrics", trechos[1].Link);
        Assert.Equal(trechos[1].Texto, trechos[1].Link);
        Assert.Equal(". E ", trechos[2].Texto);
        Assert.Contains(trechos, t => t.Texto == "o PR" && t.Link == "https://github.com/x/y/pull/1");
        Assert.Single(Markdown.Trechos("sem link nenhum aqui"));
    }

    [Fact]
    public void ResolveCaminhoDeArquivoComLinha()
    {
        Directory.CreateDirectory(Path.Combine(_pasta, "src", "Calc"));
        var arquivo = Path.Combine(_pasta, "src", "Calc", "CalcAppService.cs");
        File.WriteAllText(arquivo, "class C {}");

        Assert.Equal(new AlvoDeLink(arquivo, null), Links.Arquivo("src/Calc/CalcAppService.cs", _pasta));
        Assert.Equal(new AlvoDeLink(arquivo, 12), Links.Arquivo("src/Calc/CalcAppService.cs#L12", _pasta));
        Assert.Equal(new AlvoDeLink(arquivo, 12), Links.Arquivo("./src/Calc/CalcAppService.cs#L12-L20", _pasta));
        Assert.Equal(new AlvoDeLink(arquivo, 7), Links.Arquivo("src/Calc/CalcAppService.cs:7", _pasta));
        Assert.Equal(new AlvoDeLink(arquivo, null), Links.Arquivo(arquivo, null));
        Assert.Equal(new AlvoDeLink(arquivo, null), Links.Arquivo("file:///" + arquivo.Replace('\\', '/'), null));
        Assert.Null(Links.Arquivo("src/Calc/NaoExiste.cs", _pasta));
        Assert.Null(Links.Arquivo("https://github.com", _pasta));
    }
}
