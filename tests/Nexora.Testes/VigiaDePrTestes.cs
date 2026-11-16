using Nexora.Core.Git;

namespace Nexora.Testes;

public sealed class VigiaDePrTestes
{
    static RetratoPr Retrato(string sha = "abc", bool aberto = true, string[]? falhos = null, bool conflito = false, params ComentarioPr[] comentarios) =>
        new(7, sha, aberto, falhos ?? [], conflito, comentarios);

    static ComentarioPr Comentario(string id, string autor, string texto = "Troque o nome desta variável", string? arquivo = "src/Loja.cs", int? linha = 12) =>
        new(id, autor, texto, arquivo, linha, DateTimeOffset.Parse("2026-11-10T10:00:00Z"), null);

    [Fact]
    public void CheckQueFalhouDisparaUmaVezPorCommit()
    {
        var vistos = new HashSet<string>();
        var eventos = VigiaDePr.Comparar(Retrato(falhos: ["build", "testes"]), "eu", vistos.Contains, false);
        var evento = Assert.Single(eventos);
        Assert.Equal(TipoEventoPr.CheckFalhou, evento.Tipo);
        Assert.Equal(["build", "testes"], evento.Checks);
        foreach (var chave in evento.Chaves) vistos.Add(chave);

        Assert.Empty(VigiaDePr.Comparar(Retrato(falhos: ["build", "testes"]), "eu", vistos.Contains, false));
        Assert.Single(VigiaDePr.Comparar(Retrato(sha: "def", falhos: ["build"]), "eu", vistos.Contains, false));
    }

    [Fact]
    public void ConflitoDisparaPorCommitEPrFechadoNaoDisparaNada()
    {
        var evento = Assert.Single(VigiaDePr.Comparar(Retrato(conflito: true), "eu", _ => false, false));
        Assert.Equal(TipoEventoPr.Conflito, evento.Tipo);
        Assert.Empty(VigiaDePr.Comparar(Retrato(aberto: false, falhos: ["build"], conflito: true), "eu", _ => false, false));
    }

    [Fact]
    public void ComentariosMeusEDeRobosNaoContam()
    {
        var eventos = VigiaDePr.Comparar(Retrato(comentarios:
        [
            Comentario("1", "eu"),
            Comentario("2", "github-actions[bot]"),
            Comentario("3", "codecov[bot]"),
            Comentario("4", "revisora"),
        ]), "EU", _ => false, false);
        var evento = Assert.Single(eventos);
        Assert.Equal("revisora", Assert.Single(evento.Comentarios).Autor);
    }

    [Fact]
    public void NaPrimeiraVezOsComentariosAntigosSoSaoMarcadosComoVistos()
    {
        var evento = Assert.Single(VigiaDePr.Comparar(Retrato(comentarios: [Comentario("9", "revisora")]), "eu", _ => false, true));
        Assert.Empty(evento.Comentarios);
        Assert.Equal(["comentario:9"], evento.Chaves);
    }

    [Fact]
    public void PedidoDeRevisaoDizSeDeveResponderOuNao()
    {
        var evento = new EventoPr(TipoEventoPr.Comentarios, 7, [], [], [Comentario("1", "revisora", "Use async aqui")]);
        var semResposta = VigiaDePr.Pedido(evento, VigiaDePr.Agir);
        Assert.Contains("@revisora (src/Loja.cs:12): Use async aqui", semResposta);
        Assert.Contains("Não responda aos comentários", semResposta);
        Assert.Contains("responda cada comentário", VigiaDePr.Pedido(evento, VigiaDePr.AgirEResponder));
    }

    [Fact]
    public void RetratoVemDoDetalheDoPrSemConversasResolvidas()
    {
        var pr = new DetalhePr(52, "Cupom", "u", "OPEN")
        {
            ShaDoHead = "f00",
            Mergeavel = "CONFLICTING",
            Checks =
            [
                new CheckDoPr("build", "CI", EstadoCheck.Falhou, "FAILURE", "", null, null),
                new CheckDoPr("lint", "CI", EstadoCheck.Ok, "SUCCESS", "", null, null),
            ],
            Revisoes = [new RevisaoDoPr("R1", "revisora", "CHANGES_REQUESTED", "Faltam testes", DateTimeOffset.Parse("2026-11-10T10:00:00Z")), new RevisaoDoPr("R2", "revisora", "APPROVED", "", null)],
            Comentarios = [new ComentarioDoPr("C1", "outro", "Pode rebasear?", "u", DateTimeOffset.Parse("2026-11-10T09:00:00Z"))],
            Conversas =
            [
                new ConversaDoPr("T1", "src/Cupom.cs", 40, false, false, [new ComentarioDoPr("L1", "revisora", "Nome ruim", "h", DateTimeOffset.Parse("2026-11-10T10:05:00Z"))]),
                new ConversaDoPr("T2", "src/Cupom.cs", 8, true, false, [new ComentarioDoPr("L2", "revisora", "Antigo", "h", null)]),
            ],
        };

        var retrato = VigiaDePr.RetratoDe(pr);

        Assert.Equal("f00", retrato.Sha);
        Assert.True(retrato.Aberto);
        Assert.True(retrato.Conflito);
        Assert.Equal(["CI / build"], retrato.ChecksFalhos);
        Assert.Equal(["cC1", "rR1", "lL1"], retrato.Comentarios.Select(c => c.Id));
        Assert.Equal(40, retrato.Comentarios.Single(c => c.Id == "lL1").Linha);
    }
}
