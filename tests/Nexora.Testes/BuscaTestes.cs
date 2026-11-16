using Nexora.Core.Arquivos;
using Nexora.Core.Git;

namespace Nexora.Testes;

public sealed class BuscaTestes : IDisposable
{
    readonly string _raiz = Directory.CreateTempSubdirectory("nexora-busca-").FullName;

    public void Dispose()
    {
        foreach (var arquivo in Directory.EnumerateFiles(_raiz, "*", SearchOption.AllDirectories)) File.SetAttributes(arquivo, FileAttributes.Normal);
        Directory.Delete(_raiz, recursive: true);
    }

    string Gravar(string relativo, string conteudo)
    {
        var caminho = Path.Combine(_raiz, relativo);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        File.WriteAllText(caminho, conteudo);
        return caminho;
    }

    [Fact]
    public async Task ProcuraComGitRespeitandoOIgnoreEIncluindoArquivoNovo()
    {
        var repo = Path.Combine(_raiz, "repo");
        Gravar(@"repo\.gitignore", "bin/\n");
        Gravar(@"repo\src\Servico.cs", "class Servico\n{\n    void Salvar() => Colaborador.Salvar();\n}\n");
        async Task G(params string[] args) => Assert.True((await ComandoGit.ExecutarAsync(repo, args)).Ok, string.Join(' ', args));
        await G("init", "-q", "-b", "dev");
        await G("add", ".");
        await G("-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "inicio");
        Gravar(@"repo\src\Novo.cs", "// colaborador novo, ainda sem commit\n");
        Gravar(@"repo\bin\Saida.cs", "Colaborador ignorado\n");

        var r = await Busca.ProcurarAsync([repo], "colaborador", maiusculas: false, regex: false);
        Assert.False(r.Cortado);
        Assert.Equal([(@"src\Novo.cs", 1), (@"src\Servico.cs", 3)], r.Achados.Select(a => (a.Relativo, a.Linha)).Order());
        var servico = r.Achados.Single(a => a.Relativo.EndsWith("Servico.cs"));
        Assert.Equal(Path.Combine(repo, "src", "Servico.cs"), servico.Caminho);
        Assert.Equal("void Salvar() => Colaborador.Salvar();", servico.Texto);
        Assert.Equal(22, servico.Coluna);

        Assert.Single((await Busca.ProcurarAsync([repo], "Colaborador", maiusculas: true, regex: false)).Achados);
        Assert.Single((await Busca.ProcurarAsync([repo], @"Salvar\(\)", maiusculas: true, regex: true)).Achados);
        Assert.Empty((await Busca.ProcurarAsync([repo], @"salvar\(\)", maiusculas: true, regex: true)).Achados);
    }

    [Fact]
    public async Task ProcuraEmPastaSemGitECortaNoMaximo()
    {
        var pasta = Path.Combine(_raiz, "solta");
        for (var i = 0; i < 5; i++) Gravar($@"solta\a{i}.txt", "alfa\nbeta alfa\n");
        Gravar(@"solta\node_modules\x.txt", "alfa\n");
        File.WriteAllBytes(Path.Combine(pasta, "binario.dat"), [0x61, 0x6c, 0x66, 0x61, 0, 1, 2]);

        var tudo = await Busca.ProcurarAsync([pasta], "ALFA", maiusculas: false, regex: false);
        Assert.Equal(10, tudo.Achados.Count);
        Assert.DoesNotContain(tudo.Achados, a => a.Relativo.Contains("node_modules") || a.Relativo.EndsWith(".dat"));

        var cortado = await Busca.ProcurarAsync([pasta], "alfa", maiusculas: false, regex: false, maximo: 3);
        Assert.True(cortado.Cortado);
        Assert.Equal(3, cortado.Achados.Count);
        Assert.Empty((await Busca.ProcurarAsync([pasta], "(", maiusculas: false, regex: true)).Achados);
        Assert.Empty((await Busca.ProcurarAsync([pasta], "", maiusculas: false, regex: false)).Achados);
    }
}
