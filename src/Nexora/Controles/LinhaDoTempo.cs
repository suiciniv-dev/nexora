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
