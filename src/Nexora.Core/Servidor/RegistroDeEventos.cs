using Nexora.Core.Claude;

namespace Nexora.Core.Servidor;

public sealed class RegistroDeEventos(int limite = 5000)
{
    readonly Queue<EventoNumerado> _eventos = new();

    public long Ultimo { get; private set; }

    public int Limite { get; } = Math.Max(1, limite);

    public EventoNumerado Acrescentar(EventoClaude evento)
    {
        var numerado = new EventoNumerado(++Ultimo, evento);
        _eventos.Enqueue(numerado);
        while (_eventos.Count > Limite) _eventos.Dequeue();
        return numerado;
    }

    public (bool Completo, IReadOnlyList<EventoNumerado> Eventos) Desde(long numero)
    {
        var primeiro = _eventos.TryPeek(out var mais) ? mais.Numero : Ultimo + 1;
        return (numero + 1 >= primeiro, [.. _eventos.Where(e => e.Numero > numero)]);
    }
}
