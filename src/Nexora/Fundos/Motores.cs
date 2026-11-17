using SkiaSharp;

namespace Nexora.Fundos;

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
