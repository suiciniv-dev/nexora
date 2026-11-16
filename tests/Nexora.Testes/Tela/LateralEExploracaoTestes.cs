using Nexora.Core.Ambientes;
using Nexora.Core.Git;
using Nexora.ViewModels;

namespace Nexora.Testes.Tela;

public sealed class LateralEExploracaoTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-lateral-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    static ItemFerramenta Feita(string nome, string resumo)
    {
        var f = new ItemFerramenta(nome, resumo);
        f.Concluir("ok", false);
        return f;
    }

    [Fact]
    public void Exploracao_com_uma_ferramenta_mostra_a_propria_ferramenta()
    {
        var grupo = new ItemExploracao(Feita("Read", "a.cs"));

        Assert.True(grupo.Unica);
        Assert.Equal("a.cs", grupo.Resumo);
        Assert.Equal(grupo.Primeira.Rotulo, grupo.Rotulo);
    }

    [Fact]
    public void Exploracao_conta_leituras_e_buscas_e_acompanha_o_estado()
    {
        var grupo = new ItemExploracao(Feita("Read", "a.cs"));
        grupo.Juntar(Feita("Read", "b.cs"));
        var rodando = new ItemFerramenta("Grep", "Cupom");
        grupo.Juntar(rodando);

        Assert.True(grupo.Varias);
        Assert.Contains("2", grupo.Resumo);
        Assert.True(grupo.Rodando);

        rodando.Concluir("x", true);
        Assert.False(grupo.Rodando);
        Assert.True(grupo.Erro);
    }

    [Fact]
    public void Ferramentas_achata_os_grupos_na_ordem()
    {
        var grupo = new ItemExploracao(Feita("Read", "a.cs"));
        grupo.Juntar(Feita("Glob", "*.cs"));
        ItemChat[] itens = [new ItemUsuario("oi"), grupo, Feita("Bash", "dotnet test")];

        Assert.Equal(["Read", "Glob", "Bash"], ItemExploracao.Ferramentas(itens).Select(f => f.Nome));
        Assert.Equal(["Read", "Glob", "Bash"], ChatVM.PassosDe(itens).Select(p => p.Ferramenta));
        Assert.True(ItemExploracao.Explora("Grep"));
        Assert.False(ItemExploracao.Explora("Edit"));
    }

    [Fact]
    public void Principal_do_worktree_vem_do_arquivo_git()
    {
        var principal = Path.Combine(_pasta, "Loja");
        var worktree = Path.Combine(_pasta, "Loja-cupom");
        Directory.CreateDirectory(Path.Combine(principal, ".git", "worktrees", "Loja-cupom"));
        Directory.CreateDirectory(worktree);
        File.WriteAllText(Path.Combine(worktree, ".git"), $"gitdir: {Path.Combine(principal, ".git", "worktrees", "Loja-cupom")}\n");

        Assert.True(Workspaces.MesmaPasta(principal, Repositorio.PrincipalDoWorktree(worktree)!));
        Assert.Null(Repositorio.PrincipalDoWorktree(principal));
        Assert.Null(Repositorio.PrincipalDoWorktree(Path.Combine(_pasta, "nada")));
    }

    [Fact]
    public void Lateral_separa_branches_e_recolhe_as_paradas()
    {
        var janela = new JanelaVM();
        var local = new Local("Loja", _pasta, []);
        janela.AbrirSessao(local);
        janela.AbrirSessao(local);
        var a = janela.Abertas[0];
        var b = janela.Abertas[1];
        a.Branch = "main";
        b.Branch = "cupom";
        var grupo = new GrupoProjetoVM(janela, null, "Loja");
        grupo.Sessoes.Add(a);
        grupo.Sessoes.Add(b);

        grupo.Arrumar();
        Assert.Equal(2, grupo.Ramos.Count);
        Assert.All(grupo.Ramos, r => Assert.True(r.Cabecalho));

        var (parada, ativa) = a.EmFoco ? (b, a) : (a, b);
        parada.Estado = EstadoChat.Pronta;
        parada.AtividadeEm = DateTime.Now.AddHours(-3);
        ativa.Estado = EstadoChat.PrecisaDeVoce;
        grupo.Arrumar();
        Assert.Equal(1, grupo.Paradas);
        Assert.True(parada.RecolhidaNaLateral);
        Assert.False(ativa.RecolhidaNaLateral);

        grupo.AlternarParadasCommand.Execute(null);
        Assert.False(parada.RecolhidaNaLateral);
    }

    [Fact]
    public void Linha_da_lateral_resume_o_estado()
    {
        var janela = new JanelaVM();
        janela.AbrirSessao(new Local("Loja", _pasta, []));
        var chat = janela.Abertas[0];

        chat.Estado = EstadoChat.Erro;
        chat.AtividadeEm = DateTime.Now.AddMinutes(-12);
        Assert.Contains("12", chat.LinhaLateral);
        Assert.True(chat.LinhaErro);

        chat.Estado = EstadoChat.PrecisaDeVoce;
        Assert.True(chat.LinhaPrecisa);
        Assert.Equal(chat.TempoTexto, chat.LinhaLateral);
    }

    [Fact]
    public void Busca_de_sessoes_ignora_acento_e_exige_todos_os_termos()
    {
        var janela = new JanelaVM();
        janela.AbrirSessao(new Local("Loja", _pasta, []));
        var chat = janela.Abertas[0];
        chat.Titulo = "Cupom de desconto no checkout";
        chat.Branch = "cupom-checkout";

        Assert.True(VisaoGeralVM.Combina(chat, ""));
        Assert.True(VisaoGeralVM.Combina(chat, "CUPOM checkout"));
        Assert.True(VisaoGeralVM.Combina(chat, "loja"));
        Assert.False(VisaoGeralVM.Combina(chat, "cupom pedidos"));
        chat.Titulo = "Migração do cupom";
        Assert.True(VisaoGeralVM.Combina(chat, "migracao"));
    }
}
