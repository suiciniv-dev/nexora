using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace Nexora.Views;

public partial class TrilhoView : UserControl
{
    public static readonly Avalonia.StyledProperty<bool> EmbutidoProperty = Avalonia.AvaloniaProperty.Register<TrilhoView, bool>(nameof(Embutido));

    readonly List<Action> _recolher = [];

    public bool Embutido { get => GetValue(EmbutidoProperty); set => SetValue(EmbutidoProperty, value); }

    IEnumerable<Border> Capsulas => this.GetLogicalDescendants().OfType<Border>().Where(b => b.Classes.Contains("capsula"));

    protected override void OnPropertyChanged(Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != EmbutidoProperty) return;
        var embutido = Embutido;
        Raiz.Width = embutido ? double.NaN : 64;
        foreach (var capsula in Capsulas)
        {
            capsula.Classes.Set("embutida", embutido);
            capsula.Classes.Set("aberta", embutido);
            capsula.Margin = embutido ? new Avalonia.Thickness(8, 0) : capsula.Classes.Contains("rodape") ? new Avalonia.Thickness(8, 0, 0, 8) : new Avalonia.Thickness(8, 4, 0, 0);
        }
    }

    public TrilhoView()
    {
        InitializeComponent();
        foreach (var capsula in this.GetLogicalDescendants().OfType<Border>().Where(b => b.Classes.Contains("capsula")))
            Ligar(capsula);
        AddHandler(Button.ClickEvent, (_, _) => Recolher());
        DetachedFromVisualTree += (_, _) => Recolher();
    }

    void Ligar(Border capsula)
    {
        if (Sistema.Mac) capsula.Transitions = [];
        var abrir = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        var fechar = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        abrir.Tick += (_, _) =>
        {
            abrir.Stop();
            if (!Embutido && capsula.IsPointerOver) capsula.Classes.Set("aberta", true);
        };
        fechar.Tick += (_, _) =>
        {
            fechar.Stop();
            if (!Embutido && !capsula.IsPointerOver) capsula.Classes.Set("aberta", false);
        };
        capsula.PointerEntered += (_, _) =>
        {
            fechar.Stop();
            if (!capsula.Classes.Contains("aberta")) abrir.Start();
        };
        capsula.PointerExited += (_, _) =>
        {
            abrir.Stop();
            if (capsula.Classes.Contains("aberta")) fechar.Start();
        };
        capsula.PointerPressed += (_, _) => abrir.Stop();
        _recolher.Add(() =>
        {
            abrir.Stop();
            fechar.Stop();
            if (!Embutido) capsula.Classes.Set("aberta", false);
        });
    }

    public void Recolher()
    {
        foreach (var recolher in _recolher) recolher();
    }
}
