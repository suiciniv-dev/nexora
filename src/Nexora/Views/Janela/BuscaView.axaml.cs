using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Nexora.ViewModels;

namespace Nexora.Views;

public partial class BuscaView : UserControl
{
    public BuscaView()
    {
        InitializeComponent();
        Campo.AddHandler(KeyDownEvent, AoTeclar, RoutingStrategies.Tunnel);
        Lista.AddHandler(KeyDownEvent, AoTeclar, RoutingStrategies.Tunnel);
    }

    public void Focar() => Dispatcher.UIThread.Post(() =>
    {
        Campo.Focus();
        Campo.SelectAll();
    }, DispatcherPriority.Input);

    void AoTeclar(object? sender, KeyEventArgs e)
    {
        if (DataContext is not JanelaVM vm) return;
        switch (e.Key)
        {
            case Key.Escape:
                vm.FecharBuscaCommand.Execute(null);
                break;
            case Key.Down:
                vm.MoverResultado(1);
                break;
            case Key.Up:
                vm.MoverResultado(-1);
                break;
            case Key.Enter:
                vm.AbrirResultado(null);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    void Resultado_Tapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is JanelaVM vm && sender is Control { DataContext: ResultadoBuscaVM resultado }) vm.AbrirResultado(resultado);
    }
}
