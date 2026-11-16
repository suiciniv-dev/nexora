using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Nexora.Views;

public sealed class BotaoLigado : ToggleButton
{
    protected override Type StyleKeyOverride => typeof(Button);

    protected override void Toggle()
    {
    }
}
