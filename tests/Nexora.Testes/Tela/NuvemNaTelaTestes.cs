using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.Core.Nuvem;
using Nexora.Core.Segredos;
using Nexora.Testes.Nuvem;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class NuvemNaTelaTestes : IDisposable
{
    readonly MaquinaFalsa _falsa = new();
    ServicoDaNuvem? _antes;
    List<string>? _projetosAntes;

    public NuvemNaTelaTestes() => ProcessoRemoto.Esperas = [TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(500)];

    public void Dispose()
    {
        Sessao.Rodar(() =>
        {
            if (_antes is { } antes) App.Nuvem = antes;
            if (_projetosAntes is { } projetos) App.Config.ProjetosNaNuvem = projetos;
        }).Wait();
        var execucoes = Path.Combine(_falsa.Casa, ".nexora", "execucoes");
        if (Directory.Exists(execucoes))
            foreach (var pasta in Directory.EnumerateDirectories(execucoes))
                _falsa.Maquina.RodarAsync(ScriptRemoto.Chamar("parar", Path.GetFileName(pasta))).Wait(TimeSpan.FromSeconds(15));
        _falsa.Dispose();
    }

    static async Task Esperar(Func<bool> condicao, int segundos = 40)
    {
        var limite = DateTime.Now.AddSeconds(segundos);
        while (!condicao() && DateTime.Now < limite)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(30);
        }
        Dispatcher.UIThread.RunJobs();
        Assert.True(condicao(), "a condição não aconteceu a tempo");
    }

    async Task<MaquinaNaNuvem> PrepararAsync(string repositorio, bool permitir)
    {
        _antes = App.Nuvem;
        _projetosAntes = App.Config.ProjetosNaNuvem;
        App.Config.ProjetosNaNuvem = permitir ? [Config.ChaveDePasta(repositorio)] : [];
        App.Nuvem = new ServicoDaNuvem(Path.Combine(_falsa.Raiz, "app"), new ArmazemNaMemoria(), _falsa.Raiz);
        App.Nuvem.GuardarTokenDoClaude("tk-da-tela");
        var maquina = App.Nuvem.Salvar(new MaquinaNaNuvem("casa", "VPS de teste", TiposDeMaquina.Teste));
        var relatorio = await Task.Run(() => App.Nuvem.TestarAsync(maquina));
        Assert.True(relatorio.Conectou, relatorio.Erro);
        return App.Nuvem.Maquina("casa")!;
    }

    [Fact]
    public Task Projeto_que_nao_permite_nunca_oferece_a_nuvem() => Sessao.Rodar(async () =>
    {
        if (!MaquinaFalsa.Disponivel) return;
        var repositorio = await _falsa.RepositorioAsync("fechado");
        await PrepararAsync(repositorio, permitir: false);
        var chat = ChatVM.Nova(new Local("fechado", repositorio, []), new AnfitriaoFalso(), _ => { }, _ => { });
        Assert.False(chat.MostraOndeRoda);
        Assert.False(chat.NaNuvem);
        Assert.Equal("Neste PC", chat.OndeRodaRotulo);
        App.Config.ProjetosNaNuvem = [Config.ChaveDePasta(repositorio)];
        Assert.True(chat.MostraOndeRoda);
    });

    [Fact]
    public Task Conversa_na_nuvem_roda_la_mostra_o_selo_reabre_e_traz_a_branch() => Sessao.Rodar(async () =>
    {
        if (!MaquinaFalsa.Disponivel) return;
        var repositorio = await _falsa.RepositorioAsync("loja");
        File.WriteAllText(Path.Combine(repositorio, "rascunho.txt"), "ainda sem commit\n");
        var maquina = await PrepararAsync(repositorio, permitir: true);
        var anfitriao = new AnfitriaoFalso();
        var chat = ChatVM.Nova(new Local("loja", repositorio, []), anfitriao, _ => { }, _ => { });
        var janela = new Window { Width = 1200, Height = 820, Content = new ChatView { DataContext = chat } };
        janela.Show();
        Dispatcher.UIThread.RunJobs();

        var chip = janela.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "BotaoOndeRoda");
        Assert.True(chip.IsVisible);
        Assert.Equal("Neste PC", chat.OndeRodaRotulo);
        chat.AbrirOndeRodaCommand.Execute(null);
        Assert.True(chat.MenuOndeRodaAberto);
        Assert.Equal(2, chat.OpcoesOndeRoda.Count);
        Assert.True(chat.OpcoesOndeRoda[0].Selecionada);
        chat.OpcoesOndeRoda[1].EscolherCommand.Execute(null);
        Assert.True(chat.NaNuvem);
        Assert.Equal("Nuvem · VPS de teste", chat.OndeRodaRotulo);

        chat.EnviarTexto("oi");
        await Esperar(() => chat.Itens.OfType<ItemTexto>().Any(t => t.Texto.Contains("eco: oi")));
        var resposta = chat.Itens.OfType<ItemTexto>().First(t => t.Texto.Contains("eco: oi")).Texto;
        Assert.Contains("token=tk-da-tela", resposta);
        Assert.Contains("rascunho.txt", resposta);
        await Esperar(() => chat.EstadoNuvem == EstadoDaNuvem.Rodando);
        Assert.True(chat.MostraFaixaNuvem);
        Assert.Contains("Conectado a VPS de teste", chat.TextoNuvem);
        Assert.True(chat.PodeEncerrarNaMaquina);
        Assert.False(chat.PodeTrocarOndeRoda);
        Assert.False(chat.PodeHibernar);
        Assert.Equal("sessao-falsa-1", chat.SessaoId);
        await Esperar(() => App.Nuvem.Execucao("sessao-falsa-1") is not null);
        var execucao = App.Nuvem.Execucao("sessao-falsa-1")!;
        Assert.Equal(maquina.Id, execucao.Maquina);
        Assert.Contains("/nexora/loja/main", execucao.PastaRemota);
        Assert.True(janela.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("Conectado a VPS de teste") == true));

        var projetos = Path.Combine(_falsa.Casa, ".claude", "projects", "loja");
        Directory.CreateDirectory(projetos);
        File.WriteAllText(Path.Combine(projetos, "sessao-falsa-1.jsonl"),
            """{"type":"user","message":{"role":"user","content":"oi"},"sessionId":"sessao-falsa-1","cwd":"/x"}""" + "\n" +
            """{"type":"assistant","message":{"id":"m1","role":"assistant","content":[{"type":"text","text":"eco: oi do historico"}]},"sessionId":"sessao-falsa-1"}""" + "\n");
        var reaberta = ChatVM.DaNuvem(execucao, new Local("loja", repositorio, []), anfitriao, _ => { }, _ => { });
        await reaberta.Carregamento;
        await Esperar(() => !reaberta.Carregando);
        Assert.True(reaberta.NaNuvem);
        Assert.False(reaberta.MostraFaixaNuvem);
        var ocioso = EstadoDaExecucao.Ler((await Task.Run(() => _falsa.Maquina.RodarAsync(ScriptRemoto.Chamar("estado", execucao.Execucao)))).Saida);
        Assert.False(ocioso.Rodando);
        Assert.Contains(reaberta.Itens.OfType<ItemUsuario>(), u => u.Texto == "oi");
        Assert.Contains(reaberta.Itens.OfType<ItemTexto>(), t => t.Texto == "eco: oi do historico");
        chat.Encerrar(esperar: true);
        reaberta.EnviarTexto("de novo");
        await Esperar(() => reaberta.Itens.OfType<ItemTexto>().Any(t => t.Texto.Contains("eco: de novo")));

        var remota = execucao.PastaRemota;
        var feito = await Task.Run(() => _falsa.Maquina.RodarAsync($"cd {Shell.Aspas(remota)} && echo da-nuvem > feito-la.txt"));
        Assert.True(feito.Ok, feito.Erros);
        await reaberta.TrazerDaNuvemCommand.ExecuteAsync(null);
        var (local, _) = Assert.Single(anfitriao.Abertas);
        Assert.Equal("main-nuvem", Core.Git.Repositorio.BranchAtual(local.Pasta));
        Assert.Equal("da-nuvem", File.ReadAllText(Path.Combine(local.Pasta, "feito-la.txt")).Trim());
        Assert.Equal("ainda sem commit", File.ReadAllText(Path.Combine(local.Pasta, "rascunho.txt")).Trim());
        Assert.Contains(reaberta.Itens.OfType<ItemAviso>(), a => a.Texto.Contains("Trouxe o código da nuvem"));

        reaberta.Encerrar();
        await Esperar(() => App.Nuvem.Execucao("sessao-falsa-1") is null);
        var estado = EstadoDaExecucao.Ler((await Task.Run(() => _falsa.Maquina.RodarAsync(ScriptRemoto.Chamar("estado", execucao.Execucao)))).Saida);
        Assert.False(estado.Rodando);
        janela.Close();
        await Task.Run(() => Core.Git.ComandoGit.ExecutarAsync(repositorio, "worktree", "remove", "--force", local.Pasta));
    });

    [Fact]
    public Task Encerrar_na_maquina_para_o_agente_e_a_proxima_mensagem_continua() => Sessao.Rodar(async () =>
    {
        if (!MaquinaFalsa.Disponivel) return;
        var repositorio = await _falsa.RepositorioAsync("parar");
        var maquina = await PrepararAsync(repositorio, permitir: true);
        var chat = ChatVM.Nova(new Local("parar", repositorio, []), new AnfitriaoFalso(), _ => { }, _ => { });
        chat.EscolherOndeRoda(maquina.Id);
        Assert.False(chat.PodeEncerrarNaMaquina);

        chat.EnviarTexto("primeira");
        await Esperar(() => chat.Itens.OfType<ItemTexto>().Any(t => t.Texto.Contains("eco: primeira")));
        await Esperar(() => chat.EstadoNuvem == EstadoDaNuvem.Rodando && chat.Estado != EstadoChat.Trabalhando);
        Assert.True(chat.PodeEncerrarNaMaquina);
        Assert.DoesNotContain("Pode desligar", chat.TextoNuvem);
        await Esperar(() => chat.ExecucaoNaNuvem is not null);
        var execucao = chat.ExecucaoNaNuvem!;

        chat.EncerrarNaMaquinaCommand.Execute(null);
        Assert.False(chat.MostraFaixaNuvem);
        Assert.False(chat.PodeEncerrarNaMaquina);
        Assert.Contains(chat.Itens.OfType<ItemAviso>(), a => a.Texto.Contains("foi encerrado"));
        await Esperar(() => !EstadoDaExecucao.Ler(_falsa.Maquina.RodarAsync(ScriptRemoto.Chamar("estado", execucao)).GetAwaiter().GetResult().Saida).Rodando);

        chat.EnviarTexto("segunda");
        await Esperar(() => chat.Itens.OfType<ItemTexto>().Any(t => t.Texto.Contains("eco: segunda")));
        Assert.DoesNotContain(chat.Itens.OfType<ItemAviso>(), a => a.Texto.Contains("erro", StringComparison.OrdinalIgnoreCase));
        chat.Encerrar();
    });

    [Fact]
    public Task Configuracoes_mostram_as_maquinas_a_chave_e_o_token_sem_o_valor() => Sessao.Rodar(async () =>
    {
        if (!MaquinaFalsa.Disponivel) return;
        var repositorio = await _falsa.RepositorioAsync("cfg");
        await PrepararAsync(repositorio, permitir: true);
        var vm = new MaquinasNaNuvemVM(App.Nuvem);
        var janela = new Window { Width = 1000, Height = 900, Content = new ContentControl { Content = vm, ContentTemplate = (Avalonia.Controls.Templates.IDataTemplate)App.Current!.FindResource("MaquinasNaNuvem")! } };
        janela.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.True(vm.TemToken);
        Assert.Equal("", vm.Token);
        Assert.DoesNotContain(janela.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("tk-da-tela") == true);
        Assert.DoesNotContain(janela.GetVisualDescendants().OfType<TextBox>(), t => t.Text?.Contains("tk-da-tela") == true);
        Assert.True(janela.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Freestyle"));
        Assert.Empty(vm.Maquinas);
        vm.ApagarTokenCommand.Execute(null);
        Assert.False(vm.TemToken);
        vm.Token = "tk-novo";
        vm.SalvarTokenCommand.Execute(null);
        Assert.True(vm.TemToken);
        Assert.Equal("", vm.Token);
        janela.Close();
    });
}
