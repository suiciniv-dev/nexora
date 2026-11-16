using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Nexora.ViewModels;

namespace Nexora.Views;

public partial class IssuesView : UserControl
{
    IssueLinhaVM? _segurada;
    Point _inicio;
    bool _arrastando;
    GrupoIssuesVM? _alvo;

    public IssuesView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, AoTeclar, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, AoPressionar, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, AoMover, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, AoSoltar, RoutingStrategies.Tunnel, handledEventsToo: true);
        SizeChanged += (_, e) =>
        {
            Raiz.Classes.Set("estreita", e.NewSize.Width < 900);
            Raiz.Classes.Set("compacta", e.NewSize.Width < 780);
        };
        Tabela.SizeChanged += (_, e) => (DataContext as IssuesVM)?.Colunas.Ajustar(e.NewSize.Width);
        DataContextChanged += (_, _) => (DataContext as IssuesVM)?.Colunas.Ajustar(Tabela.Bounds.Width > 0 ? Tabela.Bounds.Width : 2000);
        AddHandler(PointerCaptureLostEvent, (_, _) => { if (!_arrastando) _segurada = null; }, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    async void CopiarLink_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not IssuesVM { LinkDaEspiada: { } link } vm || TopLevel.GetTopLevel(this)?.Clipboard is not { } area) return;
        await area.SetTextAsync(link);
        vm.MarcarLinkCopiado();
    }

    void AoTeclar(object? sender, KeyEventArgs e)
    {
        if (DataContext is not IssuesVM vm) return;
        if (_arrastando && e.Key == Key.Escape)
        {
            Cancelar();
            e.Handled = true;
            return;
        }
        if (!vm.Espiando) return;
        if (e.Key == Key.Escape)
        {
            vm.FecharEspiadaCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (e.KeyModifiers != KeyModifiers.None || EmCampo(e.Source)) return;
        var passo = e.Key switch
        {
            Key.Up => -1,
            Key.Down => 1,
            _ => 0,
        };
        if (passo == 0) return;
        vm.EspiarVizinho(passo);
        e.Handled = true;
    }

    static bool EmCampo(object? origem) =>
        origem is Visual v && (v is TextBox or ComboBox || v.FindAncestorOfType<TextBox>() is not null || v.FindAncestorOfType<ComboBox>() is not null);

    void AoPressionar(object? sender, PointerPressedEventArgs e)
    {
        _segurada = null;
        if (DataContext is not IssuesVM { Espiando: false } vm || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.Source is not Visual v || v.FindAncestorOfType<Button>(includeSelf: true) is not { DataContext: IssueLinhaVM linha } || !vm.PodeArrastar(linha)) return;
        _segurada = linha;
        _inicio = e.GetPosition(this);
    }

    void AoMover(object? sender, PointerEventArgs e)
    {
        if (_segurada is not { } linha) return;
        var agora = e.GetPosition(this);
        if (!_arrastando)
        {
            if (Math.Abs(agora.X - _inicio.X) < 6 && Math.Abs(agora.Y - _inicio.Y) < 6) return;
            _arrastando = true;
            TextoFantasma.Text = $"{linha.Numero}  {linha.Titulo}";
            Fantasma.IsVisible = true;
        }
        Canvas.SetLeft(Fantasma, agora.X + 14);
        Canvas.SetTop(Fantasma, agora.Y + 10);
        Marcar(GrupoEm(agora));
        e.Handled = true;
    }

    async void AoSoltar(object? sender, PointerReleasedEventArgs e)
    {
        if (!_arrastando)
        {
            _segurada = null;
            return;
        }
        e.Handled = true;
        var linha = _segurada;
        var grupo = GrupoEm(e.GetPosition(this));
        Cancelar();
        if (linha is not null && grupo is not null && DataContext is IssuesVM vm) await vm.SoltarAsync(linha, grupo);
    }

    GrupoIssuesVM? GrupoEm(Point ponto) =>
        this.InputHitTest(ponto, v => v is not Canvas && v != Fantasma) is Visual alvo
            ? alvo.GetSelfAndVisualAncestors().OfType<Control>().Select(c => c.DataContext).OfType<GrupoIssuesVM>().FirstOrDefault()
            : null;

    void Marcar(GrupoIssuesVM? grupo)
    {
        if (_alvo == grupo) return;
        if (_alvo is not null) _alvo.Alvo = false;
        _alvo = grupo;
        if (grupo is { OpcaoId: not null }) grupo.Alvo = true;
    }

    void Cancelar()
    {
        Marcar(null);
        _arrastando = false;
        _segurada = null;
        Fantasma.IsVisible = false;
    }

    void FundoEspiada_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is IssuesVM vm) vm.FecharEspiadaCommand.Execute(null);
        e.Handled = true;
    }
}
