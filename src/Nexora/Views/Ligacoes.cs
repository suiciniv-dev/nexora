using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

using Nexora.ViewModels;

namespace Nexora.Views;

public sealed class Ligacoes : Control
{
    public static readonly StyledProperty<OrquestraVM?> OrquestraProperty =
        AvaloniaProperty.Register<Ligacoes, OrquestraVM?>(nameof(Orquestra));

    public static readonly StyledProperty<IBrush?> LinhaProperty =
        AvaloniaProperty.Register<Ligacoes, IBrush?>(nameof(Linha));

    public static readonly StyledProperty<IBrush?> AtivaProperty =
        AvaloniaProperty.Register<Ligacoes, IBrush?>(nameof(Ativa));

    public static readonly StyledProperty<IBrush?> AlertaProperty =
        AvaloniaProperty.Register<Ligacoes, IBrush?>(nameof(Alerta));

    static Ligacoes() => AffectsRender<Ligacoes>(LinhaProperty, AtivaProperty, AlertaProperty);

    public OrquestraVM? Orquestra
    {
        get => GetValue(OrquestraProperty);
        set => SetValue(OrquestraProperty, value);
    }

    public IBrush? Linha
    {
        get => GetValue(LinhaProperty);
        set => SetValue(LinhaProperty, value);
    }

    public IBrush? Ativa
    {
        get => GetValue(AtivaProperty);
        set => SetValue(AtivaProperty, value);
    }

    public IBrush? Alerta
    {
        get => GetValue(AlertaProperty);
        set => SetValue(AlertaProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs mudanca)
    {
        base.OnPropertyChanged(mudanca);
        if (mudanca.Property != OrquestraProperty) return;
        if (mudanca.OldValue is OrquestraVM antiga) antiga.Mexeu -= InvalidateVisual;
        if (mudanca.NewValue is OrquestraVM nova) nova.Mexeu += InvalidateVisual;
        InvalidateVisual();
    }

    public override void Render(DrawingContext contexto)
    {
        if (Orquestra is not { NoChefe: { } chefe } orquestra) return;
        var inicio = new Point(chefe.X + OrquestraVM.LarguraNo, chefe.Y + OrquestraVM.AlturaNo / 2);
        foreach (var no in orquestra.Nos)
        {
            var fim = new Point(no.X, no.Y + OrquestraVM.AlturaNo / 2);
            var curva = Math.Max(48, Math.Abs(fim.X - inicio.X) / 2);
            var geometria = new StreamGeometry();
            using (var traco = geometria.Open())
            {
                traco.BeginFigure(inicio, isFilled: false);
                traco.CubicBezierTo(new Point(inicio.X + curva, inicio.Y), new Point(fim.X - curva, fim.Y), fim);
            }
            var pincel = no.Chat.Precisa ? Alerta : no.Chat.Trabalhando ? Ativa : Linha;
            contexto.DrawGeometry(null, new Pen(pincel, no.Chat.Trabalhando ? 2 : 1.4), geometria);
            contexto.DrawEllipse(pincel, null, fim, 3.5, 3.5);
        }
        contexto.DrawEllipse(Ativa, null, inicio, 4, 4);
    }
}
