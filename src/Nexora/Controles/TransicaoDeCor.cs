using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Nexora.Controles;

public sealed class TransicaoDeCor : InterpolatingTransitionBase<IBrush?>
{
    public TransicaoDeCor() => Easing = new CubicEaseOut();

    static Color Efetiva(ISolidColorBrush pincel) =>
        Color.FromArgb((byte)Math.Round(pincel.Color.A * Math.Clamp(pincel.Opacity, 0, 1)), pincel.Color.R, pincel.Color.G, pincel.Color.B);

    public static Color Misturar(Color de, Color para, double progresso)
    {
        if (de.A == 0) de = Color.FromArgb(0, para.R, para.G, para.B);
        if (para.A == 0) para = Color.FromArgb(0, de.R, de.G, de.B);
        byte Canal(byte a, byte b) => (byte)Math.Round(a + (b - a) * progresso);
        return Color.FromArgb(Canal(de.A, para.A), Canal(de.R, para.R), Canal(de.G, para.G), Canal(de.B, para.B));
    }

    protected override IBrush? Interpolate(double progresso, IBrush? de, IBrush? para)
    {
        if (progresso >= 1) return para;
        var a = de as ISolidColorBrush;
        var b = para as ISolidColorBrush;
        if (de is null && b is not null) a = new ImmutableSolidColorBrush(Color.FromArgb(0, b.Color.R, b.Color.G, b.Color.B));
        if (para is null && a is not null) b = new ImmutableSolidColorBrush(Color.FromArgb(0, a.Color.R, a.Color.G, a.Color.B));
        if (a is null || b is null) return progresso < 0.5 ? de : para;
        return new ImmutableSolidColorBrush(Misturar(Efetiva(a), Efetiva(b), progresso));
    }
}
