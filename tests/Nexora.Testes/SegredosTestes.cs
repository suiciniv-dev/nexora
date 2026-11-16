using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Claude;
using Nexora.Core.Codex;
using Nexora.Core.Segredos;

namespace Nexora.Testes;

public sealed class SegredosTestes : IDisposable
{
    const string Valor = "sk-teste-NAO-E-REAL-9f3b1c";

    readonly string _repo = Directory.CreateTempSubdirectory("nexora-segredos-").FullName;
    readonly string _dados = Directory.CreateTempSubdirectory("nexora-segredos-dados-").FullName;

    public SegredosTestes()
    {
        Git("init", "-q");
        File.WriteAllText(Path.Combine(_repo, ".gitignore"), ".env\n.env.local\n");
    }

    public void Dispose()
    {
        foreach (var pasta in new[] { _repo, _dados })
            try
            {
                foreach (var arquivo in Directory.EnumerateFiles(pasta, "*", SearchOption.AllDirectories)) File.SetAttributes(arquivo, FileAttributes.Normal);
                Directory.Delete(pasta, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
    }

    void Git(params string[] argumentos)
    {
        var inicio = new ProcessStartInfo("git") { WorkingDirectory = _repo, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in argumentos) inicio.ArgumentList.Add(a);
        using var processo = Process.Start(inicio)!;
        processo.StandardOutput.ReadToEnd();
        processo.StandardError.ReadToEnd();
        processo.WaitForExit(20_000);
    }

    static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public async Task PedidoValidaNomeDestinoEGitignore()
    {
        var (ok, erro) = await LeituraDoPedido.LerAsync(Args("""{"nome":"OPENAI_API_KEY","motivo":"testes","destino":"env"}"""), _repo);
        Assert.Null(erro);
        Assert.Equal(".env", ok!.Arquivo);
        Assert.Equal(DestinoDoSegredo.ArquivoEnv, ok.Destino);

        var (local, _) = await LeituraDoPedido.LerAsync(Args("""{"nome":"X","motivo":"m","destino":"env","arquivo":"./.env.local"}"""), _repo);
        Assert.Equal(".env.local", local!.Arquivo);

        var (variavel, _) = await LeituraDoPedido.LerAsync(Args("""{"nome":"TOKEN","motivo":"m","destino":"ambiente"}"""), _repo);
        Assert.Equal(DestinoDoSegredo.Variavel, variavel!.Destino);
        Assert.Null(variavel.Arquivo);

        foreach (var invalido in new[]
        {
            """{"nome":"1ABC","motivo":"m","destino":"env"}""",
            """{"nome":"A B","motivo":"m","destino":"env"}""",
            """{"nome":"A","motivo":"","destino":"env"}""",
            """{"nome":"A","motivo":"m","destino":"nuvem"}""",
            """{"nome":"A","motivo":"m","destino":"env","arquivo":"../.env"}""",
            """{"nome":"A","motivo":"m","destino":"env","arquivo":"appsettings.json"}""",
        })
        {
            var (pedido, problema) = await LeituraDoPedido.LerAsync(Args(invalido), _repo);
            Assert.Null(pedido);
            Assert.False(string.IsNullOrWhiteSpace(problema));
        }

        var (fora, motivo) = await LeituraDoPedido.LerAsync(Args("""{"nome":"A","motivo":"m","destino":"env","arquivo":".env.production"}"""), _repo);
        Assert.Null(fora);
        Assert.Contains(".gitignore", motivo);
    }

    [Fact]
    public async Task EnvJaRastreadoPeloGitERecusado()
    {
        File.WriteAllText(Path.Combine(_repo, ".env"), "A=1\n");
        Git("add", "-f", ".env");
        var (pedido, motivo) = await LeituraDoPedido.LerAsync(Args("""{"nome":"A","motivo":"m","destino":"env"}"""), _repo);
        Assert.Null(pedido);
        Assert.Contains("Recusado", motivo);
    }

    [Fact]
    public async Task PastaSemGitRecusaOEnv()
    {
        var solta = Directory.CreateTempSubdirectory("nexora-segredos-solta-").FullName;
        try
        {
            var (pedido, motivo) = await LeituraDoPedido.LerAsync(Args("""{"nome":"A","motivo":"m","destino":"env"}"""), solta);
            Assert.Null(pedido);
            Assert.Contains("ambiente", motivo);
        }
        finally
        {
            Directory.Delete(solta, true);
        }
    }

    [Fact]
    public void EnvTrocaAChaveSemMexerNoResto()
    {
        var arquivo = Path.Combine(_repo, ".env");
        File.WriteAllText(arquivo, "# comentário\r\nOUTRA=1\r\nexport CHAVE=velha\r\nCHAVE_LONGA=2\r\n");
        ArquivoEnv.Gravar(arquivo, "CHAVE", "nova valor \"com\" aspas");
        Assert.Equal("# comentário\r\nOUTRA=1\r\nexport CHAVE=\"nova valor \\\"com\\\" aspas\"\r\nCHAVE_LONGA=2\r\n", File.ReadAllText(arquivo));
        ArquivoEnv.Gravar(arquivo, "NOVA", "abc-123_x");
        Assert.EndsWith("CHAVE_LONGA=2\r\nNOVA=abc-123_x\r\n", File.ReadAllText(arquivo));
        Assert.True(ArquivoEnv.Remover(arquivo, "CHAVE"));
        Assert.Equal("# comentário\r\nOUTRA=1\r\nCHAVE_LONGA=2\r\nNOVA=abc-123_x\r\n", File.ReadAllText(arquivo));
        Assert.False(ArquivoEnv.Remover(arquivo, "NAO_TEM"));
    }

    [Fact]
    public void CofreGuardaSemOValorNoIndiceEInjetaSoNoProjeto()
    {
        var armazem = new ArmazemNaMemoria();
        var cofre = new CofreDeSegredos(_dados, armazem);
        var guardado = cofre.GuardarVariavel("TOKEN", _repo, Valor);
        cofre.GuardarVariavel("TOKEN", _repo, Valor + "2");
        Assert.Single(cofre.Lista());

        Assert.DoesNotContain(Valor, File.ReadAllText(Path.Combine(_dados, "segredos.json")));
        Assert.Equal(Valor + "2", cofre.VariaveisDe(_repo)["TOKEN"]);
        Assert.Equal(Valor + "2", cofre.VariaveisDe(Path.Combine(_repo, "src", "Api"))["TOKEN"]);
        Assert.Empty(cofre.VariaveisDe(_repo + "-outro"));
        Assert.Empty(cofre.VariaveisDe(Path.GetTempPath()));

        var deNovo = new CofreDeSegredos(_dados, armazem);
        Assert.Equal(guardado.Id, Assert.Single(deNovo.Lista()).Id);
        deNovo.Apagar(guardado.Id);
        Assert.Empty(deNovo.Lista());
        Assert.Null(armazem.Ler(guardado.Id));
    }

    [Fact]
    public void ApagarSegredoDoEnvTiraALinha()
    {
        var arquivo = Path.Combine(_repo, ".env");
        File.WriteAllText(arquivo, "FICA=1\n");
        ArquivoEnv.Gravar(arquivo, "SAI", Valor);
        var cofre = new CofreDeSegredos(_dados, new ArmazemNaMemoria());
        var item = cofre.RegistrarArquivo("SAI", _repo, ".env");
        Assert.Equal(DestinoDoSegredo.ArquivoEnv, item.Destino);
        Assert.Empty(cofre.VariaveisDe(_repo));
        cofre.Apagar(item.Id);
        Assert.Equal("FICA=1\n", File.ReadAllText(arquivo));
    }

    [Fact]
    public void DpapiGuardaCifradoEDevolveOValor()
    {
        if (!OperatingSystem.IsWindows()) return;
        var pasta = Path.Combine(_dados, "valores");
        var armazem = new ArmazemDpapi(pasta);
        armazem.Gravar("abc", Valor);
        var bytes = File.ReadAllBytes(Path.Combine(pasta, "abc.bin"));
        Assert.DoesNotContain(Valor, Encoding.UTF8.GetString(bytes));
        Assert.DoesNotContain(Valor, Encoding.Unicode.GetString(bytes));
        Assert.Equal(Valor, armazem.Ler("abc"));
        armazem.Apagar("abc");
        Assert.Null(armazem.Ler("abc"));
    }

    [Fact]
    public async Task FerramentaPeloHttpDevolveSoAConfirmacao()
    {
        using var servico = new ServicoDeSegredos(new CofreDeSegredos(_dados, new ArmazemNaMemoria()));
        var pedidos = new List<PedidoDeSegredo>();
        var chave = servico.Registrar("s1", _repo, p =>
        {
            lock (pedidos) pedidos.Add(p);
            _ = Task.Run(async () => Assert.Null(await servico.SalvarAsync(p.Id, Valor)));
        });
        var config = JsonNode.Parse(servico.Configuracao(chave))!["mcpServers"]![FerramentaDeSegredo.Servidor]!;
        var url = config["url"]!.GetValue<string>();
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", chave);

        var lista = await Post(http, url, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");
        Assert.Contains(FerramentaDeSegredo.Pedir, lista);

        var resposta = await Post(http, url, """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"pedir_segredo","arguments":{"nome":"OPENAI_API_KEY","motivo":"testes de integração","destino":"env"}}}""");
        Assert.DoesNotContain(Valor, resposta);
        var texto = JsonDocument.Parse(resposta).RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString();
        Assert.Contains("Guardado em .env como OPENAI_API_KEY", texto);
        Assert.Single(pedidos);
        Assert.Contains($"OPENAI_API_KEY={Valor}", File.ReadAllText(Path.Combine(_repo, ".env")));
        Assert.Single(servico.Cofre.Lista());

        using var semChave = new HttpClient();
        var negado = await semChave.PostAsync(url, new StringContent("""{"jsonrpc":"2.0","id":3,"method":"tools/list"}""", Encoding.UTF8, "application/json"));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, negado.StatusCode);
    }

    static async Task<string> Post(HttpClient http, string url, string corpo)
    {
        using var r = await http.PostAsync(url, new StringContent(corpo, Encoding.UTF8, "application/json"));
        return await r.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task RecusaEEsperaLongaVoltamParaOAgente()
    {
        using var servico = new ServicoDeSegredos(new CofreDeSegredos(_dados, new ArmazemNaMemoria())) { Espera = TimeSpan.FromMilliseconds(150) };
        var mostrados = new List<PedidoDeSegredo>();
        servico.Registrar("s1", _repo, p =>
        {
            lock (mostrados) mostrados.Add(p);
        });
        var args = Args("""{"nome":"TOKEN","motivo":"deploy","destino":"ambiente"}""");

        var primeira = await servico.ChamarAsync("s1", args, default);
        Assert.False(primeira.Erro);
        Assert.Contains("ainda não respondeu", primeira.Texto);
        var segunda = servico.ChamarAsync("s1", args, default);
        await Task.Delay(30);
        var pedido = Assert.Single(mostrados);
        Assert.True(servico.Aberto(pedido.Id));
        servico.Recusar(pedido.Id);
        Assert.Contains("recusou", (await segunda).Texto);
        Assert.Empty(servico.Cofre.Lista());

        var desconhecida = await servico.ChamarAsync("s9", args, default);
        Assert.True(desconhecida.Erro);
        var invalida = await servico.ChamarAsync("s1", Args("""{"nome":"TOKEN","motivo":"m","destino":"env","arquivo":"segredo.txt"}"""), default);
        Assert.True(invalida.Erro);
    }

    [Fact]
    public async Task VariavelGuardadaEntraNasOpcoesDaSessao()
    {
        using var servico = new ServicoDeSegredos(new CofreDeSegredos(_dados, new ArmazemNaMemoria()));
        servico.Registrar("s1", _repo, p => _ = Task.Run(() => servico.SalvarAsync(p.Id, Valor)));
        var r = await servico.ChamarAsync("s1", Args("""{"nome":"DEPLOY_TOKEN","motivo":"deploy","destino":"ambiente"}"""), default);
        Assert.DoesNotContain(Valor, r.Texto);
        Assert.Contains("variável de ambiente DEPLOY_TOKEN", r.Texto);

        var opcoes = new OpcoesSessao(_repo, Ambiente: servico.Cofre.VariaveisDe(_repo));
        var info = new ProcessStartInfo("x");
        opcoes.Aplicar(info);
        Assert.Equal(Valor, info.Environment["DEPLOY_TOKEN"]);
        Assert.DoesNotContain(SessaoClaude.Argumentos(opcoes), a => a.Contains(Valor));
        Assert.Contains("DEPLOY_TOKEN=" + Valor + "\0", Core.Terminal.Pseudoconsole.BlocoDeAmbiente(servico.Cofre.VariaveisDe(_repo)));
    }

    [Fact]
    public void CodexEsperaMaisPeloServidorDeSegredos()
    {
        var mcp = """{"mcpServers":{"nexora-segredos":{"type":"http","url":"http://localhost:1/mcp"},"outro":{"type":"http","url":"http://localhost:2/mcp"}}}""";
        var servidores = ProtocoloCodex.ServidoresMcp(mcp).ToDictionary(s => s.Nome, s => s.Servidor);
        Assert.Equal(300, servidores["nexora-segredos"]["tool_timeout_sec"]!.GetValue<int>());
        Assert.Null(servidores["outro"]["tool_timeout_sec"]);
    }
}
