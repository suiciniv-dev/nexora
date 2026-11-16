using Nexora.Core.Git;
using Nexora.ViewModels;

namespace Nexora.Testes;

public sealed class IssuesGuardadasTestes
{
    [Fact]
    public async Task GuardaELeAsIssuesEOQuadro()
    {
        var antes = IssuesVM.ArquivoGuardadas;
        var pasta = Directory.CreateTempSubdirectory("nexora-issues-");
        IssuesVM.ArquivoGuardadas = Path.Combine(pasta.FullName, "issues.json");
        try
        {
            Assert.Null(IssuesVM.LerGuardadas());
            var issue = new IssueGh(48, "Cupom vencido", "u48", ["bug"], DateTimeOffset.Parse("2026-11-03T10:00:00Z")) { Responsaveis = ["eu"], EtiquetasComCor = [new("bug", "d73a4a")] };
            var quadro = new QuadroGh("PVT_1", "Loja", 3, "q", "voce/loja", @"D:\loja")
            {
                Campos = [new CampoDoQuadro("F1", "Status", [new OpcaoStatus("o1", "Ready", "GREEN")])],
                Visoes = [new VisaoDoQuadro("Atual", "iteration:@current", "Status")],
                CampoIteracao = "Sprint",
                Detalhado = true,
            };
            var item = new ItemDoQuadro("I1", "Issue", 48, "Cupom vencido", "u48", "OPEN", "voce/loja", ["eu"], [new("bug", "d73a4a")],
                new Dictionary<string, ValorDeCampo> { ["Status"] = new("Ready", "GREEN", "o1") }, "Sprint 4", [52], null);
            await IssuesVM.GravarGuardadasAsync(new IssuesGuardadas(true, [new RepoGuardado(@"D:\loja", "voce/loja", [issue]), new RepoGuardado(@"D:\sem", null, null)],
                [quadro], "PVT_1", "Atual", [item]));

            var lidas = IssuesVM.LerGuardadas()!;
            Assert.True(lidas.SoMinhas);
            Assert.Equal(["eu"], lidas.Repos[0].Issues![0].Responsaveis);
            Assert.Equal("d73a4a", lidas.Repos[0].Issues![0].EtiquetasComCor[0].Cor);
            Assert.Null(lidas.Repos[1].Issues);
            Assert.True(lidas.Quadros[0].Detalhado);
            Assert.Equal("Ready", lidas.Quadros[0].Status!.Opcoes[0].Nome);
            Assert.Equal("iteration:@current", lidas.Quadros[0].Visoes[0].Filtro);
            var lido = Assert.Single(lidas.Itens!);
            Assert.Equal("o1", lido.Valor("status")!.OpcaoId);
            Assert.Equal([52], lido.Prs);

            await File.WriteAllTextAsync(IssuesVM.ArquivoGuardadas, "{ quebrado");
            Assert.Null(IssuesVM.LerGuardadas());
        }
        finally
        {
            IssuesVM.ArquivoGuardadas = antes;
            pasta.Delete(true);
        }
    }
}
