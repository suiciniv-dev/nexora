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

        public Ambiente(TimeSpan? ocioso = null, TimeSpan? esperaSolta = null)
        {
            Sessoes = new SessoesDoServidor((_, opcoes) =>
            {
                var sessao = new SessaoFalsa(opcoes);
                Criadas.Enqueue(sessao);
                return sessao;
            })
            { EsperaSolta = esperaSolta ?? TimeSpan.FromMinutes(3) };
            Servidor = Task.Run(() => ServidorDoNucleo.RodarAsync(Cano, NucleoLocal.Instancia, ocioso ?? TimeSpan.FromSeconds(10), _fim.Token, sessoes: Sessoes));
        }

        public async Task<NucleoRemoto> ConectarAsync() => await NucleoRemoto.ConectarAsync(Cano, TimeSpan.FromSeconds(10)) ?? throw new InvalidOperationException("Sem servidor.");

        public SessaoFalsa Unica() => Assert.Single(Criadas);

        public async ValueTask DisposeAsync()
        {
            _fim.Cancel();
            await Servidor.WaitAsync(TimeSpan.FromSeconds(10));
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
}
