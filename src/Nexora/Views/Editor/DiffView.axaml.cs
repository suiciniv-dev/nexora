using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.ViewModels;

namespace Nexora.Views;

public partial class DiffView : UserControl
{
    bool _shift;

    public DiffView()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, (_, e) => _shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift), RoutingStrategies.Tunnel, true);
        AddHandler(KeyDownEvent, Tecla, RoutingStrategies.Tunnel, true);
    }

    void Tecla(object? sender, KeyEventArgs e)
    {
        _shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (e.Source is Control { Name: "CampoComentario" } campo) Campo_KeyDown(campo, e);
    }

    void Comentar_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: LinhaDiffVM linha } || DataContext is not DiffVM diff) return;
        var estender = _shift;
        _shift = false;
        linha.Comentar(estender);
        Dispatcher.UIThread.Post(() => Focar(diff), DispatcherPriority.Background);
    }

    void Focar(DiffVM diff)
    {
        if (diff.Linhas.FirstOrDefault(l => l.Escrevendo) is not { } alvo) return;
        Lista.ScrollIntoView(alvo);
        if (Lista.ContainerFromItem(alvo) is { } container && container.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.Name == "CampoComentario") is { } campo)
            campo.Focus();
    }

    void Campo_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control { DataContext: LinhaDiffVM linha }) return;
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            linha.ConfirmarCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            linha.CancelarCommand.Execute(null);
            e.Handled = true;
        }
    }
}
