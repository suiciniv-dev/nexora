using Nexora.Core.Ambientes;
using Nexora.Core.Git;
using Nexora.ViewModels;

namespace Nexora.Testes.Tela;

public sealed class PrimeiroUsoTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-primeiro-uso-").FullName;
    readonly List<string> _workspaces = [];

    public PrimeiroUsoTestes() => JanelaVM.Isolada = true;

    public void Dispose()
    {
        foreach (var arquivo in _workspaces)
        {
            App.Config.Workspaces.RemoveAll(w => string.Equals(w, arquivo, StringComparison.OrdinalIgnoreCase));
            if (File.Exists(arquivo)) File.Delete(arquivo);
        }
        App.Config.Ocultos.RemoveAll(o => o.StartsWith(_pasta, StringComparison.OrdinalIgnoreCase));
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    [Fact]
    public void Pasta_comum_vira_projeto_sem_criar_nada_dentro_dela()
    {
        var codigo = Directory.CreateDirectory(Path.Combine(_pasta, "meu-app")).FullName;
        var janela = new JanelaVM();

        var projeto = janela.AdicionarPastaComoProjeto(codigo + Path.DirectorySeparatorChar);

        Assert.NotNull(projeto);
        _workspaces.Add(projeto.Arquivo!);
        Assert.Equal("meu-app", projeto.Nome);
        Assert.True(Workspaces.MesmaPasta(codigo, projeto.Principal));
        Assert.StartsWith(JanelaVM.PastaDeProjetos, projeto.Arquivo);
        Assert.Empty(Directory.EnumerateFileSystemEntries(codigo));
        Assert.Contains(projeto.Arquivo, App.Config.Workspaces);
        Assert.Same(projeto, janela.AdicionarPastaComoProjeto(codigo));
    }

    [Fact]
    public void Pasta_com_workspace_proprio_usa_o_workspace_e_volta_se_estava_escondida()
    {
        var codigo = Directory.CreateDirectory(Path.Combine(_pasta, "loja")).FullName;
        var arquivo = Path.Combine(codigo, "Loja.code-workspace");
        File.WriteAllText(arquivo, """{"folders":[{"path":"."}]}""");
        App.Config.Ocultos.Add(codigo);
        var janela = new JanelaVM();

        var projeto = janela.AdicionarPastaComoProjeto(codigo);

        Assert.NotNull(projeto);
        _workspaces.Add(arquivo);
        Assert.Equal(arquivo, projeto.Arquivo);
        Assert.DoesNotContain(App.Config.Ocultos, o => Workspaces.MesmaPasta(o, codigo));
    }

    [Fact]
    public void Todo_atalho_com_comando_tem_gesto_valido_e_comando_na_janela()
    {
        var janela = new JanelaVM();
        foreach (var atalho in CatalogoDeAtalhos.Todos().Where(a => a.Comando is not null))
        {
            Assert.NotEmpty(atalho.Gestos);
            foreach (var gesto in atalho.Gestos) Avalonia.Input.KeyGesture.Parse(gesto);
            Assert.NotNull(atalho.Comando!(janela));
        }
        var teclas = CatalogoDeAtalhos.Todos().Where(a => a.Comando is not null).Select(a => a.Tecla).ToList();
        Assert.Equal(teclas.Count, teclas.Distinct().Count());
        Assert.Contains("Ctrl+Alt+L", teclas);
        Assert.Contains("Ctrl+\\", teclas);
        Assert.Contains(CatalogoDeAtalhos.Todos(), a => a.Tecla == "Alt+M");
    }

    [Fact]
    public void Atalho_aparece_com_simbolos_do_Mac()
    {
        Assert.Equal("⇧⌘O", CatalogoDeAtalhos.Exibir("Ctrl+Shift+O", mac: true));
        Assert.Equal("⌃Tab", CatalogoDeAtalhos.Exibir("Ctrl+Tab", mac: true));
        Assert.Equal("⌥M", CatalogoDeAtalhos.Exibir("Alt+M", mac: true));
        Assert.Equal("⌘\\", CatalogoDeAtalhos.Exibir("Ctrl+\\", mac: true));
        Assert.Equal("Ctrl+K", CatalogoDeAtalhos.Exibir("Ctrl+K", mac: false));
    }

    [Fact]
    public void Busca_lista_comandos_com_o_atalho_e_abre_a_lista_de_atalhos()
    {
        var janela = new JanelaVM();
        janela.ProcurarCommand.Execute(null);
        Assert.Contains(janela.Resultados, r => r.Titulo == Core.Texto.Idioma.T("Ver todos os atalhos de teclado"));

        janela.Busca = "split";
        var dividir = Assert.Single(janela.Resultados, r => r.Titulo == Core.Texto.Idioma.T("Ligar ou desligar a tela dividida (split view)"));
        Assert.Contains("Ctrl+Shift+\\", dividir.Detalhe);

        janela.Busca = "atalhos";
        janela.AbrirResultado(janela.Resultados.First(r => r.Icone == "teclado"));
        Assert.False(janela.BuscaAberta);
        Assert.True(janela.Atalhos.Aberto);
        Assert.True(janela.TemSobreposicao);
        Assert.Contains(janela.Atalhos.Grupos, g => g.Itens.Any(a => a.Tecla == "Ctrl+Alt+L"));
        janela.Atalhos.FecharCommand.Execute(null);
        Assert.False(janela.Atalhos.Aberto);
    }

    [Fact]
    public Task Janela_liga_os_atalhos_do_catalogo() => Sessao.Rodar(() =>
    {
        var vm = new JanelaVM();
        var janela = new Janela { DataContext = vm, Width = 1200, Height = 800 };
        var gestos = janela.KeyBindings.Select(k => k.Gesture?.ToString()).ToHashSet();
        foreach (var gesto in CatalogoDeAtalhos.Todos().SelectMany(a => a.Gestos))
            Assert.Contains(Avalonia.Input.KeyGesture.Parse(gesto).ToString(), gestos);
        janela.DataContext = new JanelaVM();
        Assert.Equal(CatalogoDeAtalhos.Todos().Sum(a => a.Gestos.Length), janela.KeyBindings.Count);
    });

    [Fact]
    public void Lateral_sem_conversas_mostra_o_que_fazer()
    {
        var janela = new JanelaVM();
        Assert.True(janela.SemConversas);
        Assert.True(janela.SemConversasNenhuma);

        janela.Busca = "nada disso";
        Assert.True(janela.SemConversasNaBusca);
        Assert.False(janela.SemConversasNenhuma);
        janela.Busca = "";

        janela.AbrirSessao(new Local("Loja", _pasta, []));
        Assert.False(janela.SemConversas);
        Assert.False(janela.SemConversasNenhuma);
    }

    [Fact]
    public void Ao_vivo_lembra_a_ultima_visao_escolhida()
    {
        var (quadro, mesa) = (App.Config.Quadro, App.Config.Mesa);
        try
        {
            new JanelaVM().VisaoGeral.MostrarListaCommand.Execute(null);
            Assert.True(new JanelaVM().VisaoGeral.ModoLista);

            new JanelaVM().VisaoGeral.MostrarMesaCommand.Execute(null);
            Assert.True(new JanelaVM().VisaoGeral.ModoMesa);

            new JanelaVM().VisaoGeral.MostrarQuadroCommand.Execute(null);
            var visao = new JanelaVM().VisaoGeral;
            Assert.True(visao.QuadroColunas);
            Assert.False(visao.ModoMesa);
        }
        finally
        {
            (App.Config.Quadro, App.Config.Mesa) = (quadro, mesa);
        }
    }

    [Fact]
    public void Configuracao_nova_comeca_com_a_lateral_larga_aberta()
    {
        var nova = new Config();
        var janela = new JanelaVM { LateralVisivel = nova.LateralVisivel, Vertical = false };

        Assert.True(janela.LateralFixa);
        Assert.True(janela.MostraTrilho);
    }
}
