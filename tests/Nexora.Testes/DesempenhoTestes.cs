using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Nexora.Core.Desempenho;
using Nexora.ViewModels;
using Xunit.Abstractions;

namespace Nexora.Testes;

public sealed partial class DesempenhoTestes(ITestOutputHelper saida)
{
    const long Mega = 1L << 20;
    static readonly DateOnly Dia = new(2026, 11, 4);

    static Amostra Amostra(DateTime hora, long ramMb = 300, double cpu = 1, int atraso = 20, int travadas = 0, int abas = 3, int trabalhando = 1) => new()
    {
        Hora = hora,
        NexoraRam = ramMb * Mega / 2,
        NexoraWs = ramMb * Mega,
        NexoraPrivada = ramMb * Mega,
        Heap = 90 * Mega,
        Handles = 1800,
        CpuNexora = cpu / 2,
        Sessoes = new Consumo(ramMb * Mega / 4, cpu / 2, 2),
        Mcps = new Consumo(ramMb * Mega / 8, 0, 1),
        Builds = new Consumo(ramMb * Mega / 8, 0, 1),
        AtrasoMaximo = atraso,
        Travadas = travadas,
        Abas = abas,
        Trabalhando = trabalhando,
        Custo = 2,
    };

    [Fact]
    public void CpuDivideOTempoUsadoPeloIntervaloEPelosNucleos()
    {
        Assert.Equal(2.5, Cpu.Percentual(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), 4), 6);
        Assert.Equal(25, Cpu.Percentual(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30), 4), 6);
        Assert.Equal(100, Cpu.Percentual(TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(30), 1));
        Assert.Equal(0, Cpu.Percentual(TimeSpan.Zero, TimeSpan.FromSeconds(30), 8));
        Assert.Equal(0, Cpu.Percentual(TimeSpan.FromSeconds(-1), TimeSpan.FromSeconds(30), 8));
        Assert.Equal(0, Cpu.Percentual(TimeSpan.FromSeconds(1), TimeSpan.Zero, 8));
        Assert.Equal(0, Cpu.Percentual(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), 0));
    }

    [Fact]
    public void PercentilUsaOPostoMaisProximo()
    {
        double[] vinte = [.. Enumerable.Range(1, 20).Select(i => (double)i)];
        Assert.Equal(19, Agregacao.Percentil(vinte, 0.95));
        Assert.Equal(20, Agregacao.Percentil(vinte, 1));
        Assert.Equal(1, Agregacao.Percentil(vinte, 0));
        Assert.Equal(7, Agregacao.Percentil([7], 0.95));
        Assert.Equal(0, Agregacao.Percentil([], 0.95));
    }

    [Fact]
    public void AgregaODiaComMediaMaximoP95ETravadas()
    {
        var inicio = Dia.ToDateTime(new TimeOnly(9, 0));
        var amostras = Enumerable.Range(1, 100)
            .Select(i => Amostra(inicio.AddSeconds(30 * i), ramMb: i, cpu: i / 10.0, atraso: i == 40 ? 800 : i == 70 ? 251 : i == 90 ? 250 : 10,
                travadas: i is 40 or 70 ? 1 : 0, abas: i % 6, trabalhando: i % 3))
            .ToList();

        var resumo = Agregacao.Resumir(Dia, amostras);

        Assert.Equal(Dia, resumo.Data);
        Assert.Equal(100, resumo.Amostras);
        Assert.Equal(50.5 * Mega, resumo.Ram.Media, 0);
        Assert.Equal(100 * Mega, resumo.Ram.Maximo);
        Assert.Equal(95 * Mega, resumo.Ram.P95);
        Assert.Equal(5.05, resumo.Cpu.Media, 2);
        Assert.Equal(10, resumo.Cpu.Maximo, 6);
        Assert.Equal(9.5, resumo.Cpu.P95, 6);
        Assert.Equal(800, resumo.Atraso.Maximo);
        Assert.Equal(2, resumo.Travadas);
        Assert.Equal(5, resumo.AbasMaximo);
        Assert.Equal(2, resumo.TrabalhandoMaximo);
        Assert.Equal(1800, resumo.HandlesMaximo);
        Assert.Equal(100 * Mega, resumo.WsMaximo);
        Assert.Equal((long)(50.5 * Mega / 2), resumo.Divisao.Nexora);
        Assert.Equal((long)(50.5 * Mega / 4), resumo.Divisao.Sessoes);
        Assert.Equal(0, resumo.Divisao.Outros);
        Assert.Equal(2, resumo.CustoMedio);
    }

    [Fact]
    public void MomentosMaisPesadosNaoRepetemOMesmoPico()
    {
        var inicio = Dia.ToDateTime(new TimeOnly(8, 0));
        var amostras = new List<Amostra>();
        for (var i = 0; i < 480; i++)
        {
            var hora = inicio.AddSeconds(30 * i);
            var pico = i is >= 100 and < 120 ? 1200 - (i - 100) : i == 400 ? 900 : 300;
            amostras.Add(Amostra(hora, ramMb: pico, cpu: i == 300 ? 40 : 1, atraso: i == 200 ? 1500 : i == 210 ? 600 : i == 350 ? 200 : 15, travadas: i is 200 or 210 ? 1 : 0, abas: 4, trabalhando: 2));
        }

        var momentos = Agregacao.Momentos(amostras);

        var ram = momentos.Where(m => m.Tipo == TipoDeMomento.Ram).ToList();
        Assert.Equal(2, ram.Count);
        Assert.Contains(ram, m => m.Hora == inicio.AddSeconds(30 * 100) && m.Valor == 1200 * Mega);
        Assert.Contains(ram, m => m.Hora == inicio.AddSeconds(30 * 400));
        var travadas = momentos.Where(m => m.Tipo == TipoDeMomento.Travada).ToList();
        Assert.Equal(2, travadas.Count);
        Assert.Equal([1500, 600], travadas.Select(m => m.Valor).OrderDescending());
        Assert.DoesNotContain(momentos, m => m.Tipo == TipoDeMomento.Travada && m.Valor == 200);
        var cpu = Assert.Single(momentos, m => m.Tipo == TipoDeMomento.Cpu && m.Valor >= 40);
        Assert.Equal(4, cpu.Abas);
        Assert.Equal(2, cpu.Trabalhando);
        Assert.Equal(momentos.OrderBy(m => m.Hora), momentos);
    }

    [Fact]
    public void ResponsividadeContaSoAsTravadasAcimaDe250ms()
    {
        var r = new Responsividade();
        foreach (var atraso in new[] { 12, 250, 251, 900, 40 }) r.Registrar(atraso);
        Assert.Equal((900, 2), r.Colher());
        Assert.Equal((0, 0), r.Colher());
        r.Registrar(30);
        Assert.Equal((1200, 0), r.Colher(pendente: 1200));
    }

    [Fact]
    public void LimpaOsArquivosComMaisDe30Dias()
    {
        var pasta = Directory.CreateTempSubdirectory("nexora-desempenho-").FullName;
        try
        {
            var hoje = new DateOnly(2026, 11, 6);
            foreach (var dias in new[] { 45, 31, 30, 29, 1 }) File.WriteAllText(Path.Combine(pasta, $"dia-{hoje.AddDays(-dias):yyyy-MM-dd}.json"), "{}");
            File.WriteAllText(Path.Combine(pasta, $"amostras-{hoje.AddDays(-40):yyyy-MM-dd}.jsonl"), "");
            File.WriteAllText(Path.Combine(pasta, $"amostras-{hoje:yyyy-MM-dd}.jsonl"), "");
            File.WriteAllText(Path.Combine(pasta, "anotacoes.txt"), "fica");

            var apagados = new Gravacao(pasta).Limpar(hoje);

            Assert.Equal(4, apagados);
            var restantes = Directory.GetFiles(pasta).Select(Path.GetFileName).Order().ToList();
            Assert.Equal(
            [
                $"amostras-{hoje:yyyy-MM-dd}.jsonl",
                "anotacoes.txt",
                $"dia-{hoje.AddDays(-29):yyyy-MM-dd}.json",
                $"dia-{hoje.AddDays(-1):yyyy-MM-dd}.json",
            ], restantes);
        }
        finally
        {
            Directory.Delete(pasta, true);
        }
    }

    [Fact]
    public void FechaOsDiasPassadosEmResumoEApagaAsAmostras()
    {
        var pasta = Directory.CreateTempSubdirectory("nexora-desempenho-").FullName;
        try
        {
            var gravacao = new Gravacao(pasta);
            var ontem = Dia.ToDateTime(new TimeOnly(14, 0));
            var deOntem = Enumerable.Range(0, 10).Select(i => Amostra(ontem.AddSeconds(30 * i), ramMb: 200 + i * 10, atraso: i == 5 ? 700 : 10, travadas: i == 5 ? 1 : 0)).ToList();
            foreach (var a in deOntem) gravacao.Acrescentar(a);
            var hoje = Dia.AddDays(1);
            gravacao.Acrescentar(Amostra(hoje.ToDateTime(new TimeOnly(8, 0))));
            File.AppendAllText(Path.Combine(pasta, $"amostras-{Dia:yyyy-MM-dd}.jsonl"), "{linha quebrada\n");

            gravacao.Arrumar(hoje);

            Assert.False(File.Exists(Path.Combine(pasta, $"amostras-{Dia:yyyy-MM-dd}.jsonl")));
            var resumo = gravacao.LerDia(Dia);
            Assert.NotNull(resumo);
            Assert.Equal(Agregacao.Resumir(Dia, deOntem).Ram, resumo.Ram);
            Assert.Equal(10, resumo.Amostras);
            Assert.Equal(1, resumo.Travadas);
            Assert.Contains(resumo.Momentos, m => m.Tipo == TipoDeMomento.Travada && m.Hora == ontem.AddSeconds(150));
            Assert.Single(gravacao.LerAmostras(hoje));
            Assert.Single(gravacao.LerDias(Dia.AddDays(-13), hoje));
        }
        finally
        {
            Directory.Delete(pasta, true);
        }
    }

    [Fact]
    public void ArquivoDoDiaParaDeCrescerNoLimite()
    {
        var pasta = Directory.CreateTempSubdirectory("nexora-desempenho-").FullName;
        try
        {
            var gravacao = new Gravacao(pasta);
            var hora = Dia.ToDateTime(new TimeOnly(9, 0));
            Assert.True(gravacao.Acrescentar(Amostra(hora)));
            var arquivo = Path.Combine(pasta, $"amostras-{Dia:yyyy-MM-dd}.jsonl");
            var umaLinha = new FileInfo(arquivo).Length;
            saida.WriteLine($"Uma amostra ocupa {umaLinha} bytes; o dia inteiro, 2880 amostras, fica em {umaLinha * 2880 / 1024} KB.");
            Assert.InRange(umaLinha * 2880, 1, Gravacao.LimiteDoArquivo / 2);

            using (var aberto = new FileStream(arquivo, FileMode.Append)) aberto.SetLength(Gravacao.LimiteDoArquivo);
            Assert.False(gravacao.Acrescentar(Amostra(hora.AddSeconds(30))));
            Assert.Equal(Gravacao.LimiteDoArquivo, new FileInfo(arquivo).Length);
            Assert.True(gravacao.Acrescentar(Amostra(hora.AddDays(1))));
        }
        finally
        {
            Directory.Delete(pasta, true);
        }
    }

    [Fact]
    public void SoEnviaComAFlagLigadaForaDeDemoEIsolada()
    {
        Assert.True(Relatorio.PodeEnviar(true, false, false));
        Assert.False(Relatorio.PodeEnviar(false, false, false));
        Assert.False(Relatorio.PodeEnviar(true, true, false));
        Assert.False(Relatorio.PodeEnviar(true, false, true));
        Assert.False(Relatorio.PodeEnviar(true, true, true));
        Assert.False(new Config().RelatorioPorEmail);
        Assert.DoesNotContain("RelatorioPorEmail", JsonSerializer.Serialize(new Config()));
    }

    static string? RaizDoRepositorio()
    {
        for (var pasta = new DirectoryInfo(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
            if (File.Exists(Path.Combine(pasta.FullName, "Nexora.slnx"))) return pasta.FullName;
        return null;
    }

    [Fact]
    public void FuncaoDoSiteAceitaOResumoMontadoPeloNexora()
    {
        var raiz = RaizDoRepositorio();
        Assert.NotNull(raiz);
        var teste = Path.Combine(raiz, "tests", "functions", "desempenho.test.mjs");
        Assert.True(File.Exists(Path.Combine(raiz, "functions", "api", "desempenho.js")));
        var inicio = Dia.ToDateTime(new TimeOnly(10, 0));
        var dias = new[] { Dia.AddDays(-1), Dia }.Select(d => Agregacao.Resumir(d, [.. Enumerable.Range(0, 120)
            .Select(i => Amostra(d.ToDateTime(new TimeOnly(10, 0)).AddSeconds(30 * i), ramMb: 600 + i, cpu: i == 50 ? 30 : 2, atraso: i == 80 ? 640 : 20, travadas: i == 80 ? 1 : 0))],
            [new Travada { Hora = d.ToDateTime(new TimeOnly(10, 40)), Duracao = 640, Causa = "Nexora.ViewModels.ChatVM.Aplicar", Ponta = "System.IO.File.ReadAllText" }])).ToList();
        var corpo = Relatorio.Montar(dias, new InfoDoComputador("0.4.4+abc", "windows", 16, 32L << 30), inicio.AddDays(1));
        var resumo = Path.Combine(Path.GetTempPath(), $"nexora-resumo-{Guid.NewGuid():N}.json");
        File.WriteAllText(resumo, corpo.ToJsonString());
        try
        {
            var info = new ProcessStartInfo("node", ["--test", "--test-reporter=tap", teste]) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = raiz };
            info.Environment["NEXORA_RESUMO"] = resumo;
            Process? processo;
            try
            {
                processo = Process.Start(info);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                saida.WriteLine("Node não está instalado: contrato com a função do site não conferido.");
                return;
            }
            using (processo)
            {
                var texto = processo!.StandardOutput.ReadToEndAsync();
                var erro = processo.StandardError.ReadToEndAsync();
                Assert.True(processo.WaitForExit(60_000), "node não terminou");
                saida.WriteLine(texto.Result + erro.Result);
                Assert.Equal(0, processo.ExitCode);
                Assert.DoesNotContain("# skipped 1", texto.Result);
            }
        }
        finally
        {
            File.Delete(resumo);
        }
    }

    [Fact]
    public void AmostraNaoTemOndeGuardarTexto()
    {
        Assert.DoesNotContain(typeof(Amostra).GetProperties(), p => p.PropertyType == typeof(string));
        Assert.DoesNotContain(typeof(Consumo).GetProperties(), p => p.PropertyType == typeof(string));
    }

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2}(T\d{2}:\d{2})?|\d{2}:\d{2}|\d+(\.\d+)*|windows|mac|linux|outro|ram|cpu|travada|[A-Za-z_]\w*(\.[A-Za-z_]\w*)+)$", RegexOptions.ECMAScript)]
    private static partial Regex TextoPermitido();

    static IEnumerable<string> Textos(JsonNode? no) => no switch
    {
        JsonObject o => o.SelectMany(p => Textos(p.Value)),
        JsonArray a => a.SelectMany(Textos),
        JsonValue v when v.GetValueKind() == JsonValueKind.String => [v.GetValue<string>()],
        _ => [],
    };

    [Fact]
    public void ResumoEnviadoSoLevaNumerosEHorarios()
    {
        var inicio = Dia.ToDateTime(new TimeOnly(10, 0));
        var amostras = Enumerable.Range(0, 200).Select(i => Amostra(inicio.AddSeconds(30 * i), ramMb: 600 + i, cpu: i == 50 ? 30 : 2, atraso: i == 80 ? 640 : 20, travadas: i == 80 ? 1 : 0, abas: 5, trabalhando: 3)).ToList();
        var dia = Agregacao.Resumir(Dia, amostras);
        var estranho = dia with
        {
            Momentos = [.. dia.Momentos, new Momento(inicio, @"D:\Projetos\Loja secreta", 1, 1, 1, 1, 1)],
            Causas = [new CausaDeTravada(@"D:\Projetos\Loja secreta", "x", 9, 900), new CausaDeTravada("Nexora.ViewModels.ChatVM.Aplicar", @"C:\Loja.txt", 2, 700)],
        };
        var vazio = new ResumoDoDia { Data = Dia.AddDays(-1) };

        var corpo = Relatorio.Montar([estranho, vazio], new InfoDoComputador("0.4.4+849e5f7 Loja", "windows", 16, 32L << 30), inicio.AddHours(5));
        var json = corpo.ToJsonString();
        saida.WriteLine(json);

        Assert.All(Textos(JsonNode.Parse(json)), t => Assert.Matches(TextoPermitido(), t));
        Assert.DoesNotContain("Loja", json);
        Assert.DoesNotContain("Projetos", json);
        Assert.True(json.Length < 8_000, $"{json.Length} bytes");
        Assert.Equal(1, (int)corpo["formato"]!);
        Assert.Equal("0.4.4", (string)corpo["versao"]!);
        Assert.Equal(32768, (long)corpo["memoriaDoPcMb"]!);
        Assert.Equal("2026-11-04T15:00", (string)corpo["geradoEm"]!);
        var dias = corpo["dias"]!.AsArray();
        var unico = Assert.Single(dias)!;
        Assert.Equal("2026-11-04", (string)unico["data"]!);
        Assert.Equal(200, (int)unico["amostras"]!);
        Assert.Equal(799, (long)unico["ramMb"]!["maximo"]!);
        Assert.Equal(640, (int)unico["atrasoMs"]!["maximo"]!);
        Assert.Equal(1, (int)unico["travadas"]!);
        Assert.Equal(5, (int)unico["abasMaximo"]!);
        var momentos = unico["momentos"]!.AsArray();
        Assert.Contains(momentos, m => (string)m!["tipo"]! == "travada" && (string)m["hora"]! == "10:40" && (double)m["valor"]! == 640);
        Assert.Contains(momentos, m => (string)m!["tipo"]! == "ram" && (double)m["valor"]! == 799);
        Assert.Equal(["abaNovaHaS", "abas", "cpu", "desdeAbrirS", "gen2", "hora", "nexoraMb", "pausaGcMs", "ramMb", "tipo", "trabalhando", "valor"], momentos[0]!.AsObject().Select(p => p.Key).Order());
        var causa = Assert.Single(unico["causas"]!.AsArray())!;
        Assert.Equal("Nexora.ViewModels.ChatVM.Aplicar", (string)causa["causa"]!);
        Assert.Equal("Nexora.ViewModels.ChatVM.Aplicar", (string)causa["ponta"]!);
        Assert.Equal(2, (int)causa["vezes"]!);
        Assert.Equal(700, (int)causa["maiorMs"]!);
    }

    [Fact]
    public void OResumoSemanalVenceNaSegundaAsNove()
    {
        var quarta = new DateTime(2026, 11, 4, 15, 0, 0);
        var segunda = new DateTime(2026, 11, 2, 9, 0, 0);
        Assert.Equal(segunda, Relatorio.UltimoHorario(quarta, DayOfWeek.Monday, new TimeOnly(9, 0)));
        Assert.Equal(segunda.AddDays(-7), Relatorio.UltimoHorario(segunda.AddMinutes(-1), DayOfWeek.Monday, new TimeOnly(9, 0)));
        Assert.Equal(segunda, Relatorio.UltimoHorario(segunda, DayOfWeek.Monday, new TimeOnly(9, 0)));
        Assert.True(Relatorio.Vencido(quarta, DayOfWeek.Monday, new TimeOnly(9, 0), null));
        Assert.True(Relatorio.Vencido(quarta, DayOfWeek.Monday, new TimeOnly(9, 0), segunda.AddDays(-6)));
        Assert.False(Relatorio.Vencido(quarta, DayOfWeek.Monday, new TimeOnly(9, 0), segunda.AddMinutes(3)));
        Assert.False(Relatorio.Vencido(segunda.AddMinutes(-1), DayOfWeek.Monday, new TimeOnly(9, 0), segunda.AddDays(-7).AddHours(2)));
        Assert.Equal(DayOfWeek.Monday, Relatorio.DiaDe(null));
        Assert.Equal(DayOfWeek.Tuesday, Relatorio.DiaDe("Terça"));
        Assert.Equal(DayOfWeek.Saturday, Relatorio.DiaDe("sabado"));
        Assert.Equal(DayOfWeek.Monday, Relatorio.DiaDe("qualquer"));
        Assert.Equal(new TimeOnly(9, 0), Relatorio.HoraDe(null));
        Assert.Equal(new TimeOnly(7, 30), Relatorio.HoraDe("7:30"));
        Assert.Equal(new TimeOnly(9, 0), Relatorio.HoraDe("25:99"));
    }

    [Fact]
    public void DemonstracaoPreencheATelaDeDesempenho()
    {
        var agora = new DateTime(2026, 11, 6, 16, 20, 0);
        var dados = DesempenhoVM.Demonstracao(agora);
        var vm = new DesempenhoVM();

        vm.Montar(dados, agora);

        Assert.Equal(4, vm.Numeros.Count);
        Assert.Equal(5, vm.Partes.Count);
        Assert.Equal(1, vm.Partes.Sum(p => p.Fracao), 6);
        Assert.Equal(3, vm.Graficos.Count);
        Assert.All(vm.Graficos, g => Assert.Equal(DesempenhoVM.Dias, g.Barras.Count));
        Assert.All(vm.Graficos, g => Assert.True(g.Barras[^1].Atual));
        Assert.Contains(vm.Graficos[0].Barras, b => b.Dica.EndsWith("sem medições"));
        Assert.InRange(vm.Momentos.Count, 1, 6);
        Assert.True(vm.TemDados);
        Assert.Contains("30 s", vm.Subtitulo);
        Assert.True(vm.TemCausas);
        Assert.Equal(
        [
            new CausaVM("DiffView.Montar", "2 vezes, até 640 ms · parada em TextLayout.ctor", "Nexora.Views.DiffView.Montar\nAvalonia.Media.TextFormatting.TextLayout.ctor"),
            new CausaVM("ChatVM.Aplicar", "1 vez, 380 ms · parada em JsonSerializer.Deserialize", "Nexora.ViewModels.ChatVM.Aplicar\nSystem.Text.Json.JsonSerializer.Deserialize"),
        ], vm.Causas);
        Assert.Equal(
            [new CausaDeTravada("Nexora.A.B", "System.X.Y", 5, 900)],
            DesempenhoVM.JuntarCausas([new("Nexora.A.B", "System.X.Y", 3, 400), new("Nexora.A.B", "System.Z.W", 2, 900)]));

        vm.Montar(null, agora);
        Assert.False(vm.TemDados);
        Assert.Empty(vm.Graficos);
    }

    [Fact]
    public void ResumoAgoraLevaOsUltimos7DiasEOResumoDaSemanaFechada()
    {
        var hoje = new DateOnly(2026, 11, 9);
        ResumoDoDia Com(DateOnly d) => new() { Data = d, Amostras = 10 };
        var dados = new DadosDeDesempenho(null, Com(hoje), [.. Enumerable.Range(1, 8).Select(i => Com(hoje.AddDays(-i)))], DateTime.Now, true);

        var agora = MonitorDeDesempenho.DiasDoResumo(dados, false);
        var semana = MonitorDeDesempenho.DiasDoResumo(dados, true);

        Assert.Equal(Enumerable.Range(0, 7).Select(i => hoje.AddDays(-i)).Order(), agora.Select(d => d.Data).Order());
        Assert.Equal(Enumerable.Range(1, 7).Select(i => hoje.AddDays(-i)).Order(), semana.Select(d => d.Data).Order());
        Assert.Equal([hoje], MonitorDeDesempenho.DiasDoResumo(dados with { Anteriores = [] }, true).Select(d => d.Data));
    }

    [Fact]
    public void MedidorAchaONexoraEOsProcessosDasSessoes()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var filho = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 8 127.0.0.1 >nul") { UseShellExecute = false, CreateNoWindow = true })!;
        try
        {
            var medidor = new Medidor();
            medidor.Medir([filho.Id], []);
            var fim = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 3;
            var x = 0.0;
            while (Stopwatch.GetTimestamp() < fim) x += Math.Sqrt(x + 1);
            Thread.Sleep(200);
            var medida = medidor.Medir([filho.Id], []);

            using var eu = Process.GetCurrentProcess();
            Assert.InRange(medida.NexoraWs, eu.WorkingSet64 / 2, eu.WorkingSet64 * 2);
            Assert.InRange(medida.NexoraPrivada, eu.PrivateMemorySize64 / 2, eu.PrivateMemorySize64 * 2);
            Assert.InRange(medida.NexoraRam, 1, medida.NexoraWs);
            Assert.InRange(medida.Handles, 10, 100_000);
            Assert.True(medida.CpuNexora > 0, $"CPU {medida.CpuNexora} ({x})");
            Assert.InRange(medida.Sessoes.Processos, 2, 4);
            Assert.True(medida.Sessoes.Ram > 0);
            Assert.Equal(0, medida.Mcps.Processos);
        }
        finally
        {
            if (!filho.HasExited) filho.Kill(true);
        }
    }

    [Fact]
    public void UmaAmostraCustaPoucosMilissegundos()
    {
        var pasta = Directory.CreateTempSubdirectory("nexora-desempenho-").FullName;
        try
        {
            var medidor = new Medidor();
            var gravacao = new Gravacao(pasta);
            medidor.Medir([], []);
            const int vezes = 40;
            var tempos = new List<double>();
            var cpuAntes = Process.GetCurrentProcess().TotalProcessorTime;
            for (var i = 0; i < vezes; i++)
            {
                var inicio = Stopwatch.GetTimestamp();
                var m = medidor.Medir([], []);
                gravacao.Acrescentar(new Amostra { Hora = DateTime.Now, NexoraRam = m.NexoraRam, NexoraWs = m.NexoraWs, Handles = m.Handles, Sessoes = m.Sessoes, Builds = m.Builds, Outros = m.Outros });
                tempos.Add(Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);
            }
            var cpu = (Process.GetCurrentProcess().TotalProcessorTime - cpuAntes).TotalMilliseconds / vezes;
            tempos.Sort();
            saida.WriteLine($"Por amostra: mediana {tempos[vezes / 2]:0.00} ms, máximo {tempos[^1]:0.00} ms, CPU {cpu:0.00} ms, {Process.GetProcesses().Length} processos no PC.");
            saida.WriteLine($"A cada 30 s isso é {tempos[vezes / 2] / 30_000 * 100:0.0000}% de um núcleo.");
            Assert.True(tempos[vezes / 2] < 60, $"mediana {tempos[vezes / 2]} ms");
        }
        finally
        {
            Directory.Delete(pasta, true);
        }
    }
}
