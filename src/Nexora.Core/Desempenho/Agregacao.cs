using System.Text.Json.Serialization;

namespace Nexora.Core.Desempenho;

public sealed record Estatistica(double Media, double Maximo, double P95)
{
    public static readonly Estatistica Zero = new(0, 0, 0);
}

public sealed record Divisao(long Nexora, long Sessoes, long Mcps, long Builds, long Outros)
{
    public static readonly Divisao Zero = new(0, 0, 0, 0, 0);

    [JsonIgnore]
    public long Total => Nexora + Sessoes + Mcps + Builds + Outros;

    public static Divisao De(Amostra a) => new(a.NexoraRam, a.Sessoes.Ram, a.Mcps.Ram, a.Builds.Ram, a.Outros.Ram);
}

public static class TipoDeMomento
{
    public const string Ram = "ram";
    public const string Cpu = "cpu";
    public const string Travada = "travada";
}

public sealed record Momento(DateTime Hora, string Tipo, double Valor, long Ram, double Cpu, int Abas, int Trabalhando, long NexoraRam = 0, double PausaGc = 0, int Gen2 = 0, int DesdeAbrir = 0, int AbaNovaHa = 0);

public sealed record ResumoDoDia
{
    public DateOnly Data { get; init; }
    public int Amostras { get; init; }
    public Estatistica Ram { get; init; } = Estatistica.Zero;
    public Estatistica RamNexora { get; init; } = Estatistica.Zero;
    public Estatistica Cpu { get; init; } = Estatistica.Zero;
    public Estatistica CpuNexora { get; init; } = Estatistica.Zero;
    public Estatistica Atraso { get; init; } = Estatistica.Zero;
    public int Travadas { get; init; }
    public Divisao Divisao { get; init; } = Divisao.Zero;
    public long HeapMaximo { get; init; }
    public long WsMaximo { get; init; }
    public long PrivadaMaxima { get; init; }
    public int HandlesMaximo { get; init; }
    public int AbasMaximo { get; init; }
    public int TrabalhandoMaximo { get; init; }
    public double CustoMedio { get; init; }
    public IReadOnlyList<Momento> Momentos { get; init; } = [];
    public IReadOnlyList<CausaDeTravada> Causas { get; init; } = [];
}

public static class Agregacao
{
    public const int LimiteDeTravada = 250;
    public const int MomentosPorTipo = 2;
    public const int CausasPorDia = 5;

    public static ResumoDoDia Resumir(DateOnly dia, IReadOnlyList<Amostra> amostras, IReadOnlyList<Travada>? travadas = null)
    {
        if (amostras.Count == 0) return new ResumoDoDia { Data = dia, Causas = Causas(travadas ?? []) };
        return new ResumoDoDia
        {
            Data = dia,
            Amostras = amostras.Count,
            Ram = Medir(amostras.Select(a => (double)a.RamTotal)),
            RamNexora = Medir(amostras.Select(a => (double)a.NexoraRam)),
            Cpu = Medir(amostras.Select(a => a.CpuTotal)),
            CpuNexora = Medir(amostras.Select(a => a.CpuNexora)),
            Atraso = Medir(amostras.Select(a => (double)a.AtrasoMaximo)),
            Travadas = amostras.Sum(a => a.Travadas),
            Divisao = new Divisao(
                Media(amostras, a => a.NexoraRam),
                Media(amostras, a => a.Sessoes.Ram),
                Media(amostras, a => a.Mcps.Ram),
                Media(amostras, a => a.Builds.Ram),
                Media(amostras, a => a.Outros.Ram)),
            HeapMaximo = amostras.Max(a => a.Heap),
            WsMaximo = amostras.Max(a => a.NexoraWs),
            PrivadaMaxima = amostras.Max(a => a.NexoraPrivada),
            HandlesMaximo = amostras.Max(a => a.Handles),
            AbasMaximo = amostras.Max(a => a.Abas),
            TrabalhandoMaximo = amostras.Max(a => a.Trabalhando),
            CustoMedio = Math.Round(amostras.Average(a => a.Custo), 2),
            Momentos = Momentos(amostras),
            Causas = Causas(travadas ?? []),
        };
    }

    public static IReadOnlyList<CausaDeTravada> Causas(IEnumerable<Travada> travadas, int quantas = CausasPorDia) =>
        [.. travadas.Where(t => t.Causa.Length > 0)
            .GroupBy(t => t.Causa)
            .Select(g => new CausaDeTravada(g.Key, g.GroupBy(t => t.Ponta).OrderByDescending(p => p.Count()).ThenBy(p => p.Key, StringComparer.Ordinal).First().Key, g.Count(), g.Max(t => t.Duracao)))
            .OrderByDescending(c => c.Vezes).ThenByDescending(c => c.Maior).ThenBy(c => c.Causa, StringComparer.Ordinal)
            .Take(quantas)];

    static long Media(IReadOnlyList<Amostra> amostras, Func<Amostra, long> valor) => (long)amostras.Average(a => (double)valor(a));

    public static Estatistica Medir(IEnumerable<double> valores)
    {
        var ordenados = valores.Order().ToArray();
        if (ordenados.Length == 0) return Estatistica.Zero;
        return new Estatistica(Math.Round(ordenados.Average(), 2), ordenados[^1], Percentil(ordenados, 0.95));
    }

    public static double Percentil(IReadOnlyList<double> ordenados, double fracao)
    {
        if (ordenados.Count == 0) return 0;
        var posicao = (int)Math.Ceiling(fracao * ordenados.Count) - 1;
        return ordenados[Math.Clamp(posicao, 0, ordenados.Count - 1)];
    }

    public static IReadOnlyList<Momento> Momentos(IReadOnlyList<Amostra> amostras, int porTipo = MomentosPorTipo)
    {
        var lista = new List<Momento>();
        lista.AddRange(Escolher(amostras, a => a.RamTotal, a => a.RamTotal > 0, TipoDeMomento.Ram, porTipo, TimeSpan.FromMinutes(30)));
        lista.AddRange(Escolher(amostras, a => a.CpuTotal, a => a.CpuTotal >= 1, TipoDeMomento.Cpu, porTipo, TimeSpan.FromMinutes(30)));
        lista.AddRange(Escolher(amostras, a => a.AtrasoMaximo, a => a.AtrasoMaximo > LimiteDeTravada, TipoDeMomento.Travada, porTipo, TimeSpan.FromMinutes(2)));
        return [.. lista.OrderBy(m => m.Hora)];
    }

    static IEnumerable<Momento> Escolher(IReadOnlyList<Amostra> amostras, Func<Amostra, double> valor, Func<Amostra, bool> conta, string tipo, int quantos, TimeSpan distancia)
    {
        var escolhidos = new List<Amostra>();
        foreach (var a in amostras.Where(conta).OrderByDescending(valor).ThenBy(a => a.Hora))
        {
            if (escolhidos.Any(e => (e.Hora - a.Hora).Duration() < distancia)) continue;
            escolhidos.Add(a);
            if (escolhidos.Count == quantos) break;
        }
        return escolhidos.Select(a => new Momento(a.Hora, tipo, Math.Round(valor(a), 2), a.RamTotal, Math.Round(a.CpuTotal, 2), a.Abas, a.Trabalhando, a.NexoraRam, Math.Round(a.PausaGc, 1), a.Gen2, a.DesdeAbrir, a.AbaNovaHa));
    }

    public static IReadOnlyList<Momento> MaisPesados(IEnumerable<Momento> momentos, int porTipo = MomentosPorTipo) =>
        [.. momentos.GroupBy(m => m.Tipo).SelectMany(g => g.OrderByDescending(m => m.Valor).Take(porTipo)).OrderByDescending(m => m.Hora)];
}
