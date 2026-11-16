using System.Text.Json.Serialization;

namespace Nexora.Core.Desempenho;

public readonly record struct Consumo(long Ram, double Cpu, int Processos)
{
    public Consumo Mais(long ram, double cpu) => new(Ram + ram, Cpu + cpu, Processos + 1);
}

public sealed record Amostra
{
    public DateTime Hora { get; init; }
    public long NexoraRam { get; init; }
    public long NexoraWs { get; init; }
    public long NexoraPrivada { get; init; }
    public long Heap { get; init; }
    public int Handles { get; init; }
    public double CpuNexora { get; init; }
    public Consumo Sessoes { get; init; }
    public Consumo Mcps { get; init; }
    public Consumo Builds { get; init; }
    public Consumo Outros { get; init; }
    public int AtrasoMaximo { get; init; }
    public int Travadas { get; init; }
    public int Abas { get; init; }
    public int Trabalhando { get; init; }
    public double Custo { get; init; }
    public int Gen2 { get; init; }
    public double PausaGc { get; init; }
    public int DesdeAbrir { get; init; }
    public int AbaNovaHa { get; init; }

    [JsonIgnore]
    public long RamTotal => NexoraRam + Sessoes.Ram + Mcps.Ram + Builds.Ram + Outros.Ram;

    [JsonIgnore]
    public double CpuTotal => CpuNexora + Sessoes.Cpu + Mcps.Cpu + Builds.Cpu + Outros.Cpu;
}
