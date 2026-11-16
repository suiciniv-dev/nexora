using Nexora.Core.Git;
using Nexora.Core.Revisao;
using Nexora.Core.Senna;
using Nexora.Core.Texto;

namespace Nexora.Testes;

[Collection("Idioma")]
public sealed class ComentariosDeRevisaoTestes
{
    const string Unificado = """
        diff --git a/src/A.cs b/src/A.cs
        @@ -10,4 +10,5 @@ class A
         var a = 1;
        -var b = 2;
        +var b = 3;
        +var c = 4;
         return a;
        diff --git a/src/B.cs b/src/B.cs
        @@ -1,1 +1,1 @@
        -x
        +y
        """;

    [Fact]
    public void ComentarioDeIntervaloLevaTrechoELinhasNovas()
    {
        var linhas = Diff.LerUnificado(Unificado);
        var c = ComentariosDeRevisao.Comentario("src/A.cs", linhas, 2, 4, "  troque o nome  ")!;
        Assert.Equal((11, 12, false), (c.Inicio, c.Fim, c.Antiga));
        Assert.Equal("-var b = 2;\n+var b = 3;\n+var c = 4;", c.Trecho);
        Assert.Equal("troque o nome", c.Texto);

        var removida = ComentariosDeRevisao.Comentario("src/A.cs", linhas, 2, 2, "por quê?")!;
        Assert.True(removida.Antiga);
        Assert.Equal(11, removida.Inicio);
        Assert.Null(ComentariosDeRevisao.Comentario("src/A.cs", linhas, 0, 0, "cabeçalho"));
    }

    [Fact]
    public void PedidoDoDiffTrazArquivoFaixaTrechoETexto()
    {
        var pedido = ComentariosDeRevisao.PedidoDoDiff(
        [
            new ComentarioDeLinha("src/B.cs", 4, 4, false, "+y", "Use um nome melhor."),
            new ComentarioDeLinha("src/A.cs", 11, 12, false, "+var b = 3;\n+var c = 4;", "Junte as duas linhas."),
        ]);
        Assert.StartsWith("Revisei o diff e deixei 2 comentários.", pedido);
        Assert.True(pedido.IndexOf("`src/A.cs` (linhas 11-12)", StringComparison.Ordinal) < pedido.IndexOf("`src/B.cs` (linha 4)", StringComparison.Ordinal));
        Assert.Contains("```diff\n   +var b = 3;\n   +var c = 4;\n   ```", pedido);
        Assert.Contains("   Junte as duas linhas.", pedido);
    }

    static DetalhePr Pr() => new(52, "Cupom", "https://github.com/voce/loja/pull/52", "OPEN")
    {
        HeadBranch = "cupom-checkout",
        Eu = "voce",
        Conversas =
        [
            new ConversaDoPr("T1", "src/A.cs", 12, false, false,
            [
                new ComentarioDoPr("C1", "ana", "Devolva a falha.", "", null),
                new ComentarioDoPr("C2", "bruno", "Concordo.", "", null),
            ]) { Inicio = 11, Trecho = "@@ -10,4 +10,5 @@\n var a = 1;\n+var b = 3;" },
        ],
    };

    [Fact]
    public void PedidoDoPrNaConversaNovaComecaPelaPassagem()
    {
        var pr = Pr();
        var passagem = new Passagem("Handoff: cupom-checkout", "Loja", DateTimeOffset.Now);
        var gerais = new[] { new ComentarioDoPr("IC1", "bruno", "Testei no celular.", "", null) };
        var revisoes = new[] { new RevisaoDoPr("R1", "ana", "CHANGES_REQUESTED", "Falta o teste.", null) };

        var nova = ComentariosDeRevisao.PedidoDoPr(pr, pr.Conversas, gerais, revisoes, DestinoDaRevisao.Nova, passagem);
        Assert.StartsWith("Retome o handoff \"Handoff: cupom-checkout\".", nova);
        Assert.Contains("1. `src/A.cs` (linhas 11-12), de ana:", nova);
        Assert.Contains("   +var b = 3;", nova);
        Assert.DoesNotContain("@@", nova);
        Assert.Contains("Resposta de bruno: Concordo.", nova);
        Assert.Contains("2. Revisão de ana:", nova);
        Assert.Contains("3. Comentário geral de bruno:", nova);
        Assert.Contains("Não responda nem comente no PR", nova);

        var semPassagem = ComentariosDeRevisao.PedidoDoPr(pr, pr.Conversas, [], [], DestinoDaRevisao.Nova, null);
        Assert.Contains("gh pr view 52", semPassagem);
        Assert.Contains("branch cupom-checkout", semPassagem);

        var original = ComentariosDeRevisao.PedidoDoPr(pr, pr.Conversas, [], [], DestinoDaRevisao.Original, passagem);
        Assert.StartsWith("Resolva estes comentários de revisão do PR #52.", original);
    }

    [Fact]
    public void PadraoEscolheAConversaNovaQuandoAOriginalEstaVelha()
    {
        var agora = new DateTime(2026, 11, 12, 15, 0, 0);
        Assert.Equal(DestinoDaRevisao.Original, ComentariosDeRevisao.Padrao(new(true, agora.AddMinutes(-20), 0.3, null), false, agora).Destino);
        Assert.Equal(DestinoDaRevisao.Original, ComentariosDeRevisao.Padrao(new(true, agora.AddMinutes(-20), 0.3, agora.AddDays(-3)), true, agora).Destino);
        Assert.Equal(DestinoDaRevisao.Nova, ComentariosDeRevisao.Padrao(new(true, agora.AddMinutes(-20), 0.3, agora.AddMinutes(-10)), true, agora).Destino);
        var parada = ComentariosDeRevisao.Padrao(new(true, agora.AddDays(-2), 0.3, null), false, agora);
        Assert.Equal(DestinoDaRevisao.Nova, parada.Destino);
        Assert.Contains("parada há", parada.Motivo);
        Assert.Equal(DestinoDaRevisao.Nova, ComentariosDeRevisao.Padrao(new(true, agora.AddMinutes(-5), 0.92, null), false, agora).Destino);
        Assert.Equal(DestinoDaRevisao.Nova, ComentariosDeRevisao.Padrao(new(false, null, 0, null), true, agora).Destino);
    }

    [Fact]
    public void ExtraiUmArquivoDoDiffDoPr()
    {
        var b = ComentariosDeRevisao.ExtrairArquivo(Unificado, "src/B.cs")!;
        Assert.StartsWith("diff --git a/src/B.cs", b);
        Assert.DoesNotContain("var a", b);
        Assert.Contains("var c = 4;", ComentariosDeRevisao.ExtrairArquivo(Unificado, @"src\A.cs"));
        Assert.Null(ComentariosDeRevisao.ExtrairArquivo(Unificado, "src/C.cs"));
    }

    [Theory]
    [InlineData("claude[bot]", "claude")]
    [InlineData("chatgpt-codex-connector[bot]", "codex")]
    [InlineData("copilot-pull-request-reviewer[bot]", "copilot")]
    [InlineData("ana-dev", null)]
    public void ReconheceAgentesPeloLogin(string login, string? agente) => Assert.Equal(agente, ComentariosDeRevisao.AgenteDoLogin(login));

    [Fact]
    public void IniciaisDoLogin()
    {
        Assert.Equal("AD", ComentariosDeRevisao.Iniciais("ana-dev"));
        Assert.Equal("B", ComentariosDeRevisao.Iniciais("bruno"));
        Assert.Equal("?", ComentariosDeRevisao.Iniciais(""));
    }

    [Fact]
    public void AchaAPassagemDaBranch()
    {
        var agora = DateTimeOffset.Now;
        IReadOnlyList<Passagem> todas =
        [
            new("Handoff: cupom-checkout", "Loja", agora.AddDays(-3)),
            new("Handoff: cupom-checkout", "Loja", agora.AddDays(-1)),
            new("Handoff: cupom-checkout", "Outro", agora),
            new("Handoff: main", "Loja", agora),
        ];
        var achada = PassagensDoSenna.DaBranch(todas, @"D:\Projetos\Loja", "cupom-checkout")!;
        Assert.Equal("Loja", achada.Projeto);
        Assert.Equal(agora.AddDays(-1), achada.Quando);
        Assert.Null(PassagensDoSenna.DaBranch(todas, @"D:\Projetos\Loja", "nada"));
        Assert.Null(PassagensDoSenna.DaBranch(todas, @"D:\Projetos\Loja", null));
    }

    [Fact]
    public void LeIntervaloETrechoDasConversas()
    {
        const string json = """
            {"data":{"viewer":{"login":"voce"},"repository":{"pullRequest":{"reviewThreads":{"nodes":[
              {"id":"T1","isResolved":true,"isOutdated":false,"path":"src/A.cs","line":12,"originalLine":12,"startLine":10,"originalStartLine":10,
               "comments":{"nodes":[{"id":"C1","author":{"login":"claude[bot]"},"body":"ok","url":"https://x","createdAt":"2026-11-10T10:00:00Z","diffHunk":"@@ -1 +1 @@\n+a"}]}}
            ]}}}}}
            """;
        var (conversas, _) = GitHub.LerConversasDoPr(json);
        var c = Assert.Single(conversas);
        Assert.Equal(10, c.Inicio);
        Assert.Equal("@@ -1 +1 @@\n+a", c.Trecho);
        Assert.True(c.Resolvida);
    }
}
