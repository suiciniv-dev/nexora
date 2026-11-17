using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Desempenho;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class DesempenhoVM : ObservableObject
{
    public const int Dias = 14;
    const double AlturaDaBarra = 40;
    static CultureInfo Br => Idioma.Cultura;
    bool _ligado;
    bool _lendo;

    public ObservableCollection<NumeroVM> Numeros { get; } = [];
    public ObservableCollection<ParteDaRamVM> Partes { get; } = [];
    public ObservableCollection<GraficoVM> Graficos { get; } = [];
    public ObservableCollection<MomentoVM> Momentos { get; } = [];
    public ObservableCollection<CausaVM> Causas { get; } = [];

    [ObservableProperty]
    public partial string Subtitulo { get; set; } = "";

    [ObservableProperty]
    public partial string? AvisoEnvio { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EnviarResumoCommand))]
    public partial bool Enviando { get; set; }

    public bool PodeEnviar => MonitorDeDesempenho.PodeEnviar;
    public bool SemMomentos => Momentos.Count == 0;
    public bool TemCausas => Causas.Count > 0;
    public bool TemDados => Numeros.Count > 0;

    public void Ativar(bool ligado)
    {
        if (ligado == _ligado) return;
        _ligado = ligado;
        if (MonitorDeDesempenho.Instancia is { } monitor)
        {
            if (ligado) monitor.Mediu += AoMedir;
            else monitor.Mediu -= AoMedir;
        }
        if (!ligado) return;
        OnPropertyChanged(nameof(PodeEnviar));
        _ = AtualizarAsync();
    }

    void AoMedir() => _ = AtualizarAsync();

    public async Task AtualizarAsync()
    {
        if (_lendo) return;
        _lendo = true;
        try
        {
            var agora = DateTime.Now;
            var dados = JanelaVM.Demo ? await Task.Run(() => Demonstracao(agora))
                : MonitorDeDesempenho.Instancia is { } monitor ? await monitor.DadosAsync(Dias) : null;
            Montar(dados, agora);
        }
        finally
        {
            _lendo = false;
        }
    }

    bool PodeClicarEnviar => !Enviando;

    [RelayCommand(CanExecute = nameof(PodeClicarEnviar))]
    async Task EnviarResumo()
    {
        if (MonitorDeDesempenho.Instancia is not { } monitor) return;
        Enviando = true;
        AvisoEnvio = Idioma.T("Enviando o resumo dos últimos 7 dias…");
        try
        {
            AvisoEnvio = (await monitor.EnviarAsync(false)).Mensagem;
        }
        finally
        {
            Enviando = false;
        }
    }

    public void Montar(DadosDeDesempenho? dados, DateTime agora)
    {
        Numeros.Clear();
        Partes.Clear();
        Graficos.Clear();
        Momentos.Clear();
        Causas.Clear();
        if (dados is null)
        {
            Subtitulo = Idioma.T("O monitor de desempenho não está ligado nesta janela.");
            Avisar();
            return;
        }
        Subtitulo = DescreverMonitor(dados);
        MontarNumeros(dados.Agora, dados.Hoje);
        if (dados.Agora is { } atual) MontarPartes(atual);
        MontarGraficos(dados);
        var recentes = dados.Anteriores.Where(d => d.Data >= dados.Hoje.Data.AddDays(-6)).Append(dados.Hoje).SelectMany(d => d.Momentos);
        foreach (var m in Agregacao.MaisPesados(recentes)) Momentos.Add(DescreverMomento(m, agora));
        var semana = dados.Anteriores.Where(d => d.Data >= dados.Hoje.Data.AddDays(-6)).Append(dados.Hoje).SelectMany(d => d.Causas);
        foreach (var c in JuntarCausas(semana)) Causas.Add(DescreverCausa(c));
        Avisar();
    }

    public static IReadOnlyList<CausaDeTravada> JuntarCausas(IEnumerable<CausaDeTravada> causas) =>
        [.. causas.GroupBy(c => c.Causa)
            .Select(g => new CausaDeTravada(g.Key, g.MaxBy(c => c.Vezes)!.Ponta, g.Sum(c => c.Vezes), g.Max(c => c.Maior)))
            .OrderByDescending(c => c.Vezes).ThenByDescending(c => c.Maior).ThenBy(c => c.Causa, StringComparer.Ordinal)
            .Take(Agregacao.CausasPorDia)];

    static string Curto(string nome)
    {
        var partes = nome.Split('.');
        return partes.Length <= 2 ? nome : string.Join('.', partes[^2..]);
    }

    static CausaVM DescreverCausa(CausaDeTravada c)
    {
        var vezes = c.Vezes == 1 ? Idioma.F("1 vez, {0} ms", c.Maior) : Idioma.F("{0} vezes, até {1} ms", c.Vezes, c.Maior);
        var detalhe = c.Ponta.Length == 0 || c.Ponta == c.Causa ? vezes : Idioma.F("{0} · parada em {1}", vezes, Curto(c.Ponta));
        return new CausaVM(Curto(c.Causa), detalhe, c.Ponta.Length == 0 || c.Ponta == c.Causa ? c.Causa : $"{c.Causa}\n{c.Ponta}");
    }

    void Avisar()
    {
        OnPropertyChanged(nameof(SemMomentos));
        OnPropertyChanged(nameof(TemCausas));
        OnPropertyChanged(nameof(TemDados));
    }

    static string DescreverMonitor(DadosDeDesempenho dados)
    {
        if (JanelaVM.Demo) return Idioma.T("Dados de demonstração. O Nexora mede a cada 30 s e cada medição leva uns 2 ms.");
        var custo = dados.Agora is { } a ? Idioma.F("{0:0.#} ms por medição", a.Custo) : Idioma.T("a primeira medição sai em até 30 s");
        var guarda = dados.Grava ? Idioma.T("guarda 30 dias neste computador") : Idioma.T("esta janela isolada não grava nada");
        return Idioma.F("Medido a cada 30 s desde as {0:HH:mm} · {1} · {2}.", dados.Desde, custo, guarda);
    }

    void MontarNumeros(Amostra? a, ResumoDoDia hoje)
    {
        var temHoje = hoje.Amostras > 0;
        var picoDaRam = hoje.Momentos.Where(m => m.Tipo == TipoDeMomento.Ram).MaxBy(m => m.Valor);
        Numeros.Add(new NumeroVM(Idioma.T("RAM agora"), a is null ? "—" : Ram(a.RamTotal),
            temHoje ? Idioma.F("Hoje: média {0}, pico {1}{2}", Ram(hoje.Ram.Media), Ram(hoje.Ram.Maximo), picoDaRam is null ? "" : Idioma.F(" às {0:HH:mm}", picoDaRam.Hora)) : Idioma.T("Sem medições hoje ainda.")));
        Numeros.Add(new NumeroVM(Idioma.T("CPU agora"), a is null ? "—" : Pct(a.CpuTotal),
            temHoje ? Idioma.F("Hoje: média {0}, p95 {1}, máxima {2}", Pct(hoje.Cpu.Media), Pct(hoje.Cpu.P95), Pct(hoje.Cpu.Maximo)) : Idioma.T("Somando o Nexora e tudo o que ele abriu.")));
        var atraso = a?.AtrasoMaximo ?? 0;
        var travadas = hoje.Travadas switch
        {
            0 => Idioma.T("nenhuma travada"),
            1 => Idioma.T("1 travada"),
            var n => Idioma.F("{0} travadas", n),
        };
        Numeros.Add(new NumeroVM(Idioma.T("Interface"), a is null ? "—" : atraso > Agregacao.LimiteDeTravada ? Idioma.F("Travou {0} ms", atraso) : atraso <= 50 ? Idioma.T("Fluida") : Idioma.F("{0} ms de atraso", atraso),
            temHoje ? Idioma.F("Hoje: {0} acima de 250 ms, maior atraso {1} ms", travadas, (int)hoje.Atraso.Maximo) : Idioma.T("Conta as travadas acima de 250 ms."),
            atraso > Agregacao.LimiteDeTravada));
        Numeros.Add(new NumeroVM(Idioma.T("Abas"), a is null ? "—" : a.Abas == 1 ? Idioma.T("1 aberta") : Idioma.F("{0} abertas", a.Abas),
            a is null ? "" : Idioma.F("{0}, heap {1}, {2:N0} handles", a.Trabalhando == 1 ? Idioma.T("1 trabalhando") : Idioma.F("{0} trabalhando", a.Trabalhando), Ram(a.Heap), a.Handles)));
    }

    void MontarPartes(Amostra a)
    {
        var total = Math.Max(1, a.RamTotal);
        void Parte(string nome, long ram, double cpu, int processos, string tipo)
        {
            var fracao = ram / (double)total;
            var quantos = processos == 1 ? Idioma.T("1 processo") : Idioma.F("{0} processos", processos);
            Partes.Add(new ParteDaRamVM(nome, $"{Ram(ram)} · {fracao.ToString("P0", Br)}", fracao, Idioma.F("{0} de {1}, CPU {2}", quantos, tipo, Pct(cpu))));
        }
        Parte("Nexora", a.NexoraRam, a.CpuNexora, 1, Idioma.T("interface"));
        Parte(Idioma.T("Sessões dos agentes"), a.Sessoes.Ram, a.Sessoes.Cpu, a.Sessoes.Processos, Idioma.T("sessão, com o que cada uma abriu"));
        Parte("MCPs", a.Mcps.Ram, a.Mcps.Cpu, a.Mcps.Processos, Idioma.T("MCP do hub"));
        Parte(Idioma.T("Builds do .NET"), a.Builds.Ram, a.Builds.Cpu, a.Builds.Processos, Idioma.T("build que ficou aberto"));
        Parte(Idioma.T("Terminais e outros"), a.Outros.Ram, a.Outros.Cpu, a.Outros.Processos, Idioma.T("terminal, navegador embutido ou execução"));
    }

    void MontarGraficos(DadosDeDesempenho dados)
    {
        var hoje = dados.Hoje.Data;
        var dias = Enumerable.Range(0, Dias).Select(i => hoje.AddDays(i - Dias + 1))
            .Select(d => d == hoje ? dados.Hoje : dados.Anteriores.FirstOrDefault(r => r.Data == d) ?? new ResumoDoDia { Data = d })
            .ToList();
        var medidos = dias.Where(d => d.Amostras > 0).ToList();
        var inicio = dias[0].Data.ToString(Idioma.T("d/M"), Br);

        var pico = medidos.MaxBy(d => d.Ram.Maximo);
        Graficos.Add(new GraficoVM(Idioma.T("Pico de RAM"),
            pico is null ? Idioma.T("Sem medições ainda.") : Idioma.F("Maior: {0} {1}", Ram(pico.Ram.Maximo), Quando(pico.Data, hoje)),
            Barras(dias, hoje, d => d.Ram.Maximo, d => Idioma.F("pico de {0}, média {1}", Ram(d.Ram.Maximo), Ram(d.Ram.Media))), inicio, Idioma.T("hoje")));

        var amostras = medidos.Sum(d => d.Amostras);
        var cpuMedia = amostras == 0 ? 0 : medidos.Sum(d => d.Cpu.Media * d.Amostras) / amostras;
        Graficos.Add(new GraficoVM(Idioma.T("CPU média"),
            medidos.Count == 0 ? Idioma.T("Sem medições ainda.") : Idioma.F("{0} na média de {1}", Pct(cpuMedia), medidos.Count == 1 ? Idioma.T("1 dia") : Idioma.F("{0} dias", medidos.Count)),
            Barras(dias, hoje, d => d.Cpu.Media, d => Idioma.F("CPU média {0}, p95 {1}", Pct(d.Cpu.Media), Pct(d.Cpu.P95))), inicio, Idioma.T("hoje")));

        var travadas = medidos.Sum(d => d.Travadas);
        Graficos.Add(new GraficoVM(Idioma.T("Travadas da interface"),
            medidos.Count == 0 ? Idioma.T("Sem medições ainda.") : travadas == 0 ? Idioma.T("Nenhuma acima de 250 ms") : Idioma.F("{0} acima de 250 ms", travadas),
            Barras(dias, hoje, d => d.Travadas, d => Idioma.F("{0}, maior atraso {1} ms", d.Travadas == 1 ? Idioma.T("1 travada") : Idioma.F("{0} travadas", d.Travadas), (int)d.Atraso.Maximo)), inicio, Idioma.T("hoje")));
    }

    static List<BarraVM> Barras(IReadOnlyList<ResumoDoDia> dias, DateOnly hoje, Func<ResumoDoDia, double> valor, Func<ResumoDoDia, string> dica)
    {
        var maximo = dias.Where(d => d.Amostras > 0).Select(valor).DefaultIfEmpty(0).Max();
        return [.. dias.Select(d =>
        {
            var rotulo = DiaDaSemana(d.Data);
            if (d.Amostras == 0) return new BarraVM(2, Idioma.F("{0}: sem medições", rotulo), d.Data == hoje);
            var altura = maximo > 0 ? Math.Max(3, valor(d) / maximo * AlturaDaBarra) : 3;
            return new BarraVM(altura, $"{rotulo}: {dica(d)}", d.Data == hoje);
        })];
    }

    static MomentoVM DescreverMomento(Momento m, DateTime agora)
    {
        var quando = Idioma.F("{0} às {1:HH:mm}", Dia(DateOnly.FromDateTime(m.Hora), DateOnly.FromDateTime(agora)), m.Hora);
        var abas = m.Abas == 1 ? Idioma.T("1 aba") : Idioma.F("{0} abas", m.Abas);
        var contexto = Idioma.F("{0}, {1} trabalhando · RAM {2} · CPU {3}", abas, m.Trabalhando, Ram(m.Ram), Pct(m.Cpu));
        return m.Tipo switch
        {
            TipoDeMomento.Cpu => new MomentoVM(quando, Idioma.F("CPU em {0}", Pct(m.Valor)), contexto, "raio", false),
            TipoDeMomento.Travada => new MomentoVM(quando, Idioma.F("A interface travou {0:0} ms", m.Valor), contexto, "alerta", true),
            _ => new MomentoVM(quando, Idioma.F("Pico de RAM: {0}", Ram(m.Valor)), contexto, "memoria", false),
        };
    }

    static string Dia(DateOnly dia, DateOnly hoje) => (hoje.DayNumber - dia.DayNumber) switch
    {
        0 => Idioma.T("hoje"),
        1 => Idioma.T("ontem"),
        _ => DiaDaSemana(dia),
    };

    static string Quando(DateOnly dia, DateOnly hoje) => (hoje.DayNumber - dia.DayNumber) switch
    {
        0 => Idioma.T("hoje"),
        1 => Idioma.T("ontem"),
        _ => Idioma.F("em {0}", dia.ToString(Idioma.T("d/M"), Br)),
    };

    static string DiaDaSemana(DateOnly dia) => $"{dia.ToString("ddd", Br).TrimEnd('.')}, {dia.ToString(Idioma.T("d/M"), Br)}";

    public static string Ram(double bytes) =>
        bytes >= 1L << 30 ? $"{(bytes / (1L << 30)).ToString("0.0", Br)} GB" : $"{(bytes / (1L << 20)).ToString("0", Br)} MB";

    public static string Pct(double valor) => (valor < 10 ? valor.ToString("0.#", Br) : valor.ToString("0", Br)) + "%";

    public static DadosDeDesempenho Demonstracao(DateTime agora)
    {
        var hoje = DateOnly.FromDateTime(agora);
        var anteriores = new List<ResumoDoDia>();
        for (var i = Dias - 1; i >= 1; i--)
        {
            var dia = hoje.AddDays(-i);
            if (dia.DayOfWeek == DayOfWeek.Sunday) continue;
            var inicio = dia.ToDateTime(new TimeOnly(8, 40));
            var fim = dia.ToDateTime(dia.DayOfWeek == DayOfWeek.Saturday ? new TimeOnly(11, 30) : new TimeOnly(18, 20));
            anteriores.Add(Agregacao.Resumir(dia, AmostrasDeDemonstracao(i, inicio, fim)));
        }
        var desde = agora.AddHours(-6) < agora.Date ? agora.Date : agora.AddHours(-6);
        var deHoje = AmostrasDeDemonstracao(0, desde, agora);
        Travada Travou(int minutos, string causa, string ponta, int ms) => new() { Hora = desde.AddMinutes(minutos), Duracao = ms, Causa = causa, Ponta = ponta };
        Travada[] travadas =
        [
            Travou(40, "Nexora.Views.DiffView.Montar", "Avalonia.Media.TextFormatting.TextLayout.ctor", 640),
            Travou(95, "Nexora.Views.DiffView.Montar", "Avalonia.Media.TextFormatting.TextLayout.ctor", 410),
            Travou(150, "Nexora.ViewModels.ChatVM.Aplicar", "System.Text.Json.JsonSerializer.Deserialize", 380),
        ];
        return new DadosDeDesempenho(deHoje[^1], Agregacao.Resumir(hoje, deHoje, travadas), anteriores, desde, true);
    }

    static List<Amostra> AmostrasDeDemonstracao(int semente, DateTime inicio, DateTime fim)
    {
        var lista = new List<Amostra>();
        var passo = 0;
        for (var hora = inicio; hora <= fim; hora = hora.AddSeconds(30), passo++)
        {
            var minuto = passo / 2;
            var abas = 2 + (int)(Ruido(semente, minuto / 40) * 4);
            var trabalhando = Math.Min(abas, (int)(Ruido(semente + 7, minuto / 6) * 3.4));
            var build = Ruido(semente + 3, minuto / 15) > 0.86;
            var travou = Ruido(semente + 11, passo) > 0.9993 + semente % 3 * 0.0002;
            var onda = Math.Sin(minuto / 37.0 + semente);
            lista.Add(new Amostra
            {
                Hora = hora,
                NexoraRam = (long)((290 + 25 * onda + abas * 9) * (1 << 20)),
                NexoraWs = (long)((380 + 30 * onda + abas * 10) * (1 << 20)),
                NexoraPrivada = (long)((340 + 28 * onda + abas * 9) * (1 << 20)),
                Heap = (long)((96 + 18 * onda) * (1 << 20)),
                Handles = 1700 + abas * 60,
                CpuNexora = Math.Round(0.4 + Ruido(semente + 5, passo) * (trabalhando > 0 ? 1.8 : 0.4), 2),
                Sessoes = new Consumo((long)(abas * (120 + 30 * Ruido(semente + 2, minuto / 20)) * (1 << 20)), Math.Round(trabalhando * (0.8 + Ruido(semente + 9, passo) * 2.5), 2), abas * 2),
                Mcps = new Consumo((long)(85 * (1 << 20)), Math.Round(0.1 + trabalhando * 0.2, 2), 3),
                Builds = build ? new Consumo((long)(640 * (1 << 20)), Math.Round(4 + Ruido(semente + 13, passo) * 9, 2), 3) : default,
                Outros = new Consumo((long)(70 * (1 << 20)), 0.1, 4),
                AtrasoMaximo = travou ? 300 + (int)(Ruido(semente + 17, passo) * 700) : 8 + (int)(Ruido(semente + 19, passo) * 30),
                Travadas = travou ? 1 : 0,
                Abas = abas,
                Trabalhando = trabalhando,
                Custo = Math.Round(1.6 + Ruido(semente + 23, passo) * 1.2, 2),
            });
        }
        return lista;
    }

    static double Ruido(int a, int b)
    {
        var x = unchecked((uint)(a * 73856093) ^ (uint)(b * 19349663) ^ 0x9E3779B9u);
        x ^= x >> 16;
        x = unchecked(x * 0x7FEB352Du);
        x ^= x >> 15;
        x = unchecked(x * 0x846CA68Bu);
        x ^= x >> 16;
        return (x & 0xFFFFFF) / (double)0xFFFFFF;
    }
}
