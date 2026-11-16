using Nexora.ViewModels;

namespace Nexora.Testes;

public sealed class RascunhoDaIssueTestes
{
    [Theory]
    [InlineData(4, "Boa noite")]
    [InlineData(5, "Bom dia")]
    [InlineData(11, "Bom dia")]
    [InlineData(12, "Boa tarde")]
    [InlineData(17, "Boa tarde")]
    [InlineData(18, "Boa noite")]
    public void SaudacaoSegueAHora(int hora, string esperada) =>
        Assert.Equal(esperada, IssuesVM.Saudacao(new DateTime(2026, 11, 5, hora, 30, 0)));

    [Fact]
    public void RascunhoTemOLinkEOTextoDaIssueEmTopicos()
    {
        var corpo = "<!-- Descreva o problema -->\r\nAo migrar está excluindo as views específicas do cliente (criadas pelo N2/N3) que dependem da VPontoCalc*\r\n" +
                    "![tela](https://github.com/user-attachments/assets/1.png)\r\n\r\n" +
                    "Antes de migrar guardar o fonte das views fora do sistema (pg_get_viewdef) e recriar depois\r\n<img width=\"300\" src=\"https://x/2.png\" />";
        var rascunho = IssuesVM.Rascunho("https://github.com/acme-corp/Acme.Shop/issues/3370", false, "Tratar views", corpo, new DateTime(2026, 11, 5, 14, 0, 0));

        Assert.Equal(
            "Boa tarde claude, issue: https://github.com/acme-corp/Acme.Shop/issues/3370\n\n" +
            "- Ao migrar está excluindo as views específicas do cliente (criadas pelo N2/N3) que dependem da VPontoCalc*\n" +
            "- Antes de migrar guardar o fonte das views fora do sistema (pg_get_viewdef) e recriar depois",
            rascunho);
    }

    [Fact]
    public void ListasETitulosViramTopicosSimples()
    {
        Assert.Equal("- Contexto\n- primeiro\n- segundo\n- [ ] tarefa\n- citado", IssuesVM.TopicosDoCorpo("## Contexto\n* primeiro\n2. segundo\n- [ ] tarefa\n> citado"));
    }

    [Fact]
    public void SemCorpoOuComCodigo()
    {
        Assert.Equal("Bom dia claude, PR: https://x/pull/9", IssuesVM.Rascunho("https://x/pull/9", true, "PR", "  ", new DateTime(2026, 11, 5, 9, 0, 0)));
        Assert.Equal("Boa noite claude, Sem link", IssuesVM.Rascunho("", false, "Sem link", null, new DateTime(2026, 11, 5, 22, 0, 0)));
        Assert.Equal("Rode isto:\n\n```\ndotnet test\n```", IssuesVM.TopicosDoCorpo("Rode isto:\n\n\n\n```\ndotnet test\n```\n"));
    }
}
