using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Nexora.Controles;

public sealed class BotaoLigado : ToggleButton
{
    protected override Type StyleKeyOverride => typeof(Botao);

    protected override void Toggle()
    {
    }
}
