using Avalonia.Controls;
using Avalonia.Threading;

namespace Nexora.Views;

public partial class AtalhosView : UserControl
{
    public AtalhosView()
    {
        InitializeComponent();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && IsVisible) Dispatcher.UIThread.Post(() => Focus());
        };
    }
}
