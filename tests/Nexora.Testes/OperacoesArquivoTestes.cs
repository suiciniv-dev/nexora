using Nexora.Core.Arquivos;

namespace Nexora.Testes;

public sealed class OperacoesArquivoTestes : IDisposable
{
    readonly string _raiz = Directory.CreateTempSubdirectory("nexora-ops-").FullName;

    public void Dispose() => Directory.Delete(_raiz, recursive: true);

    [Fact]
    public void ValidaNomesQueOWindowsRecusa()
    {
        Assert.Null(OperacoesArquivo.NomeInvalido("Servico.cs"));
        Assert.NotNull(OperacoesArquivo.NomeInvalido("   "));
        Assert.NotNull(OperacoesArquivo.NomeInvalido("a:b.cs"));
        Assert.NotNull(OperacoesArquivo.NomeInvalido("con.txt"));
        Assert.NotNull(OperacoesArquivo.NomeInvalido("nome."));
    }

    [Fact]
    public void CriaERenomeiaArquivosEPastas()
    {
        var arquivo = OperacoesArquivo.Criar(_raiz, " Novo.cs ", ehPasta: false);
        Assert.Equal(Path.Combine(_raiz, "Novo.cs"), arquivo);
        Assert.True(File.Exists(arquivo));
        Assert.Throws<IOException>(() => OperacoesArquivo.Criar(_raiz, "novo.cs", ehPasta: false));

        var pasta = OperacoesArquivo.Criar(_raiz, "pagina", ehPasta: true);
        File.WriteAllText(Path.Combine(pasta, "dentro.txt"), "x");
        var renomeada = OperacoesArquivo.Renomear(pasta, "Pagina");
        Assert.Equal("Pagina", new DirectoryInfo(renomeada).Name);
        Assert.Equal("Pagina", Path.GetFileName(Directory.GetDirectories(_raiz).Single()));
        Assert.True(File.Exists(Path.Combine(renomeada, "dentro.txt")));

        var movido = OperacoesArquivo.Renomear(arquivo, "Servico.cs");
        Assert.False(File.Exists(arquivo));
        Assert.True(File.Exists(movido));
        Assert.Throws<IOException>(() => OperacoesArquivo.Renomear(movido, "Pagina"));
        Assert.Throws<IOException>(() => OperacoesArquivo.Renomear(movido, "a|b"));
    }
}
