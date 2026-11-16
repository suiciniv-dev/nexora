using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Nexora.ViewModels;

namespace Nexora.Views;

public partial class OrquestraView : UserControl
{
    object? _pego;
    Point _ultimo;
    OrquestraVM? _orquestra;

    public OrquestraView()
    {
        InitializeComponent();
        Rolagem.AddHandler(PointerWheelChangedEvent, AoRolar, RoutingStrategies.Tunnel);
        SizeChanged += (_, e) => Raiz.Classes.Set("estreita", e.NewSize.Width < 900);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == Disposicao.VerticalProperty) Reorganizar(Disposicao.GetVertical(this));
    }

    void Reorganizar(bool vertical)
    {
        if (vertical)
        {
            Divisao.ColumnDefinitions = new ColumnDefinitions("*");
            Divisao.RowDefinitions = new RowDefinitions(_orquestra is { ConversaVisivel: false } ? "*,Auto,Auto" : "2*,Auto,3*");
            Grid.SetRow(Rolagem, 0);
            Grid.SetColumn(Separador, 0);
            Grid.SetRow(Separador, 1);
            Separador.Width = double.NaN;
            Separador.Height = 1;
            Grid.SetColumn(PainelConversa, 0);
            Grid.SetRow(PainelConversa, 2);
            PainelConversa.Width = double.NaN;
            return;
        }
        Divisao.ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto");
        Divisao.RowDefinitions = new RowDefinitions();
        Grid.SetRow(Rolagem, 0);
        Grid.SetColumn(Separador, 1);
        Grid.SetRow(Separador, 0);
        Separador.Width = 1;
        Separador.Height = double.NaN;
        Grid.SetColumn(PainelConversa, 2);
        Grid.SetRow(PainelConversa, 0);
        PainelConversa.Width = 480;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_orquestra is not null) _orquestra.PropertyChanged -= AoMudar;
        _orquestra = DataContext as OrquestraVM;
        if (_orquestra is null) return;
        _orquestra.PropertyChanged += AoMudar;
        Escalar();
    }

    void AoMudar(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OrquestraVM.Zoom)) Escalar();
        else if (e.PropertyName == nameof(OrquestraVM.ConversaVisivel) && Disposicao.GetVertical(this)) Reorganizar(true);
    }

    void Escalar() => Escala.LayoutTransform = new ScaleTransform(_orquestra?.Zoom ?? 1, _orquestra?.Zoom ?? 1);

    void AoRolar(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(Sistema.Comando) || _orquestra is null) return;
        if (e.Delta.Y > 0) _orquestra.Aproximar();
        else if (e.Delta.Y < 0) _orquestra.Afastar();
        e.Handled = true;
    }

    void AoPegar(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control alca || !e.GetCurrentPoint(alca).Properties.IsLeftButtonPressed) return;
        _pego = alca.DataContext;
        _ultimo = e.GetPosition(Quadro);
        e.Pointer.Capture(alca);
        e.Handled = true;
    }

    void AoArrastar(object? sender, PointerEventArgs e)
    {
        if (_pego is null || _orquestra is null) return;
        var agora = e.GetPosition(Quadro);
        var (dx, dy) = (agora.X - _ultimo.X, agora.Y - _ultimo.Y);
        _ultimo = agora;
        switch (_pego)
        {
            case NoVM no:
                no.X = Math.Max(0, no.X + dx);
                no.Y = Math.Max(0, no.Y + dy);
                break;
            case GrupoVM grupo:
                _orquestra.MoverGrupo(grupo, dx, dy);
                break;
        }
    }

    void AoSoltar(object? sender, PointerReleasedEventArgs e)
    {
        if (_pego is NoVM { EhChefe: false } no) _orquestra?.Soltar(no);
        _pego = null;
        e.Pointer.Capture(null);
    }
}
