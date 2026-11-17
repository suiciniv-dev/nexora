using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Mcp;

namespace Nexora.Testes;

public sealed class HubMcpTestes : IDisposable
{
    const string Servidor = """
        const fs = require('fs');
        const readline = require('readline');
        const [marca, nome] = process.argv.slice(2);
        fs.appendFileSync(marca, `${nome} ${process.pid}\n`);
        const enviar = m => process.stdout.write(JSON.stringify(m) + '\n');
        readline.createInterface({ input: process.stdin }).on('line', l => {
          const m = JSON.parse(l);
          if (m.id === undefined) return;
          if (m.method === 'initialize') return enviar({ jsonrpc: '2.0', id: m.id, result: { protocolVersion: m.params.protocolVersion, capabilities: { tools: {} }, serverInfo: { name: nome, version: '1' } } });
          if (m.method === 'tools/list') return enviar({ jsonrpc: '2.0', id: m.id, result: { tools: [{ name: 'eco', description: 'eco', inputSchema: { type: 'object' } }] } });
          if (m.method === 'tools/call') return enviar({ jsonrpc: '2.0', id: m.id, result: { content: [{ type: 'text', text: `${nome}:${process.pid}:${m.params.arguments.texto}` }] } });
          enviar({ jsonrpc: '2.0', id: m.id, error: { code: -32601, message: 'nao' } });
        });
        """;

    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-hub-").FullName;
    readonly string _casa = Directory.CreateTempSubdirectory("nexora-hub-casa-").FullName;
    readonly string _marca;
    readonly HttpClient _http = new();

    public HubMcpTestes()
    {
        _marca = Path.Combine(_pasta, "marca.txt");
        File.WriteAllText(Path.Combine(_pasta, "servidor.js"), Servidor);
        var marca = _marca.Replace('\\', '/');
        File.WriteAllText(Path.Combine(_pasta, ".mcp.json"), $$"""
            {
              // comentário e vírgula sobrando, como o Claude Code aceita
              "mcpServers": {
                "banco": { "command": "node", "args": ["servidor.js", "{{marca}}", "banco"] },
                "navegador": { "command": "node", "args": ["servidor.js", "{{marca}}", "navegador"] },
                "remoto": { "type": "http", "url": "https://exemplo.com/mcp" },
                "desligado": { "command": "node", "args": ["servidor.js", "{{marca}}", "desligado"] },
                "variavel": { "command": "node", "args": ["${NEXORA_TESTE_NAO_EXISTE:-padrao}"] },
              }
            }
            """);
        Directory.CreateDirectory(Path.Combine(_pasta, ".claude"));
        File.WriteAllText(Path.Combine(_pasta, ".claude", "settings.local.json"), """{ "disabledMcpjsonServers": ["desligado"] }""");
    }

    public void Dispose()
    {
        _http.Dispose();
        foreach (var p in new[] { _pasta, _casa })
            try
            {
                Directory.Delete(p, true);
            }
            catch (IOException)
            {
            }
    }

    static bool TemNode() =>
        (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Any(d => File.Exists(Path.Combine(d.Trim('"'), "node.exe")));

    string[] Subidas() => File.Exists(_marca) ? File.ReadAllLines(_marca) : [];

    [Fact]
    public void LeSoOsServidoresStdioLigados()
    {
        var lista = ServidoresDoProjeto.Ler(_pasta, _casa);
        Assert.Equal(["banco", "navegador", "variavel"], lista.Select(d => d.Nome));
        Assert.False(lista[0].PorSessao);
        Assert.True(lista[1].PorSessao);
        Assert.Equal("padrao", Assert.Single(lista[2].Argumentos));
        Assert.NotEqual(lista[0].Chave, lista[1].Chave);
    }

    [Fact]
    public void DesligadoNoClaudeJsonTambemFicaDeFora()
    {
        File.WriteAllText(Path.Combine(_casa, ".claude.json"), $$"""
            { "projects": { "{{_pasta.Replace('\\', '/').ToUpperInvariant()}}": { "disabledMcpjsonServers": ["banco"] } } }
            """);
        Assert.Equal(["navegador", "variavel"], ServidoresDoProjeto.Ler(_pasta, _casa).Select(d => d.Nome));
    }

    [Fact]
    public void JuntaAConfiguracaoDoOrquestradorComADoHub()
    {
        var junto = JsonNode.Parse(HubMcp.Juntar("""{"mcpServers":{"nexora":{"type":"http","url":"a"}}}""", """{"mcpServers":{"banco":{"type":"http","url":"b"}}}"""))!;
        Assert.Equal(["nexora", "banco"], junto["mcpServers"]!.AsObject().Select(p => p.Key));
        Assert.Equal("{\"mcpServers\":{}}", HubMcp.Juntar("{}", null));
    }

    [Fact]
    public async Task UmProcessoCompartilhadoENavegadorPorSessao()
    {
        if (!TemNode()) return;
        using var hub = new HubMcp(Path.Combine(_casa, "cache"));
        var a = hub.Registrar(_pasta, "A", _casa);
        var b = hub.Registrar(_pasta, "B", _casa);
        Assert.Equal(["banco", "navegador", "variavel"], a.Nomes);

        foreach (var r in new[] { a, b })
        {
            await Inicializar(r, "banco");
            Assert.Contains("banco:", await Chamar(r, "banco", "oi"));
            Assert.Contains("navegador:", await Chamar(r, "navegador", "oi"));
        }

        var subidas = Subidas();
        Assert.Single(subidas, s => s.StartsWith("banco "));
        Assert.Equal(2, subidas.Count(s => s.StartsWith("navegador ")));
        Assert.DoesNotContain(subidas, s => s.StartsWith("desligado "));

        var estado = hub.Estado();
        Assert.Single(estado, e => e.Nome == "banco" && e.Ativo && e.Sessoes == 2);
        Assert.Equal(2, estado.Count(e => e.Nome == "navegador" && e.Ativo));
        Assert.True(estado.First(e => e.Nome == "banco").Memoria > 0);

        await hub.EsquecerAsync(a.Chave);
        estado = hub.Estado();
        Assert.Single(estado, e => e.Nome == "navegador");
        Assert.Equal(1, estado.Single(e => e.Nome == "banco").Sessoes);
    }

    [Fact]
    public async Task SessaoNovaUsaOCacheSemSubirOServidor()
    {
        if (!TemNode()) return;
        using var hub = new HubMcp(Path.Combine(_casa, "cache"));
        var a = hub.Registrar(_pasta, "A", _casa);
        Assert.Equal(-32601, (await Pedir(a, "banco", "server/discover", null))["error"]!["code"]!.GetValue<int>());
        await Inicializar(a, "banco");
        await Pedir(a, "banco", "tools/list", null);
        await Pedir(a, "banco", "prompts/list", null);
        var instancia = Assert.Single(hub.Estado(), e => e.Nome == "banco").Instancia;
        Assert.Equal("server/discover", Assert.Single(hub.Estado(), e => e.Nome == "banco").SubiuPor);
        await hub.PararAsync(instancia);

        using var outroHub = new HubMcp(Path.Combine(_casa, "cache"));
        var b = outroHub.Registrar(_pasta, "B", _casa);
        Assert.Equal(-32601, (await Pedir(b, "banco", "server/discover", null))["error"]!["code"]!.GetValue<int>());
        var inicio = await Inicializar(b, "banco");
        Assert.Equal("banco", inicio["result"]!["serverInfo"]!["name"]!.GetValue<string>());
        var lista = await Pedir(b, "banco", "tools/list", null);
        Assert.Equal("eco", lista["result"]!["tools"]![0]!["name"]!.GetValue<string>());
        Assert.Equal(-32601, (await Pedir(b, "banco", "prompts/list", null))["error"]!["code"]!.GetValue<int>());
        Assert.Single(Subidas(), s => s.StartsWith("banco "));
        Assert.DoesNotContain(outroHub.Estado(), e => e.Ativo);

        Assert.Contains("banco:", await Chamar(b, "banco", "depois"));
        Assert.Equal(2, Subidas().Count(s => s.StartsWith("banco ")));
    }

    [Fact]
    public async Task RecusaSemChaveOuDeOutroProjeto()
    {
        using var hub = new HubMcp();
        var a = hub.Registrar(_pasta, "A", _casa);
        var url = Url(a, "banco");
        using var sem = await _http.PostAsync(url, new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, sem.StatusCode);

        var vazio = hub.Registrar(_casa, "vazio", _casa);
        Assert.Null(vazio.Configuracao);
        using var pedido = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", vazio.Chave);
        using var outro = await _http.SendAsync(pedido);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, outro.StatusCode);
    }

    [Fact]
    public async Task DuasSessoesDoClaudeUsamOMesmoServidor()
    {
        if (Environment.GetEnvironmentVariable("NEXORA_INTEGRACAO") != "1" || !TemNode()) return;
        using var hub = new HubMcp(Path.Combine(_casa, "cache"));
        var sessoes = new List<Core.Claude.SessaoClaude>();
        try
        {
            foreach (var rotulo in new[] { "A", "B" })
            {
                var registro = hub.Registrar(_pasta, rotulo);
                var sessao = new Core.Claude.SessaoClaude(new Core.Claude.OpcoesSessao(_pasta, Isolada: true, Mcp: registro.Configuracao));
                sessao.Iniciar();
                sessoes.Add(sessao);
            }
            foreach (var sessao in sessoes)
            {
                Core.Claude.ControleRespondido status = default!;
                for (var tentativa = 0; tentativa < 20; tentativa++)
                {
                    status = await sessao.PedirAsync(new JsonObject { ["subtype"] = "mcp_status" });
                    if (status.Dados is { } d && d.GetProperty("mcpServers").EnumerateArray().Any(s => s.GetProperty("name").GetString() == "banco" && s.GetProperty("status").GetString() == "connected")) break;
                    await Task.Delay(500);
                }
                var banco = status.Dados!.Value.GetProperty("mcpServers").EnumerateArray().Single(s => s.GetProperty("name").GetString() == "banco");
                Assert.Equal("connected", banco.GetProperty("status").GetString());
                Assert.Equal("dynamic", banco.GetProperty("scope").GetString());
            }
            Assert.Single(Subidas(), s => s.StartsWith("banco "));
            Assert.DoesNotContain(Subidas(), s => s.StartsWith("desligado "));
        }
        finally
        {
            foreach (var s in sessoes) await s.DisposeAsync();
        }
    }

    static string Url(RegistroHub r, string nome) =>
        JsonNode.Parse(r.Configuracao!)!["mcpServers"]![nome]!["url"]!.GetValue<string>();

    Task<JsonObject> Inicializar(RegistroHub r, string nome) =>
        Pedir(r, nome, "initialize", new JsonObject { ["protocolVersion"] = "2025-06-18", ["capabilities"] = new JsonObject() });

    async Task<string> Chamar(RegistroHub r, string nome, string texto)
    {
        var resposta = await Pedir(r, nome, "tools/call", new JsonObject { ["name"] = "eco", ["arguments"] = new JsonObject { ["texto"] = texto } });
        return resposta["result"]!["content"]![0]!["text"]!.GetValue<string>();
    }

    async Task<JsonObject> Pedir(RegistroHub r, string nome, string metodo, JsonObject? parametros)
    {
        var corpo = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = Guid.NewGuid().ToString("N"), ["method"] = metodo };
        if (parametros is not null) corpo["params"] = parametros;
        using var pedido = new HttpRequestMessage(HttpMethod.Post, Url(r, nome)) { Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json") };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", r.Chave);
        using var resposta = await _http.SendAsync(pedido);
        resposta.EnsureSuccessStatusCode();
        return (JsonNode.Parse(await resposta.Content.ReadAsStringAsync()) as JsonObject)!;
    }
}
