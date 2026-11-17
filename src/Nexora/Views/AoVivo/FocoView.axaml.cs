using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Nexora.ViewModels;

namespace Nexora.Views;

public partial class FocoView : UserControl
{
    public FocoView()
    {
        InitializeComponent();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && IsVisible) Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Input);
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (DataContext is not FocoVM foco || !foco.Aberto || e.KeyModifiers != KeyModifiers.None)
        {
            base.OnKeyDown(e);
            return;
        }
        e.Handled = true;
        switch (e.Key)
        {
            case Key.Escape:
                foco.FecharCommand.Execute(null);
                break;
            case Key.Right or Key.Down or Key.Tab:
                foco.ProximoCommand.Execute(null);
                break;
            case Key.Left or Key.Up:
                foco.AnteriorCommand.Execute(null);
                break;
            case Key.Enter or Key.P:
                foco.Permitir();
                break;
            case Key.N:
                foco.Negar();
                break;
            case Key.T:
                foco.EmTodas();
                break;
            case Key.S:
                foco.Sempre();
                break;
            case Key.O:
                foco.AbrirConversa();
                break;
            case >= Key.D1 and <= Key.D9:
                foco.Escolher(e.Key - Key.D0);
                break;
            case >= Key.NumPad1 and <= Key.NumPad9:
                foco.Escolher(e.Key - Key.NumPad0);
                break;
            default:
                e.Handled = false;
                base.OnKeyDown(e);
                break;
        }
    }
}
