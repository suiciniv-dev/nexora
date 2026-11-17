using SkiaSharp;

namespace Nexora.Fundos;

sealed class Lote
{
    const int Niveis = 32;
    readonly List<SKPoint>[] _pontos = Enumerable.Range(0, Niveis + 1).Select(_ => new List<SKPoint>()).ToArray();

    static int Nivel(float alfa) => alfa <= 0 ? 0 : alfa >= 1 ? Niveis : (int)MathF.Round(alfa * Niveis);

    public void Linha(float x1, float y1, float x2, float y2, float alfa)
    {
        var n = Nivel(alfa);
        if (n == 0) return;
        _pontos[n].Add(new SKPoint(x1, y1));
        _pontos[n].Add(new SKPoint(x2, y2));
    }

    public void Ponto(float x, float y, float alfa)
    {
        var n = Nivel(alfa);
        if (n > 0) _pontos[n].Add(new SKPoint(x, y));
    }

    public void Desenhar(SKCanvas tela, SKPaint tinta, SKColor cor, SKPointMode modo)
    {
        for (var n = 1; n <= Niveis; n++)
        {
            var lista = _pontos[n];
            if (lista.Count == 0) continue;
            tinta.Color = cor.WithAlpha((byte)Math.Round(cor.Alpha * n / (float)Niveis));
            tela.DrawPoints(modo, [.. lista], tinta);
            lista.Clear();
        }
    }
}
