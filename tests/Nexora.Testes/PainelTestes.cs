using Nexora.Core.Git;
using Nexora.Core.Painel;

namespace Nexora.Testes;

public sealed class PainelTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-painel-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_pasta, true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void LeFeedRssComDataEmFormatoDeEmail()
    {
        const string xml = """
            <?xml version="1.0"?>
            <rss version="2.0"><channel><title>Hacker News</title>
              <item><title>  Show HN:   um app  </title><link>https://exemplo.com/a</link><pubDate>Mon, 16 Nov 2026 14:30:00 +0000</pubDate></item>
              <item><title>Sem data</title><link>https://exemplo.com/b</link></item>
              <item><title></title><link>https://exemplo.com/c</link></item>
            </channel></rss>
            """;
        var lista = Noticias.Ler(xml);
        Assert.Equal(2, lista.Count);
        Assert.Equal("Show HN: um app", lista[0].Titulo);
        Assert.Equal("Hacker News", lista[0].Fonte);
        Assert.Equal(new DateTimeOffset(2026, 11, 16, 14, 30, 0, TimeSpan.Zero), lista[0].Quando);
        Assert.Null(lista[1].Quando);
    }

    [Fact]
    public void LeFeedAtomDoGitHubPreferindoOLinkAlternativo()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <feed xmlns="http://www.w3.org/2005/Atom"><title>Release notes from claude-code</title>
              <entry><title>v2.1.0</title><updated>2026-11-15T10:00:00Z</updated>
                <link rel="self" href="https://api/x"/><link rel="alternate" type="text/html" href="https://github.com/anthropics/claude-code/releases/tag/v2.1.0"/></entry>
            </feed>
            """;
        var noticia = Assert.Single(Noticias.Ler(xml, "claude-code"));
        Assert.Equal("v2.1.0", noticia.Titulo);
        Assert.Equal("https://github.com/anthropics/claude-code/releases/tag/v2.1.0", noticia.Link);
        Assert.Equal("claude-code", noticia.Fonte);
        Assert.Equal(2026, noticia.Quando!.Value.Year);
    }

    [Fact]
    public void FeedInvalidoNaoQuebra()
    {
        Assert.Empty(Noticias.Ler("<html><body>não é feed"));
        Assert.Empty(Noticias.Ler("<html><body>não é feed</body></html>"));
    }

    [Theory]
    [InlineData("https://github.com/anthropics/claude-code/releases.atom", "claude-code")]
    [InlineData("https://www.theverge.com/rss/index.xml", "theverge.com")]
    [InlineData("https://hnrss.org/frontpage", "hnrss.org")]
    public void NomeDaFonteVemDoEndereco(string url, string esperado) => Assert.Equal(esperado, Noticias.Fonte(url));

    [Fact]
    public void LeCommitsDoGitLog()
    {
        var saida = "a1b2c3d\u001fAna\u001f2026-11-16T10:20:30-03:00\u001fFix the cart total\n"
            + "e4f5a6b\u001fBia\u001fdata ruim\u001fIgnorado\n"
            + "c7d8e9f\u001fCaio\u001f2026-11-15T08:00:00-03:00\u001fAssunto com \u001f separador\r\n";
        var commits = Recentes.LerCommits(saida);
        Assert.Equal(2, commits.Count);
        Assert.Equal("Fix the cart total", commits[0].Assunto);
        Assert.Equal("Ana", commits[0].Autor);
        Assert.Equal("Assunto com \u001f separador", commits[1].Assunto);
    }

    [Fact]
    public void LePrsDoGhPrList()
    {
        const string json = """
            [{"number":430,"title":"Fix usage","state":"MERGED","isDraft":false,"updatedAt":"2026-11-16T12:00:00Z","url":"https://github.com/acme/shop/pull/430","author":{"login":"ana"}},
             {"number":431,"title":"WIP","state":"OPEN","isDraft":true,"updatedAt":"2026-11-16T13:00:00Z","url":"u","author":null},
             {"title":"sem número"}]
            """;
        var prs = Recentes.LerPrs(json);
        Assert.Equal(2, prs.Count);
        Assert.True(prs[0].Mergeado);
        Assert.Equal("ana", prs[0].Autor);
        Assert.True(prs[1].Aberto);
        Assert.True(prs[1].Rascunho);
        Assert.Empty(Recentes.LerPrs("não é json"));
    }

    [Fact]
    public void RegistroSomaTempoDaSemanaEGuardaNoDisco()
    {
        var arquivo = RegistroDoPainel.Caminho(_pasta);
        var registro = RegistroDoPainel.Ler(arquivo);
        var segunda = new DateTime(2026, 11, 16, 9, 0, 0);
        registro.SomarAtivo(segunda, TimeSpan.FromMinutes(30));
        registro.SomarAtivo(segunda.AddDays(1), TimeSpan.FromMinutes(15));
        registro.SomarAtivo(segunda.AddDays(-1), TimeSpan.FromHours(5));
        registro.SomarTurno(segunda, TimeSpan.FromMinutes(4));
        registro.Gravar(arquivo);

        var lido = RegistroDoPainel.Ler(arquivo);
        var (ativo, agentes, turnos) = lido.Semana(new DateOnly(2026, 11, 17));
        Assert.Equal(TimeSpan.FromMinutes(45), ativo);
        Assert.Equal(TimeSpan.FromMinutes(4), agentes);
        Assert.Equal(1, turnos);
    }

    [Fact]
    public void PlanoGuardaUmPontoACadaCincoMinutosEPodaOAntigo()
    {
        var registro = new RegistroDoPainel();
        var hora = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);
        Assert.False(registro.AnotarPlano(new PontoDoPlano(hora, null, null, null, null)));
        Assert.True(registro.AnotarPlano(new PontoDoPlano(hora, 0.10, 0.30, null, null)));
        Assert.False(registro.AnotarPlano(new PontoDoPlano(hora.AddMinutes(1), 0.10, 0.30, null, null)));
        Assert.True(registro.AnotarPlano(new PontoDoPlano(hora.AddMinutes(2), 0.12, 0.30, null, null)));
        Assert.Single(registro.Plano);
        Assert.Equal(0.12, registro.Plano[0].Sessao);
        Assert.True(registro.AnotarPlano(new PontoDoPlano(hora.AddMinutes(8), 0.15, 0.31, 0.2, 0.4)));
        Assert.Equal(2, registro.Plano.Count);

        registro.Plano.Insert(0, new PontoDoPlano(hora.AddDays(-20), 0.5, 0.5, null, null));
        registro.Dias["2025-01-01"] = new DiaDeUso { Ativo = 1 };
        registro.Podar(hora.UtcDateTime);
        Assert.Equal(2, registro.Plano.Count);
        Assert.DoesNotContain("2025-01-01", registro.Dias.Keys);
    }

    [Fact]
    public void LeContribuicoesEPrsDoGitHub()
    {
        const string json = """
            {"data":{"viewer":{"login":"ana","contributionsCollection":{"contributionCalendar":{"totalContributions":1768,"weeks":[
              {"contributionDays":[{"date":"2026-11-15","contributionCount":0,"contributionLevel":"NONE"},{"date":"2026-11-16","contributionCount":7,"contributionLevel":"THIRD_QUARTILE"}]},
              {"contributionDays":[{"date":"2026-11-17","contributionCount":2,"contributionLevel":"FIRST_QUARTILE"}]}]}}},
             "meus":{"nodes":[{"number":7,"title":"Fix","state":"MERGED","isDraft":false,"updatedAt":"2026-11-16T10:00:00Z","url":"u","repository":{"name":"shop"},"author":{"login":"ana"}},{}]},
             "revisar":{"nodes":[{"number":61,"title":"Rate limit","state":"OPEN","isDraft":false,"updatedAt":"2026-11-17T10:00:00Z","url":"v","repository":{"name":"api"},"author":{"login":"bruno"}}]}}}
            """;
        var painel = GitHubDoUsuario.Ler(json);
        Assert.NotNull(painel);
        Assert.Equal("ana", painel.Login);
        Assert.Equal(1768, painel.Total);
        Assert.Equal([0, 3, 1], painel.Dias.Select(d => d.Nivel));
        Assert.Equal(new DateOnly(2026, 11, 15), painel.Dias[0].Dia);
        var meu = Assert.Single(painel.Meus);
        Assert.True(meu.Mergeado);
        Assert.Equal("shop", meu.Repositorio);
        Assert.Equal("bruno", Assert.Single(painel.ParaRevisar).Autor);
        Assert.Null(GitHubDoUsuario.Ler("{\"errors\":[]}"));
        Assert.Null(GitHubDoUsuario.Ler("não é json"));
    }
}
