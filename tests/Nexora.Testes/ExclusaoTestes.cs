using Nexora.Core.Ambientes;
using Nexora.Core.Git;

namespace Nexora.Testes;

public sealed class ExclusaoTestes : IDisposable
{
    readonly string _raiz = Directory.CreateTempSubdirectory("nexora-excluir-").FullName;

    public void Dispose()
    {
        foreach (var arquivo in Directory.EnumerateFiles(_raiz, "*", SearchOption.AllDirectories))
            File.SetAttributes(arquivo, FileAttributes.Normal);
        Directory.Delete(_raiz, recursive: true);
    }

    async Task<string> RepositorioAsync(string nome)
    {
        var pasta = Directory.CreateDirectory(Path.Combine(_raiz, nome)).FullName;
        await ComandoGit.ExecutarAsync(pasta, "init", "-b", "main");
        await ComandoGit.ExecutarAsync(pasta, "config", "user.email", "teste@nexora.local");
        await ComandoGit.ExecutarAsync(pasta, "config", "user.name", "Teste");
        File.WriteAllText(Path.Combine(pasta, "a.txt"), "um");
        await ComandoGit.ExecutarAsync(pasta, "add", ".");
        await ComandoGit.ExecutarAsync(pasta, "commit", "-m", "primeiro");
        return pasta;
    }

    [Fact]
    public void PastaDasConversasSegueONomeDoClaudeCode()
    {
        Assert.Equal(Path.Combine("C:\\h", "D--Repositorios-Acme-Shop"), ExclusaoDeProjeto.PastaDasConversas(@"D:\Repositorios\Acme.Shop", "C:\\h"));
    }

    [Fact]
    public void ContaAsConversasDaPasta()
    {
        var raiz = Directory.CreateDirectory(Path.Combine(_raiz, "projects")).FullName;
        var pasta = Directory.CreateDirectory(ExclusaoDeProjeto.PastaDasConversas(@"D:\Loja", raiz)).FullName;
        File.WriteAllText(Path.Combine(pasta, "a.jsonl"), "{}");
        File.WriteAllText(Path.Combine(pasta, "b.jsonl"), "{}");
        Assert.Equal(2, ExclusaoDeProjeto.Conversas(@"D:\Loja", raiz));
        Assert.Equal(0, ExclusaoDeProjeto.Conversas(@"D:\Outro", raiz));
    }

    [Fact]
    public void DentroDeNaoConfundeIrmaoComPrefixoIgual()
    {
        Assert.True(ExclusaoDeProjeto.DentroDe(@"D:\Loja\Loja.code-workspace", @"D:\Loja"));
        Assert.False(ExclusaoDeProjeto.DentroDe(@"D:\Loja-pedidos\x.code-workspace", @"D:\Loja"));
    }

    [Fact]
    public async Task PastaSemGitNaoTemAvisos()
    {
        var pasta = Directory.CreateDirectory(Path.Combine(_raiz, "solta")).FullName;
        var d = await ExclusaoDeProjeto.DiagnosticarAsync(pasta, _raiz);
        Assert.False(d.Git);
        Assert.Empty(ExclusaoDeProjeto.Avisos(d));
    }

    [Fact]
    public async Task RepositorioSemRemotoAvisaQueOHistoricoVaiJunto()
    {
        var pasta = await RepositorioAsync("loja");
        File.WriteAllText(Path.Combine(pasta, "a.txt"), "dois");
        var d = await ExclusaoDeProjeto.DiagnosticarAsync(pasta, _raiz);
        Assert.True(d.Git);
        Assert.False(d.EhWorktree);
        Assert.Equal(1, d.Alteracoes);
        var avisos = ExclusaoDeProjeto.Avisos(d);
        Assert.Contains(avisos, a => a.Contains("sem commit"));
        Assert.Contains(avisos, a => a.Contains("não tem remoto"));
        Assert.False(ExclusaoDeProjeto.Impede(d));
    }

    [Fact]
    public async Task WorktreeComAlteracaoNaoPodeSerRemovido()
    {
        var pasta = await RepositorioAsync("app");
        var irmao = Path.Combine(_raiz, "app-tarefa");
        await ComandoGit.ExecutarAsync(pasta, "worktree", "add", "-b", "tarefa", irmao);
        var principal = await ExclusaoDeProjeto.DiagnosticarAsync(pasta, _raiz);
        Assert.Single(principal.OutrosWorktrees);
        Assert.Contains(ExclusaoDeProjeto.Avisos(principal), a => a.Contains("para de funcionar"));

        File.WriteAllText(Path.Combine(irmao, "novo.txt"), "x");
        var d = await ExclusaoDeProjeto.DiagnosticarAsync(irmao, _raiz);
        Assert.True(d.EhWorktree);
        Assert.Equal("tarefa", d.Branch);
        Assert.True(Workspaces.MesmaPasta(d.RepositorioPrincipal!, pasta));
        Assert.True(ExclusaoDeProjeto.Impede(d));
    }
}
