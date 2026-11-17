using Nexora.Aplicativo;

namespace Nexora.Testes;

public sealed class ProcessoDoNucleoTestes : IDisposable
{
    const string Exe = @"D:\App\Nexora.exe";
    const string Copia = @"C:\Dados\nucleo\abc";
    const string Dotnet = @"C:\Program Files\dotnet\dotnet.exe";

    readonly string _raiz = Directory.CreateTempSubdirectory("nexora-copia-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_raiz, true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void No_windows_o_servidor_sobe_pelo_dotnet_com_a_dll_da_copia_antes_do_exe()
    {
        var formas = ProcessoDoNucleo.Formas(Exe, Copia, windows: true, dependeDoDotnet: true, Dotnet).ToList();

        Assert.Equal([(Dotnet, Path.Combine(Copia, "Nexora.dll")), (Path.Combine(Copia, "Nexora.exe"), (string?)null)], formas);
    }

    [Fact]
    public void Rodando_pelo_dotnet_o_servidor_usa_o_mesmo_dotnet_com_a_dll()
    {
        var formas = ProcessoDoNucleo.Formas(Dotnet, Copia, windows: true, dependeDoDotnet: true, @"D:\outro\dotnet.exe").ToList();

        Assert.Equal([(Dotnet, Path.Combine(Copia, "Nexora.dll"))], formas);
    }

    [Theory]
    [InlineData(false, true, Dotnet)]
    [InlineData(true, false, Dotnet)]
    [InlineData(true, true, null)]
    public void Sem_dotnet_utilizavel_o_servidor_sobe_pelo_proprio_executavel(bool windows, bool dependeDoDotnet, string? dotnet)
    {
        var formas = ProcessoDoNucleo.Formas(Exe, Copia, windows, dependeDoDotnet, dotnet).ToList();

        Assert.Equal([(Path.Combine(Copia, "Nexora.exe"), (string?)null)], formas);
    }

    string Build(string nome, string conteudo)
    {
        var pasta = Path.Combine(_raiz, nome);
        Directory.CreateDirectory(Path.Combine(pasta, "runtimes", "win-x64"));
        File.WriteAllText(Path.Combine(pasta, "Nexora.dll"), conteudo);
        File.WriteAllText(Path.Combine(pasta, "runtimes", "win-x64", "nativo.dll"), "n");
        return pasta;
    }

    [Fact]
    public void A_copia_da_versao_e_feita_uma_vez_e_traz_as_subpastas()
    {
        var instalada = Build("instalada", "v1");
        var raiz = Path.Combine(_raiz, "nucleo");
        var versao = Guid.NewGuid();

        var copia = CopiaDoNucleo.Preparar(instalada, raiz, versao);
        File.WriteAllText(Path.Combine(instalada, "Nexora.dll"), "v2");
        var de_novo = CopiaDoNucleo.Preparar(instalada, raiz, versao);

        Assert.Equal(copia, de_novo);
        Assert.Equal(Path.Combine(raiz, versao.ToString("N")), copia);
        Assert.Equal("v1", File.ReadAllText(Path.Combine(copia, "Nexora.dll")));
        Assert.True(File.Exists(Path.Combine(copia, "runtimes", "win-x64", "nativo.dll")));
        Assert.Single(Directory.GetDirectories(raiz));
    }

    [Fact]
    public void Limpar_apaga_as_copias_velhas_e_poupa_a_atual_e_a_que_esta_em_uso()
    {
        var instalada = Build("instalada", "v1");
        var raiz = Path.Combine(_raiz, "nucleo");
        var atual = CopiaDoNucleo.Preparar(instalada, raiz, Guid.NewGuid());
        var velha = CopiaDoNucleo.Preparar(instalada, raiz, Guid.NewGuid());
        var emUso = CopiaDoNucleo.Preparar(instalada, raiz, Guid.NewGuid());
        var parcialNova = Directory.CreateDirectory(Path.Combine(raiz, "x.parcial-1234")).FullName;

        using (new FileStream(Path.Combine(emUso, "Nexora.dll"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.True(CopiaDoNucleo.EmUso(emUso));
            CopiaDoNucleo.Limpar(raiz, atual);
        }

        Assert.True(Directory.Exists(atual));
        Assert.False(Directory.Exists(velha));
        Assert.True(Directory.Exists(emUso));
        Assert.True(Directory.Exists(parcialNova));
        Assert.False(CopiaDoNucleo.EmUso(emUso));
    }
}
