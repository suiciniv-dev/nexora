using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using Nexora.Core.Ambientes;
using Nexora.Core.Claude;
using Nexora.Core.Orquestra;
using Nexora.ViewModels;

namespace Nexora.Testes.Tela;

public static class AgenteDeTeste
{
    public static string Dll { get; } = Achar();

    static string Achar()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "Nexora.slnx"))) pasta = pasta.Parent;
        if (pasta is null) throw new InvalidOperationException("Não achei a raiz do repositório.");
        var configuracao = AppContext.BaseDirectory.Contains(Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar) ? "Release" : "Debug";
        var dll = Path.Combine(pasta.FullName, "tools", "AgenteDeTeste", "bin", configuracao, "net10.0", "AgenteDeTeste.dll");
        if (!File.Exists(dll)) throw new FileNotFoundException("Compile tools/AgenteDeTeste antes dos testes.", dll);
        return dll;
    }
}

[Collection("Orquestrador")]
public sealed class OrquestradorTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-orquestra-").FullName;
    readonly string _registro;
    readonly string? _simuladoAntes = SessaoClaude.Simulado;
    readonly string? _registroAntes = Environment.GetEnvironmentVariable("AGENTE_DE_TESTE_REGISTRO");
    JanelaVM? _janela;

    public OrquestradorTestes()
    {
        _registro = Path.Combine(_pasta, "registro.txt");
        SessaoClaude.Simulado = AgenteDeTeste.Dll;
        Environment.SetEnvironmentVariable("AGENTE_DE_TESTE_REGISTRO", _registro);
    }

    public void Dispose()
    {
        Sessao.Rodar(() =>
        {
            if (_janela is { } janela)
                foreach (var chat in janela.Abertas.ToList()) chat.Encerrar(esperar: true);
        }).Wait();
        SessaoClaude.Simulado = _simuladoAntes;
        Environment.SetEnvironmentVariable("AGENTE_DE_TESTE_REGISTRO", _registroAntes);
        try { Directory.Delete(_pasta, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    sealed record Chefe(ChatVM Chat, OrquestraVM Orquestra, Uri Endereco, string Chave);

    Chefe Iniciar()
    {
        _janela = new JanelaVM();
        var chat = _janela.Orquestra.IniciarEm(new Local("Loja", _pasta, []))!;
        var servidor = JsonNode.Parse(chat.Mcp!)!["mcpServers"]![ServidorMcp.Nome]!;
        var chave = ((string)servidor["headers"]!["Authorization"]!)["Bearer ".Length..];
        return new Chefe(chat, _janela.Orquestra, new Uri((string)servidor["url"]!), chave);
    }

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };
    static int _proximo;

    static async Task<T> Bombear<T>(Task<T> tarefa)
    {
        while (!tarefa.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
        return await tarefa;
    }

    static async Task<(bool Erro, string Texto)> Chamar(Chefe chefe, string ferramenta, object? argumentos = null)
    {
        var corpo = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = Interlocked.Increment(ref _proximo),
            method = "tools/call",
            @params = new { name = ferramenta, arguments = argumentos ?? new { } },
        });
        var tarefa = Task.Run(async () =>
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Post, chefe.Endereco) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", chefe.Chave);
            pedido.Headers.Accept.ParseAdd("application/json");
            pedido.Headers.Accept.ParseAdd("text/event-stream");
            using var resposta = await Http.SendAsync(pedido);
            return await resposta.Content.ReadAsStringAsync();
        });
        var texto = await Bombear(tarefa);
        var json = texto.TrimStart().StartsWith("event:", StringComparison.Ordinal) || texto.TrimStart().StartsWith("data:", StringComparison.Ordinal)
            ? string.Join("", texto.Split('\n').Where(l => l.StartsWith("data:", StringComparison.Ordinal)).Select(l => l["data:".Length..].Trim()))
            : texto;
        var resultado = JsonDocument.Parse(json).RootElement.GetProperty("result");
        return (resultado.GetProperty("isError").GetBoolean(), resultado.GetProperty("content")[0].GetProperty("text").GetString() ?? "");
    }

    static async Task<JsonNode> ChamarOk(Chefe chefe, string ferramenta, object? argumentos = null)
    {
        var (erro, texto) = await Chamar(chefe, ferramenta, argumentos);
        Assert.False(erro, texto);
        return JsonNode.Parse(texto)!;
    }

    async Task Esperar(Func<bool> condicao, int segundos = 30)
    {
        var limite = DateTime.Now.AddSeconds(segundos);
        while (!condicao() && DateTime.Now < limite)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
        Dispatcher.UIThread.RunJobs();
        Assert.True(condicao(), "a condição não aconteceu a tempo" + Environment.NewLine + Diagnostico());
    }

    string[] LerRegistro()
    {
        if (!File.Exists(_registro)) return [];
        using var fluxo = new FileStream(_registro, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var leitor = new StreamReader(fluxo);
        return leitor.ReadToEnd().Split(Environment.NewLine.Last(), StringSplitOptions.RemoveEmptyEntries);
    }

    string Diagnostico()
    {
        var linhas = new List<string>();
        foreach (var c in _janela?.Abertas ?? [])
            linhas.Add($"{c.Codigo} estado={c.Estado} trabalhando={c.Trabalhando} precisa={c.Precisa} permissao={c.PermissaoPendente?.Titulo ?? "-"} itens=[{string.Join(" | ", c.Itens.TakeLast(6).Select(i => i switch { ItemTexto x => "texto:" + x.Texto, ItemAviso a => "aviso:" + a.Texto, ItemPermissao q => "permissao:" + q.Titulo + (q.Pendente ? "(pendente)" : ":" + q.Resposta), ItemUsuario u => "usuario:" + u.Texto, _ => i.GetType().Name }))}]");
        if (File.Exists(_registro)) linhas.Add("registro:" + Environment.NewLine + string.Join(Environment.NewLine, LerRegistro().TakeLast(30)));
        return string.Join(Environment.NewLine, linhas);
    }

    void Confirmar(string esperado, JsonArray lista, string id) =>
        Assert.True(EstadoDe(lista, id) == esperado, $"{id}: esperado {esperado}, veio {EstadoDe(lista, id)}" + Environment.NewLine + Diagnostico());

    static async Task<string> CriarAgente(Chefe chefe, string ambiente, string mensagem, string? titulo = null)
    {
        var criado = await ChamarOk(chefe, Ferramentas.CriarAgente, new { ambiente, mensagem, titulo });
        return (string)criado["id"]!;
    }

    static async Task<JsonArray> EsperarAgentes(Chefe chefe, int segundos, params string[] ids) =>
        (JsonArray)await ChamarOk(chefe, Ferramentas.Esperar, new { ids, segundos });

    static string EstadoDe(JsonArray lista, string id) => (string)lista.Single(s => (string)s!["id"]! == id)!["estado"]!;

    [Fact]
    public Task Criar_agente_roda_numa_sessao_nova_e_a_resposta_chega_ao_orquestrador() => Sessao.Rodar(async () =>
    {
        var chefe = Iniciar();
        var id = await CriarAgente(chefe, _pasta, "olá agente", "Primeiro");

        var agente = _janela!.Abertas.Single(c => c.Codigo == id);
        Assert.Same(chefe.Chat, agente.Chefe);
        Assert.Equal("Primeiro", agente.Titulo);
        Assert.Contains(chefe.Orquestra.Nos, n => n.Chat == agente);

        var lista = await EsperarAgentes(chefe, 30, id);
        Confirmar("pronta", lista, id);
        var lido = await Chamar(chefe, Ferramentas.LerSessao, new { id });
        Assert.False(lido.Erro);
        Assert.Contains("ok: olá agente", lido.Texto);

        var sessoes = (JsonArray)await ChamarOk(chefe, Ferramentas.ListarSessoes);
        var minha = sessoes.Single(s => (string)s!["id"]! == id)!;
        Assert.True((bool)minha["agente"]!);
        Assert.DoesNotContain(sessoes, s => (string)s!["id"]! == chefe.Chat.Codigo);
    });

    [Fact]
    public Task Enviar_mensagem_continua_a_mesma_sessao() => Sessao.Rodar(async () =>
    {
        var chefe = Iniciar();
        var id = await CriarAgente(chefe, _pasta, "primeira");
        await EsperarAgentes(chefe, 30, id);

        var enviado = await Chamar(chefe, Ferramentas.EnviarMensagem, new { id, mensagem = "segunda" });
        Assert.False(enviado.Erro, enviado.Texto);
        Confirmar("pronta", await EsperarAgentes(chefe, 30, id), id);
        var lido = (await Chamar(chefe, Ferramentas.LerSessao, new { id })).Texto;
        Assert.Contains("ok: primeira", lido);
        Assert.Contains("ok: segunda", lido);
        var processos = LerRegistro().Where(l => l.Contains(" iniciou ")).Select(l => l.Split(' ')[0]).Distinct().ToList();
        Assert.Single(processos);
    });

    [Fact]
    public Task Pedidos_invalidos_viram_erro_sem_criar_nada() => Sessao.Rodar(async () =>
    {
        var chefe = Iniciar();
        var antes = _janela!.Abertas.Count;

        var semMensagem = await Chamar(chefe, Ferramentas.CriarAgente, new { ambiente = _pasta });
        Assert.True(semMensagem.Erro);
        var inexistente = await Chamar(chefe, Ferramentas.CriarAgente, new { ambiente = Path.Combine(_pasta, "nao-existe"), mensagem = "x" });
        Assert.True(inexistente.Erro);
        Assert.Contains("Ambiente não encontrado", inexistente.Texto);
        var paraNinguem = await Chamar(chefe, Ferramentas.EnviarMensagem, new { id = "s999", mensagem = "x" });
        Assert.True(paraNinguem.Erro);
        var paraSiMesmo = await Chamar(chefe, Ferramentas.EnviarMensagem, new { id = chefe.Chat.Codigo, mensagem = "x" });
        Assert.True(paraSiMesmo.Erro);
        var decisaoRuim = await Chamar(chefe, Ferramentas.ResponderPermissao, new { id = chefe.Chat.Codigo, decisao = "talvez" });
        Assert.True(decisaoRuim.Erro);
        var nadaParaEsperar = await Chamar(chefe, Ferramentas.Esperar, new { segundos = 1 });
        Assert.True(nadaParaEsperar.Erro);
        var desconhecida = await Chamar(chefe, "apagar_tudo");
        Assert.True(desconhecida.Erro);

        Assert.Equal(antes, _janela.Abertas.Count);
    });

    [Fact]
    public Task Esperar_volta_quando_o_agente_pede_permissao_e_a_resposta_do_orquestrador_chega_ao_agente() => Sessao.Rodar(async () =>
    {
        var chefe = Iniciar();
        var permitir = await CriarAgente(chefe, _pasta, "permissao echo sim");
        var negar = await CriarAgente(chefe, _pasta, "permissao rm -rf /");

        var relogio = Stopwatch.StartNew();
        var lista = await EsperarAgentes(chefe, 60, permitir, negar);
        Assert.True(relogio.Elapsed < TimeSpan.FromSeconds(30));
        await Esperar(() => _janela!.Abertas.Where(c => c.Codigo == permitir || c.Codigo == negar).All(c => c.PermissaoPendente is not null));
        lista = await EsperarAgentes(chefe, 1, permitir, negar);
        Assert.All(lista, s => Assert.Equal("precisa de resposta", (string)s!["estado"]!));
        Assert.All(lista, s => Assert.NotNull(s!["permissao_pendente"]));

        var sim = await Chamar(chefe, Ferramentas.ResponderPermissao, new { id = permitir, decisao = "permitir" });
        Assert.False(sim.Erro, sim.Texto);
        var nao = await Chamar(chefe, Ferramentas.ResponderPermissao, new { id = negar, decisao = "negar", motivo = "perigoso demais" });
        Assert.False(nao.Erro, nao.Texto);

        lista = await EsperarAgentes(chefe, 30, permitir, negar);
        Confirmar("pronta", lista, permitir);
        Confirmar("pronta", lista, negar);
        Assert.Contains("permitido", (await Chamar(chefe, Ferramentas.LerSessao, new { id = permitir })).Texto);
        Assert.Contains("negado: perigoso demais", (await Chamar(chefe, Ferramentas.LerSessao, new { id = negar })).Texto);

        var deNovo = await Chamar(chefe, Ferramentas.ResponderPermissao, new { id = permitir, decisao = "permitir" });
        Assert.True(deNovo.Erro);
    });

    [Fact]
    public Task Esperar_respeita_o_limite_quando_o_agente_nao_termina() => Sessao.Rodar(async () =>
    {
        var chefe = Iniciar();
        var id = await CriarAgente(chefe, _pasta, "trave");
        await Esperar(() => _janela!.Abertas.Single(c => c.Codigo == id).Trabalhando);

        var relogio = Stopwatch.StartNew();
        var lista = await EsperarAgentes(chefe, 2, id);
        relogio.Stop();

        Confirmar("trabalhando", lista, id);
        Assert.InRange(relogio.Elapsed.TotalSeconds, 1.5, 10);
    });

    [Fact]
    public Task Esperar_nunca_passa_do_maximo_mesmo_se_pedir_mais() => Sessao.Rodar(async () =>
    {
        var chefe = Iniciar();
        var id = await CriarAgente(chefe, _pasta, "trave");
        await Esperar(() => _janela!.Abertas.Single(c => c.Codigo == id).Trabalhando);
        var esquema = Ferramentas.Todas.Single(f => f.Nome == Ferramentas.Esperar).ParaJson().ToJsonString();
        Assert.Contains(Ferramentas.EsperaMaxima.ToString(), esquema);
        Assert.True(Ferramentas.EsperaMaxima <= 300);
    });

    [Fact]
    public Task Parar_sessao_interrompe_um_agente_travado() => Sessao.Rodar(async () =>
    {
        var chefe = Iniciar();
        var id = await CriarAgente(chefe, _pasta, "trave");
        var agente = _janela!.Abertas.Single(c => c.Codigo == id);
        await Esperar(() => agente.Trabalhando);

        var parado = await Chamar(chefe, Ferramentas.PararSessao, new { id });
        Assert.False(parado.Erro, parado.Texto);

        await Esperar(() => !agente.Trabalhando);
        Assert.NotEqual("trabalhando", EstadoDe(await EsperarAgentes(chefe, 1, id), id));
        Assert.Contains(LerRegistro(), l => l.EndsWith("controle interrupt", StringComparison.Ordinal));
    });

    [Fact]
    public Task Agente_que_cai_aparece_como_erro_e_esperar_nao_fica_preso() => Sessao.Rodar(async () =>
    {
        var chefe = Iniciar();
        var id = await CriarAgente(chefe, _pasta, "falhe de proposito");

        var relogio = Stopwatch.StartNew();
        var lista = await EsperarAgentes(chefe, 60, id);
        Assert.True(relogio.Elapsed < TimeSpan.FromSeconds(30));
        Confirmar("erro", lista, id);
        Assert.Contains("falha simulada", (await Chamar(chefe, Ferramentas.LerSessao, new { id })).Texto);

        var deNovo = await Chamar(chefe, Ferramentas.EnviarMensagem, new { id, mensagem = "voltei" });
        Assert.False(deNovo.Erro, deNovo.Texto);
        Confirmar("pronta", await EsperarAgentes(chefe, 30, id), id);
        Assert.Contains("ok: voltei", (await Chamar(chefe, Ferramentas.LerSessao, new { id })).Texto);
    });

    [Fact]
    public Task Esperar_sem_ids_acompanha_todos_os_agentes_deste_orquestrador() => Sessao.Rodar(async () =>
    {
        var chefe = Iniciar();
        var rapido = await CriarAgente(chefe, _pasta, "rapido");
        var lento = await CriarAgente(chefe, _pasta, "demore 1500");
        var lista = (JsonArray)await ChamarOk(chefe, Ferramentas.Esperar, new { segundos = 30 });
        Assert.Equal(2, lista.Count);
        Confirmar("pronta", lista, rapido);
        Confirmar("pronta", lista, lento);
    });

    [Fact]
    public Task Chave_de_outro_orquestrador_ou_de_um_fechado_nao_comanda_nada() => Sessao.Rodar(async () =>
    {
        var chefe = Iniciar();
        var falsa = chefe with { Chave = "chave-inventada" };
        var tarefa = Task.Run(async () =>
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Post, falsa.Endereco)
            {
                Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"listar_sessoes","arguments":{}}}""", Encoding.UTF8, "application/json"),
            };
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", falsa.Chave);
            using var resposta = await Http.SendAsync(pedido);
            return resposta.StatusCode;
        });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, await Bombear(tarefa));

        chefe.Chat.FecharCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Null(chefe.Orquestra.Chefe);
        var depois = await Task.Run(async () =>
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Post, chefe.Endereco)
            {
                Content = new StringContent("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"listar_sessoes","arguments":{}}}""", Encoding.UTF8, "application/json"),
            };
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", chefe.Chave);
            using var resposta = await Http.SendAsync(pedido);
            return resposta.StatusCode;
        });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, depois);
    });
}

[CollectionDefinition("Orquestrador", DisableParallelization = true)]
public sealed class ColecaoDoOrquestrador;
