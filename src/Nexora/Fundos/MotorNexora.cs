using SkiaSharp;

namespace Nexora.Fundos;

abstract class MotorNexora(Paleta paleta, int semente) : IMotorFundo
{
    protected const float VelocidadeDaOnda = 440;
    protected const float VidaDaOnda = 2;
    const float RaioDoMouse = 190;
    const float VidaDaCadente = 1.1f;
    const float EscalaDoPano = 0.25f;
    static readonly SKSamplingOptions Suave = new(SKFilterMode.Linear);
    static readonly float[] Larguras = [1.35f, 1.9f, 2.7f];
    static readonly float[] Paralaxe = [0.45f, 0.7f, 1f];

    protected struct Estrela
    {
        public float X, Y, Brilho, Fase, Freq;
        public int Camada;
    }

    protected readonly Paleta P = paleta;
    protected readonly Random Sorte = new(semente);
    protected readonly SKPaint Traco = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
    protected readonly SKPaint Ponto = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    protected readonly SKPaint Tinta = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    readonly List<(float X, float Y, double T)> _ondas = [];
    readonly Lote[] _ceu = [new(), new(), new()];
    readonly Lote _halos = new();
    SKImage? _pano;
    double _panoEm = double.NaN;
    float _dpr;
    bool _trabalhando;
    double _proximaCadente = double.NaN;
    (float X, float Y, float Dx, float Dy, double T)? _cadente;

    protected float W { get; private set; }
    protected float H { get; private set; }
    protected float WAntes { get; private set; }
    protected float HAntes { get; private set; }
    protected float Atividade { get; private set; }
    protected SKPoint? Mouse { get; private set; }
    protected double Agora { get; private set; }
    protected IReadOnlyList<(float X, float Y, double T)> Ondas => _ondas;
    protected virtual (float Min, float Max)? Cadentes => null;
    protected virtual double IntervaloDoPano => double.PositiveInfinity;

    public virtual int Fps => 24;

    public void Clique(float x, float y, double t)
    {
        if (_ondas.Count >= 5) _ondas.RemoveAt(0);
        _ondas.Add((x, y, t));
        if (W > 0) AoClicar(x, y);
    }

    public void Agito(double t, bool trabalhando) => _trabalhando = trabalhando;

    public void Quadro(SKCanvas tela, float largura, float altura, float dpr, double t, double dt, SKPoint? mouse)
    {
        if (largura != W || altura != H || dpr != _dpr || _pano is null)
        {
            (WAntes, HAntes) = (W, H);
            (W, H, _dpr) = (largura, altura, dpr);
            Medir();
            _panoEm = double.NaN;
        }
        Agora = t;
        Mouse = mouse;
        var passo = (float)Math.Clamp(dt, 0, 0.1);
        Atividade += ((_trabalhando ? 1 : 0) - Atividade) * MathF.Min(1, passo * 0.8f);
        _ondas.RemoveAll(o => t - o.T > VidaDaOnda || o.T - t > 1);
        Avancar(passo);
        if (double.IsNaN(_panoEm) || t - _panoEm >= IntervaloDoPano || t < _panoEm)
        {
            _pano?.Dispose();
            _pano = GravarPano();
            _panoEm = t;
        }
        tela.Save();
        tela.Scale(dpr);
        tela.DrawImage(_pano, SKRect.Create(W, H), Suave);
        Desenhar(tela, t);
        DesenharCadente(tela, t);
        DesenharOndas(tela);
        tela.Restore();
    }

    protected abstract void Medir();
    protected abstract void Avancar(float dt);
    protected abstract void Desenhar(SKCanvas tela, double t);
    protected virtual void AoClicar(float x, float y) { }
    protected virtual void Pano(SKCanvas tela) { }

    protected float Perto(float x, float y)
    {
        if (Mouse is not { } m) return 0;
        float dx = x - m.X, dy = y - m.Y, d2 = dx * dx + dy * dy;
        if (d2 >= RaioDoMouse * RaioDoMouse) return 0;
        var k = 1 - MathF.Sqrt(d2) / RaioDoMouse;
        return k * k * (3 - 2 * k);
    }

    protected float Onda(float x, float y)
    {
        var forca = 0f;
        foreach (var o in _ondas)
        {
            var idade = (float)(Agora - o.T);
            if (idade < 0) continue;
            var k = 1 - MathF.Abs(Distancia(x - o.X, y - o.Y) - idade * VelocidadeDaOnda) / 56;
            if (k > 0) forca = MathF.Max(forca, k * (1 - idade / VidaDaOnda));
        }
        return forca;
    }

    protected float Realce(float x, float y) => MathF.Max(Perto(x, y), Onda(x, y));

    protected float Presenca(float x, float y, float margem = 20) =>
        Limitar(MathF.Min(MathF.Min(x + margem, W + margem - x), MathF.Min(y + margem, H + margem - y)) / 40);

    protected Random Fresca(int salto) => new(semente * 7919 + salto);

    protected static Estrela NovaEstrela(Random sorte, float x, float y, int? camada = null)
    {
        var c = camada ?? (sorte.NextSingle() switch { < 0.68f => 0, < 0.92f => 1, _ => 2 });
        var r = sorte.NextSingle();
        return new Estrela
        {
            X = x,
            Y = y,
            Camada = c,
            Brilho = c switch { 0 => 0.42f + 0.3f * r, 1 => 0.6f + 0.3f * r, _ => 0.8f + 0.2f * r },
            Fase = sorte.NextSingle() * MathF.Tau,
            Freq = 0.45f + 1.9f * sorte.NextSingle(),
        };
    }

    protected Estrela[] Semear(Estrela[] antigas, int quantas)
    {
        float kx = WAntes > 0 ? W / WAntes : 1, ky = HAntes > 0 ? H / HAntes : 1;
        var novas = new Estrela[quantas];
        for (var i = 0; i < quantas; i++)
        {
            if (i < antigas.Length)
            {
                novas[i] = antigas[i];
                novas[i].X *= kx;
                novas[i].Y *= ky;
            }
            else novas[i] = NovaEstrela(Sorte, Sorte.NextSingle() * W, Sorte.NextSingle() * H);
        }
        return novas;
    }

    protected static float Cintilar(in Estrela e, double t)
    {
        var s = 0.5f + 0.5f * MathF.Sin((float)(t * e.Freq % Math.Tau) + e.Fase);
        return e.Brilho * (0.25f + 0.75f * s * s);
    }

    protected void Derivar(Estrela[] estrelas, float dx, float dy)
    {
        for (var i = 0; i < estrelas.Length; i++)
        {
            ref var e = ref estrelas[i];
            var k = Paralaxe[e.Camada];
            e.X += dx * k;
            e.Y += dy * k;
            if (e.X < -4) e.X += W + 8;
            else if (e.X > W + 4) e.X -= W + 8;
            if (e.Y < -4) e.Y += H + 8;
            else if (e.Y > H + 4) e.Y -= H + 8;
        }
    }

    protected void DesenharEstrelas(SKCanvas tela, Estrela[] estrelas, double t, float halo = 1)
    {
        foreach (ref readonly var e in estrelas.AsSpan())
        {
            var a = MathF.Min(1, Cintilar(e, t) + 0.55f * Realce(e.X, e.Y));
            _ceu[e.Camada].Ponto(e.X, e.Y, a);
            if (e.Camada == 2) _halos.Ponto(e.X, e.Y, a);
        }
        Ponto.StrokeWidth = 9;
        _halos.Desenhar(tela, Ponto, Alfa(P.Destaque, (P.Escuro ? 0.16f : 0.07f) * halo), SKPointMode.Points);
        for (var c = 0; c < _ceu.Length; c++)
        {
            Ponto.StrokeWidth = Larguras[c];
            _ceu[c].Desenhar(tela, Ponto, P.No, SKPointMode.Points);
        }
    }

    protected void Linhas(SKCanvas tela, Lote lote, SKColor cor, float largura = 1)
    {
        Traco.StrokeWidth = largura;
        lote.Desenhar(tela, Traco, cor, SKPointMode.Lines);
        Traco.StrokeWidth = 1;
    }

    protected void Pontos(SKCanvas tela, Lote lote, SKColor cor, float largura)
    {
        Ponto.StrokeWidth = largura;
        lote.Desenhar(tela, Ponto, cor, SKPointMode.Points);
    }

    protected void Anel(SKCanvas tela, float x, float y, float raio, SKColor cor, float alfa, float largura = 1.4f)
    {
        Traco.StrokeWidth = largura;
        Traco.Color = Alfa(cor, alfa);
        tela.DrawCircle(x, y, raio, Traco);
        Traco.StrokeWidth = 1;
    }

    void DesenharCadente(SKCanvas tela, double t)
    {
        if (Cadentes is not { } intervalo) return;
        if (double.IsNaN(_proximaCadente) || _proximaCadente - t > intervalo.Max * 2)
            _proximaCadente = t + intervalo.Min * (0.3 + Sorte.NextDouble());
        if (_cadente is null && t >= _proximaCadente)
        {
            var angulo = 0.38f + Sorte.NextSingle() * 0.4f;
            var lado = Sorte.Next(2) == 0 ? -1 : 1;
            _cadente = (W * (0.2f + 0.6f * Sorte.NextSingle()), H * 0.32f * Sorte.NextSingle(), lado * MathF.Cos(angulo), MathF.Sin(angulo), t);
            _proximaCadente = t + (intervalo.Min + Sorte.NextSingle() * (intervalo.Max - intervalo.Min)) / (1 + 1.5f * Atividade);
        }
        if (_cadente is not { } c) return;
        var idade = (float)(t - c.T);
        if (idade < 0 || idade > VidaDaCadente)
        {
            _cadente = null;
            return;
        }
        var andou = idade * 640;
        var cauda = MathF.Min(andou, 160);
        var alfa = MathF.Sin(MathF.PI * idade / VidaDaCadente);
        float hx = c.X + c.Dx * andou, hy = c.Y + c.Dy * andou, tx = hx - c.Dx * cauda, ty = hy - c.Dy * cauda;
        using var rastro = SKShader.CreateLinearGradient(new SKPoint(tx, ty), new SKPoint(hx, hy),
            [P.Anel.WithAlpha(0), Alfa(P.Anel, alfa * 0.85f)], SKShaderTileMode.Clamp);
        Traco.Shader = rastro;
        Traco.StrokeWidth = 1.5f;
        tela.DrawLine(tx, ty, hx, hy, Traco);
        Traco.Shader = null;
        Traco.StrokeWidth = 1;
        Ponto.StrokeWidth = 2.6f;
        Ponto.Color = Alfa(P.Anel, alfa);
        tela.DrawPoint(hx, hy, Ponto);
    }

    void DesenharOndas(SKCanvas tela)
    {
        foreach (var o in _ondas)
        {
            var idade = (float)(Agora - o.T);
            if (idade < 0) continue;
            Anel(tela, o.X, o.Y, idade * VelocidadeDaOnda, P.Destaque, (1 - idade / VidaDaOnda) * (P.Escuro ? 0.28f : 0.36f), 1.2f);
        }
    }

    SKImage GravarPano()
    {
        float w = W, h = H;
        var area = SKRect.Create(w, h);
        using var superficie = SKSurface.Create(new SKImageInfo(Math.Max(8, (int)MathF.Ceiling(w * EscalaDoPano)), Math.Max(8, (int)MathF.Ceiling(h * EscalaDoPano)),
            SKColorType.Rgba8888, SKAlphaType.Premul));
        var tela = superficie.Canvas;
        tela.Scale(superficie.Canvas.DeviceClipBounds.Width / w, superficie.Canvas.DeviceClipBounds.Height / h);
        using var tinta = new SKPaint { IsDither = true };
        using (var vertical = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, h), [P.Fundo, P.FundoFim], SKShaderTileMode.Clamp))
        {
            tinta.Shader = vertical;
            tela.DrawRect(area, tinta);
        }
        Brilho(tela, tinta, area, w * 0.80f, h * 0.06f, w * 0.55f, h * 0.50f, 0, P.Luz, 0.7f);
        Brilho(tela, tinta, area, w * 0.08f, h * 1.00f, w * 0.60f, h * 0.55f, 0, P.Sombra, 0.7f);
        tinta.Shader = null;
        Pano(tela);
        using (var vinheta = SKShader.CreateRadialGradient(new SKPoint(w / 2, h / 2), w * 0.95f, [P.Vinheta.WithAlpha(0), P.Vinheta], [0.45f, 1],
            SKShaderTileMode.Clamp, SKMatrix.CreateScale(1, h * 0.85f / (w * 0.95f), w / 2, h / 2)))
        {
            tinta.Shader = vinheta;
            tela.DrawRect(area, tinta);
        }
        tinta.Shader = null;
        return superficie.Snapshot();
    }

    protected static void Brilho(SKCanvas tela, SKPaint tinta, SKRect area, float cx, float cy, float rx, float ry, float giro, SKColor cor, float corte)
    {
        var forma = SKMatrix.CreateScale(1, ry / rx, cx, cy);
        if (giro != 0) forma = forma.PostConcat(SKMatrix.CreateRotation(giro, cx, cy));
        using var sombra = SKShader.CreateRadialGradient(new SKPoint(cx, cy), rx, [cor, cor.WithAlpha(0)], [0, corte], SKShaderTileMode.Clamp, forma);
        tinta.Shader = sombra;
        tela.DrawRect(area, tinta);
        tinta.Shader = null;
    }

    protected static float Distancia(float dx, float dy) => MathF.Sqrt(dx * dx + dy * dy);

    protected static float Limitar(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

    protected static SKColor Alfa(SKColor cor, float alfa) => cor.WithAlpha((byte)(Limitar(alfa) * cor.Alpha));

    public virtual void Dispose()
    {
        _pano?.Dispose();
        Traco.Dispose();
        Ponto.Dispose();
        Tinta.Dispose();
    }
}

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
