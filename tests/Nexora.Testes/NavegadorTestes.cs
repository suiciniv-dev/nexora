using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Navegador;
using Nexora.Core.Orquestra;
using Nexora.ViewModels;

namespace Nexora.Testes;

[Collection("Idioma")]
public sealed class NavegadorTestes
{
    [Theory]
    [InlineData("localhost:5173", "http://localhost:5173/")]
    [InlineData("5173", "http://localhost:5173/")]
    [InlineData(":3000", "http://localhost:3000/")]
    [InlineData("  127.0.0.1:8080/admin?x=1  ", "http://127.0.0.1:8080/admin?x=1")]
    [InlineData("app.localhost:4200", "http://app.localhost:4200/")]
    [InlineData("acme-local.dev.local:44315", "http://acme-local.dev.local:44315/")]
    [InlineData("https://localhost:5001", "https://localhost:5001/")]
    [InlineData("example.com/docs", "https://example.com/docs")]
    [InlineData("about:blank", "about:blank")]
    public void NormalizaEnderecosDigitados(string entrada, string esperado) => Assert.Equal(esperado, Enderecos.Normalizar(entrada));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/segredo.txt")]
    [InlineData("70000")]
    [InlineData("0")]
    [InlineData("palavra")]
    [InlineData("duas palavras")]
    public void RecusaEnderecosInvalidos(string entrada) => Assert.Null(Enderecos.Normalizar(entrada));

    [Fact]
    public void ServidorLocalParadoViraDicaDoPainelExecutar()
    {
        var falha = ErrosDeNavegacao.Descrever("ConnectionAborted", "http://localhost:5173/");
        Assert.True(falha.ServidorParado);
        Assert.Contains("localhost:5173", falha.Titulo);
        Assert.Contains("Executar", falha.Dica);

        var fora = ErrosDeNavegacao.Descrever("CannotConnect", "https://example.com/");
        Assert.False(fora.ServidorParado);
        Assert.Contains("example.com", fora.Titulo);

        Assert.Contains("example.org", ErrosDeNavegacao.Descrever("HostNameNotResolved", "https://example.org/").Titulo);
        Assert.Contains("404", ErrosDeNavegacao.Descrever("Unknown", "http://localhost:3000/x", 404).Titulo);
        Assert.Contains("RedirectFailed", ErrosDeNavegacao.Descrever("RedirectFailed", "http://localhost:3000/").Dica);
    }

    static JsonElement Json(string texto) => JsonDocument.Parse(texto).RootElement.Clone();

    const string AnotacaoDoBotao = """
        {"t":"anotacao","id":3,"seletor":"#novo-pedido","tag":"BUTTON","texto":"  Novo\n   pedido ","html":"<button class=\"btn\" id=\"novo-pedido\">Novo pedido</button>",
         "x":448.4,"y":14,"largura":87,"altura":26,"larguraJanela":559,"alturaJanela":673,"comentario":"  Deixa esse botão maior ","url":"http://localhost:5173/"}
        """;

    [Fact]
    public void LeAnotacaoDaPaginaEMontaAMensagemParaOAgente()
    {
        var a = Anotacoes.Ler(Json(AnotacaoDoBotao))!;
        Assert.Equal(3, a.Id);
        Assert.Equal("button", a.Tag);
        Assert.Equal("Novo pedido", a.Texto);
        Assert.Equal(448, a.X);
        Assert.Equal("Deixa esse botão maior", a.Comentario);
        Assert.Null(Anotacoes.Ler(Json("""{"id":1,"seletor":"a","comentario":"   "}""")));
        Assert.Null(Anotacoes.Ler(Json("""{"seletor":"a","comentario":"x"}""")));

        var outra = a with { Id = 4, Seletor = "main > h2", Texto = "", Html = "", Comentario = "Linha 1\nLinha 2", Url = "http://localhost:5173/clientes" };
        var mensagem = Anotacoes.Mensagem("http://localhost:5173/", [a, outra], comPrint: true);
        Assert.Contains("http://localhost:5173/", mensagem);
        Assert.Contains("print anexo", mensagem);
        Assert.Contains("1. Deixa esse botão maior", mensagem);
        Assert.Contains("`#novo-pedido`", mensagem);
        Assert.Contains("87×26 px, na posição (448, 14)", mensagem);
        Assert.Contains("```html", mensagem);
        Assert.Contains("2. Linha 1\n   Linha 2", mensagem.Replace("\r", ""));
        Assert.Contains("Página: http://localhost:5173/clientes", mensagem);
        Assert.Contains("559×673", mensagem);
        Assert.DoesNotContain("print anexo", Anotacoes.Mensagem("http://localhost:5173/", [a], comPrint: false));
    }

    sealed class NativoFalso : INavegadorNativo
    {
        public List<string> Chamadas { get; } = [];
        public byte[]? Print { get; set; } = [1, 2, 3];
        public void Navegar(string url) => Chamadas.Add("ir " + url);
        public void Voltar() => Chamadas.Add("voltar");
        public void Avancar() => Chamadas.Add("avancar");
        public void Recarregar() => Chamadas.Add("recarregar");
        public Task DesignAsync(bool ligado) { Chamadas.Add("design " + ligado); return Task.CompletedTask; }
        public Task RestaurarAsync(IReadOnlyList<Anotacao> anotacoes) { Chamadas.Add("restaurar " + anotacoes.Count); return Task.CompletedTask; }
        public Task RemoverAsync(int id) { Chamadas.Add("remover " + id); return Task.CompletedTask; }
        public Task LimparAsync() { Chamadas.Add("limpar"); return Task.CompletedTask; }
        public Task<byte[]?> CapturarAsync() { Chamadas.Add("capturar"); return Task.FromResult(Print); }
    }

    static NavegadorVM Novo(List<(string Texto, byte[]? Print)> mandados, string sessao = "sessao-navegador-teste") =>
        new(() => sessao, () => @"C:\nexora-teste-navegador", () => [new ServidorNoAr("npm run dev", "http://localhost:5173/", "Loja", true)], (t, p) => mandados.Add((t, p)));

    [Fact]
    public async Task AnotacoesDaPaginaViramMensagemComPrintELimpamDepois()
    {
        var isolada = JanelaVM.Isolada;
        JanelaVM.Isolada = true;
        try
        {
            var mandados = new List<(string, byte[]?)>();
            var vm = Novo(mandados);
            var nativo = new NativoFalso();
            Assert.True(vm.Vazio);
            Assert.Single(vm.Servidores);
            vm.Nativo = nativo;
            Assert.Empty(nativo.Chamadas);

            vm.AbrirServidorCommand.Execute(vm.Servidores[0]);
            Assert.Equal("ir http://localhost:5173/", nativo.Chamadas[^1]);
            Assert.True(vm.MostraPagina);
            vm.AoTerminar("http://localhost:5173/", true, "Success", 200);
            Assert.Equal("http://localhost:5173/", App.Config.EnderecosDoNavegador["sessao-navegador-teste"]);

            await vm.AlternarDesignCommand.ExecuteAsync(null);
            Assert.True(vm.ModoDesign);
            Assert.Equal("design True", nativo.Chamadas[^1]);
            Assert.False(vm.MandarParaAgenteCommand.CanExecute(null));

            vm.AoReceber(AnotacaoDoBotao);
            vm.AoReceber(AnotacaoDoBotao.Replace("\"id\":3", "\"id\":4").Replace("#novo-pedido", "main h2"));
            vm.AoReceber(AnotacaoDoBotao.Replace("Deixa esse botão maior", "Maior ainda"));
            vm.AoReceber("{\"t\":\"desconhecido\"}");
            vm.AoReceber("nada de json");
            Assert.Equal(2, vm.Anotacoes.Count);
            Assert.Equal("Maior ainda", vm.Anotacoes[0].Comentario);
            Assert.Equal([1, 2], vm.Anotacoes.Select(a => a.Numero));
            Assert.Equal("2 anotações", vm.TextoAnotacoes);

            vm.Anotacoes[0].RemoverCommand.Execute(null);
            Assert.Equal("remover 3", nativo.Chamadas[^1]);
            Assert.Equal(1, vm.Anotacoes[0].Numero);
            vm.AoReceber("""{"t":"removida","id":4}""");
            Assert.False(vm.TemAnotacoes);
            vm.AoReceber(AnotacaoDoBotao);

            vm.AoReceber("""{"t":"sair"}""");
            Assert.False(vm.ModoDesign);
            await vm.AlternarDesignCommand.ExecuteAsync(null);

            await vm.MandarParaAgenteCommand.ExecuteAsync(null);
            var (texto, print) = Assert.Single(mandados);
            Assert.Contains("Deixa esse botão maior", texto);
            Assert.Contains("print anexo", texto);
            Assert.Equal(new byte[] { 1, 2, 3 }, print);
            Assert.Equal(["design False", "capturar", "limpar"], nativo.Chamadas.TakeLast(3));
            Assert.False(vm.TemAnotacoes);
            Assert.False(vm.ModoDesign);

            var outro = Novo([], "outra-sessao");
            Assert.Equal("http://localhost:5173/", outro.Endereco);
            Assert.False(outro.Vazio);
        }
        finally
        {
            App.Config.EnderecosDoNavegador.Remove("sessao-navegador-teste");
            App.Config.EnderecosDoNavegador.Remove(@"pasta:C:\nexora-teste-navegador");
            JanelaVM.Isolada = isolada;
        }
    }

    [Fact]
    public void PaginaQueFalhaMostraOErroETentaDeNovo()
    {
        var isolada = JanelaVM.Isolada;
        JanelaVM.Isolada = true;
        try
        {
            var vm = Novo([], "sessao-falha");
            vm.Endereco = "não é endereço";
            vm.IrCommand.Execute(null);
            Assert.True(vm.TemFalha);
            Assert.Equal("Endereço inválido", vm.TituloFalha);

            var nativo = new NativoFalso();
            vm.Endereco = "4321";
            vm.IrCommand.Execute(null);
            Assert.False(vm.TemFalha);
            vm.Nativo = nativo;
            Assert.Equal("ir http://localhost:4321/", nativo.Chamadas[^1]);
            vm.AoTerminar("http://localhost:4321/", false, "ConnectionAborted", 0);
            Assert.True(vm.TemFalha);
            Assert.True(vm.FalhaDeServidor);
            Assert.False(vm.MostraPagina);
            Assert.False(App.Config.EnderecosDoNavegador.ContainsKey("sessao-falha"));

            vm.RecarregarCommand.Execute(null);
            Assert.Equal("ir http://localhost:4321/", nativo.Chamadas[^1]);
            Assert.False(vm.TemFalha);
            Assert.True(vm.Carregando);

            vm.AoTerminar("http://localhost:4321/", false, "OperationCanceled", 0);
            Assert.False(vm.TemFalha);

            vm.Soltar(nativo);
            Assert.Null(vm.Nativo);
        }
        finally
        {
            App.Config.EnderecosDoNavegador.Remove(@"pasta:C:\nexora-teste-navegador");
            JanelaVM.Isolada = isolada;
        }
    }

    [Fact]
    public async Task AgentePedeParaAbrirONavegadorPeloMcp()
    {
        var abertas = new List<string>();
        Task<bool> Abrir(string url)
        {
            abertas.Add(url);
            return Task.FromResult(true);
        }

        var ok = await FerramentasDoNavegador.ExecutarAsync(FerramentasDoNavegador.AbrirNavegador, Json("""{"url":"localhost:5173"}"""), Abrir);
        Assert.False(ok.Erro);
        Assert.Equal(["http://localhost:5173/"], abertas);
        Assert.True((await FerramentasDoNavegador.ExecutarAsync(FerramentasDoNavegador.AbrirNavegador, Json("""{"url":"javascript:x"}"""), Abrir)).Erro);
        Assert.True((await FerramentasDoNavegador.ExecutarAsync(FerramentasDoNavegador.AbrirNavegador, Json("""{"url":"5173"}"""), _ => Task.FromResult(false))).Erro);
        Assert.Equal(["mcp__nexora-navegador__abrir_navegador"], FerramentasDoNavegador.Livres);

        using var servidor = new ServidorMcp(FerramentasDoNavegador.Todas,
            (dono, ferramenta, argumentos, _) => FerramentasDoNavegador.ExecutarAsync(ferramenta, argumentos, u => Abrir(dono + " " + u)),
            FerramentasDoNavegador.Servidor);
        var chave = servidor.Registrar("s7");
        var config = JsonNode.Parse(servidor.Configuracao(chave))!;
        Assert.NotNull(config["mcpServers"]![FerramentasDoNavegador.Servidor]);
        var inicio = JsonNode.Parse((await servidor.ResponderAsync("s7", """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18"}}""", default))!)!;
        Assert.Equal(FerramentasDoNavegador.Servidor, (string?)inicio["result"]!["serverInfo"]!["name"]);
        var lista = JsonNode.Parse((await servidor.ResponderAsync("s7", """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""", default))!)!;
        Assert.Equal("abrir_navegador", (string?)lista["result"]!["tools"]![0]!["name"]);
        var chamada = JsonNode.Parse((await servidor.ResponderAsync("s7", """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"abrir_navegador","arguments":{"url":"3000"}}}""", default))!)!;
        Assert.False((bool)chamada["result"]!["isError"]!);
        Assert.Equal("s7 http://localhost:3000/", abertas[^1]);
        Assert.Equal("Nexora · abrir navegador", Core.Claude.Resumos.NomeDaFerramenta("mcp__nexora-navegador__abrir_navegador"));

        using var http = new HttpClient();
        var url = (string)config["mcpServers"]![FerramentasDoNavegador.Servidor]!["url"]!;
        using var pedido = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":9,"method":"tools/call","params":{"name":"abrir_navegador","arguments":{"url":"localhost:8080/x"}}}""", System.Text.Encoding.UTF8, "application/json"),
        };
        pedido.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", chave);
        using var resposta = await http.SendAsync(pedido);
        Assert.True(resposta.IsSuccessStatusCode);
        Assert.Equal("s7 http://localhost:8080/x", abertas[^1]);
        using var semChave = await http.PostAsync(url, new StringContent("{}"));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, semChave.StatusCode);
    }

    [Fact]
    public void RoteiroDoModoDesignVaiJuntoComOApp()
    {
        var roteiro = Path.Combine(AppContext.BaseDirectory, "Assets", "Navegador", "design.js");
        Assert.True(File.Exists(roteiro), roteiro);
        var texto = File.ReadAllText(roteiro);
        foreach (var parte in new[] { "window.__nexoraDesign", "ligar", "desligar", "restaurar", "limpar", "foto:", "isComposing", "Escape", "postMessage", "adoptedStyleSheets" })
            Assert.Contains(parte, texto);
    }
}
