using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Nexora.Core.Claude;
using Nexora.Core.Orquestra;

namespace Nexora.Testes;

public sealed class OrquestraTestes : IDisposable
{
    readonly List<(string Dono, string Ferramenta, string Argumentos)> _chamadas = [];
    readonly ServidorMcp _servidor;

    public OrquestraTestes() => _servidor = new ServidorMcp(Ferramentas.Todas, (dono, ferramenta, args, _) =>
    {
        lock (_chamadas) _chamadas.Add((dono, ferramenta, args.GetRawText()));
        return Task.FromResult(ferramenta == Ferramentas.PararSessao
            ? new ResultadoFerramenta("sessão não existe", Erro: true)
            : new ResultadoFerramenta($"ok de {dono}"));
    });

    public void Dispose() => _servidor.Dispose();

    static JsonElement Ler(string? json) => JsonDocument.Parse(json!).RootElement;

    [Fact]
    public async Task InicializaListaEChamaFerramentas()
    {
        var inicio = Ler(await _servidor.ResponderAsync("chefe", """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{}}}""", default));
        Assert.Equal("2025-06-18", inicio.GetProperty("result").GetProperty("protocolVersion").GetString());
        Assert.Equal("nexora", inicio.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());

        Assert.Null(await _servidor.ResponderAsync("chefe", """{"jsonrpc":"2.0","method":"notifications/initialized"}""", default));

        var lista = Ler(await _servidor.ResponderAsync("chefe", """{"jsonrpc":"2.0","id":"a","method":"tools/list"}""", default));
        var nomes = lista.GetProperty("result").GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.Equal(Ferramentas.Todas.Select(f => f.Nome), nomes);
        var criar = lista.GetProperty("result").GetProperty("tools").EnumerateArray().Single(t => t.GetProperty("name").GetString() == Ferramentas.CriarAgente);
        Assert.Equal(["ambiente", "mensagem"], criar.GetProperty("inputSchema").GetProperty("required").EnumerateArray().Select(r => r.GetString()));

        var chamada = Ler(await _servidor.ResponderAsync("chefe", """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"ler_sessao","arguments":{"id":"s3"}}}""", default));
        Assert.Equal("ok de chefe", chamada.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString());
        Assert.False(chamada.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal(("chefe", "ler_sessao", """{"id":"s3"}"""), Assert.Single(_chamadas));
    }

    [Fact]
    public async Task ErrosViramRespostaDoProtocolo()
    {
        var falha = Ler(await _servidor.ResponderAsync("c", """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"parar_sessao","arguments":{"id":"x"}}}""", default));
        Assert.True(falha.GetProperty("result").GetProperty("isError").GetBoolean());

        var desconhecida = Ler(await _servidor.ResponderAsync("c", """{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"apagar_tudo"}}""", default));
        Assert.True(desconhecida.GetProperty("result").GetProperty("isError").GetBoolean());

        var metodo = Ler(await _servidor.ResponderAsync("c", """{"jsonrpc":"2.0","id":5,"method":"resources/list"}""", default));
        Assert.Equal(-32601, metodo.GetProperty("error").GetProperty("code").GetInt32());

        Assert.Equal(-32700, Ler(await _servidor.ResponderAsync("c", "{ quebrado", default)).GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task HttpExigeAChaveDoOrquestrador()
    {
        var chave = _servidor.Registrar("chefe-1");
        using var http = new HttpClient();
        StringContent Corpo(string json) => new(json, Encoding.UTF8, "application/json");

        var semChave = await http.PostAsync(_servidor.Endereco, Corpo("""{"jsonrpc":"2.0","id":1,"method":"ping"}"""));
        Assert.Equal(HttpStatusCode.Unauthorized, semChave.StatusCode);

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", chave);
        var ping = await http.PostAsync(_servidor.Endereco, Corpo("""{"jsonrpc":"2.0","id":1,"method":"ping"}"""));
        Assert.Equal(HttpStatusCode.OK, ping.StatusCode);
        Assert.Equal("application/json", ping.Content.Headers.ContentType?.MediaType);

        var aviso = await http.PostAsync(_servidor.Endereco, Corpo("""{"jsonrpc":"2.0","method":"notifications/initialized"}"""));
        Assert.Equal(HttpStatusCode.Accepted, aviso.StatusCode);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await http.GetAsync(_servidor.Endereco)).StatusCode);

        await http.PostAsync(_servidor.Endereco, Corpo("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"listar_sessoes","arguments":{}}}"""));
        Assert.Equal("chefe-1", Assert.Single(_chamadas).Dono);

        _servidor.Esquecer(chave);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsync(_servidor.Endereco, Corpo("""{"jsonrpc":"2.0","id":3,"method":"ping"}"""))).StatusCode);
    }

    [Fact]
    public void ConfiguracaoESoLeituraLiberadas()
    {
        var config = Ler(_servidor.Configuracao("abc")).GetProperty("mcpServers").GetProperty("nexora");
        Assert.Equal("http", config.GetProperty("type").GetString());
        Assert.Equal(_servidor.Endereco, config.GetProperty("url").GetString());
        Assert.Equal("Bearer abc", config.GetProperty("headers").GetProperty("Authorization").GetString());
        Assert.Equal(
            ["mcp__nexora__listar_ambientes", "mcp__nexora__listar_sessoes", "mcp__nexora__ler_sessao", "mcp__nexora__esperar"],
            Ferramentas.Livres);

        var args = SessaoClaude.Argumentos(new OpcoesSessao(@"D:\x", Mcp: "{}", PromptExtra: "seja breve", Permitidas: Ferramentas.Livres));
        Assert.Equal("{}", args[args.ToList().IndexOf("--mcp-config") + 1]);
        Assert.Equal("seja breve", args[args.ToList().IndexOf("--append-system-prompt") + 1]);
        Assert.Equal(string.Join(',', Ferramentas.Livres), args[args.ToList().IndexOf("--allowedTools") + 1]);
    }

    [Fact]
    public async Task ClaudeDeVerdadeChamaAFerramentaDoNexora()
    {
        if (Environment.GetEnvironmentVariable("NEXORA_INTEGRACAO") != "1") return;

        var pasta = Directory.CreateTempSubdirectory("nexora-orquestra-").FullName;
        var chave = _servidor.Registrar("chefe-real");
        var fim = new TaskCompletionSource<TurnoEncerrado>();
        await using (var sessao = new SessaoClaude(new OpcoesSessao(pasta, Modelo: "haiku", Isolada: true,
            Mcp: _servidor.Configuracao(chave), PromptExtra: Ferramentas.Instrucoes, Permitidas: Ferramentas.Livres)))
        {
            sessao.Evento += e =>
            {
                if (e is TurnoEncerrado t) fim.TrySetResult(t);
            };
            sessao.Iniciar();
            sessao.Enviar("Chame a ferramenta listar_sessoes do servidor nexora uma vez e responda só com o texto que ela devolveu.");
            var turno = await fim.Task.WaitAsync(TimeSpan.FromSeconds(180));
            Assert.False(turno.Erro, turno.Texto);
        }

        Directory.Delete(pasta, recursive: true);
        foreach (var d in new DirectoryInfo(Nexora.Core.Historico.ConversasSalvas.PastaPadrao).EnumerateDirectories($"*{Path.GetFileName(pasta).Replace('.', '-')}"))
            d.Delete(recursive: true);
        Assert.Contains(_chamadas, c => c is ("chefe-real", Ferramentas.ListarSessoes, _));
    }
}
