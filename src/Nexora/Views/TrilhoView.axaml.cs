using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace Nexora.Views;

public partial class TrilhoView : UserControl
{
    readonly List<Action> _recolher = [];

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
        var abrir = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        var fechar = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        abrir.Tick += (_, _) =>
        {
            abrir.Stop();
            if (capsula.IsPointerOver) capsula.Classes.Set("aberta", true);
        };
        fechar.Tick += (_, _) =>
        {
            fechar.Stop();
            if (!capsula.IsPointerOver) capsula.Classes.Set("aberta", false);
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
            capsula.Classes.Set("aberta", false);
        });
    }

    public void Recolher()
    {
        foreach (var recolher in _recolher) recolher();
    }
}
