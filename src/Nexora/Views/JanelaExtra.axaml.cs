using Avalonia.Controls;
using Nexora.Views;

namespace Nexora.Views;

public partial class JanelaExtra : Window
{
    public JanelaExtra()
    {
        InitializeComponent();
        IdentidadeJanela.Aplicar(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
        Temas.VidroDaJanela.Acompanhar(this);
        Views.FecharPopups.Ligar(this);
        Nexora.Controles.Disposicao.Acompanhar(this);
        if (Sistema.Mac)
        {
            Sistema.AjustarAtalhos(this);
            MarcaTitulo.Margin = new Avalonia.Thickness(80, 0, 10, 0);
            BarraTitulo.ColumnDefinitions[2].Width = new GridLength(14);
        }
        Opened += (_, _) => IdentidadeJanela.Aplicar(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
        Aparencia.Mudou += AplicarEscala;
        Closed += (_, _) => Aparencia.Mudou -= AplicarEscala;
        AplicarEscala();
    }

    void AplicarEscala()
    {
        var escala = Aparencia.Zoom;
        Escalador.LayoutTransform = Math.Abs(escala - 1) < 0.001 ? null : new Avalonia.Media.ScaleTransform(escala, escala);
        if (Sistema.Mac) MarcaTitulo.Margin = new Avalonia.Thickness(80 / escala, 0, 10, 0);
        else BarraTitulo.ColumnDefinitions[2].Width = new GridLength(150 / escala);
    }

    public AbasView FaixaDeAbas => Abas;
}
