using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Nexora.ViewModels;

namespace Nexora.Views;

public static class FecharPopups
{
    public static void Ligar(TopLevel janela) => janela.AddHandler(InputElement.KeyDownEvent, (_, e) =>
    {
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && FecharUm(janela)) e.Handled = true;
    }, RoutingStrategies.Tunnel);

    public static bool FecharUm(Visual raiz)
    {
        foreach (var c in raiz.GetVisualDescendants().OfType<Control>().Reverse())
        {
            switch (c)
            {
                case Popup { IsOpen: true, IsLightDismissEnabled: true } popup:
                    popup.IsOpen = false;
                    return true;
                case Button { Flyout: { IsOpen: true } flyout }:
                    flyout.Hide();
                    return true;
                case ChatView { IsEffectivelyVisible: true } chat when chat.FecharDetalhes():
                    return true;
            }
            if (c.ContextMenu is { IsOpen: true } menu)
            {
                menu.Close();
                return true;
            }
            if (c.ContextFlyout is { IsOpen: true } contexto)
            {
                contexto.Hide();
                return true;
            }
        }
        return raiz is Janela { DataContext: JanelaVM { LateralSobreposta: true } vm } && FecharLateral(vm);
    }

    static bool FecharLateral(JanelaVM vm)
    {
        vm.LateralAberta = false;
        return true;
    }
}
