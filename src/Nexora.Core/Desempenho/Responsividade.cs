namespace Nexora.Core.Desempenho;

public sealed class Responsividade
{
    readonly Lock _trava = new();
    int _maior;
    int _travadas;

    public void Registrar(int atraso)
    {
        lock (_trava)
        {
            if (atraso > _maior) _maior = atraso;
            if (atraso > Agregacao.LimiteDeTravada) _travadas++;
        }
    }

    public (int Maior, int Travadas) Colher(int pendente = 0)
    {
        lock (_trava)
        {
            var colhido = (Math.Max(_maior, pendente), _travadas);
            _maior = 0;
            _travadas = 0;
            return colhido;
        }
    }
}
