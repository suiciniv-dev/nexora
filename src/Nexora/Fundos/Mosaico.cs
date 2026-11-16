using SkiaSharp;

namespace Nexora.Fundos;

sealed class Mosaico(Paleta paleta) : MotorNexora(paleta, 37)
{
    const float Celula = 9;
    const float Passo = 13;
    static readonly float[] Niveis = [0.14f, 0.28f, 0.48f, 0.80f];

    struct Casa
    {
        public float X, Y, Sorte, Fase, Brilho;
    }

    readonly Lote _vazias = new();
    readonly Lote _acesas = new();
    readonly Lote _quentes = new();
    readonly SKPaint _quadrado = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Square, StrokeWidth = Celula };
    Casa[] _casas = [];
    double _relogio;
    double _proximoCommit;

    public override int Fps => 12;

    protected override void Medir()
    {
        var colunas = (int)MathF.Ceiling(W / Passo) + 1;
        var linhas = (int)MathF.Ceiling(H / Passo) + 1;
        var sorte = Fresca(colunas * 31 + linhas);
        var casas = new Casa[colunas * linhas];
        var folga = (W - (colunas - 1) * Passo) / 2;
        for (var l = 0; l < linhas; l++)
            for (var c = 0; c < colunas; c++)
                casas[l * colunas + c] = new Casa
                {
                    X = folga + c * Passo,
                    Y = Celula / 2 + l * Passo,
                    Sorte = sorte.NextSingle(),
                    Fase = sorte.NextSingle() * MathF.Tau,
                };
        _casas = casas;
    }

    protected override void Avancar(float dt)
    {
        _relogio += dt * (1 + 1.6f * Atividade);
        for (var i = 0; i < _casas.Length; i++)
        {
            ref var c = ref _casas[i];
            if (c.Brilho > 0) c.Brilho = MathF.Max(0, c.Brilho - dt * 0.55f);
        }
        if (_casas.Length == 0 || _relogio < _proximoCommit) return;
        var quantas = 1 + (int)(Atividade * 3);
        for (var q = 0; q < quantas; q++)
        {
            var i = Sorte.Next(_casas.Length);
            if (Densidade(_casas[i].Y) > 0.05f) _casas[i].Brilho = 1;
        }
        _proximoCommit = _relogio + 0.35 + Sorte.NextDouble() * 0.9;
    }

    float Densidade(float y)
    {
        var k = Limitar(1 - y / (H * 0.62f));
        return 0.08f + 0.92f * k * k;
    }

    float Campo(float x, float y, float t)
    {
        var a = MathF.Sin(x * 0.011f + t * 0.06f) * MathF.Cos(y * 0.014f - t * 0.045f);
        var b = MathF.Sin((x + y) * 0.0065f - t * 0.03f + 1.3f);
        var c = MathF.Cos(x * 0.027f - y * 0.019f + t * 0.08f);
        return Limitar(0.5f + 0.32f * a + 0.22f * b + 0.12f * c);
    }

    protected override void Desenhar(SKCanvas tela, double t)
    {
        var tempo = (float)(_relogio % 100000);
        foreach (ref readonly var c in _casas.AsSpan())
        {
            var nivel = Densidade(c.Y) * Campo(c.X, c.Y, tempo);
            var pisca = 0.06f * MathF.Sin(tempo * 0.9f + c.Fase);
            var degrau = (nivel + pisca - c.Sorte * 0.55f) * 4.2f;
            var realce = Realce(c.X, c.Y);
            if (realce > 0) degrau += realce * 2.6f;
            var indice = Math.Clamp((int)degrau, -1, Niveis.Length - 1);
            if (c.Brilho > 0) _quentes.Ponto(c.X, c.Y, c.Brilho);
            else if (indice >= 0) _acesas.Ponto(c.X, c.Y, Niveis[indice]);
            else _vazias.Ponto(c.X, c.Y, 0.5f + 0.5f * Densidade(c.Y));
        }
        _vazias.Desenhar(tela, _quadrado, P.Sombra, SKPointMode.Points);
        _acesas.Desenhar(tela, _quadrado, P.Destaque, SKPointMode.Points);
        _quentes.Desenhar(tela, _quadrado, P.Anel, SKPointMode.Points);
    }

    public override void Dispose()
    {
        _quadrado.Dispose();
        base.Dispose();
    }
}
