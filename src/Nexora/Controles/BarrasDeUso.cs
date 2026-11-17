using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Nexora.Core.Texto;

namespace Nexora.Controles;

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
