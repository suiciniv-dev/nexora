using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Nexora.Core.Texto;

namespace Nexora.Controles;

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
