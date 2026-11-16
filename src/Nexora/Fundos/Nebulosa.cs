using SkiaSharp;

namespace Nexora.Fundos;

sealed class Nebulosa(Paleta paleta) : MotorNexora(paleta, 29)
{
    const float Alcance = 150;

    sealed record Nuvem(SKColor Cor, float X, float Y, float Raio, float Achatado, float Fx, float Fy, float Fase);

    static readonly Nuvem[] Nuvens =
    [
        new(Paleta.C("#E59AC4", 0.24f), 0.26f, 0.34f, 0.42f, 0.62f, 0.031f, 0.026f, 0.0f),
        new(Paleta.C("#948BAE", 0.28f), 0.74f, 0.28f, 0.50f, 0.70f, 0.024f, 0.037f, 1.7f),
        new(Paleta.C("#86C5E6", 0.17f), 0.62f, 0.80f, 0.38f, 0.66f, 0.041f, 0.029f, 3.1f),
        new(Paleta.C("#5B5470", 0.34f), 0.14f, 0.82f, 0.46f, 0.74f, 0.022f, 0.044f, 4.4f),
    ];

    readonly Lote _linhas = new();
    Estrela[] _estrelas = [];
    int[] _brilhantes = [];
    double _relogio;

    protected override (float Min, float Max)? Cadentes => (30, 60);
    protected override double IntervaloDoPano => 0.4;

    protected override void Medir()
    {
        _estrelas = Semear(_estrelas, Math.Clamp((int)(W * H / 4500), 40, 600));
        _brilhantes = Enumerable.Range(0, _estrelas.Length).Where(i => _estrelas[i].Camada >= 1).ToArray();
    }

    protected override void Avancar(float dt)
    {
        var ritmo = dt * (1 + 2f * Atividade);
        _relogio += ritmo;
        Derivar(_estrelas, -2.4f * ritmo, 0.9f * ritmo);
    }

    protected override void Pano(SKCanvas tela)
    {
        var area = SKRect.Create(W, H);
        var lado = MathF.Max(W, H);
        var tempo = (float)(_relogio % 100000);
        using var nevoa = new SKPaint { IsDither = true };
        foreach (var n in Nuvens)
        {
            var x = (n.X + 0.10f * MathF.Sin(tempo * n.Fx + n.Fase)) * W;
            var y = (n.Y + 0.08f * MathF.Cos(tempo * n.Fy + n.Fase * 1.3f)) * H;
            var raio = n.Raio * lado * (1 + 0.06f * MathF.Sin(tempo * 0.05f + n.Fase));
            var cor = Alfa(n.Cor, 1 + 0.25f * Atividade);
            Brilho(tela, nevoa, area, x, y, raio, raio * n.Achatado, 0.4f * MathF.Sin(tempo * 0.013f + n.Fase), cor, 1);
        }
    }

    protected override void Desenhar(SKCanvas tela, double t)
    {
        for (var i = 0; i < _brilhantes.Length; i++)
            for (var j = i + 1; j < _brilhantes.Length; j++)
            {
                ref readonly var a = ref _estrelas[_brilhantes[i]];
                ref readonly var b = ref _estrelas[_brilhantes[j]];
                float dx = a.X - b.X, dy = a.Y - b.Y;
                if (MathF.Abs(dx) > Alcance || MathF.Abs(dy) > Alcance) continue;
                var d = Distancia(dx, dy);
                if (d > Alcance) continue;
                var alfa = (1 - d / Alcance) * (0.26f + 0.08f * Atividade) * MathF.Min(Presenca(a.X, a.Y, 4), Presenca(b.X, b.Y, 4));
                alfa *= 1 + 1.5f * Realce((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                _linhas.Linha(a.X, a.Y, b.X, b.Y, alfa);
            }
        Linhas(tela, _linhas, P.Linha);
        DesenharEstrelas(tela, _estrelas, t);
    }
}
