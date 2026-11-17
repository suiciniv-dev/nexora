using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Nexora.Controles;

public sealed class AnelDeUso : Control
{
    public static readonly StyledProperty<double> FracaoProperty = AvaloniaProperty.Register<AnelDeUso, double>(nameof(Fracao));
    public static readonly StyledProperty<IBrush?> CorProperty = AvaloniaProperty.Register<AnelDeUso, IBrush?>(nameof(Cor));
    public static readonly StyledProperty<IBrush?> TrilhoProperty = AvaloniaProperty.Register<AnelDeUso, IBrush?>(nameof(Trilho));
    public static readonly StyledProperty<double> EspessuraProperty = AvaloniaProperty.Register<AnelDeUso, double>(nameof(Espessura), 3);

    static AnelDeUso() => AffectsRender<AnelDeUso>(FracaoProperty, CorProperty, TrilhoProperty, EspessuraProperty);

    public double Fracao { get => GetValue(FracaoProperty); set => SetValue(FracaoProperty, value); }
    public IBrush? Cor { get => GetValue(CorProperty); set => SetValue(CorProperty, value); }
    public IBrush? Trilho { get => GetValue(TrilhoProperty); set => SetValue(TrilhoProperty, value); }
    public double Espessura { get => GetValue(EspessuraProperty); set => SetValue(EspessuraProperty, value); }

    public override void Render(DrawingContext contexto)
    {
        var lado = Math.Min(Bounds.Width, Bounds.Height);
        if (lado <= 0) return;
        var espessura = Espessura;
        var raio = (lado - espessura) / 2;
        var centro = new Point(Bounds.Width / 2, Bounds.Height / 2);
        if (Trilho is { } trilho) contexto.DrawEllipse(null, new Pen(trilho, espessura), centro, raio, raio);
        var fracao = Math.Clamp(Fracao, 0, 1);
        if (Cor is not { } cor || fracao <= 0) return;
        var caneta = new Pen(cor, espessura, lineCap: PenLineCap.Round);
        if (fracao >= 0.999)
        {
            contexto.DrawEllipse(null, caneta, centro, raio, raio);
            return;
        }
        var angulo = fracao * Math.Tau - Math.PI / 2;
        var inicio = new Point(centro.X, centro.Y - raio);
        var fim = new Point(centro.X + raio * Math.Cos(angulo), centro.Y + raio * Math.Sin(angulo));
        var arco = new StreamGeometry();
        using (var g = arco.Open())
        {
            g.BeginFigure(inicio, false);
            g.ArcTo(fim, new Size(raio, raio), 0, fracao > 0.5, SweepDirection.Clockwise);
            g.EndFigure(false);
        }
        contexto.DrawGeometry(null, caneta, arco);
    }
}
