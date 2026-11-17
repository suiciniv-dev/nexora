using Avalonia;
using Avalonia.Controls;

namespace Nexora.Views;

public partial class TrechoView : UserControl
{
    public static readonly StyledProperty<double> AlturaProperty = AvaloniaProperty.Register<TrechoView, double>(nameof(Altura), 380);

    public double Altura { get => GetValue(AlturaProperty); set => SetValue(AlturaProperty, value); }

    public TrechoView() => InitializeComponent();
}
