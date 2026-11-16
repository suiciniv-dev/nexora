using SkiaSharp;

namespace Nexora.Fundos;

sealed class Aurora : MotorNexora
{
    const float Alcance = 150;
    const float Passo = 24;

    sealed record Cortina(SKColor Cor, float Altura, float Amplitude, float Tamanho, float Forca, float Fase);

    static readonly Lazy<SKImage> Feixe = new(DesenharFeixe);

    static readonly Cortina[] Cortinas =
    [
        new(Paleta.C("#7BCB9F"), 0.30f, 0.055f, 0.30f, 0.32f, 0.0f),
        new(Paleta.C("#86C5E6"), 0.21f, 0.045f, 0.22f, 0.21f, 2.3f),
        new(Paleta.C("#948BAE"), 0.40f, 0.050f, 0.20f, 0.25f, 4.1f),
    ];

    readonly Lote _linhas = new();
    readonly SKPaint _luz = new() { IsAntialias = true, BlendMode = SKBlendMode.Screen };
    readonly SKColorFilter[] _tintas;
    Estrela[] _estrelas = [];
    int[] _brilhantes = [];
    double _relogio;

    public Aurora(Paleta paleta) : base(paleta, 31) =>
        _tintas = Cortinas.Select(c => SKColorFilter.CreateBlendMode(c.Cor, SKBlendMode.SrcIn)).ToArray();

    protected override (float Min, float Max)? Cadentes => (24, 50);
    protected override double IntervaloDoPano => 0.14;

    static SKImage DesenharFeixe()
    {
        const int largura = 64, altura = 256;
        using var superficie = SKSurface.Create(new SKImageInfo(largura, altura, SKColorType.Rgba8888, SKAlphaType.Premul));
        var tela = superficie.Canvas;
        tela.Clear(SKColors.Transparent);
        static SKColor Branco(byte alfa) => SKColors.White.WithAlpha(alfa);
        using var tinta = new SKPaint { IsDither = true };
        using (var vertical = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, altura),
            [Branco(0), Branco(40), Branco(150), Branco(255), Branco(0)], [0, 0.3f, 0.66f, 0.84f, 1], SKShaderTileMode.Clamp))
        {
            tinta.Shader = vertical;
            tela.DrawRect(0, 0, largura, altura, tinta);
        }
        using (var horizontal = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(largura, 0),
            [Branco(0), Branco(90), Branco(255), Branco(90), Branco(0)], [0, 0.22f, 0.5f, 0.78f, 1], SKShaderTileMode.Clamp))
        {
            tinta.Shader = horizontal;
            tinta.BlendMode = SKBlendMode.DstIn;
            tela.DrawRect(0, 0, largura, altura, tinta);
        }
        return superficie.Snapshot();
    }

    protected override void Medir()
    {
        _estrelas = Semear(_estrelas, Math.Clamp((int)(W * H / 5500), 30, 500));
        _brilhantes = Enumerable.Range(0, _estrelas.Length).Where(i => _estrelas[i].Camada == 2).ToArray();
    }

    protected override void Avancar(float dt)
    {
        var ritmo = dt * (1 + 1.6f * Atividade);
        _relogio += ritmo;
        Derivar(_estrelas, -1.4f * ritmo, 0.3f * ritmo);
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
                var alfa = (1 - d / Alcance) * 0.26f * MathF.Min(Presenca(a.X, a.Y, 4), Presenca(b.X, b.Y, 4));
                alfa *= 1 + 1.5f * Realce((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                _linhas.Linha(a.X, a.Y, b.X, b.Y, alfa);
            }
        Linhas(tela, _linhas, P.Linha);
        DesenharEstrelas(tela, _estrelas, t);
    }

    protected override void Pano(SKCanvas tela)
    {
        var feixe = Feixe.Value;
        var tempo = (float)(_relogio % 100000);
        var amostragem = new SKSamplingOptions(SKFilterMode.Linear);
        for (var c = 0; c < Cortinas.Length; c++)
        {
            var cortina = Cortinas[c];
            _luz.ColorFilter = _tintas[c];
            for (var x = -Passo * 2; x < W + Passo * 2; x += Passo)
            {
                var onda = MathF.Sin(x * 0.0042f + tempo * 0.11f + cortina.Fase) + 0.5f * MathF.Sin(x * 0.011f - tempo * 0.17f + cortina.Fase * 1.7f);
                var baseY = H * (cortina.Altura + cortina.Amplitude * onda);
                var altura = H * cortina.Tamanho * (0.7f + 0.3f * MathF.Sin(x * 0.006f + tempo * 0.08f + cortina.Fase));
                var raio = 0.5f + 0.5f * MathF.Sin(x * 0.013f + tempo * 0.35f + cortina.Fase) * MathF.Sin(x * 0.0047f - tempo * 0.16f);
                var forca = cortina.Forca * (0.45f + 0.55f * raio) * (1 + 0.35f * Atividade) * (0.75f + 0.25f * Perto(x, baseY) * 2);
                _luz.Color = SKColors.White.WithAlpha((byte)(Limitar(forca) * 255));
                var largura = Passo * 5;
                tela.DrawImage(feixe, new SKRect(x - largura / 2, baseY - altura * 0.84f, x + largura / 2, baseY + altura * 0.16f), amostragem, _luz);
            }
        }
        _luz.ColorFilter = null;
    }

    public override void Dispose()
    {
        foreach (var t in _tintas) t.Dispose();
        _luz.Dispose();
        base.Dispose();
    }
}
