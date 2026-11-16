using Nexora.Core.Uso;

namespace Nexora.Testes;

public sealed class UsoTestes : IDisposable
{
    readonly string _pasta = Directory.CreateTempSubdirectory("nexora-uso-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_pasta, true);
        }
        catch (IOException)
        {
        }
    }

    const string Saida = """
        You are currently using your subscription to power your Claude Code usage

        Current session: 14% used · resets Nov 4, 10:30pm (America/Sao_Paulo)
        Current week (all models): 19.5% used · resets Nov 10, 7pm (America/Sao_Paulo)
        Current week (Fable): 0% used · resets in 2d 3h (America/Sao_Paulo)
        """;

    [Fact]
    public void LeAsJanelasDoUsage()
    {
        var agora = new DateTime(2026, 11, 4, 18, 0, 0);
        var plano = PlanoDoClaude.Interpretar(Saida, agora)!;
        Assert.Equal(14, plano.Sessao!.Percentual);
        Assert.Equal(new DateTime(2026, 11, 4, 22, 30, 0), plano.Sessao.Reinicia);
        Assert.Equal(19.5, plano.Semana!.Percentual);
        Assert.Equal(new DateTime(2026, 11, 10, 19, 0, 0), plano.Semana.Reinicia);
        var fable = Assert.Single(plano.PorModelo);
        Assert.Equal("Fable", fable.Nome);
        Assert.Equal(agora.AddDays(2).AddHours(3), fable.Reinicia);
    }

    [Fact]
    public void SemJanelasNaoInventaPlano() => Assert.Null(PlanoDoClaude.Interpretar("Login required", DateTime.Now));

    [Fact]
    public void PesoSegueOPrecoDeCadaModelo()
    {
        var milhao = new TokensDaResposta(1_000_000, 0, 0, 0, 0);
        Assert.Equal(4, PesoDoModelo.Peso("claude-opus-5-5", milhao), 6);
        Assert.Equal(2, PesoDoModelo.Peso("claude-sonnet-5-5", milhao), 6);
        Assert.Equal(10, PesoDoModelo.Peso("claude-fable-5-1", milhao), 6);
        Assert.Equal(0.2, PesoDoModelo.Peso("claude-opus-5-5", new TokensDaResposta(0, 0, 0, 0, 1_000_000)), 6);
        Assert.Equal(8, PesoDoModelo.Peso("claude-opus-5-5", new TokensDaResposta(0, 0, 0, 1_000_000, 0)), 6);
        Assert.Equal(8, PesoDoModelo.Peso("claude-opus-5-5", new TokensDaResposta(1_000_000, 0, 0, 0, 0, Rapido: true)), 6);
        Assert.Equal("Opus", PesoDoModelo.Familia("claude-opus-5-5"));
    }

    static string Linha(string id, string cwd, string quando, int entrada, int saida, string modelo = "claude-opus-5-5") =>
        "{\"type\":\"assistant\",\"cwd\":\"" + cwd.Replace("\\", "\\\\") + "\",\"sessionId\":\"s-" + cwd.Length + "\",\"timestamp\":\"" + quando
        + "\",\"message\":{\"id\":\"" + id + "\",\"model\":\"" + modelo + "\",\"usage\":{\"input_tokens\":" + entrada + ",\"output_tokens\":" + saida
        + ",\"cache_read_input_tokens\":0}}}";

    [Fact]
    public void SomaPorPastaESemanaSemContarDuasVezesAMesmaResposta()
    {
        var projeto = Path.Combine(_pasta, "projetos", "loja");
        Directory.CreateDirectory(projeto);
        var arquivo = Path.Combine(projeto, "a.jsonl");
        var cache = Path.Combine(_pasta, "cache.json");
        File.WriteAllLines(arquivo,
        [
            Linha("m1", @"D:\Projetos\Loja", "2026-11-03T12:00:00Z", 100, 10),
            Linha("m1", @"D:\Projetos\Loja", "2026-11-03T12:00:00Z", 100, 10),
            """{"type":"user","message":{"content":"oi"}}""",
            Linha("m2", @"D:\Projetos\Site", "2026-11-04T12:00:00Z", 50, 5, "claude-sonnet-5-5"),
            Linha("m0", @"D:\Projetos\Loja", "2026-10-20T12:00:00Z", 999, 9),
        ]);
        var desde = new DateTime(2026, 10, 1);

        var parcelas = UsoLocal.Ler(Path.Combine(_pasta, "projetos"), cache, desde);
        var loja = parcelas.Single(p => p.Pasta.EndsWith("Loja") && p.Semana == "2026-11-02");
        Assert.Equal(100, loja.Entrada);
        Assert.Equal(10, loja.Saida);
        Assert.Equal(1, loja.Respostas);
        Assert.Contains(parcelas, p => p.Pasta.EndsWith("Loja") && p.Semana == "2026-10-19");
        Assert.Contains(parcelas, p => p.Pasta.EndsWith("Site") && p.Modelos.ContainsKey("Sonnet"));

        File.AppendAllLines(arquivo, [Linha("m3", @"D:\Projetos\Loja", "2026-11-04T13:00:00Z", 20, 2), Linha("m3", @"D:\Projetos\Loja", "2026-11-04T13:00:00Z", 20, 2)]);
        parcelas = UsoLocal.Ler(Path.Combine(_pasta, "projetos"), cache, desde);
        loja = parcelas.Single(p => p.Pasta.EndsWith("Loja") && p.Semana == "2026-11-02");
        Assert.Equal(120, loja.Entrada);
        Assert.Equal(2, loja.Respostas);
    }

    [Fact]
    public void SeparaPorAgenteEModeloComCodexEGemini()
    {
        var claude = Path.Combine(_pasta, "claude", "loja");
        var codex = Path.Combine(_pasta, "codex", "2026", "11", "04");
        var gemini = Path.Combine(_pasta, "gemini", "tmp", "loja", "chats");
        Directory.CreateDirectory(claude);
        Directory.CreateDirectory(codex);
        Directory.CreateDirectory(gemini);
        File.WriteAllLines(Path.Combine(claude, "a.jsonl"),
        [
            Linha("m1", @"D:\Projetos\Loja", "2026-11-03T12:00:00Z", 100, 10),
            Linha("m2", @"D:\Projetos\Loja", "2026-11-03T13:00:00Z", 100, 10, "claude-sonnet-5-5"),
        ]);
        var rollout = Path.Combine(codex, "rollout-1.jsonl");
        File.WriteAllLines(rollout,
        [
            """{"timestamp":"2026-11-04T10:00:00Z","type":"session_meta","payload":{"id":"cx1","cwd":"D:\\Projetos\\Loja"}}""",
            """{"timestamp":"2026-11-04T10:00:01Z","type":"turn_context","payload":{"cwd":"D:\\Projetos\\Loja","model":"gpt-5.1-codex"}}""",
            """{"timestamp":"2026-11-04T10:00:05Z","type":"event_msg","payload":{"type":"token_count","info":{"total_token_usage":{"total_tokens":300},"last_token_usage":{"input_tokens":200,"cached_input_tokens":50,"output_tokens":100}}}}""",
            """{"timestamp":"2026-11-04T10:00:06Z","type":"event_msg","payload":{"type":"token_count","info":{"total_token_usage":{"total_tokens":300},"last_token_usage":{"input_tokens":200,"cached_input_tokens":50,"output_tokens":100}}}}""",
        ]);
        File.WriteAllText(Path.Combine(_pasta, "gemini", "projects.json"), """{"projects":{"d:\\projetos\\loja":"loja"}}""");
        File.WriteAllText(Path.Combine(gemini, "session-1.json"),
            """{"sessionId":"g1","messages":[{"type":"user","content":"oi"},{"type":"gemini","timestamp":"2026-11-05T10:00:00Z","model":"gemini-2.5-pro","tokens":{"input":40,"output":7,"cached":10,"thoughts":3}}]}""");
        FonteDeUso[] fontes =
        [
            new(UsoLocal.Claude, Path.Combine(_pasta, "claude"), "*.jsonl"),
            new(UsoLocal.Codex, Path.Combine(_pasta, "codex"), "*.jsonl"),
            new(UsoLocal.Gemini, Path.Combine(_pasta, "gemini", "tmp"), "session-*.json"),
        ];
        var cache = Path.Combine(_pasta, "cache.json");

        var parcelas = UsoLocal.Ler(fontes, cache, new DateTime(2026, 10, 1));

        Assert.Equal(2, parcelas.Count(p => p.Agente == UsoLocal.Claude));
        var cx = Assert.Single(parcelas, p => p.Agente == UsoLocal.Codex);
        Assert.Equal("GPT-5.1 Codex", cx.Modelo);
        Assert.Equal(@"D:\Projetos\Loja", cx.Pasta);
        Assert.Equal(150, cx.Entrada);
        Assert.Equal(50, cx.Lida);
        Assert.Equal(1, cx.Respostas);
        var ge = Assert.Single(parcelas, p => p.Agente == UsoLocal.Gemini);
        Assert.Equal("Gemini Pro", ge.Modelo);
        Assert.Equal(@"d:\projetos\loja", ge.Pasta);
        Assert.Equal(10, ge.Saida);

        File.AppendAllLines(rollout,
        [
            """{"timestamp":"2026-11-04T10:01:00Z","type":"event_msg","payload":{"type":"token_count","info":{"total_token_usage":{"total_tokens":420},"last_token_usage":{"input_tokens":100,"cached_input_tokens":80,"output_tokens":20}}}}""",
        ]);
        parcelas = UsoLocal.Ler(fontes, cache, new DateTime(2026, 10, 1));
        cx = Assert.Single(parcelas, p => p.Agente == UsoLocal.Codex);
        Assert.Equal(2, cx.Respostas);
        Assert.Equal("GPT-5.1 Codex", cx.Modelo);

        Assert.Equal([UsoLocal.Claude, UsoLocal.Codex, UsoLocal.Gemini], Nexora.ViewModels.UsoVM.OpcoesDeAgente(parcelas).Order());
        Assert.Equal(["Opus", "Sonnet"], Nexora.ViewModels.UsoVM.OpcoesDeModelo(parcelas, UsoLocal.Claude).Order());
        var soSonnet = Nexora.ViewModels.UsoVM.Filtrar(parcelas, UsoLocal.Claude, "Sonnet");
        Assert.Equal(1, Assert.Single(soSonnet).Respostas);
        Assert.Equal(parcelas.Count, Nexora.ViewModels.UsoVM.Filtrar(parcelas, null, null).Count);
    }

    [Fact]
    public void FamiliasDeOutrosAgentes()
    {
        Assert.Equal("GPT-5 Codex Mini", PesoDoModelo.Familia("gpt-5-codex-mini"));
        Assert.Equal("GPT-5", PesoDoModelo.Familia("gpt-5"));
        Assert.Equal("Gemini Flash", PesoDoModelo.Familia("gemini-2.5-flash"));
        Assert.True(PesoDoModelo.Peso("gemini-2.5-flash", new TokensDaResposta(1_000_000, 0, 0, 0, 0)) < 1);
    }

    [Fact]
    public void UsoDesteComputador()
    {
        if (Environment.GetEnvironmentVariable("NEXORA_MEDIR") != "1") return;
        var cache = Path.Combine(_pasta, "cache.json");
        var relogio = System.Diagnostics.Stopwatch.StartNew();
        var parcelas = UsoLocal.Ler(UsoLocal.RaizPadrao, cache, DateTime.Now.AddDays(-56));
        var primeira = relogio.Elapsed;
        relogio.Restart();
        UsoLocal.Ler(UsoLocal.RaizPadrao, cache, DateTime.Now.AddDays(-56));
        var segunda = relogio.Elapsed;
        var semana = UsoLocal.Chave(UsoLocal.SemanaDe(DateTime.Now));
        var desta = parcelas.Where(p => p.Semana == semana).ToList();
        var total = desta.Sum(p => p.Peso);
        Console.WriteLine($"primeira leitura {primeira.TotalSeconds:0.0}s, segunda {segunda.TotalSeconds:0.00}s, cache {new FileInfo(cache).Length / 1024} KB, {parcelas.Count} parcelas");
        foreach (var p in desta.OrderByDescending(p => p.Peso).Take(8))
            Console.WriteLine($"{p.Pasta}: {p.Peso / total:P0}, {p.Tokens / 1_000_000.0:0.0} mi tokens, {p.Sessoes.Count} sessões, {string.Join(" ", p.Modelos.Select(m => $"{m.Key} {m.Value / p.Peso:P0}"))}");
    }

    [Fact]
    public void SemanaComecaNaSegunda()
    {
        Assert.Equal(new DateOnly(2026, 11, 2), UsoLocal.SemanaDe(new DateTime(2026, 11, 8, 23, 0, 0)));
        Assert.Equal(new DateOnly(2026, 11, 2), UsoLocal.SemanaDe(new DateTime(2026, 11, 2, 0, 1, 0)));
    }
}
