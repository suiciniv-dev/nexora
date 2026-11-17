using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Nexora.Core.Texto;

namespace Nexora.Controles;

public sealed class LinhaDoTempo : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> ValoresProperty = AvaloniaProperty.Register<LinhaDoTempo, IReadOnlyList<double>?>(nameof(Valores));
    public static readonly StyledProperty<IBrush?> CorProperty = AvaloniaProperty.Register<LinhaDoTempo, IBrush?>(nameof(Cor));
    public static readonly StyledProperty<IBrush?> GuiaProperty = AvaloniaProperty.Register<LinhaDoTempo, IBrush?>(nameof(Guia));
    public static readonly StyledProperty<double> MaximoProperty = AvaloniaProperty.Register<LinhaDoTempo, double>(nameof(Maximo), 1);

    static LinhaDoTempo() => AffectsRender<LinhaDoTempo>(ValoresProperty, CorProperty, GuiaProperty, MaximoProperty);

    public IReadOnlyList<double>? Valores { get => GetValue(ValoresProperty); set => SetValue(ValoresProperty, value); }
    public IBrush? Cor { get => GetValue(CorProperty); set => SetValue(CorProperty, value); }
    public IBrush? Guia { get => GetValue(GuiaProperty); set => SetValue(GuiaProperty, value); }
    public double Maximo { get => GetValue(MaximoProperty); set => SetValue(MaximoProperty, value); }

    public override void Render(DrawingContext contexto)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 2 || h <= 2) return;
        if (Guia is { } guia) contexto.DrawLine(new Pen(guia, 1), new Point(0, h - 0.5), new Point(w, h - 0.5));
        if (Valores is not { Count: > 1 } valores || Cor is not { } cor) return;
        var maximo = Maximo > 0 ? Maximo : Math.Max(valores.Max(), 1e-9);
        var pontos = valores.Select((v, i) => new Point(w * i / (valores.Count - 1), 2 + (h - 4) * (1 - Math.Clamp(v / maximo, 0, 1)))).ToList();
        var area = new StreamGeometry();
        using (var g = area.Open())
        {
            g.BeginFigure(new Point(0, h), true);
            foreach (var p in pontos) g.LineTo(p);
            g.LineTo(new Point(w, h));
            g.EndFigure(true);
        }
        var cheia = cor is ISolidColorBrush solida ? solida.Color : Colors.White;
        contexto.DrawGeometry(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.FromArgb(70, cheia.R, cheia.G, cheia.B), 0), new GradientStop(Color.FromArgb(0, cheia.R, cheia.G, cheia.B), 1) },
        }, null, area);
        var linha = new StreamGeometry();
        using (var g = linha.Open())
        {
            g.BeginFigure(pontos[0], false);
            foreach (var p in pontos.Skip(1)) g.LineTo(p);
            g.EndFigure(false);
        }
        contexto.DrawGeometry(null, new Pen(cor, 1.6, lineJoin: PenLineJoin.Round), linha);
        contexto.DrawEllipse(cor, null, pontos[^1], 2.6, 2.6);
    }
}

public sealed record SerieDoGrafico(string Nome, IReadOnlyList<double> Valores, IBrush Cor);

public abstract class DesenhoGuardado : Control
{
    RenderTargetBitmap? _imagem;
    Size _medida;
    double _escala;
    bool _sujo = true;

    public int Desenhos { get; private set; }

    protected abstract void Pintar(DrawingContext contexto, Size tamanho);

    protected virtual void PintarPorCima(DrawingContext contexto, Size tamanho) { }

    protected FormattedText Rotulo(string texto, IBrush? cor, double tamanho = 11)
    {
        var familia = this.TryFindResource("FonteUi", ActualThemeVariant, out var f) && f is FontFamily ff ? ff : FontFamily.Default;
        return new FormattedText(texto, Idioma.Cultura, FlowDirection.LeftToRight, new Typeface(familia, FontStyle.Normal, FontWeight.Medium), tamanho, cor ?? Brushes.Gray);
    }

    protected static Point NoPixel(double x, double y) => new(Math.Round(x), Math.Round(y));

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property.OwnerType == GetType())
        {
            _sujo = true;
            InvalidateVisual();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _imagem?.Dispose();
        _imagem = null;
        _sujo = true;
    }

    public sealed override void Render(DrawingContext contexto)
    {
        var tamanho = Bounds.Size;
        if (tamanho.Width < 1 || tamanho.Height < 1) return;
        var escala = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        if (_sujo || _imagem is null || _medida != tamanho || Math.Abs(_escala - escala) > 0.001)
        {
            _imagem?.Dispose();
            _imagem = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(tamanho.Width * escala), (int)Math.Ceiling(tamanho.Height * escala)), new Vector(96 * escala, 96 * escala));
            using (var c = _imagem.CreateDrawingContext()) Pintar(c, tamanho);
            (_medida, _escala, _sujo) = (tamanho, escala, false);
            Desenhos++;
        }
        contexto.DrawImage(_imagem, new Rect(tamanho));
        PintarPorCima(contexto, tamanho);
    }
}

public sealed class GradeDeAtividade : DesenhoGuardado
{
    public static readonly StyledProperty<IReadOnlyList<int>?> NiveisProperty = AvaloniaProperty.Register<GradeDeAtividade, IReadOnlyList<int>?>(nameof(Niveis));
    public static readonly StyledProperty<DateTime> InicioProperty = AvaloniaProperty.Register<GradeDeAtividade, DateTime>(nameof(Inicio));
    public static readonly StyledProperty<IBrush?> CorProperty = AvaloniaProperty.Register<GradeDeAtividade, IBrush?>(nameof(Cor));
    public static readonly StyledProperty<IBrush?> VazioProperty = AvaloniaProperty.Register<GradeDeAtividade, IBrush?>(nameof(Vazio));
    public static readonly StyledProperty<IBrush?> RotuloProperty = AvaloniaProperty.Register<GradeDeAtividade, IBrush?>(nameof(Rotulo));
    const double Espaco = 3, Margem = 30, Topo = 18, MaximoDaCelula = 13;
    static readonly double[] Opacidades = [0, 0.32, 0.55, 0.78, 1];

    static GradeDeAtividade()
    {
        AffectsRender<GradeDeAtividade>(NiveisProperty, InicioProperty, CorProperty, VazioProperty, RotuloProperty);
        AffectsMeasure<GradeDeAtividade>(NiveisProperty);
    }

    public IReadOnlyList<int>? Niveis { get => GetValue(NiveisProperty); set => SetValue(NiveisProperty, value); }
    public DateTime Inicio { get => GetValue(InicioProperty); set => SetValue(InicioProperty, value); }
    public IBrush? Cor { get => GetValue(CorProperty); set => SetValue(CorProperty, value); }
    public IBrush? Vazio { get => GetValue(VazioProperty); set => SetValue(VazioProperty, value); }
    public IBrush? Rotulo { get => GetValue(RotuloProperty); set => SetValue(RotuloProperty, value); }

    int Colunas => Math.Max(1, (int)Math.Ceiling(((Niveis?.Count ?? 0) + (int)Inicio.DayOfWeek) / 7.0));

    public static double Celula(double largura, int colunas) =>
        Math.Clamp((largura - Margem - Espaco * (colunas - 1)) / colunas, 4, MaximoDaCelula);

    protected override Size MeasureOverride(Size disponivel)
    {
        var largura = double.IsInfinity(disponivel.Width) ? 700 : disponivel.Width;
        var celula = Celula(largura, Colunas);
        return new Size(largura, Topo + 7 * (celula + Espaco) - Espaco);
    }

    protected override void Pintar(DrawingContext contexto, Size tamanho)
    {
        if (Niveis is not { Count: > 0 } niveis) return;
        var colunas = Colunas;
        var celula = Celula(tamanho.Width, colunas);
        var passo = celula + Espaco;
        var cor = (Cor as ISolidColorBrush)?.Color ?? Colors.LimeGreen;
        var tintas = Opacidades.Select(o => (IBrush)new SolidColorBrush(cor, o)).ToArray();
        var deslocamento = (int)Inicio.DayOfWeek;
        var raio = Math.Max(1.5, celula * 0.22);
        for (var i = 0; i < niveis.Count; i++)
        {
            var posicao = i + deslocamento;
            var x = Margem + posicao / 7 * passo;
            var y = Topo + posicao % 7 * passo;
            var nivel = Math.Clamp(niveis[i], 0, 4);
            var tinta = nivel > 0 ? tintas[nivel] : Vazio;
            if (tinta is not null) contexto.DrawRectangle(tinta, null, new RoundedRect(new Rect(x, y, celula, celula), raio));
        }
    }

    protected override void PintarPorCima(DrawingContext contexto, Size tamanho)
    {
        if (Niveis is not { Count: > 0 }) return;
        var colunas = Colunas;
        var celula = Celula(tamanho.Width, colunas);
        var passo = celula + Espaco;
        var deslocamento = (int)Inicio.DayOfWeek;
        var ultimoMes = -1;
        for (var coluna = 0; coluna < colunas; coluna++)
        {
            var dia = Inicio.AddDays(coluna * 7 - deslocamento);
            var primeiro = dia.AddDays(Math.Max(0, deslocamento - coluna * 7));
            if (primeiro.Month == ultimoMes) continue;
            ultimoMes = primeiro.Month;
            if (coluna > colunas - 2 && coluna > 0) continue;
            if (coluna == 0 && primeiro.Day > 14) continue;
            var mes = primeiro.ToString("MMM", Idioma.Cultura).TrimEnd('.');
            contexto.DrawText(Rotulo(char.ToUpper(mes[0], Idioma.Cultura) + mes[1..], Rotulo), NoPixel(Margem + coluna * passo, 0));
        }
        foreach (var (linha, nome) in new[] { (1, Idioma.T("Seg")), (3, Idioma.T("Qua")), (5, Idioma.T("Sex")) })
        {
            var texto = Rotulo(nome, Rotulo);
            contexto.DrawText(texto, NoPixel(0, Topo + linha * passo + (celula - texto.Height) / 2));
        }
    }
}

public sealed class GraficoDeUso : DesenhoGuardado
{
    public static readonly StyledProperty<IReadOnlyList<SerieDoGrafico>?> SeriesProperty = AvaloniaProperty.Register<GraficoDeUso, IReadOnlyList<SerieDoGrafico>?>(nameof(Series));
    public static readonly StyledProperty<IBrush?> GuiaProperty = AvaloniaProperty.Register<GraficoDeUso, IBrush?>(nameof(Guia));
    public static readonly StyledProperty<IBrush?> RotuloProperty = AvaloniaProperty.Register<GraficoDeUso, IBrush?>(nameof(Rotulo));
    const double Eixo = 40;

    static GraficoDeUso() => AffectsRender<GraficoDeUso>(SeriesProperty, GuiaProperty, RotuloProperty);

    public IReadOnlyList<SerieDoGrafico>? Series { get => GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
    public IBrush? Guia { get => GetValue(GuiaProperty); set => SetValue(GuiaProperty, value); }
    public IBrush? Rotulo { get => GetValue(RotuloProperty); set => SetValue(RotuloProperty, value); }

    public static Func<double, string> Formatar { get; set; } = v => v.ToString("0", Idioma.Cultura);

    static IBrush Pontilhado(Color cor)
    {
        var desenho = new DrawingGroup();
        desenho.Children.Add(new GeometryDrawing { Brush = new SolidColorBrush(cor, 0.10), Geometry = new RectangleGeometry(new Rect(0, 0, 3, 3)) });
        desenho.Children.Add(new GeometryDrawing { Brush = new SolidColorBrush(cor, 0.85), Geometry = new EllipseGeometry(new Rect(0.6, 0.6, 1.6, 1.6)) });
        return new DrawingBrush(desenho)
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            DestinationRect = new RelativeRect(0, 0, 3, 3, RelativeUnit.Absolute),
        };
    }

    double Maximo(out int quantos)
    {
        quantos = 0;
        if (Series is not { Count: > 0 } series) return 0;
        quantos = series.Max(s => s.Valores.Count);
        if (quantos == 0) return 0;
        var somas = new double[quantos];
        foreach (var s in series)
            for (var i = 0; i < s.Valores.Count; i++) somas[i] += s.Valores[i];
        return somas.Max();
    }

    protected override void PintarPorCima(DrawingContext contexto, Size tamanho)
    {
        if (tamanho.Width - Eixo <= 4 || tamanho.Height <= 12 || Series is not { Count: > 0 }) return;
        var topo = Rotulo(Formatar(Maximo(out _)), Rotulo);
        contexto.DrawText(topo, NoPixel(Eixo - 6 - topo.Width, 0));
        var zero = Rotulo("0", Rotulo);
        contexto.DrawText(zero, NoPixel(Eixo - 6 - zero.Width, tamanho.Height - zero.Height));
    }

    protected override void Pintar(DrawingContext contexto, Size tamanho)
    {
        double w = tamanho.Width - Eixo, h = tamanho.Height;
        if (w <= 4 || h <= 12 || Series is not { Count: > 0 } series) return;
        var quantos = series.Max(s => s.Valores.Count);
        if (quantos == 0) return;
        var somas = new double[quantos];
        foreach (var s in series)
            for (var i = 0; i < s.Valores.Count; i++) somas[i] += s.Valores[i];
        var maximo = somas.Max();
        if (Guia is { } guia) contexto.DrawLine(new Pen(guia, 1), new Point(Eixo, h - 0.5), new Point(Eixo + w, h - 0.5));
        if (maximo <= 0) return;
        var passo = w / quantos;
        double Y(double v) => h - 1 - (h - 6) * v / maximo;
        var base_ = new double[quantos];
        foreach (var s in series)
        {
            var cor = (s.Cor as ISolidColorBrush)?.Color ?? Colors.White;
            var cima = new double[quantos];
            for (var i = 0; i < quantos; i++) cima[i] = base_[i] + (i < s.Valores.Count ? s.Valores[i] : 0);
            var area = new StreamGeometry();
            var borda = new StreamGeometry();
            using (var g = area.Open())
            using (var b = borda.Open())
            {
                g.BeginFigure(new Point(Eixo, Y(base_[0])), true);
                b.BeginFigure(new Point(Eixo, Y(cima[0])), false);
                for (var i = 0; i < quantos; i++)
                {
                    var x0 = Eixo + i * passo;
                    var x1 = x0 + passo;
                    g.LineTo(new Point(x0, Y(cima[i])));
                    g.LineTo(new Point(x1, Y(cima[i])));
                    b.LineTo(new Point(x0, Y(cima[i])));
                    b.LineTo(new Point(x1, Y(cima[i])));
                }
                for (var i = quantos - 1; i >= 0; i--)
                {
                    var x0 = Eixo + i * passo;
                    g.LineTo(new Point(x0 + passo, Y(base_[i])));
                    g.LineTo(new Point(x0, Y(base_[i])));
                }
                g.EndFigure(true);
                b.EndFigure(false);
            }
            contexto.DrawGeometry(Pontilhado(cor), null, area);
            contexto.DrawGeometry(null, new Pen(new SolidColorBrush(cor), 1.2, lineJoin: PenLineJoin.Miter), borda);
            base_ = cima;
        }
    }
}

public sealed class BarrasDeUso : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> PrimeiraProperty = AvaloniaProperty.Register<BarrasDeUso, IReadOnlyList<double>?>(nameof(Primeira));
    public static readonly StyledProperty<IReadOnlyList<double>?> SegundaProperty = AvaloniaProperty.Register<BarrasDeUso, IReadOnlyList<double>?>(nameof(Segunda));
    public static readonly StyledProperty<IBrush?> CorPrimeiraProperty = AvaloniaProperty.Register<BarrasDeUso, IBrush?>(nameof(CorPrimeira));
    public static readonly StyledProperty<IBrush?> CorSegundaProperty = AvaloniaProperty.Register<BarrasDeUso, IBrush?>(nameof(CorSegunda));
    public static readonly StyledProperty<IBrush?> GuiaProperty = AvaloniaProperty.Register<BarrasDeUso, IBrush?>(nameof(Guia));

    static BarrasDeUso() => AffectsRender<BarrasDeUso>(PrimeiraProperty, SegundaProperty, CorPrimeiraProperty, CorSegundaProperty, GuiaProperty);

    public IReadOnlyList<double>? Primeira { get => GetValue(PrimeiraProperty); set => SetValue(PrimeiraProperty, value); }
    public IReadOnlyList<double>? Segunda { get => GetValue(SegundaProperty); set => SetValue(SegundaProperty, value); }
    public IBrush? CorPrimeira { get => GetValue(CorPrimeiraProperty); set => SetValue(CorPrimeiraProperty, value); }
    public IBrush? CorSegunda { get => GetValue(CorSegundaProperty); set => SetValue(CorSegundaProperty, value); }
    public IBrush? Guia { get => GetValue(GuiaProperty); set => SetValue(GuiaProperty, value); }

    public override void Render(DrawingContext contexto)
    {
        double w = Bounds.Width, h = Bounds.Height;
        var a = Primeira ?? [];
        var b = Segunda ?? [];
        var quantos = Math.Max(a.Count, b.Count);
        if (w <= 2 || h <= 2 || quantos == 0) return;
        if (Guia is { } guia) contexto.DrawLine(new Pen(guia, 1), new Point(0, h - 0.5), new Point(w, h - 0.5));
        var maximo = Enumerable.Range(0, quantos).Max(i => (i < a.Count ? a[i] : 0) + (i < b.Count ? b[i] : 0));
        if (maximo <= 0) return;
        var passo = w / quantos;
        var largura = Math.Max(1.5, passo * 0.62);
        for (var i = 0; i < quantos; i++)
        {
            var x = i * passo + (passo - largura) / 2;
            var va = (i < a.Count ? a[i] : 0) / maximo * (h - 2);
            var vb = (i < b.Count ? b[i] : 0) / maximo * (h - 2);
            if (va > 0 && CorPrimeira is { } ca) contexto.DrawRectangle(ca, null, new RoundedRect(new Rect(x, h - va, largura, va), 1.5));
            if (vb > 0 && CorSegunda is { } cb) contexto.DrawRectangle(cb, null, new RoundedRect(new Rect(x, h - va - vb, largura, vb), 1.5));
        }
    }
}
