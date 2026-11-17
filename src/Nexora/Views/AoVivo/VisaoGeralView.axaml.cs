using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.ViewModels;

namespace Nexora.Views;

public partial class VisaoGeralView : UserControl
{
    const int Guardadas = 4;
    readonly List<(ChatVM Chat, ChatView Vista)> _espiadas = [];
    VisaoGeralVM? _vm;

    public int VistasCriadas { get; private set; }

    public VisaoGeralView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Ligar(DataContext as VisaoGeralVM);
        AddHandler(PointerMovedEvent, AoPassar, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(KeyDownEvent, AoTeclar, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        SizeChanged += (_, e) =>
        {
            Raiz.Classes.Set("estreita", e.NewSize.Width < 1200);
            Raiz.Classes.Set("compacta", e.NewSize.Width < 1060);
            Raiz.Classes.Set("empilhada", e.NewSize.Width < 900);
        };
    }

    void Ligar(VisaoGeralVM? vm)
    {
        if (_vm is not null) _vm.PropertyChanged -= AoMudarVm;
        _vm = vm;
        if (vm is not null) vm.PropertyChanged += AoMudarVm;
        MostrarEspiada();
    }

    void AoMudarVm(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VisaoGeralVM.Espiado)) MostrarEspiada();
    }

    ChatView Vista(ChatVM chat)
    {
        var i = _espiadas.FindIndex(e => ReferenceEquals(e.Chat, chat));
        ChatView vista;
        if (i >= 0)
        {
            vista = _espiadas[i].Vista;
            _espiadas.RemoveAt(i);
        }
        else
        {
            vista = new ChatView { Compacto = true, DataContext = chat, IsVisible = false };
            CorpoEspiada.Children.Add(vista);
            VistasCriadas++;
        }
        _espiadas.Insert(0, (chat, vista));
        while (_espiadas.Count > Guardadas)
        {
            var velha = _espiadas[^1];
            _espiadas.RemoveAt(_espiadas.Count - 1);
            CorpoEspiada.Children.Remove(velha.Vista);
            velha.Vista.DataContext = null;
        }
        return vista;
    }

    void MostrarEspiada()
    {
        var chat = _vm?.EspiadoChat;
        var ativa = chat is null ? null : Vista(chat);
        foreach (var (_, vista) in _espiadas) vista.IsVisible = ReferenceEquals(vista, ativa);
    }

    void AoPassar(object? sender, PointerEventArgs e)
    {
        if (_vm is null || _vm.Espiando || e.Source is not Visual origem) return;
        var botao = origem as Button ?? origem.FindAncestorOfType<Button>();
        if (botao is null || !botao.Classes.Contains("card") || botao.DataContext is not ChatVM chat) return;
        if (_espiadas.Any(x => ReferenceEquals(x.Chat, chat))) return;
        Dispatcher.UIThread.Post(() => Preparar(chat), DispatcherPriority.Background);
    }

    public void Preparar(ChatVM chat)
    {
        if (_vm is null || _vm.Espiando || _espiadas.Any(x => ReferenceEquals(x.Chat, chat))) return;
        var vista = Vista(chat);
        vista.IsVisible = true;
        vista.Opacity = 0;
        Dispatcher.UIThread.Post(() =>
        {
            vista.Opacity = 1;
            vista.IsVisible = ReferenceEquals(_vm?.EspiadoChat, chat);
        }, DispatcherPriority.Background);
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
