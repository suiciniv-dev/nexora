using System.Text.Json;
using Nexora.Core.Claude;
using Nexora.Core.Git;
using Nexora.Core.Texto;

namespace Nexora.Testes;

public class TextoTestes
{
    [Fact]
    public void MarkdownSeparaBlocos()
    {
        var blocos = Markdown.Blocos("""
            ## Plano

            Primeira linha
            segunda linha

            - item **um**
              continuação
            - item dois
              1. sub

            ```csharp
            var x = 1;
            ```

            | A | B |
            |---|---|
            | 1 | 2 |

            > citação
            ---
            """);

        Assert.Collection(blocos,
            b => Assert.Equal(new Titulo(2, "Plano"), b),
            b => Assert.Equal(new Paragrafo("Primeira linha\nsegunda linha"), b),
            b =>
            {
                var lista = Assert.IsType<Lista>(b);
                Assert.Equal(3, lista.Itens.Count);
                Assert.Equal("item **um** continuação", lista.Itens[0].Texto);
                Assert.Equal(1, lista.Itens[2].Nivel);
                Assert.Equal("1.", lista.Itens[2].Marcador);
            },
            b => Assert.Equal(new Codigo("csharp", "var x = 1;"), b),
            b =>
            {
                var tabela = Assert.IsType<Tabela>(b);
                Assert.Equal(2, tabela.Linhas.Count);
                Assert.Equal(["1", "2"], tabela.Linhas[1]);
            },
            b => Assert.Equal(new Citacao("citação"), b),
            b => Assert.IsType<Regua>(b));
    }

    [Fact]
    public void MarkdownSeparaTrechos()
    {
        var trechos = Markdown.Trechos("Use `dotnet test` e **depois** veja [o plano](docs/plano.md) com *calma*.");
        Assert.Equal(
            [
                new Trecho("Use "),
                new Trecho("dotnet test", Codigo: true),
                new Trecho(" e "),
                new Trecho("depois", Negrito: true),
                new Trecho(" veja "),
                new Trecho("o plano", Link: "docs/plano.md"),
                new Trecho(" com "),
                new Trecho("calma", Italico: true),
                new Trecho("."),
            ],
            trechos);
    }

    [Fact]
    public void MarkdownAceitaItalicoECodigoDentroDoNegrito()
    {
        Assert.Equal(
            [
                new Trecho("Teste de ", Negrito: true),
                new Trecho("timeout", Negrito: true, Italico: true),
                new Trecho(" no ", Negrito: true),
                new Trecho("Assistente", Negrito: true, Codigo: true),
                new Trecho(": falta."),
            ],
            Markdown.Trechos("**Teste de *timeout* no `Assistente`**: falta."));
    }

    [Fact]
    public void MarkdownNaoConfundeAsteriscoSolto()
    {
        Assert.Equal([new Trecho("2 * 3 = 6")], Markdown.Trechos("2 * 3 = 6"));
    }

    [Fact]
    public void ResumoDeFerramentas()
    {
        JsonElement E(string json) => JsonDocument.Parse(json).RootElement.Clone();
        Assert.Equal("dotnet test", Resumos.DaFerramenta("Bash", E("""{"command":"dotnet test\ncd x","description":"Testes"}""")));
        Assert.Equal(Path.Combine("src", "A.cs"), Resumos.DaFerramenta("Read", E("""{"file_path":"D:\\Proj\\src\\A.cs"}"""), @"D:\Proj"));
        Assert.Equal(@"C:\fora.cs", Resumos.DaFerramenta("Edit", E("""{"file_path":"C:\\fora.cs"}"""), @"D:\Proj"));
        Assert.Equal("TODO  em src", Resumos.DaFerramenta("Grep", E("""{"pattern":"TODO","path":"D:\\Proj\\src"}"""), @"D:\Proj"));
        Assert.Equal("qualquer", Resumos.DaFerramenta("mcp__x__y", E("""{"n":1,"q":"qualquer"}""")));
        Assert.Equal("", Resumos.DaFerramenta("TaskList", E("{}")));
        Assert.Equal("", Resumos.DaFerramenta("mcp__x__y", E("""{"n":1}""")));
        Assert.Equal(new string('a', 139) + "…", Resumos.DaFerramenta("Bash", E($$"""{"command":"{{new string('a', 300)}}"}""")));
    }

    [Fact]
    public void FerramentasDoNexoraTemNomeEResumoLegiveis()
    {
        JsonElement E(string json) => JsonDocument.Parse(json).RootElement.Clone();
        Assert.Equal("Nexora · criar agente", Resumos.NomeDaFerramenta("mcp__nexora__criar_agente"));
        Assert.Equal("claude-vscode · getDiagnostics", Resumos.NomeDaFerramenta("mcp__claude-vscode__getDiagnostics"));
        Assert.Equal("Bash", Resumos.NomeDaFerramenta("Bash"));

        Assert.Equal("Olá", Resumos.DaFerramenta("mcp__nexora__criar_agente", E("""{"ambiente":"D:\\x","titulo":"Olá","mensagem":"faça"}""")));
        Assert.Equal("login", Resumos.DaFerramenta("mcp__nexora__criar_agente", E("""{"ambiente":"D:\\x","tarefa":"login","mensagem":"faça"}""")));
        Assert.Equal("s2: permitir", Resumos.DaFerramenta("mcp__nexora__responder_permissao", E("""{"id":"s2","decisao":"permitir"}""")));
        Assert.Equal("s2, s3", Resumos.DaFerramenta("mcp__nexora__esperar", E("""{"ids":["s2","s3"]}""")));
        Assert.Equal("os agentes", Resumos.DaFerramenta("mcp__nexora__esperar", E("{}")));
    }

    [Fact]
    public void DetalheDaPermissaoDeEdicaoMostraAntesEDepois()
    {
        var entrada = JsonDocument.Parse("""{"file_path":"D:\\P\\a.cs","old_string":"int x;","new_string":"long x;"}""").RootElement;
        Assert.Equal("a.cs\n\n- int x;\n+ long x;", Resumos.DetalheDaPermissao("Edit", entrada, @"D:\P"));
    }

    [Fact]
    public void TempoRelativo()
    {
        var agora = new DateTime(2026, 10, 29, 15, 0, 0);
        Assert.Equal("agora", Tempo.Relativo(agora.AddSeconds(-20), agora));
        Assert.Equal("há 5 min", Tempo.Relativo(agora.AddMinutes(-5), agora));
        Assert.Equal("há 3 h", Tempo.Relativo(agora.AddHours(-3), agora));
        Assert.Equal("ontem", Tempo.Relativo(agora.AddDays(-1), agora));
        Assert.Equal("há 3 dias", Tempo.Relativo(agora.AddDays(-3), agora));
        Assert.Equal("01/09", Tempo.Relativo(new DateTime(2026, 9, 1), agora));
        Assert.Equal("13 s", Tempo.Duracao(13_400));
        Assert.Equal("2 min 05 s", Tempo.Duracao(125_000));
    }

    [Fact]
    public void BranchDoRepositorioEDoWorktree()
    {
        var raiz = Directory.CreateTempSubdirectory("nexora-git-").FullName;
        try
        {
            var principal = Directory.CreateDirectory(Path.Combine(raiz, "principal", ".git")).FullName;
            File.WriteAllText(Path.Combine(principal, "HEAD"), "ref: refs/heads/main\n");
            var sub = Directory.CreateDirectory(Path.Combine(raiz, "principal", "src", "App")).FullName;
            Assert.Equal("main", Repositorio.BranchAtual(sub));

            var dadosWt = Directory.CreateDirectory(Path.Combine(principal, "worktrees", "melhorias")).FullName;
            File.WriteAllText(Path.Combine(dadosWt, "HEAD"), "ref: refs/heads/melhorias-20261029\n");
            var wt = Directory.CreateDirectory(Path.Combine(raiz, "melhorias")).FullName;
            File.WriteAllText(Path.Combine(wt, ".git"), $"gitdir: {dadosWt}\n");
            Assert.Equal("melhorias-20261029", Repositorio.BranchAtual(wt));

            Assert.Null(Repositorio.BranchAtual(Path.GetTempPath()[..3]));
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }
}
