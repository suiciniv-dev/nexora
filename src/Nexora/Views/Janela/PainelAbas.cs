using Avalonia;
using Avalonia.Controls;

using Nexora.ViewModels;

namespace Nexora.Views;

public sealed class PainelAbas : Panel
{
    public const double Minimo = 40;
    public const double MinimoSelecionada = 110;
    const double Estreita = 96;

    double[] _larguras = [];

    protected override Size MeasureOverride(Size disponivel)
    {
        var naturais = new double[Children.Count];
        var minimos = new double[Children.Count];
        double altura = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Measure(new Size(double.PositiveInfinity, disponivel.Height));
            naturais[i] = Children[i].DesiredSize.Width;
            minimos[i] = Children[i].DataContext is GuiaVM { Selecionada: true } ? MinimoSelecionada : Minimo;
            altura = Math.Max(altura, Children[i].DesiredSize.Height);
        }
        _larguras = Distribuir(naturais, minimos, disponivel.Width);
        for (var i = 0; i < Children.Count; i++)
            if (_larguras[i] < naturais[i]) Children[i].Measure(new Size(_larguras[i], disponivel.Height));
        return new Size(_larguras.Sum(), altura);
    }

    protected override Size ArrangeOverride(Size final)
    {
        double x = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            var largura = i < _larguras.Length ? _larguras[i] : Children[i].DesiredSize.Width;
            Children[i].Classes.Set("estreita", largura < Estreita);
            Children[i].Arrange(new Rect(x, 0, largura, final.Height));
            x += largura;
        }
        return final;
    }

    public static double[] Distribuir(IReadOnlyList<double> naturais, IReadOnlyList<double> minimos, double disponivel)
    {
        if (double.IsInfinity(disponivel) || naturais.Sum() <= disponivel) return [.. naturais];
        double Soma(double teto) => naturais.Select((n, i) => Math.Min(n, Math.Max(teto, minimos[i]))).Sum();
        double baixo = 0, alto = naturais.Max();
        for (var i = 0; i < 40; i++)
        {
            var meio = (baixo + alto) / 2;
            if (Soma(meio) <= disponivel) baixo = meio;
            else alto = meio;
        }
        var teto = Math.Floor(baixo + 1e-6);
        if (Soma(teto) > disponivel) teto = Math.Max(0, teto - 1);
        return [.. naturais.Select((n, i) => Math.Min(n, Math.Max(teto, minimos[i])))];
    }
}
