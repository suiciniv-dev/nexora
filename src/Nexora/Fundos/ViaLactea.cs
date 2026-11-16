using SkiaSharp;

namespace Nexora.Fundos;

sealed class ViaLactea(Paleta paleta) : MotorNexora(paleta, 23)
{
    const float Alcance = 95;

    readonly Lote _linhas = new();
    SKPoint _centro;
    SKPoint _u;
    SKPoint _n;
    float _giro;
    float _comprimento;
    float _largura;
    float _sigma;
    int _campo;
    double _deriva;
    Estrela[] _estrelas = [];
    float[] _s = [];
    float[] _o = [];
    int[] _brilhantes = [];

    protected override (float Min, float Max)? Cadentes => (16, 34);

    void Geometria()
    {
        _centro = new SKPoint(W / 2, H / 2);
        _giro = MathF.Atan2(-0.62f * H, W);
        _u = new SKPoint(MathF.Cos(_giro), MathF.Sin(_giro));
        _n = new SKPoint(-_u.Y, _u.X);
        _comprimento = (W * MathF.Abs(_u.X) + H * MathF.Abs(_u.Y)) / 2 + 30;
        _largura = (W * MathF.Abs(_n.X) + H * MathF.Abs(_n.Y)) / 2 + 30;
        _sigma = 0.12f * MathF.Min(W, H) + 26;
    }

    float Curva(float s) => 0.05f * _comprimento * MathF.Sin(s / _comprimento * 1.6f);

    static float Gauss(Random sorte)
    {
        var u1 = 1 - sorte.NextSingle();
        var u2 = sorte.NextSingle();
        return MathF.Sqrt(-2 * MathF.Log(u1)) * MathF.Cos(MathF.Tau * u2);
    }

    protected override void Medir()
    {
        Geometria();
        _campo = Math.Clamp((int)(W * H / 9000), 20, 300);
        var total = _campo + Math.Clamp((int)(W * H / 1100), 160, 2000);
        _estrelas = new Estrela[total];
        _s = new float[total];
        _o = new float[total];
        Random noCampo = Fresca(0), naFaixa = Fresca(1);
        for (var i = 0; i < total; i++)
        {
            var campo = i < _campo;
            var sorte = campo ? noCampo : naFaixa;
            var sorteio = sorte.NextSingle();
            var camada = campo ? sorteio < 0.6f ? 0 : sorteio < 0.9f ? 1 : 2 : sorteio < 0.8f ? 0 : sorteio < 0.97f ? 1 : 2;
            _estrelas[i] = NovaEstrela(sorte, 0, 0, camada);
            _s[i] = sorte.NextSingle() * 2 - 1;
            _o[i] = campo ? sorte.NextSingle() * 2 - 1 : Math.Clamp(Gauss(sorte) * 0.5f, -1, 1);
        }
        _brilhantes = Enumerable.Range(0, total).Where(i => _estrelas[i].Camada == 2).ToArray();
        Posicionar();
    }

    void Posicionar()
    {
        var volta = 2 * _comprimento;
        for (var i = 0; i < _estrelas.Length; i++)
        {
            ref var e = ref _estrelas[i];
            var s = (float)((_s[i] * _comprimento + _deriva * (0.45 + 0.28 * e.Camada) + _comprimento) % volta) - _comprimento;
            var o = _o[i] * (i < _campo ? _largura : 2 * _sigma) + Curva(s);
            e.X = _centro.X + _u.X * s + _n.X * o;
            e.Y = _centro.Y + _u.Y * s + _n.Y * o;
        }
    }

    protected override void Avancar(float dt)
    {
        _deriva += dt * 6 * (1 + 2f * Atividade);
        Posicionar();
    }

    protected override void Pano(SKCanvas tela)
    {
        Geometria();
        var area = SKRect.Create(W, H);
        using var tinta = new SKPaint { IsDither = true };
        Brilho(tela, tinta, area, _centro.X, _centro.Y, _comprimento * 1.1f, _sigma * 1.9f, _giro, Paleta.C("#948BAE", 0.16f), 1);
        Brilho(tela, tinta, area, _centro.X + _u.X * _comprimento * 0.2f, _centro.Y + _u.Y * _comprimento * 0.2f,
            _comprimento * 0.7f, _sigma * 0.8f, _giro, Alfa(P.Destaque, 0.09f), 1);
        Brilho(tela, tinta, area, _centro.X + _n.X * _sigma * 0.18f, _centro.Y + _n.Y * _sigma * 0.18f,
            _comprimento * 0.75f, _sigma * 0.2f, _giro, Alfa(P.Fundo, 0.5f), 1);
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
                var alfa = (1 - d / Alcance) * (0.3f + 0.1f * Atividade) * (1 + 1.5f * Realce((a.X + b.X) / 2, (a.Y + b.Y) / 2));
                _linhas.Linha(a.X, a.Y, b.X, b.Y, alfa);
            }
        Linhas(tela, _linhas, P.Linha);
        DesenharEstrelas(tela, _estrelas, t);
    }
}
