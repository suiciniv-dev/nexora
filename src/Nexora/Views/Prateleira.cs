using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Nexora.Views;

public sealed class Prateleira : Panel
{
    public static readonly StyledProperty<Orientation> OrientacaoProperty = AvaloniaProperty.Register<Prateleira, Orientation>(nameof(Orientacao));
    public static readonly StyledProperty<int> FocoProperty = AvaloniaProperty.Register<Prateleira, int>(nameof(Foco));
    public static readonly StyledProperty<bool> AtivaProperty = AvaloniaProperty.Register<Prateleira, bool>(nameof(Ativa), true);
    public static readonly StyledProperty<double> EspiaProperty = AvaloniaProperty.Register<Prateleira, double>(nameof(Espia));
    public static readonly StyledProperty<bool> AncoradaProperty = AvaloniaProperty.Register<Prateleira, bool>(nameof(Ancorada));
    public static readonly StyledProperty<double> FolgaProperty = AvaloniaProperty.Register<Prateleira, double>(nameof(Folga));
    public static readonly StyledProperty<double> DeslocamentoProperty = AvaloniaProperty.Register<Prateleira, double>(nameof(Deslocamento));

    static Prateleira()
    {
        AffectsMeasure<Prateleira>(OrientacaoProperty, FolgaProperty);
        AffectsArrange<Prateleira>(FocoProperty, EspiaProperty, AncoradaProperty, DeslocamentoProperty);
        FocoProperty.Changed.AddClassHandler<Prateleira>((p, _) => p.Marcar());
        AtivaProperty.Changed.AddClassHandler<Prateleira>((p, _) => p.Marcar());
    }

    public Prateleira() => ClipToBounds = true;

    public Orientation Orientacao { get => GetValue(OrientacaoProperty); set => SetValue(OrientacaoProperty, value); }
    public int Foco { get => GetValue(FocoProperty); set => SetValue(FocoProperty, value); }
    public bool Ativa { get => GetValue(AtivaProperty); set => SetValue(AtivaProperty, value); }
    public double Espia { get => GetValue(EspiaProperty); set => SetValue(EspiaProperty, value); }
    public bool Ancorada { get => GetValue(AncoradaProperty); set => SetValue(AncoradaProperty, value); }
    public double Folga { get => GetValue(FolgaProperty); set => SetValue(FolgaProperty, value); }
    public double Deslocamento { get => GetValue(DeslocamentoProperty); set => SetValue(DeslocamentoProperty, value); }

    bool Deitada => Orientacao == Orientation.Horizontal;

    public static double Alvo(IReadOnlyList<double> tamanhos, int foco, double janela, double espia, bool ancorada = false)
    {
        var total = tamanhos.Sum();
        if (tamanhos.Count == 0 || total <= janela) return 0;
        foco = Math.Clamp(foco, 0, tamanhos.Count - 1);
        var inicio = tamanhos.Take(foco).Sum() - (foco > 0 ? espia : 0);
        return ancorada ? Math.Max(0, inicio) : Math.Clamp(inicio, 0, total - janela);
    }

    protected override void ChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);
        Marcar();
    }

    void Marcar()
    {
        for (var i = 0; i < Children.Count; i++) Children[i].Classes.Set("foco", Ativa && i == Foco);
    }

    protected override Size MeasureOverride(Size disponivel)
    {
        var livre = Deitada ? new Size(double.PositiveInfinity, disponivel.Height) : new Size(disponivel.Width, double.PositiveInfinity);
        double comprido = Folga * 2, atraves = 0;
        foreach (var filho in Children)
        {
            filho.Measure(livre);
            var d = filho.DesiredSize;
            comprido += Deitada ? d.Width : d.Height;
            atraves = Math.Max(atraves, Deitada ? d.Height : d.Width);
        }
        var limite = Deitada ? disponivel.Width : disponivel.Height;
        if (!double.IsInfinity(limite)) comprido = Math.Min(comprido, limite);
        return Deitada ? new Size(comprido, atraves) : new Size(atraves, comprido);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var tamanhos = Children.Select(f => Deitada ? f.DesiredSize.Width : f.DesiredSize.Height).ToList();
        var alvo = Alvo([Folga, .. tamanhos, Folga], Foco + 1, Deitada ? final.Width : final.Height, Espia + Folga, Ancorada);
        if (Math.Abs(alvo - _alvo) > 0.5)
        {
            _alvo = alvo;
            if (IsEffectivelyVisible && _arrumou) Deslizar();
            else SetCurrentValue(DeslocamentoProperty, alvo);
        }
        _arrumou = true;
        var posicao = Folga - Deslocamento;
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Arrange(Deitada ? new Rect(posicao, 0, tamanhos[i], final.Height) : new Rect(0, posicao, final.Width, tamanhos[i]));
            posicao += tamanhos[i];
        }
        return final;
    }

    const double Tempo = 0.06;
    double _alvo;
    bool _arrumou, _deslizando;
    TimeSpan? _ultimo;

    void Deslizar()
    {
        if (_deslizando || TopLevel.GetTopLevel(this) is not { } raiz) return;
        _deslizando = true;
        _ultimo = null;
        raiz.RequestAnimationFrame(Quadro);
    }

    void Quadro(TimeSpan agora)
    {
        var passou = _ultimo is { } antes ? Math.Clamp((agora - antes).TotalSeconds, 0, 0.1) : 1 / 60.0;
        _ultimo = agora;
        var falta = Deslocamento - _alvo;
        var raiz = TopLevel.GetTopLevel(this);
        if (Math.Abs(falta) < 1 || raiz is null || !IsEffectivelyVisible)
        {
            _deslizando = false;
            SetCurrentValue(DeslocamentoProperty, _alvo);
            return;
        }
        SetCurrentValue(DeslocamentoProperty, _alvo + falta * Math.Exp(-passou / Tempo));
        raiz.RequestAnimationFrame(Quadro);
    }
}
