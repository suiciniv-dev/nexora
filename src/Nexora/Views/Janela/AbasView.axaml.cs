using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Nexora.ViewModels;

namespace Nexora.Views;

public partial class AbasView : UserControl
{
    const double Folga = 10;

    GuiaVM? _pega;
    Point _inicio;
    bool _arrastando;

    AreaVM? _area;

    public AbasView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_area is not null) _area.PropertyChanged -= AoMudarArea;
            _area = DataContext as AreaVM;
            if (_area is not null) _area.PropertyChanged += AoMudarArea;
        };
    }

    void AoMudarArea(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AreaVM.Selecionada)) Lista.ItemsPanelRoot?.InvalidateMeasure();
    }

    public bool Contem(PixelPoint tela) => NaFaixa(this.PointToClient(tela));

    public int IndiceEm(PixelPoint tela) => IndiceEm(this.PointToClient(tela).X);

    int IndiceEm(double x)
    {
        for (var i = 0; i < Lista.ItemCount; i++)
            if (Lista.ContainerFromIndex(i) is { } guia && guia.TranslatePoint(new Point(guia.Bounds.Width / 2, 0), this) is { } meio && x < meio.X)
                return i;
        return Lista.ItemCount;
    }

    void AoApertar(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: GuiaVM guia } alca) return;
        var botoes = e.GetCurrentPoint(alca).Properties;
        if (botoes.IsMiddleButtonPressed)
        {
            guia.FecharCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (!botoes.IsLeftButtonPressed) return;
        guia.SelecionarCommand.Execute(null);
        _pega = guia;
        _inicio = e.GetPosition(this);
        _arrastando = false;
        e.Pointer.Capture(alca);
        e.Handled = true;
    }

    void AoMover(object? sender, PointerEventArgs e)
    {
        if (_pega is null) return;
        var agora = e.GetPosition(this);
        if (!_arrastando && Math.Abs(agora.X - _inicio.X) + Math.Abs(agora.Y - _inicio.Y) < 8) return;
        _arrastando = true;
        if (!NaFaixa(agora) || DataContext is not AreaVM area) return;
        var alvo = IndiceEm(agora.X);
        var atual = area.Guias.IndexOf(_pega);
        if (alvo > atual) alvo--;
        if (alvo != atual) area.Mover(_pega, alvo);
    }

    void AoSoltar(object? sender, PointerReleasedEventArgs e)
    {
        var guia = _pega;
        _pega = null;
        e.Pointer.Capture(null);
        if (guia is null || !_arrastando) return;
        var agora = e.GetPosition(this);
        if (NaFaixa(agora)) return;
        Janelas.Soltar(guia, this.PointToScreen(agora));
    }

    bool NaFaixa(Point local) => local.X >= -Folga && local.X <= Bounds.Width + Folga && local.Y >= -Folga && local.Y <= Bounds.Height + Folga;
}
