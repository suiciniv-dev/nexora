using SkiaSharp;

namespace Nexora.Fundos;

public interface IMotorFundo : IDisposable
{
    int Fps { get; }
    void Quadro(SKCanvas tela, float largura, float altura, float dpr, double t, double dt, SKPoint? mouse);
    void Clique(float x, float y, double t);
    void Agito(double t, bool trabalhando);
}

public static class Motores
{
    public static IMotorFundo Criar(string papel) => papel switch
    {
        "nexora:grafo" => new Grafo(Paleta.Grafo),
        "nexora:grafo-claro" => new Grafo(Paleta.GrafoClaro),
        "nexora:constelacao" => new Constelacao(Paleta.Constelacao, carta: false),
        "nexora:carta-celeste" => new Constelacao(Paleta.CartaCeleste, carta: true),
        "nexora:via-lactea" => new ViaLactea(Paleta.ViaLactea),
        "nexora:nebulosa" => new Nebulosa(Paleta.Nebulosa),
        "nexora:pulso" => new Pulso(Paleta.Pulso),
        "nexora:aurora" => new Aurora(Paleta.Aurora),
        "nexora:mosaico" or "nexora:mosaico-estatico" => new Mosaico(Paleta.Mosaico),
        _ => throw new ArgumentException($"Fundo {papel} não existe."),
    };
}
