using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.ViewModels;

namespace Nexora.Views;

public partial class VisaoGeralView : UserControl
{
    public VisaoGeralView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, AoTeclar, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        SizeChanged += (_, e) =>
        {
            Raiz.Classes.Set("estreita", e.NewSize.Width < 1200);
            Raiz.Classes.Set("compacta", e.NewSize.Width < 1060);
            Raiz.Classes.Set("empilhada", e.NewSize.Width < 900);
        };
    }

    void AoTeclar(object? sender, KeyEventArgs e)
    {
        if (DataContext is not VisaoGeralVM vm || !vm.ModoQuadro) return;
        if (e.Key == Key.Escape && vm.Espiando)
        {
            Fechar(vm);
            e.Handled = true;
            return;
        }
        if (e.KeyModifiers != KeyModifiers.None || EmTexto(e.Source)) return;
        if (e.Key == Key.F && !vm.Espiando)
        {
            vm.AbrirFocoCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (Navegar(e.Key)) e.Handled = true;
    }

    public bool Navegar(Key tecla)
    {
        if (DataContext is not VisaoGeralVM vm || !vm.ModoQuadro) return false;
        (int dx, int dy)? direcao = tecla switch
        {
            Key.Left => (-1, 0),
            Key.Right => (1, 0),
            Key.Up => (0, -1),
            Key.Down => (0, 1),
            _ => null,
        };
        if (direcao is not { } d) return false;
        if (vm.Espiando)
        {
            if (vm.EspiarVizinho(d.dx + d.dy) is { } espiado) Focar(espiado);
            return true;
        }
        var atual = CartaoEmFoco();
        if (vm.Vizinho(atual, d.dx, d.dy) is not { } alvo || ReferenceEquals(alvo, atual)) return false;
        Focar(alvo);
        return true;
    }

    public static bool EmTexto(object? origem) => origem is Visual v && (v is TextBox || v.FindAncestorOfType<TextBox>() is not null);

    CartaoVM? CartaoEmFoco()
    {
        var foco = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        var botao = foco as Button ?? foco?.FindAncestorOfType<Button>();
        return botao is not null && botao.Classes.Contains("card") ? botao.DataContext as CartaoVM : null;
    }

    void Focar(CartaoVM cartao)
    {
        var botao = this.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.Classes.Contains("card") && ReferenceEquals(b.DataContext, cartao) && b.IsEffectivelyVisible);
        if (botao is null) return;
        botao.Focus(NavigationMethod.Directional);
        botao.BringIntoView();
    }

    void Fechar(VisaoGeralVM vm)
    {
        var cartao = vm.Espiado;
        vm.FecharEspiadaCommand.Execute(null);
        if (cartao is not null) Dispatcher.UIThread.Post(() => Focar(cartao), DispatcherPriority.Background);
    }

    void FundoEspiada_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is VisaoGeralVM vm) Fechar(vm);
        e.Handled = true;
    }
}
