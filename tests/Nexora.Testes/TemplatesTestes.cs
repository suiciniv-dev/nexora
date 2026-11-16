using Nexora.Core.Texto;
using Nexora.ViewModels;

namespace Nexora.Testes;

[Collection("Idioma")]
public sealed class TemplatesTestes
{
    [Fact]
    public void TrocaAsVariaveisEDeixaOCursorNoFim()
    {
        var (texto, cursor) = Templates.Preencher("{saudacao} claude, {tipo}: {url}", new Dictionary<string, string?>
        {
            ["saudacao"] = "Bom dia",
            ["tipo"] = "issue",
            ["url"] = "https://x/issues/1",
        });
        Assert.Equal("Bom dia claude, issue: https://x/issues/1", texto);
        Assert.Equal(texto.Length, cursor);
    }

    [Fact]
    public void OPrimeiroQueFaltaViraOCursorEOsOutrosSomem()
    {
        var (texto, cursor) = Templates.Preencher("Handoff: x, pediram ajustes.\n\n{links}\n\n{corpo}\n\nSem commit.", new Dictionary<string, string?>());
        Assert.Equal("Handoff: x, pediram ajustes.\n\n\n\nSem commit.", texto);
        Assert.Equal("Handoff: x, pediram ajustes.\n\n".Length, cursor);
    }

    [Fact]
    public void ChaveQueNaoEVariavelFicaComoEsta()
    {
        var (texto, _) = Templates.Preencher("Use {json} e {url}", new Dictionary<string, string?> { ["url"] = "u" });
        Assert.Equal("Use {json} e u", texto);
    }

    [Fact]
    public void SubtopicosDaIssueMantemORecuo()
    {
        Assert.Equal("- Painel\n  - Minha escala\n  - Atalho do setor\n- Escalas",
            IssuesVM.TopicosDoCorpo("- Painel\n  - Minha escala\n  - Atalho do setor\n- Escalas"));
        Assert.Equal("- A\n  - B", IssuesVM.TopicosDoCorpo("- A\n\t- B"));
    }

    [Fact]
    public void RascunhoDaIssueUsaOTemplate()
    {
        var rascunho = IssuesVM.Rascunho("https://github.com/o/r/issues/3389", false, "Escalas", "- Notificar\n  - por regra", new DateTime(2026, 11, 7, 9, 0, 0),
            "{saudacao} claude, {tipo} #{numero}: {url}\n\n{corpo}\n\nPode criar a branch, sem commit.");
        Assert.Equal("Bom dia claude, issue #3389: https://github.com/o/r/issues/3389\n\n- Notificar\n  - por regra\n\nPode criar a branch, sem commit.", rascunho);
    }

    [Fact]
    public void PadroesTemIdsUnicosEOTemplateDaIssue()
    {
        var padroes = Templates.Padroes();
        Assert.Equal(padroes.Count, padroes.Select(p => p.Id).Distinct().Count());
        Assert.Contains(padroes, p => p.Id == Templates.IdIssue && p.Texto.Contains("{url}") && p.Texto.Contains("{corpo}"));
        Assert.All(padroes, p => Assert.False(string.IsNullOrWhiteSpace(p.Nome)));
    }

    [Fact]
    public void HandoffUsaABranch()
    {
        Assert.Equal("Handoff: visual-tour-fixes", Templates.Handoff("visual-tour-fixes"));
        Assert.Equal("", Templates.Handoff(null));
    }

    [Fact]
    public void Passos_separam_pelo_marcador_e_ignoram_vazios()
    {
        var texto = "Analise a issue.\n\n--- próximo passo ---\n\nRode os testes.\n--- NEXT STEP ---\n\n--- next step ---\nAbra o PR.";

        Assert.Equal(["Analise a issue.", "Rode os testes.", "Abra o PR."], Templates.Passos(texto));
        Assert.Equal(["Só um pedido --- com traços"], Templates.Passos("Só um pedido --- com traços"));
    }

    [Fact]
    public void Fluxos_padrao_tem_mais_de_um_passo()
    {
        Assert.All(Templates.Padroes().Where(t => t.Id.StartsWith("fluxo-")), t => Assert.True(Templates.Passos(t.Texto).Count >= 3));
    }

    [Fact]
    public void Enviar_um_fluxo_com_o_claude_trabalhando_poe_todos_os_passos_na_fila_em_ordem()
    {
        var pasta = Directory.CreateTempSubdirectory("nexora-fluxo-").FullName;
        try
        {
            var janela = new JanelaVM();
            janela.AbrirSessao(new Local("Loja", pasta, []));
            var chat = janela.Abertas[0];
            chat.Estado = EstadoChat.Trabalhando;
            chat.Rascunho = "Um\n--- próximo passo ---\nDois\n--- próximo passo ---\nTrês";

            chat.EnviarCommand.Execute(null);

            Assert.Equal(["Um", "Dois", "Três"], chat.Fila.Select(f => f.Texto));
            Assert.Equal("", chat.Rascunho);
        }
        finally
        {
            try { Directory.Delete(pasta, true); } catch (IOException) { }
        }
    }
}
