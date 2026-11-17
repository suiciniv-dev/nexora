using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using System.Text.RegularExpressions;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed record ChipVM(string Nome, IBrush Fundo, IBrush Borda, IBrush Texto, IBrush Ponto)
{
    static readonly Dictionary<string, Color> Nomeadas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GRAY"] = Color.Parse("#9198A1"),
        ["BLUE"] = Color.Parse("#4493F8"),
        ["GREEN"] = Color.Parse("#3FB950"),
        ["YELLOW"] = Color.Parse("#D29922"),
        ["ORANGE"] = Color.Parse("#DB6D28"),
        ["RED"] = Color.Parse("#F85149"),
        ["PINK"] = Color.Parse("#DB61A2"),
        ["PURPLE"] = Color.Parse("#AB7DF8"),
    };

    public static Color Cor(string? cor) =>
        cor is null ? Nomeadas["GRAY"]
        : Nomeadas.TryGetValue(cor, out var nomeada) ? nomeada
        : Color.TryParse("#" + cor.TrimStart('#'), out var hex) ? hex
        : Nomeadas["GRAY"];

    static Color Misturar(Color a, Color b, double t) =>
        Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    public static ChipVM De(string nome, string? cor)
    {
        var c = Cor(cor);
        var escuro = Application.Current?.ActualThemeVariant != ThemeVariant.Light;
        var texto = escuro ? Misturar(c, Colors.White, .45) : Misturar(c, Colors.Black, .4);
        return new ChipVM(nome, new SolidColorBrush(c, escuro ? .16 : .14), new SolidColorBrush(c, .5), new SolidColorBrush(texto), new SolidColorBrush(c));
    }
}
