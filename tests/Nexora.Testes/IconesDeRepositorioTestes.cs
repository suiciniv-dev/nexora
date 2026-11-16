namespace Nexora.Testes;

public sealed class IconesDeRepositorioTestes
{
    [Fact]
    public void PrefereOFaviconAoLogo()
    {
        Assert.Equal("web/wwwroot/favicon.png", IconesDeRepositorio.Escolher(["docs/logo.png", "assets/logo.png", "web/wwwroot/favicon.png"]));
    }

    [Fact]
    public void UsaOIcoDoAppQuandoNaoHaFavicon()
    {
        Assert.Equal("src/Nexora/Assets/nexora.ico", IconesDeRepositorio.Escolher(["src/Nexora/Assets/nexora.png", "src/Nexora/Assets/nexora.ico", "README.png"]));
    }

    [Fact]
    public void IgnoraDependenciasTestesESaidasDeBuild()
    {
        Assert.Null(IconesDeRepositorio.Escolher(["node_modules/pacote/favicon.png", "tests/fixtures/icon.png", "src/App/bin/Debug/app.ico"]));
    }

    [Fact]
    public void ImagemQualquerNaoViraIcone()
    {
        Assert.Null(IconesDeRepositorio.Escolher(["src/telas/captura.png", "fotos/equipe.png"]));
    }

    [Fact]
    public void EntreTamanhosFicaOMaior()
    {
        Assert.Equal("src/Resources/Images/icone512.png", IconesDeRepositorio.Escolher(["src/Resources/Images/icone50.png", "src/Resources/Images/icone512.png", "src/Resources/Images/logo_smx.png"]));
    }

    [Fact]
    public void EntreIguaisFicaOMaisRaso()
    {
        Assert.Equal("favicon.ico", IconesDeRepositorio.Escolher(["src/web/public/favicon.ico", "favicon.ico"]));
    }
}
