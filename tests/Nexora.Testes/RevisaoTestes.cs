using Nexora.Core.Git;
using Nexora.Core.Revisao;
using Nexora.ViewModels;

namespace Nexora.Testes;

public sealed class RevisaoTestes
{
    static PassoDaSessao Bash(string comando, bool erro = false) => new("Bash", comando, true, erro, false);
    static PassoDaSessao Edicao() => new("Edit", null, true, false, true);

    [Theory]
    [InlineData("dotnet test tests/Nexora.Testes", TipoVerificacao.Teste)]
    [InlineData("cd app && npm run test -- --watch=false", TipoVerificacao.Teste)]
    [InlineData("pnpm test", TipoVerificacao.Teste)]
    [InlineData("python -m pytest -q", TipoVerificacao.Teste)]
    [InlineData("./mvnw -q verify", TipoVerificacao.Teste)]
    [InlineData("dotnet build src/Nexora", TipoVerificacao.Build)]
    [InlineData("npm run build", TipoVerificacao.Build)]
    [InlineData("cargo build --release", TipoVerificacao.Build)]
    public void ReconheceComandosDeTesteEBuild(string comando, TipoVerificacao tipo) =>
        Assert.Equal(tipo, Verificacoes.Classificar(comando));

    [Theory]
    [InlineData("git status")]
    [InlineData("ls tests")]
    [InlineData("")]
    [InlineData(null)]
    public void IgnoraOutrosComandos(string? comando) => Assert.Null(Verificacoes.Classificar(comando));

    [Fact]
    public void TesteDepoisDaUltimaEdicaoContaComoValidado()
    {
        var r = Verificacoes.Resumir([Edicao(), Bash("dotnet build"), Bash("dotnet test", erro: true), Edicao(), Bash("dotnet test")]);
        Assert.True(r.Teste!.Passou);
        Assert.False(r.EditouDepoisDoTeste);
        Assert.True(r.EditouDepoisDoBuild);
        Assert.Equal(["Testes passaram", "Build passou"], ChatVM.Selos(r, null).Select(s => s.Texto));
        Assert.Equal(["ok", "alerta"], ChatVM.Selos(r, null).Select(s => s.Tipo));
    }

    [Fact]
    public void EdicaoSemTesteOuTesteQueFalhouPedeCorrecao()
    {
        var semTeste = ChatVM.Selos(Verificacoes.Resumir([Edicao(), Bash("git diff")]), null);
        var selo = Assert.Single(semTeste);
        Assert.Equal("Nenhum teste rodou", selo.Texto);
        Assert.True(selo.PodeCorrigir);

        var falhou = ChatVM.Selos(Verificacoes.Resumir([Edicao(), Bash("npm test", erro: true)]), null);
        Assert.Equal("Testes falharam", Assert.Single(falhou).Texto);
        Assert.Contains("npm test", falhou[0].Correcao);

        var editouDepois = ChatVM.Selos(Verificacoes.Resumir([Bash("dotnet test"), Edicao()]), null);
        Assert.Equal("alerta", Assert.Single(editouDepois).Tipo);

        Assert.False(Verificacoes.Resumir([Bash("git status")]).TemAlgo);
        Assert.Empty(ChatVM.Selos(Verificacoes.Resumir([Bash("git status")]), null));
    }

    [Fact]
    public void AvisaDoPlanoSoPertoDoLimite()
    {
        Assert.Null(JanelaVM.AvisoDe(0.5, 0.7, null));
        Assert.Null(JanelaVM.AvisoDe(null, null, null));
        Assert.Contains("87%", JanelaVM.AvisoDe(0.87, 0.4, null));
        Assert.Contains("sessão de 5 horas", JanelaVM.AvisoDe(0.87, 0.4, null));
        Assert.Contains("da semana", JanelaVM.AvisoDe(0.3, 0.91, null));
        Assert.Contains("da semana", JanelaVM.AvisoDe(0.85, 0.96, null));
    }

    [Fact]
    public void ResumeAsSessoesDaMesmaIssue()
    {
        Assert.Null(ChatVM.TextoDaTarefa(1, 1));
        Assert.Equal("1 de 3 pronta", ChatVM.TextoDaTarefa(3, 1));
        Assert.Equal("0 de 2 prontas", ChatVM.TextoDaTarefa(2, 0));
    }

    [Fact]
    public void LeOsArquivosEmConflitoDoMergeTree()
    {
        Assert.Equal(["src/A.cs", "src/B.cs"], Conflitos.Ler("1669e964c81a725601eee7e1261b35db24114254\nsrc/A.cs\r\nsrc/B.cs\nsrc/A.cs\n"));
        Assert.Empty(Conflitos.Ler("1669e964c81a725601eee7e1261b35db24114254\n"));
        var selos = ChatVM.Selos(Verificacoes.Resumir([Edicao(), Bash("dotnet test")]), new ConflitoComBase("origin/main", ["src/A.cs"]));
        Assert.Equal(["Testes passaram", "Conflito com origin/main"], selos.Select(s => s.Texto));
        Assert.Contains("src/A.cs", selos[1].Correcao);
    }
}
