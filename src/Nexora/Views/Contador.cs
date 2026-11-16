using Avalonia;
using Avalonia.Controls;
using Nexora.ViewModels;

namespace Nexora.Views;

public sealed class Contador : TextBlock
{
    public static readonly StyledProperty<int> ValorProperty = AvaloniaProperty.Register<Contador, int>(nameof(Valor));

    const double SegundosPorSalto = 0.9;
    double _exibido;
    double _porSegundo;
    long? _ultimoQuadro;
    bool _quadroPedido;

    protected override Type StyleKeyOverride => typeof(TextBlock);

    public int Valor
    {
        get => GetValue(ValorProperty);
        set => SetValue(ValorProperty, value);
    }

    public int Exibido => (int)_exibido;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs mudanca)
    {
        base.OnPropertyChanged(mudanca);
        if (mudanca.Property == ValorProperty) Mirar();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _exibido = Valor;
        Escrever();
    }

    void Mirar()
    {
        if (Valor <= _exibido || TopLevel.GetTopLevel(this) is not { } topo)
        {
            _exibido = Valor;
            Escrever();
            return;
        }
        _porSegundo = Math.Max(40, (Valor - _exibido) / SegundosPorSalto);
        _ultimoQuadro = null;
        if (_quadroPedido) return;
        _quadroPedido = true;
        topo.RequestAnimationFrame(Quadro);
    }

    void Quadro(TimeSpan _)
    {
        _quadroPedido = false;
        var agora = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_ultimoQuadro is { } antes) Avancar(System.Diagnostics.Stopwatch.GetElapsedTime(antes, agora));
        _ultimoQuadro = agora;
        if (_exibido >= Valor || TopLevel.GetTopLevel(this) is not { } topo) return;
        _quadroPedido = true;
        topo.RequestAnimationFrame(Quadro);
    }

    public void Avancar(TimeSpan passo)
    {
        _exibido = Math.Min(Valor, _exibido + _porSegundo * passo.TotalSeconds);
        Escrever();
    }

    void Escrever() => Text = ItemPensamento.TextoTokens((int)_exibido);
}
