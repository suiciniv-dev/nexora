namespace Nexora.Testes;

public sealed class AtualizacoesTestes
{
    const string Json = """
        {"tag_name":"v0.3.1","body":"Novidades","assets":[
          {"name":"Nexora-Setup-0.3.1.exe","browser_download_url":"https://github.com/x/releases/download/v0.3.1/Nexora-Setup-0.3.1.exe"},
          {"name":"Nexora-Portatil-0.3.1.zip","browser_download_url":"https://github.com/x/releases/download/v0.3.1/Nexora-Portatil-0.3.1.zip"},
          {"name":"Nexora-Mac-AppleSilicon-0.3.1.dmg","browser_download_url":"https://github.com/x/releases/download/v0.3.1/Nexora-Mac-AppleSilicon-0.3.1.dmg"},
          {"name":"suspeito.exe","browser_download_url":"http://inseguro/suspeito.exe"}]}
        """;

    [Fact]
    public void AchaAVersaoNovaEOsArquivos()
    {
        var nova = Atualizacoes.Ler(Json, new Version(0, 3, 0))!;
        Assert.Equal("0.3.1", nova.Rotulo);
        Assert.Equal(3, nova.Arquivos.Count);
        Assert.DoesNotContain("suspeito.exe", nova.Arquivos.Keys);
        Assert.Equal("Novidades", nova.Notas);
    }

    [Fact]
    public void IgnoraAMesmaVersaoOuMaisVelha()
    {
        Assert.Null(Atualizacoes.Ler(Json, new Version(0, 3, 1)));
        Assert.Null(Atualizacoes.Ler(Json, new Version(0, 4, 0)));
        Assert.Null(Atualizacoes.Ler("""{"tag_name":"sem-versao"}""", new Version(0, 1, 0)));
    }
}
