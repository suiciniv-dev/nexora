using Nexora.Core.Git;

namespace Nexora.Testes;

public sealed class GitHubTestes
{
    [Fact]
    public void LeOEstadoDoPrComChecksERevisao()
    {
        var pr = GitHub.LerPr("""
            {"number":52,"url":"https://github.com/x/y/pull/52","state":"OPEN","isDraft":true,"reviewDecision":"APPROVED",
             "statusCheckRollup":[
               {"__typename":"CheckRun","status":"COMPLETED","conclusion":"SUCCESS"},
               {"__typename":"CheckRun","status":"COMPLETED","conclusion":"FAILURE"},
               {"__typename":"CheckRun","status":"IN_PROGRESS","conclusion":""},
               {"__typename":"StatusContext","state":"PENDING"},
               {"__typename":"StatusContext","state":"SUCCESS"},
               {"__typename":"CheckRun","status":"COMPLETED","conclusion":"SKIPPED"}]}
            """)!;
        Assert.Equal(52, pr.Numero);
        Assert.True(pr.Aberto);
        Assert.True(pr.Rascunho);
        Assert.Equal("APPROVED", pr.Revisao);
        Assert.Equal(3, pr.ChecksOk);
        Assert.Equal(1, pr.ChecksFalhos);
        Assert.Equal(2, pr.ChecksPendentes);
    }

    [Fact]
    public void PrSemRevisaoNemChecks()
    {
        var pr = GitHub.LerPr("""{"number":7,"url":"u","state":"MERGED","isDraft":false,"reviewDecision":"","statusCheckRollup":[]}""")!;
        Assert.True(pr.Mergeado);
        Assert.Null(pr.Revisao);
        Assert.Equal(0, pr.ChecksOk + pr.ChecksFalhos + pr.ChecksPendentes);
        Assert.Null(GitHub.LerPr("não é json"));
    }

    [Fact]
    public void LeAsIssuesAbertas()
    {
        var issues = GitHub.LerIssues("""
            [{"number":48,"title":"Cupom vencido","url":"https://github.com/x/y/issues/48","labels":[{"name":"bug"},{"name":"checkout"}],"updatedAt":"2026-11-03T10:00:00Z"},
             {"number":49,"title":"Sem etiqueta","url":"u","labels":[],"updatedAt":""}]
            """);
        Assert.Equal(2, issues.Count);
        Assert.Equal(["bug", "checkout"], issues[0].Etiquetas);
        Assert.NotNull(issues[0].Atualizada);
        Assert.Null(issues[1].Atualizada);
    }

    [Fact]
    public void JuntaAsMinhasEAsSemResponsavelDaConsultaDoRepositorio()
    {
        var issues = GitHub.LerIssuesDoRepositorio("""
            {"data":{
              "minhas":{"nodes":[{"number":10,"title":"Minha","url":"u10","updatedAt":"2026-11-01T10:00:00Z","labels":{"nodes":[{"name":"bug","color":"d73a4a"}]},"assignees":{"nodes":[{"login":"eu"}]}}]},
              "livres":{"nodes":[{"number":11,"title":"Livre","url":"u11","updatedAt":"2026-11-05T10:00:00Z","labels":{"nodes":[]},"assignees":{"nodes":[]}},
                                 {"number":10,"title":"Minha","url":"u10","updatedAt":"2026-11-01T10:00:00Z","labels":{"nodes":[]},"assignees":{"nodes":[]}},{}]},
              "repository":{"projectsV2":{"nodes":[]}}}}
            """);
        Assert.Equal([11, 10], issues.Select(i => i.Numero));
        Assert.Equal(["eu"], issues[1].Responsaveis);
        Assert.Equal("d73a4a", issues[1].EtiquetasComCor[0].Cor);

        var todas = GitHub.LerIssuesDoRepositorio("""{"data":{"repository":{"abertas":{"nodes":[{"number":3,"title":"A","url":"u","updatedAt":"","labels":{"nodes":[]},"assignees":{"nodes":[]}}]}}}}""");
        Assert.Equal(3, Assert.Single(todas).Numero);
        Assert.Empty(GitHub.LerIssuesDoRepositorio("não é json"));
    }

    [Fact]
    public void LeAsContasDoArquivoDoGh()
    {
        var hosts = """
            github.com:
                git_protocol: https
                users:
                    acme-dev:
                    suiciniv-dev:
                        oauth_token: x
                        extra:
                user: acme-dev
            ghe.empresa.com:
                users:
                    outra:
            """;
        Assert.Equal(["acme-dev", "suiciniv-dev"], GitHub.LerContasDoHosts(hosts));
        Assert.Empty(GitHub.LerContasDoHosts(""));
    }

    [Fact]
    public void AchaAsContasEAContaDeCadaRepositorio()
    {
        var status = """
            github.com
              ✓ Logged in to github.com account acme-dev (keyring)
              - Active account: true
              ✓ Logged in to github.com account suiciniv-dev (keyring)
              - Active account: false
            """;
        Assert.Equal(["acme-dev", "suiciniv-dev"], GitHub.LerContas(status));
        Assert.Equal([("acme-dev", true), ("suiciniv-dev", false)], GitHub.LerContasComAtiva(status));
        Assert.Equal("suiciniv-dev", GitHub.ContaDoAjudante("credential.https://github.com.helper !f() { echo password=$(gh auth token --hostname github.com --user suiciniv-dev); }; f"));
        Assert.Null(GitHub.ContaDoAjudante("credential.helper manager"));
        Assert.Equal("suiciniv-dev", GitHub.DonoDoRemoto("https://github.com/suiciniv-dev/nexora.git"));
        Assert.Equal("Acme", GitHub.DonoDoRemoto("git@github.com:Acme/shop.git"));
    }

    [Fact]
    public void LeAIssueCompleta()
    {
        var issue = GitHub.LerIssue("""
            {"number":48,"title":"Cupom vencido","body":"Quando o cupom vence...","url":"https://github.com/x/y/issues/48",
             "author":{"login":"vini"},"labels":[{"name":"bug"}],"createdAt":"2026-11-01T12:00:00Z","comments":[{},{}]}
            """)!;
        Assert.Equal("vini", issue.Autor);
        Assert.Equal(["bug"], issue.Etiquetas);
        Assert.Equal(2, issue.Comentarios);
        Assert.NotNull(issue.Criada);
    }

    [Fact]
    public async Task CadaRepositorioUsaAContaQueTemAcesso()
    {
        if (Environment.GetEnvironmentVariable("NEXORA_MEDIR") != "1") return;
        foreach (var pasta in new[] { @"D:\Nexora", @"D:\Repositorios\Acme.Shop" })
        {
            var r = await GitHub.GhAsync(pasta, ["repo", "view", "--json", "nameWithOwner,visibility"]);
            Console.WriteLine($"{pasta}: ok={r.Ok} {r.Saida.Trim()} {(r.Ok ? "" : r.Erro.Trim())}");
            Assert.True(r.Ok);
        }
    }

    [Fact]
    public void LeOStatusDaIssueNosQuadros()
    {
        var quadros = GitHub.LerStatus("""
            {"data":{"repository":{"issue":{"projectItems":{"nodes":[
              {"id":"I1","isArchived":false,"project":{"id":"P1","title":"Sprint","closed":false,"field":{"id":"F1","options":[{"id":"a","name":"Todo"},{"id":"b","name":"In Progress"},{"id":"c","name":"Review"}]}},"fieldValueByName":{"optionId":"b"}},
              {"id":"I2","isArchived":false,"project":{"id":"P2","title":"Roadmap","closed":false,"field":{"id":"F2","options":[{"id":"x","name":"Ideia"}]}},"fieldValueByName":null},
              {"id":"I3","isArchived":true,"project":{"id":"P3","title":"Velho","closed":false,"field":{"id":"F3","options":[]}},"fieldValueByName":null},
              {"id":"I4","isArchived":false,"project":{"id":"P4","title":"Sem status","closed":false,"field":null},"fieldValueByName":null},
              {"id":"I5","isArchived":false,"project":{"id":"P5","title":"Fechado","closed":true,"field":{"id":"F5","options":[]}},"fieldValueByName":null}]}}}}}
            """);
        Assert.Equal(2, quadros.Count);
        Assert.Equal("Sprint", quadros[0].Quadro);
        Assert.Equal("b", quadros[0].AtualId);
        Assert.Equal(["Todo", "In Progress", "Review"], quadros[0].Opcoes.Select(o => o.Nome));
        Assert.Null(quadros[1].AtualId);
        Assert.Empty(GitHub.LerStatus("""{"data":{"repository":{"issue":{"projectItems":{"nodes":[]}}}}}"""));
        Assert.Empty(GitHub.LerStatus("não é json"));
    }

    [Fact]
    public void SeparaAUrlDaIssueEReconheceAFaltaDePermissao()
    {
        Assert.Equal(("acme-corp", "Acme.Shop", 3345), GitHub.PartesDaUrl("https://github.com/acme-corp/Acme.Shop/issues/3345"));
        Assert.Null(GitHub.PartesDaUrl("https://github.com/x/y"));
        Assert.True(GitHub.FaltaPermissaoDeQuadro("""{"errors":[{"type":"INSUFFICIENT_SCOPES","message":"... ['read:project'] ..."}]}"""));
        Assert.False(GitHub.FaltaPermissaoDeQuadro("Could not resolve to a Repository"));
    }

    [Fact]
    public void LeOsQuadrosAsVisoesEOsItens()
    {
        var quadros = GitHub.LerQuadros("""
            {"data":{"repository":{"projectsV2":{"nodes":[
              {"id":"P1","title":"Acme.Shop Home","number":3,"url":"https://github.com/orgs/x/projects/3","closed":false},
              {"id":"P2","title":"Antigo","number":1,"url":"u","closed":true}]}}}}
            """, "x/y", @"D:\y");
        Assert.Single(quadros);
        Assert.Equal("x/y", quadros[0].Repositorio);

        var quadro = GitHub.LerDetalheDoQuadro("""
            {"data":{"node":{"id":"P1","title":"Acme.Shop Home","number":3,"url":"u",
              "fields":{"nodes":[{},{"id":"F1","name":"Status","options":[{"id":"a","name":"Ready","color":"RED"},{"id":"b","name":"In progress","color":"ORANGE"}]},
                {"id":"F2","name":"Priority","options":[{"id":"h","name":"High","color":"RED"}]},{"id":"F3","name":"Iteration"}]},
              "views":{"nodes":[{"name":"Home","number":1,"filter":"iteration:@current","layout":"TABLE_LAYOUT","groupByFields":{"nodes":[{"name":"Status"}]}},
                {"name":"Backlog","number":2,"filter":null,"layout":"TABLE_LAYOUT","groupByFields":{"nodes":[]}}]}}}}
            """, quadros[0]);
        Assert.True(quadro.Detalhado);
        Assert.Equal("F1", quadro.Status?.Id);
        Assert.NotNull(quadro.Campo("Prioridade", "Priority"));
        Assert.Equal("Iteration", quadro.CampoIteracao);
        Assert.Equal(["Home", "Backlog"], quadro.Visoes.Select(v => v.Nome));
        Assert.Equal("Status", quadro.Visoes[0].AgruparPor);
        Assert.Equal("", quadro.Visoes[1].Filtro);

        var (itens, temMais, cursor) = GitHub.LerItens("""
            {"data":{"node":{"items":{"pageInfo":{"hasNextPage":true,"endCursor":"abc"},"nodes":[
              {"id":"I1","content":{"__typename":"Issue","number":3345,"title":"Integração por API","url":"https://github.com/x/y/issues/3345","state":"OPEN","updatedAt":"2026-11-04T10:00:00Z",
                "repository":{"nameWithOwner":"x/y"},"assignees":{"nodes":[{"login":"acme-dev"}]},"labels":{"nodes":[{"name":"bug","color":"d73a4a"}]},
                "closedByPullRequestsReferences":{"nodes":[{"number":74},{"number":3352}]}},
               "fieldValues":{"nodes":[{"__typename":"ProjectV2ItemFieldTextValue"},{"__typename":"ProjectV2ItemFieldSingleSelectValue","name":"In review","color":"PURPLE","optionId":"c","field":{"name":"Status"}},
                 {"__typename":"ProjectV2ItemFieldIterationValue","title":"Iteration 48","field":{"name":"Iteration"}}]}},
              {"id":"I2","content":{"__typename":"DraftIssue","title":"Ideia solta","updatedAt":"2026-11-01T10:00:00Z"},"fieldValues":{"nodes":[]}},
              {"id":"I3","content":null,"fieldValues":{"nodes":[]}}]}}}}
            """);
        Assert.True(temMais);
        Assert.Equal("abc", cursor);
        Assert.Equal(2, itens.Count);
        Assert.Equal("In review", itens[0].Valor("status")?.Nome);
        Assert.Equal("PURPLE", itens[0].Valor("Status")?.Cor);
        Assert.Equal([74, 3352], itens[0].Prs);
        Assert.Equal("Iteration 48", itens[0].Iteracao);
        Assert.Equal(["acme-dev"], itens[0].Responsaveis);
        Assert.Equal("d73a4a", itens[0].Etiquetas[0].Cor);
        Assert.Equal(0, itens[1].Numero);
        Assert.Equal("DraftIssue", itens[1].Tipo);
    }

    [Fact]
    public void LeAsContasComEscoposEORepositorioDoRemoto()
    {
        var contas = GitHub.LerContasGh("""
            github.com
              ✓ Logged in to github.com account acme-dev (keyring)
              - Active account: true
              - Token scopes: 'gist', 'project', 'read:org', 'repo', 'workflow'
              ✓ Logged in to github.com account suiciniv-dev (keyring)
              - Active account: false
              - Token scopes: 'gist', 'read:org', 'repo', 'workflow'
            """);
        Assert.Equal(2, contas.Count);
        Assert.True(contas[0].Ativa && contas[0].VeQuadros);
        Assert.False(contas[1].Ativa || contas[1].VeQuadros);
        Assert.Equal("suiciniv-dev/nexora", GitHub.RepositorioDoRemoto("https://github.com/suiciniv-dev/nexora.git"));
        Assert.Equal("acme-corp/Acme.Shop", GitHub.RepositorioDoRemoto("git@github.com:acme-corp/Acme.Shop.git\n"));
        Assert.Null(GitHub.RepositorioDoRemoto("https://dev.azure.com/x/y"));
    }

    [Fact]
    public void BranchPrincipalNaoTemPr()
    {
        Assert.False(GitHub.TemPr("main"));
        Assert.False(GitHub.TemPr("Master"));
        Assert.False(GitHub.TemPr(null));
        Assert.True(GitHub.TemPr("cupom-checkout"));
    }
}
