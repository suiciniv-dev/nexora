using SkiaSharp;

namespace Nexora.Fundos;

sealed class Grafo(Paleta paleta) : MotorNexora(paleta, 7)
{
    const float Alcance = 170;

    struct No
    {
        public float X, Y, Vx, Vy, Fase;
        public bool Anel;
    }

    readonly Lote _linhas = new();
    readonly Lote _pontos = new();
    No[] _nos = [];
    Estrela[] _estrelas = [];

    protected override (float Min, float Max)? Cadentes => P.Escuro ? (28, 60) : null;

    protected override void Medir()
    {
        var n = Math.Clamp((int)MathF.Round(W * H / 22000), 14, 130);
        float kx = WAntes > 0 ? W / WAntes : 1, ky = HAntes > 0 ? H / HAntes : 1;
        var antigos = _nos;
        _nos = new No[n];
        for (var i = 0; i < n; i++)
            _nos[i] = i < antigos.Length ? antigos[i] with { X = antigos[i].X * kx, Y = antigos[i].Y * ky } : new No
            {
                X = Sorte.NextSingle() * W,
                Y = Sorte.NextSingle() * H,
                Vx = (Sorte.NextSingle() - 0.5f) * 9,
                Vy = (Sorte.NextSingle() - 0.5f) * 9,
                Fase = Sorte.NextSingle() * MathF.Tau,
                Anel = Sorte.NextSingle() < 0.28f,
            };
        _estrelas = Semear(_estrelas, Math.Clamp((int)(W * H / 7000), 20, 400));
    }

    protected override void Avancar(float dt)
    {
        var ritmo = dt * (1 + 2.2f * Atividade);
        for (var i = 0; i < _nos.Length; i++)
        {
            ref var n = ref _nos[i];
            n.X += n.Vx * ritmo;
            n.Y += n.Vy * ritmo;
            if (n.X < -20) n.X = W + 20;
            else if (n.X > W + 20) n.X = -20;
            if (n.Y < -20) n.Y = H + 20;
            else if (n.Y > H + 20) n.Y = -20;
        }
        Derivar(_estrelas, -1.6f * ritmo, -0.5f * ritmo);
    }

    protected override void Desenhar(SKCanvas tela, double t)
    {
        DesenharEstrelas(tela, _estrelas, t, 0.6f);
        var forca = (P.Escuro ? 0.4f : 0.5f) + 0.1f * Atividade;
        for (var i = 0; i < _nos.Length; i++)
            for (var j = i + 1; j < _nos.Length; j++)
            {
                ref readonly var a = ref _nos[i];
                ref readonly var b = ref _nos[j];
                float dx = a.X - b.X, dy = a.Y - b.Y;
                if (MathF.Abs(dx) > Alcance || MathF.Abs(dy) > Alcance) continue;
                var d = Distancia(dx, dy);
                if (d > Alcance) continue;
                var alfa = (1 - d / Alcance) * forca * MathF.Min(Presenca(a.X, a.Y), Presenca(b.X, b.Y));
                alfa *= 1 + 1.6f * Realce((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                _linhas.Linha(a.X, a.Y, b.X, b.Y, alfa);
            }
        Linhas(tela, _linhas, P.Linha);
        var tempo = (float)(t % 3600);
        foreach (ref readonly var n in _nos.AsSpan())
        {
            var brilho = 0.78f + 0.22f * MathF.Sin(tempo * 0.9f + n.Fase);
            var alfa = MathF.Min(1, (0.6f + 0.4f * Realce(n.X, n.Y)) * brilho);
            if (n.Anel) Anel(tela, n.X, n.Y, 4.5f, P.Anel, alfa, 1.6f);
            else _pontos.Ponto(n.X, n.Y, alfa);
        }
        Pontos(tela, _pontos, P.No, 4.4f);
    }
}
