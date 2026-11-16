using Nexora.Core.Claude;
using Nexora.Core.Historico;

namespace Nexora.Testes;

public sealed class HistoricoTestes : IDisposable
{
    readonly string _raiz = Directory.CreateTempSubdirectory("nexora-hist-").FullName;

    public void Dispose() => Directory.Delete(_raiz, recursive: true);

    string Gravar(string projeto, string sessao, params string[] linhas)
    {
        var pasta = Directory.CreateDirectory(Path.Combine(_raiz, projeto)).FullName;
        var arquivo = Path.Combine(pasta, sessao + ".jsonl");
        File.WriteAllLines(arquivo, linhas);
        return arquivo;
    }

    [Fact]
    public void ListaComTituloPastaEBranch()
    {
        Gravar("d--Proj", "s1",
            """{"type":"queue-operation","operation":"enqueue","sessionId":"s1"}""",
            """{"parentUuid":null,"isSidechain":false,"type":"user","message":{"role":"user","content":"Revise o disco C"},"cwd":"D:\\Proj","sessionId":"s1","gitBranch":"main"}""",
            """{"parentUuid":"a","isSidechain":false,"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"Vou olhar"}]},"cwd":"D:\\Proj","gitBranch":"melhorias-20261029"}""",
            """{"type":"ai-title","aiTitle":"Disco C e Visual Studio","sessionId":"s1"}""");

        var c = Assert.Single(ConversasSalvas.Listar(_raiz));
        Assert.Equal("s1", c.SessaoId);
        Assert.Equal("Disco C e Visual Studio", c.Titulo);
        Assert.Equal(@"D:\Proj", c.Pasta);
        Assert.Equal("melhorias-20261029", c.Branch);
    }

    [Fact]
    public void TituloProprioVenceOTituloDaIaESemTituloUsaOPrimeiroPedido()
    {
        Gravar("p", "com-nome",
            """{"type":"user","message":{"content":[{"type":"text","text":"<ide_selection>x</ide_selection>"},{"type":"text","text":"Arruma o login"}]},"cwd":"D:\\A"}""",
            """{"type":"ai-title","aiTitle":"Login"}""",
            """{"type":"custom-title","customTitle":"Issue 480"}""");
        Gravar("p", "sem-nome",
            """{"type":"user","message":{"content":"Arruma o login\ncom detalhes"},"cwd":"D:\\A"}""");

        var lista = ConversasSalvas.Listar(_raiz);
        Assert.Equal("Issue 480", lista.Single(c => c.SessaoId == "com-nome").Titulo);
        Assert.Equal("Arruma o login", lista.Single(c => c.SessaoId == "sem-nome").Titulo);
    }

    [Fact]
    public void IgnoraConversaVaziaESubagente()
    {
        Gravar("p", "vazia", """{"type":"queue-operation","operation":"enqueue"}""");
        Gravar("p", "lateral", """{"type":"user","isSidechain":true,"message":{"content":"tarefa do subagente"},"cwd":"D:\\A"}""");
        Assert.Empty(ConversasSalvas.Listar(_raiz));
    }

    [Fact]
    public void ArquivoGrandeLeCabecaECauda()
    {
        var meio = Enumerable.Repeat("""{"type":"assistant","message":{"content":[{"type":"text","text":"PREENCHIMENTO_PREENCHIMENTO_PREENCHIMENTO_PREENCHIMENTO"}]}}""", 12000);
        var linhas = new[] { """{"type":"user","message":{"content":"Primeiro pedido"},"cwd":"D:\\Grande","gitBranch":"a"}""" }
            .Concat(meio)
            .Append("""{"type":"user","message":{"content":"último"},"cwd":"D:\\Grande","gitBranch":"b"}""")
            .Append("""{"type":"ai-title","aiTitle":"Titulo do fim"}""")
            .ToArray();
        Gravar("g", "grande", linhas);

        var c = Assert.Single(ConversasSalvas.Listar(_raiz));
        Assert.Equal("Titulo do fim", c.Titulo);
        Assert.Equal(@"D:\Grande", c.Pasta);
        Assert.Equal("b", c.Branch);
    }

    [Fact]
    public void TranscricaoTrazMensagensFerramentasEPulaMetaESubagente()
    {
        var arquivo = Gravar("p", "t",
            """{"type":"user","isSidechain":false,"message":{"role":"user","content":"Oi"}}""",
            """{"type":"user","isMeta":true,"message":{"role":"user","content":"Caveat: interno"}}""",
            """{"type":"user","message":{"role":"user","content":"<task-notification>x</task-notification>"}}""",
            """{"type":"assistant","isSidechain":false,"message":{"content":[{"type":"thinking","thinking":""},{"type":"text","text":"Olá!"}]}}""",
            """{"type":"assistant","message":{"content":[{"type":"tool_use","id":"t1","name":"Read","input":{"file_path":"D:\\x.cs"}}]}}""",
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t1","content":"conteúdo"}]}}""",
            """{"type":"assistant","isSidechain":true,"message":{"content":[{"type":"text","text":"do subagente"}]}}""",
            """{"type":"ai-title","aiTitle":"x"}""");

        var eventos = Transcricao.Ler(arquivo);
        Assert.Collection(eventos,
            e => Assert.Equal(new MensagemDoUsuario("Oi"), e),
            e => Assert.Equal(new TextoCompleto("Olá!"), e),
            e => Assert.Equal("Read", Assert.IsType<FerramentaChamada>(e).Nome),
            e => Assert.Equal(new FerramentaRespondeu("t1", "conteúdo", false), e));
    }

    [Fact]
    public void TranscricaoLimitaAosUltimosEventos()
    {
        var linhas = Enumerable.Range(1, 50).Select(i => $$$"""{"type":"user","message":{"content":"msg {{{i}}}"}}""").ToArray();
        var eventos = Transcricao.Ler(Gravar("p", "longa", linhas), maximo: 10);
        Assert.Equal(10, eventos.Count);
        Assert.Equal(new MensagemDoUsuario("msg 50"), eventos[^1]);
    }

    [Fact]
    public void FimDaTranscricaoCresceAteAcharEventosSuficientes()
    {
        var grande = new string('x', 40_000);
        var linhas = Enumerable.Range(1, 30).Select(i => $$$"""{"type":"user","message":{"content":"msg {{{i}}}"}}""")
            .Concat(Enumerable.Range(1, 8).Select(i => $$$"""{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t{{{i}}}","content":"{{{grande}}}"}]}}"""))
            .ToArray();
        var arquivo = Gravar("p", "pesada", linhas);

        var fim = Transcricao.LerFim(arquivo, maximo: 12, bytes: 64 * 1024);
        Assert.Equal(12, fim.Count);
        Assert.Equal(new MensagemDoUsuario("msg 27"), fim[0]);
        Assert.Equal(Transcricao.Ler(arquivo, maximo: 12), fim);
        Assert.Empty(Transcricao.LerFim(Path.Combine(_raiz, "nao-existe.jsonl")));
    }

    [Fact]
    public void AndamentoTrazUltimaAtividadeContextoEBranch()
    {
        var arquivo = Gravar("d--Proj", "s1",
            """{"type":"ai-title","aiTitle":"Integração de arquivos"}""",
            """{"type":"user","message":{"content":"Revise o login"},"cwd":"D:\\Proj","gitBranch":"main","timestamp":"2026-10-30T12:00:00Z"}""",
            """{"type":"assistant","message":{"model":"claude-opus-5-5","content":[{"type":"text","text":"Vou olhar"}],"usage":{"input_tokens":10,"cache_read_input_tokens":40000,"output_tokens":5,"cache_creation":{"ephemeral_1h_input_tokens":200}}},"gitBranch":"login-20261030","timestamp":"2026-10-30T12:00:05Z"}""",
            """{"type":"assistant","message":{"content":[{"type":"tool_use","id":"t1","name":"Read","input":{"file_path":"D:\\Proj\\src\\Login.cs"}}]},"timestamp":"2026-10-30T12:00:09Z"}""",
            """{"type":"assistant","isSidechain":true,"message":{"content":[{"type":"text","text":"do subagente"}]},"timestamp":"2026-10-30T12:00:20Z"}""",
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t1","content":"ok"}]},"timestamp":"2026-10-30T12:00:10Z"}""",
            """{ cortada""");

        var a = Andamento.Ler(arquivo, @"D:\Proj");
        Assert.Equal("Integração de arquivos", a.Titulo);
        Assert.Equal("login-20261030", a.Branch);
        Assert.Equal(@"Read  src\Login.cs", a.Atividade);
        Assert.Equal(DateTimeOffset.Parse("2026-10-30T12:00:09Z"), a.AtividadeEm);
        Assert.Equal(new ContextoUsado(40015, true, "claude-opus-5-5", DateTimeOffset.Parse("2026-10-30T12:00:05Z")), a.Uso);
    }

    [Fact]
    public void AchaOArquivoDaSessaoPelaPastaOuProcurando()
    {
        var pelaPasta = Gravar("d--Repositorios-Acme-Shop", "s1", "{}");
        var perdida = Gravar("outro-nome", "s2", "{}");

        Assert.Equal(pelaPasta, ConversasSalvas.ArquivoDe("s1", @"d:\Repositorios\Acme.Shop", _raiz));
        Assert.Equal(perdida, ConversasSalvas.ArquivoDe("s2", @"D:\Qualquer", _raiz));
        Assert.Null(ConversasSalvas.ArquivoDe("s3", null, _raiz));
    }
}
