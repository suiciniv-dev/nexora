using Avalonia.Controls;

namespace Nexora.Views;

public partial class LateralView : UserControl
{
    public LateralView()
    {
        InitializeComponent();
        SolturaDePastas.Ligar(AreaDeSoltar, RealceSoltar);
    }
}
