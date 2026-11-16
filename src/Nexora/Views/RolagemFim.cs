using Avalonia.Controls;

namespace Nexora.Views;

public sealed class RolagemFim : ScrollViewer
{
    const double Folga = 24;
    bool _noFim = true;

    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    public RolagemFim() => ScrollChanged += (_, e) =>
    {
        if (e.ExtentDelta.Y != 0 || e.ViewportDelta.Y != 0)
        {
            if (_noFim) ScrollToEnd();
            return;
        }
        _noFim = Offset.Y >= Extent.Height - Viewport.Height - Folga;
    };
}
