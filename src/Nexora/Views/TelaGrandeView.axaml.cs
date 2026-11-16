using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Nexora.ViewModels;

namespace Nexora.Views;

public partial class TelaGrandeView : UserControl
{
    public TelaGrandeView()
    {
        InitializeComponent();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && IsVisible) Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Input);
        };
    }

    public static bool Teclar(TelaGrandeVM tela, KeyEventArgs e)
    {
        if (!tela.Ligada || !(tela.Aberta || tela.NoMenu)) return false;
        if (e.Key == Key.Tab && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            tela.Rodar(e.KeyModifiers == KeyModifiers.Shift ? -1 : 1);
            return true;
        }
        if (e.KeyModifiers != KeyModifiers.None) return false;
        switch (e.Key)
        {
            case Key.Left:
                tela.Mover(-1, 0);
                break;
            case Key.Right:
                tela.Mover(1, 0);
                break;
            case Key.Up:
                tela.Mover(0, -1);
                break;
            case Key.Down:
                tela.Mover(0, 1);
                break;
            case Key.Enter or Key.Space:
                tela.Confirmar();
                break;
            case Key.P:
                tela.Permitir();
                break;
            case Key.N:
                tela.Negar();
                break;
            case Key.F:
                tela.ModoFocoCommand.Execute(null);
                break;
            case >= Key.D1 and <= Key.D9:
                tela.ResponderNumero(e.Key - Key.D0);
                break;
            case >= Key.NumPad1 and <= Key.NumPad9:
                tela.ResponderNumero(e.Key - Key.NumPad0);
                break;
            case Key.Escape:
                tela.Voltar();
                break;
            default:
                return false;
        }
        return true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Handled || DataContext is not TelaGrandeVM tela || tela.NoMenu || !tela.Aberta) return;
        if (Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y)) tela.Mover(-Math.Sign(e.Delta.X), 0);
        else if (e.KeyModifiers == KeyModifiers.Shift) tela.Mover(-Math.Sign(e.Delta.Y), 0);
        else if (e.Delta.Y < 0 || tela.Linha > 0) tela.Mover(0, -Math.Sign(e.Delta.Y));
        e.Handled = true;
    }

    void Cartao_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: CartaoVM cartao } || DataContext is not TelaGrandeVM tela) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        tela.Selecionar(cartao);
        e.Handled = true;
    }
}
