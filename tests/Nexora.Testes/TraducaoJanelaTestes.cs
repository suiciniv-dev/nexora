using Nexora.Core.Agenda;
using Nexora.Core.Ambientes;
using Nexora.Core.Git;
using Nexora.Core.Texto;
using Nexora.Fundos;
using Nexora.Temas;
using Nexora.Traducao;
using Nexora.ViewModels;

namespace Nexora.Testes;

public sealed partial class TraducaoTestes
{
    static void Em(string codigo, Action acao)
    {
        Idioma.Registrar(Ingles.Pares);
        try
        {
            Idioma.Definir(codigo);
            acao();
        }
        finally
        {
            Idioma.Definir("pt");
        }
    }

    [Fact]
    public void TempoRelativoEmInglesUsaCulturaAmericana()
    {
        var agora = new DateTime(2026, 11, 6, 12, 0, 0);
        Em("en", () =>
        {
            Assert.Equal("now", Tempo.Relativo(agora.AddSeconds(-20), agora));
            Assert.Equal("4 min ago", Tempo.Relativo(agora.AddMinutes(-4), agora));
            Assert.Equal("2 h ago", Tempo.Relativo(agora.AddHours(-2), agora));
            Assert.Equal("yesterday", Tempo.Relativo(agora.AddDays(-1), agora));
            Assert.Equal("3 days ago", Tempo.Relativo(agora.AddDays(-3), agora));
            Assert.Equal("Sep 14", Tempo.Relativo(new DateTime(2026, 9, 14, 8, 0, 0), agora));
            Assert.Equal("Dec 31, 2025", Tempo.Relativo(new DateTime(2025, 12, 31, 8, 0, 0), agora));
        });
        Assert.Equal("agora", Tempo.Relativo(agora.AddSeconds(-20), agora));
        Assert.Equal("há 4 min", Tempo.Relativo(agora.AddMinutes(-4), agora));
        Assert.Equal("há 2 h", Tempo.Relativo(agora.AddHours(-2), agora));
        Assert.Equal("ontem", Tempo.Relativo(agora.AddDays(-1), agora));
        Assert.Equal("há 3 dias", Tempo.Relativo(agora.AddDays(-3), agora));
        Assert.Equal("14/09", Tempo.Relativo(new DateTime(2026, 9, 14, 8, 0, 0), agora));
        Assert.Equal("31/12/25", Tempo.Relativo(new DateTime(2025, 12, 31, 8, 0, 0), agora));
    }

    [Fact]
    public void AgendaEmInglesDescreveDiasEHorario()
    {
        var uteis = new Agendamento("a", "n", "p", "t", new TimeOnly(9, 0), [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);
        var alguns = new Agendamento("b", "n", "p", "t", new TimeOnly(13, 5), [DayOfWeek.Monday, DayOfWeek.Sunday]);
        Em("en", () =>
        {
            Assert.Equal("Monday to Friday at 09:00", Agenda.Descrever(uteis));
            Assert.Equal("Mon, Sun at 13:05", Agenda.Descrever(alguns));
        });
        Assert.Equal("de segunda a sexta às 09:00", Agenda.Descrever(uteis));
        Assert.Equal("seg, dom às 13:05", Agenda.Descrever(alguns));
    }

    [Fact]
    public async Task ErroDoGitEmInglesMenciona()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "nexora-sem-pasta-" + Guid.NewGuid().ToString("N"));
        var emPortugues = await ComandoGit.ExecutarAsync(pasta, "status");
        Assert.Equal($"A pasta {pasta} não existe neste computador.", emPortugues.Erro);
        Idioma.Registrar(Ingles.Pares);
        try
        {
            Idioma.Definir("en");
            var emIngles = await ComandoGit.ExecutarAsync(pasta, "status");
            Assert.Equal($"The folder {pasta} does not exist on this computer.", emIngles.Erro);
        }
        finally
        {
            Idioma.Definir("pt");
        }
    }

    [Fact]
    public void AvisosDeExclusaoTemPluralEmIngles()
    {
        var principal = new DiagnosticoDeExclusao(true, 3, false, 0, null, ["D:\\a\\Loja-wt1", "D:\\a\\Loja-wt2"], "main", 0);
        var worktree = new DiagnosticoDeExclusao(true, 1, true, 0, "D:\\a\\Loja", [], "feature", 0);
        Em("en", () =>
        {
            var avisos = ExclusaoDeProjeto.Avisos(principal);
            Assert.Contains("3 uncommitted files go with it.", avisos);
            Assert.Contains("The repository has no remote: the git history only exists in this folder.", avisos);
            Assert.Contains("2 worktrees of this repository (Loja-wt1, Loja-wt2) will stop working.", avisos);
            Assert.Contains("It has 1 uncommitted change, and git will not remove the worktree like this. Commit or discard it first.", ExclusaoDeProjeto.Avisos(worktree));
        });
        var emPortugues = ExclusaoDeProjeto.Avisos(principal);
        Assert.Contains("3 arquivos alterados sem commit vão junto.", emPortugues);
        Assert.Contains("2 worktrees deste repositório (Loja-wt1, Loja-wt2) param de funcionar.", emPortugues);
    }

    [Fact]
    public void ResumoDoPrEAlteracoesEmIngles()
    {
        var aberto = new PullRequest(7, "t", "OPEN", false, "u", "CHANGES_REQUESTED", "falhou");
        Em("en", () =>
        {
            Assert.Equal("open  ·  changes requested  ·  checks failing", aberto.Resumo);
            Assert.Equal("merged", new PullRequest(7, "t", "MERGED", false, "u", null, null).Resumo);
            Assert.Equal("new", new Alteracao("??", "a.cs", null).Rotulo);
            Assert.Equal("deleted", new Alteracao(" D", "a.cs", null).Rotulo);
        });
        Assert.Equal("aberto  ·  pediram mudanças  ·  checks falharam", aberto.Resumo);
        Assert.Equal("novo", new Alteracao("??", "a.cs", null).Rotulo);
    }

    [Fact]
    public void TokensPorExtensoEmInglesUsaPontoDecimal()
    {
        Em("en", () =>
        {
            Assert.Equal("171 thousand", CartaoVM.TokensPorExtenso(171_000));
            Assert.Equal("1.5 million", CartaoVM.TokensPorExtenso(1_500_000));
            Assert.Equal("2 million", CartaoVM.TokensPorExtenso(2_000_000));
        });
        Assert.Equal("171 mil", CartaoVM.TokensPorExtenso(171_000));
        Assert.Equal("1,5 milhão", CartaoVM.TokensPorExtenso(1_500_000));
    }

    [Fact]
    public void NomesDeTemasEDeFundosTemIngles()
    {
        Idioma.Registrar(Ingles.Pares);
        var faltando = new List<string>();
        foreach (var papel in Papeis.Todos)
        {
            if (!Idioma.TemTraducao(papel.Nome)) faltando.Add(papel.Nome);
            if (!Idioma.TemTraducao(papel.Ideia)) faltando.Add(papel.Ideia);
        }
        foreach (var tema in Catalogo.Embutidos)
            if (!Idioma.TemTraducao(tema.Nome)) faltando.Add(tema.Nome);
        Assert.True(faltando.Count == 0, "Sem tradução:\n" + string.Join("\n", faltando));
        Em("en", () =>
        {
            Assert.Equal("Nexora Midnight", Catalogo.Embutidos[0].NomeExibido);
            Assert.Equal("Default light", Catalogo.Embutidos[1].NomeExibido);
            Assert.Equal("Light graph", Catalogo.Embutidos.First(t => t.Id == "grafo-claro").NomeExibido);
        });
        Assert.Equal("Nexora Midnight", Catalogo.Embutidos[0].NomeExibido);
    }

    [Fact]
    public void NomeDeTemaDoUsuarioNaoSofreTraducao()
    {
        var meu = new Tema { Id = "x", Nome = "Padrão", Embutido = false };
        Em("en", () => Assert.Equal("Padrão", meu.NomeExibido));
    }
}
