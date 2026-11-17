using Avalonia;
using Avalonia.Controls;

namespace Nexora.Controles;

public sealed class GradeAdaptativa : Panel
{
    public static readonly StyledProperty<int> ColunasProperty = AvaloniaProperty.Register<GradeAdaptativa, int>(nameof(Colunas), 1);
    public static readonly StyledProperty<double> LarguraMinimaProperty = AvaloniaProperty.Register<GradeAdaptativa, double>(nameof(LarguraMinima));
    public static readonly StyledProperty<double> EspacoProperty = AvaloniaProperty.Register<GradeAdaptativa, double>(nameof(Espaco));
    public static readonly StyledProperty<double> EspacoVerticalProperty = AvaloniaProperty.Register<GradeAdaptativa, double>(nameof(EspacoVertical));
    public static readonly StyledProperty<bool> PreencherAlturaProperty = AvaloniaProperty.Register<GradeAdaptativa, bool>(nameof(PreencherAltura));

    static GradeAdaptativa() => AffectsMeasure<GradeAdaptativa>(ColunasProperty, LarguraMinimaProperty, EspacoProperty, EspacoVerticalProperty);

    public int Colunas { get => GetValue(ColunasProperty); set => SetValue(ColunasProperty, value); }
    public double LarguraMinima { get => GetValue(LarguraMinimaProperty); set => SetValue(LarguraMinimaProperty, value); }
    public double Espaco { get => GetValue(EspacoProperty); set => SetValue(EspacoProperty, value); }
    public double EspacoVertical { get => GetValue(EspacoVerticalProperty); set => SetValue(EspacoVerticalProperty, value); }
    public bool PreencherAltura { get => GetValue(PreencherAlturaProperty); set => SetValue(PreencherAlturaProperty, value); }

    readonly List<double> _alturas = [];
    int _colunas = 1;
    double _celula;

    public static int ContarColunas(double largura, double larguraMinima, double espaco, int padrao) =>
        larguraMinima <= 0 || double.IsInfinity(largura) || double.IsNaN(largura)
            ? Math.Max(1, padrao)
            : Math.Max(1, (int)Math.Floor((largura + espaco) / (larguraMinima + espaco)));

    protected override Size MeasureOverride(Size disponivel)
    {
        var filhos = Children.Where(c => c.IsVisible).ToList();
        _alturas.Clear();
        if (filhos.Count == 0) return default;
        _colunas = ContarColunas(disponivel.Width, LarguraMinima, Espaco, Colunas);
        var largura = double.IsInfinity(disponivel.Width) ? _colunas * Math.Max(LarguraMinima, 0) + (_colunas - 1) * Espaco : disponivel.Width;
        _celula = largura <= 0 ? 0 : Math.Max(0, (largura - (_colunas - 1) * Espaco) / _colunas);
        var altura = 0.0;
        for (var inicio = 0; inicio < filhos.Count; inicio += _colunas)
        {
            var linha = 0.0;
            for (var i = inicio; i < Math.Min(inicio + _colunas, filhos.Count); i++)
            {
                filhos[i].Measure(new Size(_celula, double.PositiveInfinity));
                linha = Math.Max(linha, filhos[i].DesiredSize.Height);
            }
            _alturas.Add(linha);
            altura += linha;
        }
        altura += (_alturas.Count - 1) * EspacoVertical;
        return new Size(largura, altura);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var filhos = Children.Where(c => c.IsVisible).ToList();
        var y = 0.0;
        var sobra = 0.0;
        if (PreencherAltura && _alturas.Count > 0 && !double.IsInfinity(final.Height))
            sobra = Math.Max(0, (final.Height - _alturas.Sum() - (_alturas.Count - 1) * EspacoVertical) / _alturas.Count);
        for (var linha = 0; linha < _alturas.Count; linha++)
        {
            for (var coluna = 0; coluna < _colunas; coluna++)
            {
                var indice = linha * _colunas + coluna;
                if (indice >= filhos.Count) break;
                filhos[indice].Arrange(new Rect(coluna * (_celula + Espaco), y, _celula, _alturas[linha] + sobra));
            }
            y += _alturas[linha] + sobra + EspacoVertical;
        }
        return final;
    }
}
