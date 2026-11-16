using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Nexora.Core.Claude;
using Nexora.Core.Nuvem;
using Nexora.Core.Segredos;

namespace Nexora.Testes.Nuvem;

public sealed class NuvemTestes : IDisposable
{
    readonly MaquinaFalsa _falsa = new();
    readonly List<string> _execucoes = [];

    public NuvemTestes() => ProcessoRemoto.Esperas = [TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(500)];

    public void Dispose()
    {
        foreach (var id in _execucoes)
            try
            {
                _falsa.Maquina.RodarAsync(ScriptRemoto.Chamar("parar", id)).Wait(TimeSpan.FromSeconds(15));
            }
            catch (AggregateException)
            {
            }
        _falsa.Dispose();
    }

    string NovaExecucao()
    {
        var id = ServicoDaNuvem.NovaExecucao();
        _execucoes.Add(id);
        return id;
    }

    [Fact]
    public void Aspas_do_shell_protegem_espacos_aspas_e_cifrao()
    {
        Assert.Equal("claude", Shell.Aspas("claude"));
        Assert.Equal("--model=opus", Shell.Aspas("--model=opus"));
        Assert.Equal("'a b'", Shell.Aspas("a b"));
        Assert.Equal("'it'\\''s $HOME'", Shell.Aspas("it's $HOME"));
        Assert.Equal("''", Shell.Aspas(""));
        Assert.Equal("\"$HOME\"/'nexora/x y'", Shell.Caminho("~/nexora/x y"));
    }

    [Fact]
    public void Comando_remoto_tira_o_caminho_local_e_a_extensao()
    {
        var info = new ProcessStartInfo(@"C:\Users\x\.local\bin\claude.exe");
        info.ArgumentList.Add("-p");
        info.ArgumentList.Add("--append-system-prompt");
        info.ArgumentList.Add("Diga 'oi'\nem duas linhas");
        Assert.Equal("exec claude -p --append-system-prompt 'Diga '\\''oi'\\''\nem duas linhas'\n", LancadorRemoto.Comando(info));
        var cmd = new ProcessStartInfo("cmd.exe");
        foreach (var a in new[] { "/c", @"C:\npm\gemini.cmd", "--acp" }) cmd.ArgumentList.Add(a);
        Assert.Equal("exec gemini --acp\n", LancadorRemoto.Comando(cmd));
    }

    [Fact]
    public void Ssh_usa_modo_sem_pergunta_a_chave_do_nexora_e_a_porta()
    {
        var maquina = new MaquinaNaNuvem("m", "VPS", TiposDeMaquina.Ssh, "vps.exemplo", "vini", 2222);
        var args = new TransporteSsh(maquina, @"C:\chave", @"C:\conhecidos").Argumentos("echo oi");
        Assert.Contains("BatchMode=yes", args);
        Assert.Contains("StrictHostKeyChecking=accept-new", args);
        Assert.Contains("UserKnownHostsFile=" + @"C:\conhecidos", args);
        Assert.Equal(@"C:\chave", args[args.ToList().IndexOf("-i") + 1]);
        Assert.Equal("2222", args[args.ToList().IndexOf("-p") + 1]);
        Assert.Equal("vini@vps.exemplo", args[^2]);
        Assert.Equal("echo oi", args[^1]);
        Assert.Equal("vini@vps.exemplo:2222", maquina.Endereco);
    }

    [Fact]
    public void Relatorio_le_o_que_a_maquina_tem()
    {
        var r = RelatorioDaMaquina.Ler("sistema=Linux 6.1 x86_64\nhome=/home/vini\ngit=git version 2.43\nnode=v22.1.0\nclaude=2.1.300 (Claude Code)\ncodex=\ntmux=\n");
        Assert.True(r.Conectou);
        Assert.Equal("/home/vini", r.Home);
        Assert.True(r.Tem("claude"));
        Assert.False(r.Tem("codex"));
        Assert.Equal("Linux · git · node · claude", ServicoDaNuvem.Resumir(r));
        Assert.Equal(new EstadoDaExecucao("rodando", 0, 120), EstadoDaExecucao.Ler("rodando 0 120\n"));
        Assert.True(EstadoDaExecucao.Ler("fim 3 40").Terminou);
    }

    [Fact]
    public async Task Gera_a_chave_ed25519_e_mostra_so_a_linha_publica()
    {
        if (!File.Exists(ChavesSsh.GeradorPadrao)) return;
        var chave = Path.Combine(_falsa.Raiz, "chaves", "id_ed25519");
        var publica = await ChavesSsh.GarantirAsync(chave);
        Assert.StartsWith("ssh-ed25519 ", publica);
        Assert.Contains("nexora@", publica);
        Assert.Equal(publica, await ChavesSsh.GarantirAsync(chave));
        Assert.Contains("PRIVATE KEY", File.ReadAllText(chave));
    }

    [Fact]
    public void Token_do_claude_vai_so_para_o_ambiente_do_claude()
    {
        var servico = new ServicoDaNuvem(Path.Combine(_falsa.Raiz, "app"), new ArmazemNaMemoria());
        Assert.False(servico.TemTokenDoClaude);
        Assert.Throws<ArgumentException>(() => servico.GuardarTokenDoClaude("dois tokens"));
        servico.GuardarTokenDoClaude(" sk-ant-oat01-abc \n");
        Assert.True(servico.TemTokenDoClaude);
        Assert.Equal("sk-ant-oat01-abc", servico.AmbienteDoAgente(Agentes.Claude)[ServicoDaNuvem.VariavelDoClaude]);
        Assert.False(servico.AmbienteDoAgente(Agentes.Codex).ContainsKey(ServicoDaNuvem.VariavelDoClaude));
        Assert.DoesNotContain("sk-ant", File.Exists(Path.Combine(_falsa.Raiz, "app", "nuvem.json")) ? File.ReadAllText(Path.Combine(_falsa.Raiz, "app", "nuvem.json")) : "");
        var maquina = servico.Salvar(new MaquinaNaNuvem("m1", "VPS", TiposDeMaquina.Ssh, "h", "u"));
        servico.Lembrar(new ExecucaoSalva("e1", "m1", "/home/u/nexora/p/main", @"D:\p", "main", Agentes.Claude, "s1", DateTime.Now));
        var relida = new ServicoDaNuvem(Path.Combine(_falsa.Raiz, "app"), new ArmazemNaMemoria());
        Assert.Equal(maquina, relida.Maquina("m1"));
        Assert.Equal("e1", relida.Execucao("s1")!.Execucao);
        Assert.DoesNotContain("sk-ant", File.ReadAllText(Path.Combine(_falsa.Raiz, "app", "nuvem.json")));
        relida.Apagar("m1");
        Assert.Null(relida.Execucao("s1"));
    }

    [Fact]
    public async Task Testar_conexao_instala_o_ajudante_e_conta_o_que_tem_la()
    {
        if (!MaquinaFalsa.Disponivel) return;
        var relatorio = await _falsa.Maquina.TestarAsync();
        Assert.True(relatorio.Conectou, relatorio.Erro);
        Assert.True(relatorio.Tem("git"));
        Assert.True(relatorio.Tem("claude"));
        Assert.False(string.IsNullOrEmpty(relatorio.Home));
        Assert.True(File.Exists(Path.Combine(_falsa.Casa, ".nexora", "bin", "nexora-remoto")));
    }

    [Fact]
    public async Task Agente_desacoplado_sobrevive_a_queda_e_o_leitor_continua_do_byte_onde_parou()
    {
        if (!MaquinaFalsa.Disponivel) return;
        var home = await _falsa.HomeAsync();
        var id = NovaExecucao();
        var plano = new PlanoDaExecucao(id, home, new Dictionary<string, string> { ["SEGREDO_DE_TESTE"] = "valor-secreto" });
        var lancador = new LancadorRemoto(_falsa.Maquina, plano);
        var estados = new ConcurrentQueue<EstadoDaNuvem>();
        lancador.Mudou += m => estados.Enqueue(m.Estado);
        var info = new ProcessStartInfo("sh");
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("echo \"pronto $SEGREDO_DE_TESTE\"; while IFS= read -r l; do case \"$l\" in devagar*) sleep 3;; esac; echo \"eco:$l\"; [ \"$l\" = sair ] && exit 7; done");
        var processo = (ProcessoRemoto)lancador.Abrir(info);
        var linhas = new BlockingCollection<string>();
        _ = Task.Run(async () =>
        {
            while (await processo.StandardOutput.ReadLineAsync() is { } l) linhas.Add(l);
            linhas.CompleteAdding();
        });
        Assert.Equal("pronto valor-secreto", Proxima(linhas));
        Assert.DoesNotContain("valor-secreto", Directory.EnumerateFiles(_falsa.Execucao(id)).Select(MaquinaFalsa.LerSolto).Where(t => !t.StartsWith("pronto")));
        await processo.StandardInput.WriteAsync("um\n");
        await processo.StandardInput.FlushAsync();
        Assert.Equal("eco:um", Proxima(linhas));
        await processo.StandardInput.WriteAsync("devagar\n");
        await processo.StandardInput.FlushAsync();
        await Task.Delay(800);
        processo.DerrubarConexao();
        for (var i = 0; i < 50 && !estados.Contains(EstadoDaNuvem.Desconectado); i++) await Task.Delay(100);
        Assert.Contains(EstadoDaNuvem.Desconectado, estados);
        Assert.False(processo.HasExited);
        Assert.Equal("eco:devagar", Proxima(linhas, 20));
        await processo.StandardInput.WriteAsync("dois\nsair\n");
        await processo.StandardInput.FlushAsync();
        Assert.Equal("eco:dois", Proxima(linhas));
        Assert.Equal("eco:sair", Proxima(linhas));
        Assert.True(processo.WaitForExit(20000));
        Assert.Equal(7, processo.ExitCode);
        Assert.Equal(new FileInfo(Path.Combine(_falsa.Execucao(id), "saida")).Length, processo.Lidos);
        Assert.Contains(EstadoDaNuvem.Rodando, estados);
    }

    [Fact]
    public async Task Fechar_o_nexora_solta_o_agente_e_outra_conexao_reencontra_no_inicio_do_turno()
    {
        if (!MaquinaFalsa.Disponivel) return;
        var home = await _falsa.HomeAsync();
        var id = NovaExecucao();
        var plano = new PlanoDaExecucao(id, home, new Dictionary<string, string>());
        var info = new ProcessStartInfo("claude");
        var primeiro = (ProcessoRemoto)new LancadorRemoto(_falsa.Maquina, plano).Abrir(info);
        var linhas = Ler(primeiro);
        await primeiro.StandardInput.WriteAsync(Protocolo.Mensagem("ola", null) + "\n");
        await primeiro.StandardInput.FlushAsync();
        Assert.Contains("eco: ola", Esperar(linhas, l => l.Contains("\"assistant\"")));
        Esperar(linhas, l => l.Contains("\"result\""));
        await primeiro.StandardInput.WriteAsync(Protocolo.Mensagem("devagar tchau", null) + "\n");
        await primeiro.StandardInput.FlushAsync();
        Esperar(linhas, l => l.Contains("\"init\""));
        primeiro.Kill(true);
        Assert.True(primeiro.HasExited);
        var estado = EstadoDaExecucao.Ler((await _falsa.Maquina.RodarAsync(ScriptRemoto.Chamar("estado", id))).Saida);
        Assert.True(estado.Rodando);
        var segundo = (ProcessoRemoto)new LancadorRemoto(_falsa.Maquina, plano).Abrir(info);
        var depois = Ler(segundo);
        var primeiraDepois = Proxima(depois, 20);
        Assert.Contains("\"init\"", primeiraDepois);
        Assert.Contains("eco: devagar tchau", Esperar(depois, l => l.Contains("\"assistant\""), 20));
        await segundo.PararNaMaquinaAsync();
        Assert.True(segundo.WaitForExit(20000));
    }

    [Fact]
    public async Task Sessao_do_claude_roda_na_maquina_com_o_codigo_sincronizado_e_o_token_so_no_processo()
    {
        if (!MaquinaFalsa.Disponivel) return;
        var repositorio = await _falsa.RepositorioAsync("projeto");
        File.WriteAllText(Path.Combine(repositorio, "novo.txt"), "sem commit\n");
        File.WriteAllText(Path.Combine(repositorio, "ignorado.txt"), "nao vai\n");
        var home = await _falsa.HomeAsync();
        var remota = CodigoNaNuvem.PastaRemota(home, repositorio, "main");
        var id = NovaExecucao();
        var plano = new PlanoDaExecucao(id, remota, new Dictionary<string, string> { [ServicoDaNuvem.VariavelDoClaude] = "tk-123" },
            (avisar, token) => CodigoNaNuvem.EnviarAsync(_falsa.Maquina, repositorio, remota, null, token));
        var lancador = new LancadorRemoto(_falsa.Maquina, plano);
        var estados = new ConcurrentQueue<EstadoDaNuvem>();
        lancador.Mudou += m => estados.Enqueue(m.Estado);
        await using var sessao = new SessaoClaude(new OpcoesSessao(remota, Lancador: lancador));
        var textos = new BlockingCollection<string>();
        sessao.Evento += e =>
        {
            if (e is TextoCompleto t) textos.Add(t.Texto);
        };
        sessao.Iniciar();
        sessao.Enviar("oi");
        var texto = Proxima(textos, 60);
        Assert.Contains("eco: oi token=tk-123", texto);
        Assert.Contains("novo.txt", texto);
        Assert.Contains("leia.txt", texto);
        Assert.DoesNotContain("ignorado.txt", texto);
        Assert.Equal("sessao-falsa-1", sessao.SessaoId);
        Assert.Contains(EstadoDaNuvem.Sincronizando, estados);
        Assert.Contains(EstadoDaNuvem.Rodando, estados);
        foreach (var arquivo in Directory.EnumerateFiles(Path.Combine(_falsa.Casa, ".nexora"), "*", SearchOption.AllDirectories).Where(a => !a.EndsWith("saida")))
            Assert.DoesNotContain("tk-123", MaquinaFalsa.LerSolto(arquivo));
        Assert.Equal("sem commit\n", File.ReadAllText(Path.Combine(ParaWindows(remota), "novo.txt")).Replace("\r", ""));
        Assert.Equal("", await MaquinaFalsa.Git(repositorio, "status", "--porcelain", "--", "leia.txt"));
        Assert.Equal("?? novo.txt", await MaquinaFalsa.Git(repositorio, "status", "--porcelain", "--", "novo.txt"));
        Assert.Equal("", await MaquinaFalsa.Git(repositorio, "for-each-ref", "refs/nexora"));
        await lancador.Ultimo!.PararNaMaquinaAsync();
    }

    [Fact]
    public async Task Sincronizar_de_novo_guarda_o_que_a_maquina_tinha_e_trazer_cria_um_worktree_com_as_mudancas()
    {
        if (!MaquinaFalsa.Disponivel) return;
        var repositorio = await _falsa.RepositorioAsync("app");
        var home = await _falsa.HomeAsync();
        var remota = CodigoNaNuvem.PastaRemota(home, repositorio, "main");
        var enviado = await CodigoNaNuvem.EnviarAsync(_falsa.Maquina, repositorio, remota);
        Assert.True(enviado.Enviou);
        Assert.Equal("main", enviado.Branch);
        var deNovo = await CodigoNaNuvem.EnviarAsync(_falsa.Maquina, repositorio, remota);
        Assert.False(deNovo.Enviou);
        var naMaquina = ParaWindows(remota);
        var feito = await _falsa.Maquina.RodarAsync($"cd {Shell.Aspas(remota)} && echo feito > agente.txt && printf 'dois\\n' >> leia.txt && git -c user.name=a -c user.email=a@a commit -q -am 'agente mexeu' && echo solto > solto.txt && rm -f .gitignore");
        Assert.True(feito.Ok, feito.Erros);
        var branch = await CodigoNaNuvem.BranchLivreAsync(repositorio, "main-nuvem");
        var worktree = Path.Combine(_falsa.Raiz, "app-nuvem");
        var trazido = await CodigoNaNuvem.TrazerAsync(_falsa.Maquina, remota, repositorio, worktree, branch);
        Assert.True(trazido.TinhaAlteracoes);
        Assert.Equal("agente mexeu", await MaquinaFalsa.Git(worktree, "log", "-1", "--format=%s"));
        Assert.Equal("um\ndois\n", File.ReadAllText(Path.Combine(worktree, "leia.txt")).Replace("\r", ""));
        var status = (await MaquinaFalsa.Git(worktree, "status", "--porcelain")).Replace("\r", "").Split('\n').Select(l => l.Trim()).OrderBy(l => l, StringComparer.Ordinal).ToList();
        Assert.Equal(["?? agente.txt", "?? solto.txt", "D .gitignore"], status);
        Assert.Equal("", await MaquinaFalsa.Git(repositorio, "for-each-ref", "refs/nexora"));
        File.WriteAllText(Path.Combine(repositorio, "leia.txt"), "local mudou\n");
        var outra = await CodigoNaNuvem.EnviarAsync(_falsa.Maquina, repositorio, remota);
        Assert.True(outra.Enviou);
        Assert.Equal("local mudou\n", File.ReadAllText(Path.Combine(naMaquina, "leia.txt")).Replace("\r", ""));
        Assert.False(File.Exists(Path.Combine(naMaquina, "solto.txt")));
        var pilha = await _falsa.Maquina.RodarAsync($"cd {Shell.Aspas(remota)} && git stash list");
        Assert.Contains("nexora: before sync", pilha.Saida);
        Assert.Equal("main-nuvem-2", await CodigoNaNuvem.BranchLivreAsync(repositorio, "main-nuvem"));
    }

    static string ParaWindows(string caminho) =>
        OperatingSystem.IsWindows() && caminho.Length > 2 && caminho[0] == '/' && caminho[2] == '/'
            ? char.ToUpperInvariant(caminho[1]) + ":" + caminho[2..].Replace('/', '\\')
            : caminho;

    static BlockingCollection<string> Ler(IProcessoDoAgente processo)
    {
        var linhas = new BlockingCollection<string>();
        _ = Task.Run(async () =>
        {
            while (await processo.StandardOutput.ReadLineAsync() is { } l) linhas.Add(l);
            linhas.CompleteAdding();
        });
        return linhas;
    }

    static string Proxima(BlockingCollection<string> linhas, int segundos = 15)
    {
        Assert.True(linhas.TryTake(out var linha, TimeSpan.FromSeconds(segundos)), "nada chegou a tempo");
        return linha!;
    }

    static string Esperar(BlockingCollection<string> linhas, Func<string, bool> condicao, int segundos = 15)
    {
        var limite = DateTime.UtcNow.AddSeconds(segundos);
        while (DateTime.UtcNow < limite)
            if (linhas.TryTake(out var linha, TimeSpan.FromMilliseconds(200)) && condicao(linha)) return linha;
        throw new Xunit.Sdk.XunitException("a linha esperada não chegou");
    }
}
