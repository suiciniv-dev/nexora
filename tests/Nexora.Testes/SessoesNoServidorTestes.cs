using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Claude;
using Nexora.Core.Servidor;

namespace Nexora.Testes;

public sealed class SessoesNoServidorTestes
{
    sealed class SessaoFalsa(OpcoesSessao opcoes) : ISessaoAgente
    {
        public OpcoesSessao Opcoes { get; } = opcoes;
        public ConcurrentQueue<string> Chamadas { get; } = new();
        bool _ativa;

        public event Action<EventoClaude>? Evento;
        public event Action<int, string>? Encerrou;

        public string Agente => Agentes.Claude;
        public string? SessaoId { get; private set; } = opcoes.Retomar;
        public bool Ativa => _ativa;
        public int? Pid => 4242;
        public bool TemRemoto => true;
        public bool TemControles => true;

        public void Emitir(EventoClaude evento) => Evento?.Invoke(evento);

        public void Iniciar()
        {
            _ativa = true;
            Chamadas.Enqueue("iniciar");
            SessaoId = "cli-1";
            Evento?.Invoke(new SessaoIniciada("cli-1", "opus", Opcoes.Pasta, null));
        }

        public void Enviar(string texto, IReadOnlyList<Anexo>? imagens = null)
        {
            Chamadas.Enqueue($"enviar:{texto}:{imagens?.Count ?? 0}");
            Evento?.Invoke(new MensagemDoUsuario(texto, imagens?.Count ?? 0));
        }

        public void Permitir(string pedidoId, JsonNode? entrada, JsonNode? regras = null) => Chamadas.Enqueue($"permitir:{pedidoId}:{entrada?.ToJsonString()}");
        public void Negar(string pedidoId, string motivo) => Chamadas.Enqueue($"negar:{pedidoId}:{motivo}");
        public void Interromper() => Chamadas.Enqueue("interromper");
        public void MudarModo(string modo) => Chamadas.Enqueue($"modo:{modo}");
        public void MudarModelo(string? modelo) => Chamadas.Enqueue($"modelo:{modelo}");
        public void AplicarConfiguracoes(JsonObject configuracoes) => Chamadas.Enqueue($"config:{configuracoes.ToJsonString()}");

        public Task<ControleRespondido> PedirAsync(JsonObject pedido, TimeSpan? limite = null) =>
            Task.FromResult(new ControleRespondido("c1", true, null, JsonDocument.Parse(new JsonObject { ["eco"] = pedido["subtype"]?.GetValue<string>() }.ToJsonString()).RootElement.Clone()));

        public Task<ControleRespondido> LigarRemotoAsync(bool ligado) => Task.FromResult(new ControleRespondido("c2", ligado, null, null));

        public void Encerrar()
        {
            if (!_ativa) return;
            _ativa = false;
            Chamadas.Enqueue("encerrar");
            Encerrou?.Invoke(0, "");
        }

        public ValueTask DisposeAsync()
        {
            Encerrar();
            return ValueTask.CompletedTask;
        }
    }

    sealed class Ambiente : IAsyncDisposable
    {
        public string Cano { get; } = "nexora-teste-" + Guid.NewGuid().ToString("N");
        public ConcurrentQueue<SessaoFalsa> Criadas { get; } = new();
        public SessoesDoServidor Sessoes { get; }
        public Task<int> Servidor { get; }
        readonly CancellationTokenSource _fim = new();

        public Ambiente(TimeSpan? ocioso = null, TimeSpan? esperaSolta = null, TimeSpan? esperaPorPermissao = null)
        {
            Sessoes = new SessoesDoServidor((_, opcoes) =>
            {
                var sessao = new SessaoFalsa(opcoes);
                Criadas.Enqueue(sessao);
                return sessao;
            })
            { EsperaSolta = esperaSolta ?? TimeSpan.FromMinutes(3), EsperaPorPermissao = esperaPorPermissao ?? TimeSpan.FromHours(24) };
            Servidor = Task.Run(() => ServidorDoNucleo.RodarAsync(Cano, NucleoLocal.Instancia, ocioso ?? TimeSpan.FromSeconds(10), _fim.Token, sessoes: Sessoes));
        }

        public async Task<NucleoRemoto> ConectarAsync() => await NucleoRemoto.ConectarAsync(Cano, TimeSpan.FromSeconds(10)) ?? throw new InvalidOperationException("Sem servidor.");

        public SessaoFalsa Unica() => Assert.Single(Criadas);

        public async ValueTask DisposeAsync()
        {
            _fim.Cancel();
            await Servidor.WaitAsync(TimeSpan.FromSeconds(10));
            await Sessoes.EncerrarTodasAsync();
        }
    }

    static async Task Ate(Func<bool> condicao, string oQue)
    {
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (!condicao())
        {
            if (DateTime.UtcNow > limite) throw new TimeoutException(oQue);
            await Task.Delay(20);
        }
    }

    static OpcoesSessao Opcoes() => new(@"D:\Projetos\Loja", Modelo: "opus", Modo: "acceptEdits", Diretorios: [@"D:\Projetos\Comum"], Permitidas: ["Read"],
        Ambiente: new Dictionary<string, string> { ["LOJA"] = "1" });

    [Fact]
    public void O_registro_numera_os_eventos_e_avisa_quando_perdeu_o_comeco()
    {
        var registro = new RegistroDeEventos(3);
        for (var i = 0; i < 5; i++) registro.Acrescentar(new TextoParcial($"t{i}"));

        Assert.Equal(5, registro.Ultimo);
        var (completo, eventos) = registro.Desde(2);
        Assert.True(completo);
        Assert.Equal([3L, 4, 5], eventos.Select(e => e.Numero));
        var (doComeco, todos) = registro.Desde(0);
        Assert.False(doComeco);
        Assert.Equal(3, todos.Count);
        Assert.Empty(registro.Desde(5).Eventos);
    }

    [Fact]
    public void As_opcoes_da_sessao_atravessam_o_cano_sem_o_lancador()
    {
        var opcoes = Opcoes();
        var json = JsonSerializer.Serialize(opcoes with { Lancador = null }, JsonDoNucleo.Opcoes);
        var volta = JsonSerializer.Deserialize<OpcoesSessao>(json, JsonDoNucleo.Opcoes)!;

        Assert.Equal(opcoes.Pasta, volta.Pasta);
        Assert.Equal(SessaoClaude.Argumentos(opcoes), SessaoClaude.Argumentos(volta));
        Assert.Equal("1", volta.Ambiente!["LOJA"]);
        Assert.Null(volta.Lancador);
    }

    [Fact]
    public async Task Sessao_aberta_pelo_app_roda_no_servidor_e_os_eventos_voltam_numerados_e_em_ordem()
    {
        await using var ambiente = new Ambiente();
        using var remoto = await ambiente.ConectarAsync();
        var sessao = new SessaoRemota(remoto, Agentes.Claude, Opcoes(), "Cupom");
        var eventos = new ConcurrentQueue<EventoClaude>();
        sessao.Evento += eventos.Enqueue;

        sessao.Iniciar();
        sessao.Enviar("Ajuste o cupom", [new Anexo("a.png", "image/png", [1, 2, 3])]);
        await Ate(() => eventos.Count >= 2, "eventos");
        await Ate(() => sessao.Pid is not null, "pid");
        var falsa = ambiente.Unica();
        falsa.Emitir(new TextoParcial("Vou "));
        falsa.Emitir(new TextoParcial("olhar"));
        falsa.Emitir(new TurnoEncerrado(false, "ok", null, 10));
        await Ate(() => eventos.Count >= 5, "turno");

        Assert.Equal(["SessaoIniciada", "MensagemDoUsuario", "TextoParcial", "TextoParcial", "TurnoEncerrado"], eventos.Select(e => e.GetType().Name));
        Assert.Equal("cli-1", sessao.SessaoId);
        Assert.Equal(4242, sessao.Pid);
        Assert.True(sessao.Ativa);
        Assert.Equal(["iniciar", "enviar:Ajuste o cupom:1"], falsa.Chamadas);
        Assert.Equal(SessaoClaude.Argumentos(Opcoes()), SessaoClaude.Argumentos(falsa.Opcoes));
        var resumo = Assert.Single(await remoto.ListarSessoesAsync());
        Assert.Equal((sessao.Id, "Cupom", "cli-1", false, 5L), (resumo.Id, resumo.Titulo, resumo.SessaoId, resumo.Trabalhando, resumo.Ultimo));
    }

    [Fact]
    public async Task Comandos_e_pedidos_de_controle_chegam_a_sessao_do_servidor()
    {
        await using var ambiente = new Ambiente();
        using var remoto = await ambiente.ConectarAsync();
        var sessao = new SessaoRemota(remoto, Agentes.Claude, Opcoes());
        sessao.Iniciar();
        sessao.MudarModo("plan");
        sessao.MudarModelo("haiku");
        sessao.AplicarConfiguracoes(new JsonObject { ["fastMode"] = true });
        sessao.Interromper();
        var resposta = await sessao.PedirAsync(new JsonObject { ["subtype"] = "get_context_usage" }, TimeSpan.FromSeconds(5));
        var remota = await sessao.LigarRemotoAsync(true);

        Assert.True(resposta.Sucesso);
        Assert.Equal("get_context_usage", resposta.Dados!.Value.GetProperty("eco").GetString());
        Assert.True(remota.Sucesso);
        Assert.Equal(["iniciar", "modo:plan", "modelo:haiku", "config:{\"fastMode\":true}", "interromper"], ambiente.Unica().Chamadas);
    }

    [Fact]
    public async Task App_que_fecha_solta_a_sessao_e_o_proximo_anexa_com_o_pedido_pendente()
    {
        await using var ambiente = new Ambiente();
        var primeiro = await ambiente.ConectarAsync();
        var sessao = new SessaoRemota(primeiro, Agentes.Claude, Opcoes());
        sessao.Iniciar();
        sessao.Enviar("Rode os testes");
        await Ate(() => !ambiente.Criadas.IsEmpty, "sessão criada");
        var falsa = ambiente.Unica();
        await Ate(() => falsa.Chamadas.Count >= 2, "mensagem");
        falsa.Emitir(new PermissaoPedida("p1", "Bash", JsonDocument.Parse("""{"command":"dotnet test"}""").RootElement.Clone(), null, null, null));
        sessao.Soltar();
        primeiro.Dispose();

        await Task.Delay(300);
        falsa.Emitir(new TextoParcial("esperando"));
        using var segundo = await ambiente.ConectarAsync();
        var resumo = Assert.Single(await segundo.ListarSessoesAsync());
        Assert.True(resumo.Trabalhando);
        Assert.Equal(0, resumo.Assinantes);
        var outra = SessaoRemota.Anexar(segundo, resumo);
        var eventos = new ConcurrentQueue<EventoClaude>();
        outra.Evento += eventos.Enqueue;
        var estado = await outra.AssinarAsync();
        Assert.NotNull(estado);

        var pedido = Assert.IsType<PermissaoPedida>(Assert.Single(estado.Pendentes));
        Assert.Equal("p1", pedido.PedidoId);
        Assert.True(estado.Trabalhando);
        Assert.True(outra.Ativa);
        Assert.Equal("cli-1", outra.SessaoId);
        outra.Permitir("p1", new JsonObject { ["command"] = "dotnet test" });
        await Ate(() => falsa.Chamadas.Count >= 3, "permissão");
        falsa.Emitir(new TurnoEncerrado(false, "ok", null, 10));
        await Ate(() => eventos.Count >= 1, "evento novo");

        Assert.Equal("permitir:p1:{\"command\":\"dotnet test\"}", falsa.Chamadas.Last());
        Assert.IsType<TurnoEncerrado>(Assert.Single(eventos));
        Assert.False(Assert.Single(await segundo.ListarSessoesAsync()).Trabalhando);
    }

    [Fact]
    public async Task Fim_da_sessao_chega_ao_app_como_encerramento()
    {
        await using var ambiente = new Ambiente();
        using var remoto = await ambiente.ConectarAsync();
        var sessao = new SessaoRemota(remoto, Agentes.Claude, Opcoes());
        var fim = new TaskCompletionSource<int>();
        sessao.Encerrou += (codigo, _) => fim.TrySetResult(codigo);
        sessao.Iniciar();
        await Ate(() => !ambiente.Criadas.IsEmpty && ambiente.Unica().Ativa, "sessão ativa");

        sessao.Encerrar();

        Assert.Equal(0, await fim.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.False(sessao.Ativa);
        Assert.False(Assert.Single(await remoto.ListarSessoesAsync()).Ativa);
    }

    [Fact]
    public async Task Servidor_que_cai_encerra_as_sessoes_do_app()
    {
        var ambiente = new Ambiente();
        using var remoto = await ambiente.ConectarAsync();
        var sessao = new SessaoRemota(remoto, Agentes.Claude, Opcoes());
        var fim = new TaskCompletionSource<int>();
        sessao.Encerrou += (codigo, _) => fim.TrySetResult(codigo);
        sessao.Iniciar();
        await Ate(() => !ambiente.Criadas.IsEmpty, "sessão criada");

        await ambiente.DisposeAsync();

        Assert.Equal(-1, await fim.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.False(sessao.Ativa);
    }

    [Fact]
    public async Task Sessao_viva_segura_o_servidor_e_solta_parada_e_encerrada_depois_da_espera()
    {
        await using var ambiente = new Ambiente(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(600));
        var remoto = await ambiente.ConectarAsync();
        var sessao = new SessaoRemota(remoto, Agentes.Claude, Opcoes());
        sessao.Iniciar();
        await Ate(() => !ambiente.Criadas.IsEmpty && ambiente.Unica().Ativa, "sessão ativa");
        sessao.Soltar();
        remoto.Dispose();

        await Task.Delay(400);
        Assert.False(ambiente.Servidor.IsCompleted);
        Assert.True(ambiente.Unica().Ativa);

        Assert.Equal(0, await ambiente.Servidor.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.False(ambiente.Unica().Ativa);
    }

    [Fact]
    public async Task Hub_de_mcp_vive_no_servidor_e_o_app_registra_e_recebe_os_avisos()
    {
        var pasta = Directory.CreateTempSubdirectory("nexora-hub-remoto-").FullName;
        var casa = Directory.CreateTempSubdirectory("nexora-hub-remoto-casa-").FullName;
        File.WriteAllText(Path.Combine(pasta, ".mcp.json"), """{ "mcpServers": { "banco": { "command": "node", "args": ["servidor.js"] } } }""");
        try
        {
            await using var ambiente = new Ambiente();
            using var remoto = await ambiente.ConectarAsync();
            var hub = await HubRemoto.LigarAsync(remoto, Path.Combine(pasta, "cache"), TimeSpan.FromMinutes(5));
            Assert.NotNull(hub);
            var mudou = 0;
            hub.Mudou += () => Interlocked.Increment(ref mudou);

            var registro = hub.Registrar(pasta, "Loja", casa);

            Assert.Equal(["banco"], registro.Nomes);
            var url = (string)System.Text.Json.Nodes.JsonNode.Parse(registro.Configuracao!)!["mcpServers"]!["banco"]!["url"]!;
            Assert.Equal($"http://localhost:{ambiente.Sessoes.Hub!.Porta}/mcp/", url[..url.LastIndexOf('/')] + "/");
            await Ate(() => Volatile.Read(ref mudou) > 0 && hub.SessoesRegistradas == 1, "aviso do hub");
            Assert.Equal(0, hub.Ativos);
            Assert.Empty(hub.Pids());
            Assert.Empty(hub.Estado());
            hub.Renomear(registro.Chave, "Loja nova");
            await hub.EsquecerAsync(registro.Chave);
            await Ate(() => hub.SessoesRegistradas == 0, "hub esqueceu");
            Assert.Equal(0, ambiente.Sessoes.Hub.SessoesRegistradas);
        }
        finally
        {
            foreach (var p in new[] { pasta, casa })
                try
                {
                    Directory.Delete(p, true);
                }
                catch (IOException)
                {
                }
        }
    }

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    static async Task<JsonElement> ChamarFerramentaAsync(string configuracao, string servidor)
    {
        var no = JsonNode.Parse(configuracao)!["mcpServers"]![servidor]!;
        using var pedido = new HttpRequestMessage(HttpMethod.Post, (string)no["url"]!)
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"eco","arguments":{}}}""", System.Text.Encoding.UTF8, "application/json"),
        };
        pedido.Headers.TryAddWithoutValidation("Authorization", (string)no["headers"]!["Authorization"]!);
        pedido.Headers.Accept.ParseAdd("application/json");
        using var resposta = await Http.SendAsync(pedido);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    static Nexora.Core.Orquestra.ServidorMcp ServidorDoApp(NucleoRemoto remoto) =>
        new([new Nexora.Core.Orquestra.FerramentaMcp("eco", "eco", [], [], true)],
            (dono, _, _, _) => Task.FromResult(new Nexora.Core.Orquestra.ResultadoFerramenta("dono=" + dono)), "nexora-teste")
        {
            PonteFixa = new PonteRemota(remoto),
        };

    [Fact]
    public async Task Ponte_mcp_guarda_o_endereco_enquanto_o_app_fecha_e_o_proximo_app_atende_pelo_apelido()
    {
        await using var ambiente = new Ambiente();
        var primeiro = await ambiente.ConectarAsync();
        using var antigo = ServidorDoApp(primeiro);
        var chave = antigo.Registrar("s1");
        var configuracao = antigo.Configuracao(chave);

        Assert.Contains($"localhost:{ambiente.Sessoes.Ponte!.Porta}/mcp/nexora-teste", configuracao);
        Assert.Equal("dono=s1", (await ChamarFerramentaAsync(configuracao, "nexora-teste")).GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString());

        primeiro.Dispose();
        await Task.Delay(300);
        var semApp = await ChamarFerramentaAsync(configuracao, "nexora-teste");
        Assert.Equal(7, semApp.GetProperty("id").GetInt32());
        Assert.Equal(PonteMcp.MensagemSemApp, semApp.GetProperty("error").GetProperty("message").GetString());

        using var segundo = await ambiente.ConectarAsync();
        using var novo = ServidorDoApp(segundo);
        novo.Registrar("s1");
        Nexora.Core.Orquestra.ServidorMcp.Apelidar(Nexora.Core.Orquestra.ServidorMcp.DonoGlobal("s1"), "s7");
        try
        {
            Assert.Equal("dono=s7", (await ChamarFerramentaAsync(configuracao, "nexora-teste")).GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString());
        }
        finally
        {
            Nexora.Core.Orquestra.ServidorMcp.Apelidar(Nexora.Core.Orquestra.ServidorMcp.DonoGlobal("s1"), "s1");
        }
    }

    [Fact]
    public async Task Permissao_esperando_sem_app_encerra_o_agente_depois_do_prazo_mas_nao_com_app_aberto()
    {
        await using var ambiente = new Ambiente(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(3), TimeSpan.FromMilliseconds(600));
        using var primeiro = await ambiente.ConectarAsync();
        var aberta = new SessaoRemota(primeiro, Agentes.Claude, Opcoes());
        var solta = new SessaoRemota(primeiro, Agentes.Claude, Opcoes() with { Pasta = @"D:\Projetos\Outra" });
        aberta.Iniciar();
        solta.Iniciar();
        await Ate(() => ambiente.Criadas.Count == 2 && ambiente.Criadas.All(c => c.Ativa), "sessões ativas");
        var pedido = new PermissaoPedida("p1", "Bash", JsonDocument.Parse("""{"command":"dotnet test"}""").RootElement.Clone(), null, null, null);
        foreach (var falsa in ambiente.Criadas) falsa.Emitir(pedido);
        await Ate(() => ambiente.Sessoes.Listar().All(r => r.Trabalhando), "pedidos pendentes");
        solta.Soltar();

        var falsaSolta = ambiente.Criadas.Single(c => c.Opcoes.Pasta == @"D:\Projetos\Outra");
        var falsaAberta = ambiente.Criadas.Single(c => c != falsaSolta);
        await Task.Delay(300);
        Assert.True(falsaSolta.Ativa);
        await Ate(() => !falsaSolta.Ativa, "agente solto encerrado pelo prazo");
        Assert.True(falsaAberta.Ativa);
        Assert.True(Assert.Single(await primeiro.ListarSessoesAsync(), r => r.Id == aberta.Id).Trabalhando);
    }

    [Fact]
    public void Ao_abrir_anexa_as_vivas_e_traz_do_historico_as_soltas_que_ja_pararam()
    {
        ResumoDaSessao Viva(string id, string sessao, bool ativa = true, int assinantes = 0) => new(id, Agentes.Claude, @"D:\Loja", null, sessao, ativa, true, 1, 3, assinantes);
        var vivas = new[] { Viva("a", "cli-a"), Viva("b", "cli-b", assinantes: 1), Viva("c", "cli-c", ativa: false) };
        var soltas = new List<Nexora.Aplicativo.SessaoSolta>
        {
            new() { IdNoServidor = "a", SessaoId = "cli-a", Pasta = @"D:\Loja" },
            new() { IdNoServidor = "c", SessaoId = "cli-c", Pasta = @"D:\Loja" },
            new() { IdNoServidor = "z", SessaoId = "cli-z", Pasta = @"D:\Loja" },
            new() { IdNoServidor = "y", SessaoId = "cli-z", Pasta = @"D:\Loja" },
            new() { IdNoServidor = "x", SessaoId = null, Pasta = @"D:\Loja" },
        };

        var (anexar, doHistorico) = Nexora.ViewModels.JanelaVM.PlanejarReabertura(vivas, soltas);

        Assert.Equal(["a"], anexar.Select(r => r.Id));
        Assert.Equal(["cli-c", "cli-z"], doHistorico.Select(s => s.SessaoId));
        var (soVivas, nada) = Nexora.ViewModels.JanelaVM.PlanejarReabertura(vivas, []);
        Assert.Equal(["a"], soVivas.Select(r => r.Id));
        Assert.Empty(nada);
    }
}
