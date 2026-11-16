namespace Nexora.Core.Desempenho;

public sealed record Travada
{
    public DateTime Hora { get; init; }
    public int Duracao { get; init; }
    public int FotoEm { get; init; }
    public string Causa { get; init; } = "";
    public string Ponta { get; init; } = "";
    public string Tela { get; init; } = "";
    public int Abas { get; init; }
    public int Trabalhando { get; init; }
    public IReadOnlyList<string> Pilha { get; init; } = [];
}

public sealed record CausaDeTravada(string Causa, string Ponta, int Vezes, int Maior);
