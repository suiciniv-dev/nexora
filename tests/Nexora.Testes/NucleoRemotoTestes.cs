using Nexora.Core.Claude;
using Nexora.Core.Historico;
using Nexora.Core.Servidor;
using Nexora.Core.Uso;

namespace Nexora.Testes;

public sealed class NucleoRemotoTestes
{
    sealed class NucleoFalso : INucleo
    {
        public bool FalharAndamento { get; set; }

        public Task<IReadOnlyList<ConversaSalva>> ListarConversasAsync(CancellationToken cancelar = default) =>
            Task.FromResult<IReadOnlyList<ConversaSalva>>(
            [
                new ConversaSalva("s1", @"C:\c\s1.jsonl", "Cupom", @"D:\Projetos\Loja", "cupom", new DateTime(2026, 11, 17, 10, 0, 0, DateTimeKind.Local), "u1", "u9"),
                new ConversaSalva("s2", @"C:\c\s2.jsonl", "Testes", null, null, new DateTime(2026, 11, 16, 9, 30, 0, DateTimeKind.Local)),
            ]);

        public Task<IReadOnlyList<Parcela>> LerUsoAsync(string? cache, DateTime desde, bool porDia, CancellationToken cancelar = default) =>
            Task.FromResult<IReadOnlyList<Parcela>>(
            [
                new Parcela { Modelo = porDia ? "opus" : "sonnet", Pasta = cache ?? "", Dia = desde.ToString("yyyy-MM-dd"), Entrada = 10, Saida = 20, Peso = 1.5, Respostas = 2, Sessoes = ["a", "b"], Modelos = { ["opus"] = 1.5 } },
            ]);

        public Task<Andamento> LerAndamentoAsync(string arquivo, string? pasta, CancellationToken cancelar = default) =>
            FalharAndamento
                ? throw new IOException("arquivo sumiu")
                : Task.FromResult(new Andamento("Cupom", "main", "Lendo " + arquivo, new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero), new ContextoUsado(1234, true, "opus")));

        public Task<TrechoBruto> LerTurnosAsync(string arquivo, int turnos, long ate, CancellationToken cancelar = default) =>
            NucleoLocal.Instancia.LerTurnosAsync(arquivo, turnos, ate, cancelar);

        public Task<string?> UltimoModoAsync(string arquivo, CancellationToken cancelar = default) =>
            NucleoLocal.Instancia.UltimoModoAsync(arquivo, cancelar);

        public int Comandos;
        public TaskCompletionSource? SegurarComando { get; set; }

        public async Task<Nexora.Core.Git.ResultadoGit> RodarComandoAsync(Nexora.Core.Git.PedidoDeComando pedido, CancellationToken cancelar = default)
        {
            Interlocked.Increment(ref Comandos);
            if (SegurarComando is { } segurar) await segurar.Task;
            return await NucleoLocal.Instancia.RodarComandoAsync(pedido, cancelar);
        }
    }

    static string Cano() => "nexora-teste-" + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task Pagina_do_historico_lida_no_servidor_e_igual_a_lida_aqui()
    {
        var cano = Cano();
        _ = Task.Run(() => ServidorDoNucleo.RodarAsync(cano, new NucleoFalso(), TimeSpan.FromSeconds(10)));
        using var remoto = await NucleoRemoto.ConectarAsync(cano, TimeSpan.FromSeconds(10));
        Assert.NotNull(remoto);
        var arquivo = Path.Combine(Directory.CreateTempSubdirectory("nexora-hist-").FullName, "longa.jsonl");
        TranscricaoDeExemplo.Escrever(arquivo, 40);

        var aqui = Transcricao.LerTurnos(arquivo, 12);
        var bruto = await remoto.LerTurnosAsync(arquivo, 12, long.MaxValue);
        var la = Transcricao.DeBruto(bruto);
        var anterior = Transcricao.DeBruto(await remoto.LerTurnosAsync(arquivo, 12, la.Inicio));

        Assert.Equal((aqui.Inicio, aqui.Turnos, aqui.Eventos.Count), (la.Inicio, la.Turnos, la.Eventos.Count));
        Assert.Equal(aqui.Eventos.Select(e => e.GetType().Name), la.Eventos.Select(e => e.GetType().Name));
        Assert.Equal(12, anterior.Turnos);
        Assert.True(anterior.Inicio < la.Inicio);
        Assert.Equal(await NucleoLocal.Instancia.UltimoModoAsync(arquivo), await remoto.UltimoModoAsync(arquivo));
    }

    [Fact]
    public async Task Comando_git_roda_no_servidor_e_volta_com_saida_e_codigo()
    {
        var falso = new NucleoFalso();
        var cano = Cano();
        _ = Task.Run(() => ServidorDoNucleo.RodarAsync(cano, falso, TimeSpan.FromSeconds(10)));
        using var remoto = await NucleoRemoto.ConectarAsync(cano, TimeSpan.FromSeconds(10));
        Assert.NotNull(remoto);
        var pasta = Directory.CreateTempSubdirectory("nexora-git-").FullName;

        var versao = await remoto.RodarComandoAsync(new Nexora.Core.Git.PedidoDeComando("git", pasta, ["--version"], TimeSpan.FromSeconds(20), new Dictionary<string, string> { ["LANG"] = "C" }, null));
        var falhou = await remoto.RodarComandoAsync(new Nexora.Core.Git.PedidoDeComando("git", pasta, ["rev-parse", "HEAD"], null, null, null));

        Assert.True(versao.Ok, versao.Mensagem);
        Assert.StartsWith("git version", versao.Saida);
        Assert.False(falhou.Ok);
        Assert.NotEmpty(falhou.Erro);
        Assert.Equal(2, falso.Comandos);
    }

    [Fact]
    public async Task Comando_que_chegou_ao_servidor_nao_e_repetido_se_a_resposta_se_perde()
    {
        var falso = new NucleoFalso { SegurarComando = new TaskCompletionSource() };
        var cano = Cano();
        using var fim = new CancellationTokenSource();
        var servidor = Task.Run(() => ServidorDoNucleo.RodarAsync(cano, falso, TimeSpan.FromSeconds(10), fim.Token));
        using var remoto = await NucleoRemoto.ConectarAsync(cano, TimeSpan.FromSeconds(10));
        Assert.NotNull(remoto);

        var comando = remoto.RodarComandoAsync(new Nexora.Core.Git.PedidoDeComando("gh", Path.GetTempPath(), ["pr", "create"], null, null, null));
        while (Volatile.Read(ref falso.Comandos) == 0) await Task.Delay(20);
        fim.Cancel();
        var resultado = await comando.WaitAsync(TimeSpan.FromSeconds(20));

        Assert.False(resultado.Ok);
        Assert.Contains("parou no meio", resultado.Erro);
        Assert.Equal(1, falso.Comandos);
        falso.SegurarComando.TrySetResult();
    }

    [Fact]
    public async Task Os_tres_pedidos_vao_e_voltam_pelo_cano_sem_perder_nada()
    {
        var falso = new NucleoFalso();
        var cano = Cano();
        var servidor = Task.Run(() => ServidorDoNucleo.RodarAsync(cano, falso, TimeSpan.FromSeconds(2)));
        using var remoto = await NucleoRemoto.ConectarAsync(cano, TimeSpan.FromSeconds(10));
        Assert.NotNull(remoto);

        Assert.Equal(await falso.ListarConversasAsync(), await remoto.ListarConversasAsync());
        var parcela = Assert.Single(await remoto.LerUsoAsync("cache.json", new DateTime(2026, 11, 1), porDia: true));
        Assert.Equal(("opus", "cache.json", "2026-11-01", 10L, 20L, 1.5, 2), (parcela.Modelo, parcela.Pasta, parcela.Dia, parcela.Entrada, parcela.Saida, parcela.Peso, parcela.Respostas));
        Assert.Equal(["a", "b"], parcela.Sessoes.Order());
        Assert.Equal(1.5, parcela.Modelos["opus"]);
        Assert.Equal(await falso.LerAndamentoAsync("x.jsonl", null), await remoto.LerAndamentoAsync("x.jsonl", null));

        var juntos = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => remoto.LerAndamentoAsync($"a{i}.jsonl", null)));
        Assert.Equal(Enumerable.Range(0, 20).Select(i => $"Lendo a{i}.jsonl"), juntos.Select(a => a.Atividade));
        Assert.True(remoto.Conectado);

        remoto.Dispose();
        Assert.Equal(0, await servidor.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task Erro_no_servidor_cai_para_o_local_sem_derrubar_a_conexao()
    {
        var falso = new NucleoFalso { FalharAndamento = true };
        var cano = Cano();
        _ = Task.Run(() => ServidorDoNucleo.RodarAsync(cano, falso, TimeSpan.FromSeconds(10)));
        using var remoto = await NucleoRemoto.ConectarAsync(cano, TimeSpan.FromSeconds(10));
        Assert.NotNull(remoto);

        var arquivo = Path.Combine(Path.GetTempPath(), "nexora-nao-existe-" + Guid.NewGuid().ToString("N") + ".jsonl");
        var andamento = await remoto.LerAndamentoAsync(arquivo, null);

        Assert.Null(andamento.Titulo);
        Assert.True(remoto.Conectado);
        Assert.Equal(2, (await remoto.ListarConversasAsync()).Count);
    }

    [Fact]
    public async Task Servidor_que_cai_avisa_e_os_pedidos_seguem_pelo_local()
    {
        var cano = Cano();
        using var fim = new CancellationTokenSource();
        var servidor = Task.Run(() => ServidorDoNucleo.RodarAsync(cano, new NucleoFalso(), TimeSpan.FromSeconds(10), fim.Token));
        using var remoto = await NucleoRemoto.ConectarAsync(cano, TimeSpan.FromSeconds(10));
        Assert.NotNull(remoto);
        var caiu = new TaskCompletionSource();
        remoto.Caiu += () => caiu.TrySetResult();
        Assert.Equal(2, (await remoto.ListarConversasAsync()).Count);

        fim.Cancel();
        await servidor.WaitAsync(TimeSpan.FromSeconds(10));
        await caiu.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(remoto.Conectado);
        Assert.NotNull(await remoto.ListarConversasAsync());
    }

    [Fact]
    public async Task O_proprio_Nexora_sobe_como_servidor_e_sai_pouco_depois_que_o_app_desconecta()
    {
        var cano = Cano();
        var info = new System.Diagnostics.ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add(typeof(App).Assembly.Location);
        info.ArgumentList.Add(ServidorDoNucleo.Argumento);
        info.ArgumentList.Add(cano);
        info.ArgumentList.Add("1");
        using var processo = System.Diagnostics.Process.Start(info)!;
        try
        {
            using (var remoto = await NucleoRemoto.ConectarAsync(cano, TimeSpan.FromSeconds(20)))
            {
                Assert.NotNull(remoto);
                Assert.NotNull(await remoto.ListarConversasAsync());
                Assert.True(remoto.Conectado);
            }
            await processo.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(0, processo.ExitCode);
        }
        finally
        {
            if (!processo.HasExited) processo.Kill();
        }
    }

    [Fact]
    public async Task Sem_app_o_servidor_desiste_e_sai()
    {
        var codigo = await ServidorDoNucleo.RodarAsync(Cano(), new NucleoFalso(), TimeSpan.FromMilliseconds(300));
        Assert.Equal(1, codigo);
        Assert.Null(await NucleoRemoto.ConectarAsync(Cano(), TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public async Task Dois_apps_usam_o_mesmo_servidor_e_ele_so_sai_depois_que_os_dois_saem()
    {
        var cano = Cano();
        var servidor = Task.Run(() => ServidorDoNucleo.RodarAsync(cano, new NucleoFalso(), TimeSpan.FromMilliseconds(500)));
        var primeiro = await NucleoRemoto.ConectarAsync(cano, TimeSpan.FromSeconds(10));
        using var segundo = await NucleoRemoto.ConectarAsync(cano, TimeSpan.FromSeconds(10));
        Assert.NotNull(primeiro);
        Assert.NotNull(segundo);
        Assert.Equal(2, (await primeiro.ListarConversasAsync()).Count);

        primeiro.Dispose();
        await Task.Delay(1200);

        Assert.False(servidor.IsCompleted);
        Assert.Equal(2, (await segundo.ListarConversasAsync()).Count);
        Assert.True(segundo.Conectado);
        segundo.Dispose();
        Assert.Equal(0, await servidor.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task Segundo_servidor_com_o_mesmo_nome_desiste_e_o_primeiro_segue()
    {
        var cano = Cano();
        using var fim = new CancellationTokenSource();
        var primeiro = Task.Run(() => ServidorDoNucleo.RodarAsync(cano, new NucleoFalso(), TimeSpan.FromSeconds(10), fim.Token));
        using var remoto = await NucleoRemoto.ConectarAsync(cano, TimeSpan.FromSeconds(10));
        Assert.NotNull(remoto);

        var segundo = await ServidorDoNucleo.RodarAsync(cano, new NucleoFalso(), TimeSpan.FromSeconds(10)).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(ServidorDoNucleo.OutroServidor, segundo);
        Assert.Equal(2, (await remoto.ListarConversasAsync()).Count);
        fim.Cancel();
        await primeiro.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Sessao_viva_segura_o_servidor_mesmo_sem_app()
    {
        var cano = Cano();
        var ocupado = 1;
        var servidor = Task.Run(() => ServidorDoNucleo.RodarAsync(cano, new NucleoFalso(), TimeSpan.FromMilliseconds(200), ocupado: () => Volatile.Read(ref ocupado) == 1));
        using (var remoto = await NucleoRemoto.ConectarAsync(cano, TimeSpan.FromSeconds(10)))
            Assert.NotNull(remoto);

        await Task.Delay(1000);
        Assert.False(servidor.IsCompleted);
        using (var outro = await NucleoRemoto.ConectarAsync(cano, TimeSpan.FromSeconds(5)))
            Assert.NotNull(outro);

        Volatile.Write(ref ocupado, 0);
        Assert.Equal(0, await servidor.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void O_nome_do_cano_muda_com_usuario_pasta_e_versao_mas_nao_com_a_grafia_da_pasta()
    {
        var versao = Guid.NewGuid();
        var nome = ServidorDoNucleo.NomeDoCano("vini", @"C:\Users\vini\AppData\Roaming\Nexora", versao);

        Assert.StartsWith("nexora-nucleo-", nome);
        Assert.True(nome.Length < 60);
        Assert.Equal(nome, ServidorDoNucleo.NomeDoCano("Vini", @"c:\users\vini\appdata\roaming\nexora\", versao));
        Assert.NotEqual(nome, ServidorDoNucleo.NomeDoCano("outro", @"C:\Users\vini\AppData\Roaming\Nexora", versao));
        Assert.NotEqual(nome, ServidorDoNucleo.NomeDoCano("vini", @"D:\Isolada\Nexora", versao));
        Assert.NotEqual(nome, ServidorDoNucleo.NomeDoCano("vini", @"C:\Users\vini\AppData\Roaming\Nexora", Guid.NewGuid()));
    }
}
