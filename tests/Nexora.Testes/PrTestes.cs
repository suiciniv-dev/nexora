using System.Text.Json;
using Nexora.Core.Git;

namespace Nexora.Testes;

public sealed class PrTestes
{
    const string Pr = """
        {"number":52,"title":"Cupom vencido devolve falha","body":"Corpo do PR","url":"https://github.com/voce/loja/pull/52","state":"OPEN","isDraft":false,
         "author":{"login":"voce","name":"Você"},"baseRefName":"main","headRefName":"cupom-checkout","headRefOid":"abc123",
         "mergeable":"CONFLICTING","mergeStateStatus":"DIRTY","reviewDecision":"CHANGES_REQUESTED","autoMergeRequest":{"enabledAt":"2026-11-09T10:00:00Z"},
         "updatedAt":"2026-11-09T12:00:00Z",
         "reviewRequests":[{"__typename":"User","login":"bruno"},{"__typename":"Team","name":"Loja","slug":"loja"}],
         "statusCheckRollup":[
           {"__typename":"CheckRun","completedAt":"2026-11-09T11:00:00Z","conclusion":"FAILURE","detailsUrl":"https://github.com/voce/loja/actions/runs/417/job/9","name":"testes","startedAt":"2026-11-09T10:50:00Z","status":"COMPLETED","workflowName":"CI"},
           {"__typename":"CheckRun","completedAt":"0001-01-01T00:00:00Z","conclusion":"","detailsUrl":"https://github.com/voce/loja/actions/runs/417/job/10","name":"lint","startedAt":"2026-11-09T10:50:00Z","status":"IN_PROGRESS","workflowName":"CI"},
           {"__typename":"CheckRun","completedAt":"2026-11-09T11:00:00Z","conclusion":"SKIPPED","detailsUrl":"","name":"deploy","startedAt":"2026-11-09T10:50:00Z","status":"COMPLETED","workflowName":"CD"},
           {"__typename":"StatusContext","context":"ci/externo","startedAt":"2026-11-09T10:50:00Z","state":"SUCCESS","targetUrl":"https://ci.example/1"}],
         "reviews":[
           {"id":"PRR_1","author":{"login":"ana-dev"},"authorAssociation":"MEMBER","body":"Faltam dois ajustes","submittedAt":"2026-11-09T09:00:00Z","state":"CHANGES_REQUESTED"},
           {"id":"PRR_2","author":{"login":"ana-dev"},"authorAssociation":"MEMBER","body":"","submittedAt":"2026-11-09T09:30:00Z","state":"COMMENTED"},
           {"id":"PRR_3","author":{"login":"carla"},"authorAssociation":"MEMBER","body":"","submittedAt":"2026-11-09T08:00:00Z","state":"APPROVED"}],
         "comments":[
           {"id":"IC_1","author":{"login":"bot"},"body":"spam","createdAt":"2026-11-09T08:00:00Z","isMinimized":true,"url":"u1"},
           {"id":"IC_2","author":{"login":"carla"},"body":"Vou testar amanhã","createdAt":"2026-11-09T08:10:00Z","isMinimized":false,"url":"https://github.com/voce/loja/pull/52#issuecomment-2"}]}
        """;

    const string Conversas = """
        {"data":{"viewer":{"login":"voce"},"repository":{"pullRequest":{"reviewThreads":{"nodes":[
          {"id":"PRRT_1","isResolved":false,"isOutdated":false,"path":"src/Cupons/CupomService.cs","line":42,"originalLine":40,
           "comments":{"nodes":[{"id":"PRRC_1","author":{"login":"ana-dev"},"body":"Devolva uma falha em vez de lançar exceção.","url":"https://github.com/voce/loja/pull/52#discussion_r1","createdAt":"2026-11-09T09:00:00Z"},
                                {"id":"PRRC_2","author":{"login":"voce"},"body":"Feito","url":"u","createdAt":"2026-11-09T09:20:00Z"}]}},
          {"id":"PRRT_2","isResolved":true,"isOutdated":true,"path":"src/Pedidos/Pedido.cs","line":null,"originalLine":88,
           "comments":{"nodes":[{"id":"PRRC_3","author":{"login":"ana-dev"},"body":"Nome melhor?","url":"u3","createdAt":"2026-11-09T09:05:00Z"}]}}]}}}}}
        """;

    [Fact]
    public void LeOPrCompleto()
    {
        var pr = GitHub.LerDetalhePr(Pr)!;
        Assert.Equal(52, pr.Numero);
        Assert.Equal("Cupom vencido devolve falha", pr.Titulo);
        Assert.Equal("voce", pr.Autor);
        Assert.Equal(("main", "cupom-checkout", "abc123"), (pr.BaseBranch, pr.HeadBranch, pr.ShaDoHead));
        Assert.True(pr.Aberto);
        Assert.True(pr.TemConflito);
        Assert.False(pr.PodeMergear);
        Assert.True(pr.AutoMerge);
        Assert.Equal("CHANGES_REQUESTED", pr.Revisao);
        Assert.Equal(["bruno", "loja"], pr.RevisoresPedidos);
        Assert.Single(pr.Comentarios);
        Assert.Equal("carla", pr.Comentarios[0].Autor);
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero), pr.Atualizado);
    }

    [Fact]
    public void LeOsChecksComEstadoELink()
    {
        var pr = GitHub.LerDetalhePr(Pr)!;
        Assert.Equal(4, pr.Checks.Count);
        var testes = pr.Checks[0];
        Assert.Equal(EstadoCheck.Falhou, testes.Estado);
        Assert.Equal("CI / testes", testes.Chave);
        Assert.Equal(417L, testes.Execucao);
        Assert.Equal(EstadoCheck.Pendente, pr.Checks[1].Estado);
        Assert.Null(pr.Checks[1].Fim);
        Assert.Equal(EstadoCheck.Pulado, pr.Checks[2].Estado);
        Assert.Equal(EstadoCheck.Ok, pr.Checks[3].Estado);
        Assert.Equal("ci/externo", pr.Checks[3].Nome);
        Assert.Equal("https://ci.example/1", pr.Checks[3].Link);
        Assert.Equal((2, 1, 1), (pr.ChecksOk, pr.ChecksFalhos, pr.ChecksPendentes));
        var estado = pr.ComoEstado();
        Assert.Equal((52, 2, 1, 1), (estado.Numero, estado.ChecksOk, estado.ChecksFalhos, estado.ChecksPendentes));
    }

    [Fact]
    public void RevisoresGuardamADecisaoMaisRecenteEOsPedidos()
    {
        var revisores = GitHub.LerDetalhePr(Pr)!.Revisores;
        Assert.Contains(revisores, r => r is { Login: "ana-dev", Estado: "CHANGES_REQUESTED" });
        Assert.Contains(revisores, r => r is { Login: "carla", Estado: "APPROVED" });
        Assert.Contains(revisores, r => r is { Login: "bruno", Estado: "REQUESTED" });
        Assert.Equal(4, revisores.Count);
    }

    [Fact]
    public void LeAsConversasDeRevisaoEQuemEstaLogado()
    {
        var (conversas, eu) = GitHub.LerConversasDoPr(Conversas);
        Assert.Equal("voce", eu);
        Assert.Equal(2, conversas.Count);
        Assert.Equal(("src/Cupons/CupomService.cs", 42), (conversas[0].Arquivo, conversas[0].Linha));
        Assert.False(conversas[0].Resolvida);
        Assert.Equal(2, conversas[0].Comentarios.Count);
        Assert.Equal("PRRC_1", conversas[0].Primeiro!.Id);
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 9, 20, 0, TimeSpan.Zero), conversas[0].UltimaEm);
        Assert.Equal(88, conversas[1].Linha);
        Assert.True(conversas[1].Resolvida && conversas[1].Desatualizada);
        var pr = GitHub.LerDetalhePr(Pr)! with { Conversas = conversas };
        Assert.Single(pr.ConversasAbertas);
    }

    [Fact]
    public void JsonRuimNaoDerruba()
    {
        Assert.Null(GitHub.LerDetalhePr("não é json"));
        Assert.Null(GitHub.LerDetalhePr("""{"number":0}"""));
        Assert.Empty(GitHub.LerConversasDoPr("[]").Conversas);
        Assert.Null(GitHub.LerConversasDoPr("""{"data":{}}""").Eu);
    }

    [Fact]
    public void PrLimpoPodeSerMergeado()
    {
        var pr = GitHub.LerDetalhePr("""{"number":9,"url":"u","state":"OPEN","isDraft":false,"mergeable":"MERGEABLE","mergeStateStatus":"CLEAN"}""")!;
        Assert.True(pr.PodeMergear);
        Assert.False(pr.TemConflito);
        Assert.False((pr with { Rascunho = true }).PodeMergear);
        Assert.Empty(pr.Checks);
    }

    [Fact]
    public void NovidadesApontamCheckConflitoComentarioDeOutroEMerge()
    {
        var antes = GitHub.LerDetalhePr("""{"number":52,"url":"u","state":"OPEN","mergeable":"MERGEABLE","mergeStateStatus":"CLEAN","headRefOid":"a1","statusCheckRollup":[],"comments":[],"reviews":[]}""")!;
        var depois = GitHub.LerDetalhePr(Pr)! with { Conversas = GitHub.LerConversasDoPr(Conversas).Conversas };
        var novidades = GitHub.Novidades(antes, depois, "voce");
        Assert.Contains(novidades, n => n.Tipo == TipoNovidadePr.Conflito);
        Assert.Contains(novidades, n => n.Tipo == TipoNovidadePr.CheckFalhou && n.Texto == "CI / testes");
        Assert.Contains(novidades, n => n is { Tipo: TipoNovidadePr.ComentarioNovo, Autor: "ana-dev" });
        Assert.DoesNotContain(novidades, n => n.Autor == "voce");
        Assert.Contains(novidades, n => n is { Tipo: TipoNovidadePr.RevisaoNova, Autor: "carla", Texto: "APPROVED" });
        Assert.Empty(GitHub.Novidades(depois, depois, "voce"));
        Assert.Empty(GitHub.Novidades(null, depois));
        var mergeado = depois with { Estado = "MERGED" };
        Assert.Contains(GitHub.Novidades(depois, mergeado), n => n.Tipo == TipoNovidadePr.Mergeado);
    }

    [Fact]
    public void ArgumentosDoMergeEDoNovoPr()
    {
        Assert.Equal(["pr", "merge", "52", "--squash", "--auto", "--match-head-commit", "abc"], GitHub.ArgumentosDoMerge(52, ModoMerge.Squash, true, "abc"));
        Assert.Equal(["pr", "merge", "7", "--rebase"], GitHub.ArgumentosDoMerge(7, ModoMerge.Rebase, false, null));
        Assert.Equal(["pr", "create", "--head", "b", "--base", "main", "--title", "T", "--body", "C", "--draft"], GitHub.ArgumentosDoNovoPr("b", "main", "T", "C", true));
        Assert.Equal("https://github.com/voce/loja/pull/53", GitHub.UrlDoPrCriado("Creating draft pull request\nhttps://github.com/voce/loja/pull/53\n"));
        Assert.True(GitHub.SemPrNaResposta(new ResultadoGit(1, "", "no pull requests found for branch \"x\"")));
    }

    [Fact]
    public void LePrsLigadosDaIssueComChecks()
    {
        using var doc = JsonDocument.Parse("""
            {"number":15009,"url":"https://github.com/cli/cli/pull/15009","state":"MERGED","isDraft":false,"headRefName":"copilot/fix","reviewDecision":"APPROVED",
             "repository":{"nameWithOwner":"cli/cli"},"commits":{"nodes":[{"commit":{"statusCheckRollup":{"state":"SUCCESS"}}}]}}
            """);
        var pr = GitHub.LerPrLigado(doc.RootElement)!;
        Assert.Equal((15009, "copilot/fix", "cli/cli", "SUCCESS"), (pr.Numero, pr.Branch, pr.Repositorio, pr.Checks));
        Assert.True(pr.Mergeado && pr.ChecksOk);
        using var semCommit = JsonDocument.Parse("""{"number":3,"url":"u","state":"OPEN","isDraft":true,"commits":{"nodes":[{"commit":{"statusCheckRollup":null}}]}}""");
        var rascunho = GitHub.LerPrLigado(semCommit.RootElement)!;
        Assert.True(rascunho.Rascunho);
        Assert.Null(rascunho.Checks);
    }

    [Fact]
    public void ItensDoQuadroTrazemOsPrsLigados()
    {
        var (itens, _, _) = GitHub.LerItens("""
            {"data":{"node":{"items":{"pageInfo":{"hasNextPage":false},"nodes":[
              {"id":"I1","content":{"__typename":"Issue","number":42,"title":"Carrinho","url":"u42","state":"OPEN","repository":{"nameWithOwner":"voce/loja"},
                "closedByPullRequestsReferences":{"nodes":[{"number":52,"url":"u52","state":"OPEN","isDraft":false,"headRefName":"carrinho","reviewDecision":"REVIEW_REQUIRED",
                  "repository":{"nameWithOwner":"voce/loja"},"commits":{"nodes":[{"commit":{"statusCheckRollup":{"state":"FAILURE"}}}]}}]}}},
              {"id":"I2","content":{"__typename":"PullRequest","number":60,"title":"PR solto","url":"u60","state":"OPEN","isDraft":true,"headRefName":"solto","repository":{"nameWithOwner":"voce/loja"}}}]}}}}
            """);
        Assert.Equal([52], itens[0].Prs);
        Assert.True(itens[0].PrsLigados[0].ChecksFalharam);
        Assert.Equal("carrinho", itens[0].PrsLigados[0].Branch);
        Assert.Equal(60, itens[1].PrsLigados.Single().Numero);
        Assert.True(itens[1].PrsLigados[0].Rascunho);
    }

    [Fact]
    public void LeCommitsDaBranch()
    {
        var commits = GeradorDePr.LerCommits("a1\u001ffeat(cupom): recusar cupom vencido\u001fCorpo longo\nlinha 2\u001e\nb2\u001ffix: limite por cliente\u001f\u001e\n");
        Assert.Equal(2, commits.Count);
        Assert.Equal(("a1", "feat(cupom): recusar cupom vencido", "Corpo longo\nlinha 2"), (commits[0].Sha, commits[0].Assunto, commits[0].Corpo));
        Assert.Equal("", commits[1].Corpo);
        Assert.Empty(GeradorDePr.LerCommits(""));
    }

    [Fact]
    public void PropostaComUmCommitUsaOAssuntoEOCorpo()
    {
        var p = GeradorDePr.Propor("cupom-checkout", [new("a", "Recusar cupom vencido", "O serviço devolve falha.")], [new("src/A.cs", 10, 2, false, false)], null);
        Assert.Equal("Recusar cupom vencido", p.Titulo);
        Assert.StartsWith("O serviço devolve falha.", p.Corpo);
        Assert.Contains("1 arquivo alterado, +10 −2", p.Corpo);
    }

    [Fact]
    public void PropostaAgrupaOChangelogEFechaAIssue()
    {
        var issue = new IssueDaSessao(48, "voce/loja", "https://github.com/voce/loja/issues/48", "Cupom vencido ainda aplica desconto");
        var p = GeradorDePr.Propor("48-cupom-vencido-20261110",
            [new("a", "feat(cupom): recusar cupom vencido", ""), new("b", "fix: limite por cliente", ""), new("c", "Ajustar testes", "")],
            [new("src/A.cs", 10, 2, false, false), new("src/B.cs", 5, 0, false, true)], issue);
        Assert.Equal("Cupom vencido ainda aplica desconto", p.Titulo);
        Assert.Contains("## Changelog", p.Corpo);
        Assert.Contains("- Recusar cupom vencido", p.Corpo);
        Assert.Contains("- Limite por cliente", p.Corpo);
        Assert.Contains("- Ajustar testes", p.Corpo);
        Assert.Contains("2 arquivos alterados, +15 −2", p.Corpo);
        Assert.EndsWith("Closes voce/loja#48", p.Corpo);
    }

    [Fact]
    public void PropostaSemIssueNemPadraoUsaABranch()
    {
        var p = GeradorDePr.Propor("feature/notas-offline-20261110", [new("a", "Sincronizar", ""), new("b", "Fila offline", "")], [], null);
        Assert.Equal("Notas offline", p.Titulo);
        Assert.Contains("- Sincronizar\n- Fila offline", p.Corpo);
        Assert.Equal("Cupom checkout", GeradorDePr.BranchLegivel("cupom-checkout"));
        Assert.Equal(("feat", "x"), GeradorDePr.Classificar("feat!: x"));
    }
}
