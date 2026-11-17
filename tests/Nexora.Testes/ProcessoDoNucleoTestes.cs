using Nexora.Aplicativo;

namespace Nexora.Testes;

public sealed class ProcessoDoNucleoTestes
{
    const string Exe = @"D:\App\Nexora.exe";
    const string Dotnet = @"C:\Program Files\dotnet\dotnet.exe";

    [Fact]
    public void No_windows_o_servidor_sobe_pelo_dotnet_antes_do_exe()
    {
        var formas = ProcessoDoNucleo.Formas(Exe, windows: true, dependeDoDotnet: true, Dotnet).ToList();

        Assert.Equal([(Dotnet, true), (Exe, false)], formas);
    }

    [Fact]
    public void Rodando_pelo_dotnet_o_servidor_usa_o_mesmo_dotnet_com_a_dll()
    {
        var formas = ProcessoDoNucleo.Formas(Dotnet, windows: true, dependeDoDotnet: true, @"D:\outro\dotnet.exe").ToList();

        Assert.Equal([(Dotnet, true)], formas);
    }

    [Theory]
    [InlineData(false, true, Dotnet)]
    [InlineData(true, false, Dotnet)]
    [InlineData(true, true, null)]
    public void Sem_dotnet_utilizavel_o_servidor_sobe_pelo_proprio_executavel(bool windows, bool dependeDoDotnet, string? dotnet)
    {
        var formas = ProcessoDoNucleo.Formas(Exe, windows, dependeDoDotnet, dotnet).ToList();

        Assert.Equal([(Exe, false)], formas);
    }
}
