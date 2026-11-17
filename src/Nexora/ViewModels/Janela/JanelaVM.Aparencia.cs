using CommunityToolkit.Mvvm.Input;

namespace Nexora.ViewModels;

public sealed partial class JanelaVM
{
    [RelayCommand]
    void AumentarTexto() => MudarZoom(Aparencia.Zoom + Aparencia.PassoDoZoom);

    [RelayCommand]
    void DiminuirTexto() => MudarZoom(Aparencia.Zoom - Aparencia.PassoDoZoom);

    public void MudarZoom(double zoom)
    {
        Aparencia.DefinirZoom(zoom);
        SalvarConfig();
    }

    public void MudarAnimacoes(string modo)
    {
        Aparencia.DefinirAnimacoes(modo);
        SalvarConfig();
    }

    public void MudarFonte(string fonte)
    {
        App.Config.Fonte = string.IsNullOrWhiteSpace(fonte) ? Aparencia.FonteDoSistema : fonte;
        Nexora.Temas.Catalogo.FonteDaInterface = App.Config.Fonte;
        Nexora.Temas.Catalogo.Aplicar(Nexora.Temas.Catalogo.Atual);
        SalvarConfig();
    }
}
