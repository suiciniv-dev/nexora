using Nexora.Core.Ambientes;
using Nexora.Core.Git;

namespace Nexora.Testes;

public sealed class AmbientesTestes : IDisposable
{
    readonly string _raiz = Directory.CreateTempSubdirectory("nexora-amb-").FullName;

    public void Dispose()
    {
        foreach (var arquivo in Directory.EnumerateFiles(_raiz, "*", SearchOption.AllDirectories))
            File.SetAttributes(arquivo, FileAttributes.Normal);
        Directory.Delete(_raiz, recursive: true);
    }

    string Pasta(params string[] partes) => Directory.CreateDirectory(Path.Combine([_raiz, .. partes])).FullName;

    [Fact]
    public void LeWorkspaceNaRaizComComentarioEVirgulaSobrando()
    {
        Pasta("Cloud");
        Pasta("Calc", "Calc");
        var arquivo = Path.Combine(_raiz, "Cloud.code-workspace");
        File.WriteAllText(arquivo, """
            {
                // workspace do Vini
                "folders": [
                    { "path": "Cloud" },
                    { "path": "Calc/Calc" },
                    { "path": "Cloud" },
                ],
                "settings": {},
            }
            """);

        var a = Workspaces.Ler(arquivo)!;
        Assert.Equal("Cloud", a.Nome);
        Assert.Equal(Path.Combine(_raiz, "Cloud"), a.Principal);
        Assert.Equal([Path.Combine(_raiz, "Calc", "Calc")], a.Vinculados);
        Assert.Equal(arquivo, a.Arquivo);
    }

    [Fact]
    public void LeWorkspaceDentroDoProjetoComPontoEPastasIrmas()
    {
        var melhorias = Pasta("Cloud-melhorias");
        Pasta("App");
        var arquivo = Path.Combine(melhorias, "Cloud-melhorias.code-workspace");
        File.WriteAllText(arquivo, """{"folders":[{"path":"."},{"path":"../App"}]}""");

        var a = Workspaces.Ler(arquivo)!;
        Assert.Equal(melhorias, a.Principal);
        Assert.Equal([Path.Combine(_raiz, "App")], a.Vinculados);
    }

    [Fact]
    public void ProcuraNaRaizENoPrimeiroNivelSemRepetirPrincipal()
    {
        Pasta("Cloud", "Interno");
        File.WriteAllText(Path.Combine(_raiz, "Cloud.code-workspace"), """{"folders":[{"path":"Cloud"}]}""");
        File.WriteAllText(Path.Combine(_raiz, "Cloud", "Cloud.code-workspace"), """{"folders":[{"path":"."}]}""");
        File.WriteAllText(Path.Combine(_raiz, "Cloud", "Interno", "Fundo.code-workspace"), """{"folders":[{"path":"."}]}""");
        Pasta("App");
        File.WriteAllText(Path.Combine(_raiz, "App", "App.code-workspace"), """{"folders":[{"path":"."}]}""");
        File.WriteAllText(Path.Combine(_raiz, "Quebrado.code-workspace"), "{ isso não é json");

        var achados = Workspaces.Procurar([_raiz, Path.Combine(_raiz, "nao-existe")]);
        Assert.Equal(["App", "Cloud"], achados.Select(a => a.Nome).Order());
    }

    [Fact]
    public void RaizSemWorkspaceTrazOsRepositoriosDoPrimeiroNivel()
    {
        var repos = Pasta("repos");
        Pasta("repos", "api", ".git");
        Pasta("repos", "site");
        File.WriteAllText(Path.Combine(repos, "site", ".git"), "gitdir: ../api/.git/worktrees/site");
        Pasta("repos", "anotacoes");
        Pasta("repos", ".escondido", ".git");
        Pasta("repos", "api", "interno", ".git");

        var achados = Workspaces.ProcurarProjetos([repos]);

        Assert.Equal(["api", "site"], achados.Select(a => a.Nome));
        Assert.All(achados, a => Assert.Null(a.Arquivo));
        Assert.All(achados, a => Assert.Empty(a.Vinculados));
    }

    [Fact]
    public void RaizQueJaEhRepositorioViraUmProjetoSo()
    {
        var app = Pasta("app");
        Pasta("app", ".git");
        Pasta("app", "modulo", ".git");

        var achado = Assert.Single(Workspaces.ProcurarProjetos([app]));
        Assert.Equal("app", achado.Nome);
        Assert.True(Workspaces.MesmaPasta(app, achado.Principal));
    }

    [Fact]
    public void RaizComWorkspaceNaoGanhaRepositoriosSoltos()
    {
        var comWorkspace = Pasta("trabalho");
        Pasta("trabalho", "Cloud", ".git");
        Pasta("trabalho", "Solto", ".git");
        File.WriteAllText(Path.Combine(comWorkspace, "Cloud.code-workspace"), """{"folders":[{"path":"Cloud"}]}""");
        var semWorkspace = Pasta("pessoal");
        Pasta("pessoal", "Cloud");
        Pasta("pessoal", "Blog", ".git");
        File.WriteAllText(Path.Combine(_raiz, "trabalho", "Cloud", "x.txt"), "");

        var achados = Workspaces.ProcurarProjetos([comWorkspace, semWorkspace, Path.Combine(_raiz, "nao-existe")]);

        Assert.Equal(["Blog", "Cloud"], achados.Select(a => a.Nome));
        Assert.NotNull(achados.Single(a => a.Nome == "Cloud").Arquivo);
    }

    [Fact]
    public void RepositorioQueJaEstaNumWorkspaceNaoRepete()
    {
        var principal = Pasta("principal");
        File.WriteAllText(Path.Combine(principal, "Junto.code-workspace"), """{"folders":[{"path":"."},{"path":"../libs/comum"}]}""");
        var libs = Pasta("libs");
        Pasta("libs", "comum", ".git");
        Pasta("libs", "outra", ".git");

        var achados = Workspaces.ProcurarProjetos([principal, libs]);

        Assert.Equal(["Junto", "outra"], achados.Select(a => a.Nome));
    }

    [Fact]
    public void EscreveWorkspaceDoWorktreeComCaminhosRelativos()
    {
        var origem = new Ambiente("Cloud", Pasta("Cloud"), [Pasta("DevShare", "Components"), Pasta("App")], null);
        var novo = Pasta("Cloud-login");

        var arquivo = Workspaces.Escrever(origem, novo);
        Assert.Equal(Path.Combine(novo, "Cloud-login.code-workspace"), arquivo);
        var lido = Workspaces.Ler(arquivo)!;
        Assert.Equal(novo, lido.Principal);
        Assert.Equal(origem.Vinculados, lido.Vinculados);
        Assert.Contains("../DevShare/Components", File.ReadAllText(arquivo));
    }

    [Fact]
    public void SalvarRegravaAsPastasEMantemOResto()
    {
        var principal = Pasta("App");
        var calc = Pasta("Calc");
        var cloud = Pasta("Cloud");
        var arquivo = Path.Combine(principal, "App.code-workspace");
        File.WriteAllText(arquivo, """{"folders":[{"path":"."},{"path":"../Calc"}],"settings":{"editor.tabSize":4},"extensions":{"recommendations":["x"]}}""");

        var lido = Workspaces.Ler(arquivo)!;
        var salvo = Workspaces.Salvar(lido with { Vinculados = [cloud] });
        Assert.Equal(arquivo, salvo.Arquivo);

        var texto = File.ReadAllText(arquivo);
        Assert.Contains("\t", texto);
        Assert.Contains("\"editor.tabSize\": 4", texto);
        Assert.Contains("recommendations", texto);
        var relido = Workspaces.Ler(arquivo)!;
        Assert.Equal(principal, relido.Principal);
        Assert.Equal([cloud], relido.Vinculados);
        Assert.DoesNotContain(calc, relido.Vinculados);
    }

    [Fact]
    public void SalvarAmbienteNovoCriaOArquivoNaPastaPrincipal()
    {
        var principal = Pasta("Novo");
        var lib = Pasta("Lib");
        var salvo = Workspaces.Salvar(new Ambiente("Meu ambiente", principal, [lib], null));
        Assert.Equal(Path.Combine(principal, "Meu ambiente.code-workspace"), salvo.Arquivo);
        var relido = Workspaces.Ler(salvo.Arquivo!)!;
        Assert.Equal("Meu ambiente", relido.Nome);
        Assert.Equal([lib], relido.Vinculados);
        Assert.Contains("\"../Lib\"", File.ReadAllText(salvo.Arquivo!));
    }

    [Fact]
    public void AmbienteDeWorktreeHerdaOsVinculados()
    {
        var a = new Ambiente("Cloud", @"D:\R\Cloud", [@"D:\R\Calc", @"D:\R\Cloud-x"], @"D:\R\Cloud.code-workspace");
        var w = a.ParaWorktree(@"D:\R\Cloud-x");
        Assert.Equal("Cloud-x", w.Nome);
        Assert.Equal([@"D:\R\Calc"], w.Vinculados);
        Assert.Null(w.Arquivo);
    }

    [Fact]
    public void LeListaDeWorktrees()
    {
        var lista = Repositorio.LerWorktrees("""
            worktree D:/Repositorios/Acme.Shop
            HEAD 0cacc99ae
            branch refs/heads/casos-assistente-ia-20261029

            worktree D:/Repositorios/Acme.Shop-melhorias
            HEAD 5aeeb1560
            branch refs/heads/erro-pagina-aberta-20261029

            worktree D:/Repositorios/solto
            HEAD 1234567
            detached

            """);
        Assert.Equal(3, lista.Count);
        Assert.Equal(new Worktree(@"D:\Repositorios\Acme.Shop", "casos-assistente-ia-20261029", "0cacc99ae", true), lista[0]);
        Assert.False(lista[1].Principal);
        Assert.Null(lista[2].Branch);
    }

    [Fact]
    public void NomeDaTarefaSegueOPadraoDoVini()
    {
        Assert.Equal("casos-assistente-ia-20261029", Repositorio.NomeDaTarefa("Casos assistente IA", new DateOnly(2026, 10, 29)));
        Assert.Equal("correcao-calculo-de-horas-20261030", Repositorio.NomeDaTarefa("  Correção: cálculo de horas!! ", new DateOnly(2026, 10, 30)));
        Assert.Equal(@"D:\Repositorios\Acme.Shop-login", Repositorio.PastaDoWorktree(@"D:\Repositorios\Acme.Shop\", "login"));
        Assert.True(Repositorio.Slug(new string('a', 80)).Length <= 40);
    }

    [Fact]
    public async Task CriaWorktreeTrocaBranchELista()
    {
        var repo = Pasta("Repo");
        async Task G(params string[] args) => Assert.True((await ComandoGit.ExecutarAsync(repo, args)).Ok, string.Join(' ', args));
        await G("init", "-q", "-b", "dev");
        await G("-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "--allow-empty", "-m", "inicio");
        await G("branch", "outra");

        Assert.Equal("dev", await Repositorio.BranchPadraoAsync(repo));
        Assert.Equal(["dev", "outra"], (await Repositorio.BranchesAsync(repo)).Order());

        var destino = Repositorio.PastaDoWorktree(repo, "login");
        var criado = await Repositorio.CriarWorktreeAsync(repo, destino, "login-20261029", "dev");
        Assert.True(criado.Ok, criado.Mensagem);
        Assert.Equal("login-20261029", Repositorio.BranchAtual(destino));

        var worktrees = await Repositorio.WorktreesAsync(repo);
        Assert.Equal(2, worktrees.Count);
        Assert.True(Workspaces.MesmaPasta(worktrees[1].Caminho, destino));

        Assert.True((await Repositorio.TrocarAsync(repo, "outra")).Ok);
        Assert.Equal("outra", Repositorio.BranchAtual(repo));
        File.WriteAllText(Path.Combine(repo, "novo.txt"), "x");
        Assert.Equal(1, await Repositorio.AlteracoesAsync(repo));
        Assert.False((await Repositorio.TrocarAsync(repo, "nao-existe")).Ok);
    }

    [Fact]
    public async Task WorktreeNovoGanhaOsArquivosLocaisComAProximaPorta()
    {
        var repo = Pasta("Cloud");
        async Task G(params string[] args) => Assert.True((await ComandoGit.ExecutarAsync(repo, args)).Ok, string.Join(' ', args));
        await G("init", "-q", "-b", "dev");
        File.WriteAllText(Path.Combine(repo, ".gitignore"), "appsettings.Development.json\n*.local.json\nbin/\n");
        File.WriteAllText(Path.Combine(Pasta("Cloud", "src", "Web"), "appsettings.json"), """{ "App": { "SelfUrl": "https://localhost:44315" } }""");
        await G("add", ".");
        await G("-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "inicio");

        var local = Path.Combine(repo, "src", "Web", "appsettings.Development.json");
        File.WriteAllText(local, """{ "App": { "SelfUrl": "https://dev.local:44315", "RedirectAllowedUrls": "https://dev.local:44315" }, "AuthServer": { "Authority": "https://localhost:44315" }, "Outro": "porta 443150" }""", new System.Text.UTF8Encoding(true));
        File.WriteAllText(Path.Combine(Pasta("Cloud", ".claude"), "settings.local.json"), """{ "permissions": { "allow": ["Bash(git status)"] } }""");
        File.WriteAllText(Path.Combine(Pasta("Cloud", "src", "Web", "bin", "Debug"), "appsettings.Development.json"), "{}");

        Assert.Equal([".claude/settings.local.json", "src/Web/appsettings.Development.json"], await ArquivosLocais.ListarAsync(repo));

        var primeiro = Repositorio.PastaDoWorktree(repo, "a");
        Assert.True((await Repositorio.CriarWorktreeAsync(repo, primeiro, "a-20261030", "dev")).Ok);
        var copia = await ArquivosLocais.CopiarAsync(repo, primeiro);
        Assert.Equal(2, copia.Arquivos.Count);
        Assert.Equal((44315, 44320), (copia.PortaAntiga!.Value, copia.PortaNova!.Value));
        var copiado = File.ReadAllBytes(Path.Combine(primeiro, "src", "Web", "appsettings.Development.json"));
        Assert.True(copiado.AsSpan().StartsWith(System.Text.Encoding.UTF8.Preamble));
        var texto = File.ReadAllText(Path.Combine(primeiro, "src", "Web", "appsettings.Development.json"));
        Assert.DoesNotContain(":44315", texto);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Count(texto, ":44320"));
        Assert.Contains("porta 443150", texto);
        Assert.True(File.Exists(Path.Combine(primeiro, ".claude", "settings.local.json")));
        Assert.False(File.Exists(Path.Combine(primeiro, "src", "Web", "bin", "Debug", "appsettings.Development.json")));
        Assert.Contains("troquei a porta 44315 por 44320", copia.Resumo);

        var segundo = Repositorio.PastaDoWorktree(repo, "b");
        Assert.True((await Repositorio.CriarWorktreeAsync(repo, segundo, "b-20261030", "dev")).Ok);
        Assert.Equal(44325, (await ArquivosLocais.CopiarAsync(repo, segundo)).PortaNova);
    }

    [Fact]
    public async Task RemoveWorktreeSemPerderTrabalho()
    {
        var repo = Pasta("Repo2");
        async Task G(string pasta, params string[] args) => Assert.True((await ComandoGit.ExecutarAsync(pasta, args)).Ok, string.Join(' ', args));
        await G(repo, "init", "-q", "-b", "dev");
        File.WriteAllText(Path.Combine(repo, ".gitignore"), "appsettings.Development.json\n");
        await G(repo, "add", ".");
        await G(repo, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "inicio");

        var limpo = Repositorio.PastaDoWorktree(repo, "limpo");
        Assert.True((await Repositorio.CriarWorktreeAsync(repo, limpo, "limpo-20261030", "dev")).Ok);
        File.WriteAllText(Path.Combine(limpo, "Repo2-limpo.code-workspace"), "{}");
        File.WriteAllText(Path.Combine(limpo, "appsettings.Development.json"), "{}");
        var removido = await Repositorio.RemoverWorktreeAsync(repo, limpo, "limpo-20261030", apagarBranch: true);
        Assert.True(removido.Ok, removido.Mensagem);
        Assert.True(removido.BranchApagada);
        Assert.False(Directory.Exists(limpo));
        Assert.DoesNotContain("limpo-20261030", await Repositorio.BranchesAsync(repo));

        var sujo = Repositorio.PastaDoWorktree(repo, "sujo");
        Assert.True((await Repositorio.CriarWorktreeAsync(repo, sujo, "sujo-20261030", "dev")).Ok);
        File.WriteAllText(Path.Combine(sujo, "novo.txt"), "trabalho");
        var recusado = await Repositorio.RemoverWorktreeAsync(repo, sujo, "sujo-20261030", apagarBranch: true);
        Assert.False(recusado.Ok);
        Assert.Contains("sem commit", recusado.Mensagem);
        Assert.True(File.Exists(Path.Combine(sujo, "novo.txt")));

        await G(sujo, "add", ".");
        await G(sujo, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "trabalho");
        var semMerge = await Repositorio.RemoverWorktreeAsync(repo, sujo, "sujo-20261030", apagarBranch: true);
        Assert.True(semMerge.Ok, semMerge.Mensagem);
        Assert.False(semMerge.BranchApagada);
        Assert.Contains("sujo-20261030", await Repositorio.BranchesAsync(repo));
    }

    [Fact]
    public void LePullRequestDoGh()
    {
        var pr = PullRequest.Ler("""
            {"number":3272,"title":"Integração de arquivos","state":"OPEN","isDraft":false,"url":"https://github.com/x/y/pull/3272","reviewDecision":"REVIEW_REQUIRED",
             "statusCheckRollup":[{"__typename":"CheckRun","status":"COMPLETED","conclusion":"SUCCESS"},{"__typename":"CheckRun","status":"IN_PROGRESS","conclusion":""}]}
            """)!;
        Assert.Equal(3272, pr.Numero);
        Assert.Equal("aberto  ·  esperando revisão  ·  checks rodando", pr.Resumo);
        Assert.True(pr.Aberto);

        var mergeado = PullRequest.Ler("""{"number":1,"title":"x","state":"MERGED","isDraft":false,"url":"u","reviewDecision":"APPROVED","statusCheckRollup":[{"state":"SUCCESS"}]}""")!;
        Assert.True(mergeado.Mergeado);
        Assert.Equal("mergeado  ·  aprovado  ·  checks ok", mergeado.Resumo);
        Assert.Equal("rascunho", PullRequest.Ler("""{"number":2,"state":"OPEN","isDraft":true,"statusCheckRollup":[]}""")!.Resumo);
        Assert.Null(PullRequest.Ler("no pull requests found"));
    }
}
