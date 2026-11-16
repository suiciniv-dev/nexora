using Nexora.Core.Arquivos;
using Nexora.Core.Git;
using Nexora.Core.Texto;

namespace Nexora.Testes;

public sealed class ArquivosTestes : IDisposable
{
    readonly string _raiz = Directory.CreateTempSubdirectory("nexora-arq-").FullName;

    public void Dispose()
    {
        foreach (var arquivo in Directory.EnumerateFiles(_raiz, "*", SearchOption.AllDirectories))
            File.SetAttributes(arquivo, FileAttributes.Normal);
        Directory.Delete(_raiz, recursive: true);
    }

    string Arquivo(string relativo, string conteudo = "")
    {
        var caminho = Path.Combine(_raiz, relativo);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        File.WriteAllText(caminho, conteudo);
        return caminho;
    }

    [Fact]
    public void FilhosVemComPastasPrimeiroESemPastasDeBuild()
    {
        Arquivo("zeta.cs");
        Arquivo("Alfa.cs");
        Arquivo("src/A.cs");
        Arquivo("bin/Debug/x.dll");
        Arquivo("obj/x.json");
        Arquivo("node_modules/p/i.js");
        Directory.CreateDirectory(Path.Combine(_raiz, ".git"));

        var filhos = Pastas.Filhos(_raiz);
        Assert.Equal(["src", "Alfa.cs", "zeta.cs"], filhos.Select(f => f.Nome));
        Assert.True(filhos[0].EhPasta);
        Assert.Empty(Pastas.Filhos(Path.Combine(_raiz, "nao-existe")));
    }

    [Fact]
    public void IndiceAchaPorNomeCaminhoESequencia()
    {
        Arquivo("src/Services/AssistenteService.cs");
        Arquivo("src/Pages/Assistente.razor");
        Arquivo("tests/AssistenteServiceTests.cs");
        Arquivo("README.md");
        Arquivo("bin/AssistenteService.dll");

        var indice = IndiceArquivos.Montar([_raiz, Path.Combine(_raiz, "nao-existe")]);
        Assert.Equal(4, indice.Total);
        Assert.Equal("AssistenteService.cs", indice.Buscar("assistenteservice.cs")[0].Nome);
        Assert.Equal("src/Services/AssistenteService.cs", indice.Buscar("AssistenteSer")[0].Relativo);
        Assert.Contains(indice.Buscar("pages/assis"), a => a.Nome == "Assistente.razor");
        Assert.Equal("AssistenteServiceTests.cs", Assert.Single(indice.Buscar("asvt")).Nome);
        Assert.Equal("Assistente.razor", indice.Buscar("asst")[0].Nome);
    }

    [Fact]
    public void IndiceMostraSoAMelhorFaixa()
    {
        var indice = IndiceArquivos.DeLista(new[]
        {
            "Pages/Pedidos.razor", "Pages/Pedidos.razor.cs", "Pages/MonitorPedidos.razor", "Pages/PedidosCancelados.razor", "Components/AcmePedidoDestino.razor",
        }.Select(r => new ArquivoIndexado("App", r, r, Path.GetFileName(r))));

        Assert.Equal(["Pedidos.razor", "Pedidos.razor.cs"], indice.Buscar("pedidos.razor").Select(a => a.Nome));
        Assert.Equal(["Pedidos.razor", "Pedidos.razor.cs", "PedidosCancelados.razor"], indice.Buscar("pedidos").Select(a => a.Nome));
        Assert.Equal(["MonitorPedidos.razor"], indice.Buscar("monitorped").Select(a => a.Nome));
        Assert.Equal(["AcmePedidoDestino.razor"], indice.Buscar("acmepd").Select(a => a.Nome));
        Assert.Empty(indice.Buscar("inexistente"));
    }

    [Fact]
    public void DiffDeTextoMarcaRemovidasEAdicionadas()
    {
        var linhas = Diff.Comparar("a\nb\nc", "a\nB\nc\nd");
        Assert.Equal(
            [
                new LinhaDiff(TipoLinha.Igual, "a", 1, 1),
                new LinhaDiff(TipoLinha.Removida, "b", 2),
                new LinhaDiff(TipoLinha.Adicionada, "B", null, 2),
                new LinhaDiff(TipoLinha.Igual, "c", 3, 3),
                new LinhaDiff(TipoLinha.Adicionada, "d", null, 4),
            ],
            linhas);
        Assert.All(Diff.Comparar("", "x\ny"), l => Assert.Equal(TipoLinha.Adicionada, l.Tipo));
    }

    [Fact]
    public void LeDiffUnificadoDoGit()
    {
        var linhas = Diff.LerUnificado("""
            diff --git a/x.cs b/x.cs
            index 1..2 100644
            --- a/x.cs
            +++ b/x.cs
            @@ -10,3 +10,3 @@ class X
             manter
            -int x;
            +long x;
             fim
            \ No newline at end of file
            """);
        Assert.Equal(TipoLinha.Cabecalho, linhas[0].Tipo);
        Assert.Equal(new LinhaDiff(TipoLinha.Igual, "manter", 10, 10), linhas[1]);
        Assert.Equal(new LinhaDiff(TipoLinha.Removida, "int x;", 11), linhas[2]);
        Assert.Equal(new LinhaDiff(TipoLinha.Adicionada, "long x;", null, 11), linhas[3]);
        Assert.Equal(new LinhaDiff(TipoLinha.Igual, "fim", 12, 12), linhas[4]);
        Assert.Equal(5, linhas.Count);
    }

    [Fact]
    public void LeStatusPorcelanaComRenomeacao()
    {
        var lista = Alteracoes.Ler(" M src/a.cs\0?? novo.txt\0R  b.cs\0velho.cs\0 D apagado.cs\0");
        Assert.Equal(4, lista.Count);
        Assert.Equal(new Alteracao(" M", "src/a.cs", null), lista[0]);
        Assert.Equal("novo", lista[1].Rotulo);
        Assert.Equal(new Alteracao("R ", "b.cs", "velho.cs"), lista[2]);
        Assert.Equal("renomeado", lista[2].Rotulo);
        Assert.Equal("apagado", lista[3].Rotulo);
    }

    [Fact]
    public async Task ListaAlteracoesEDiffDeUmRepositorioDeVerdade()
    {
        async Task G(params string[] args) => Assert.True((await ComandoGit.ExecutarAsync(_raiz, args)).Ok, string.Join(' ', args));
        await G("init", "-q", "-b", "dev");
        Arquivo("a.cs", "linha 1\nlinha 2\n");
        await G("add", ".");
        await G("-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "inicio");
        Arquivo("a.cs", "linha 1\nlinha dois\n");
        Arquivo("pasta/novo.cs", "oi");

        var lista = await Alteracoes.ListarAsync(_raiz);
        Assert.Equal(2, lista.Count);
        var alterado = lista.Single(a => a.Caminho == "a.cs");
        var diff = Diff.LerUnificado(await Alteracoes.DiffAsync(_raiz, alterado));
        Assert.Contains(diff, l => l is { Tipo: TipoLinha.Removida, Texto: "linha 2" });
        Assert.Contains(diff, l => l is { Tipo: TipoLinha.Adicionada, Texto: "linha dois" });

        var novo = lista.Single(a => a.Caminho == "pasta/novo.cs");
        Assert.True(novo.Nova);
        Assert.Contains(Diff.LerUnificado(await Alteracoes.DiffAsync(_raiz, novo)), l => l is { Tipo: TipoLinha.Adicionada, Texto: "oi" });
    }

    [Fact]
    public void LocalDoArquivoMostraAPastaEmQueEleEsta()
    {
        var curto = new Nexora.Core.Arquivos.ArquivoIndexado("Nexora", @"D:\Nexora\CLAUDE.md", "CLAUDE.md", "CLAUDE.md");
        var fundo = new Nexora.Core.Arquivos.ArquivoIndexado("Nexora", @"D:\Repos\Acme\Shop\src\Core\a.cs", "src/Core/a.cs", "a.cs");
        Assert.Equal("D:/Nexora", curto.Local);
        Assert.Equal("…/Shop/src/Core", fundo.Local);
    }
}
