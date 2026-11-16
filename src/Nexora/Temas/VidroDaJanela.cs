using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Nexora.Temas;

public static class VidroDaJanela
{
    public static void Acompanhar(Window janela)
    {
        Aplicar(janela);
        void AoMudar() => Dispatcher.UIThread.Post(() => Aplicar(janela));
        Catalogo.Mudou += AoMudar;
        janela.Closed += (_, _) => Catalogo.Mudou -= AoMudar;
    }

    static void Aplicar(Window janela)
    {
        var tema = Catalogo.Atual;
        var fundo = tema.Cor("Fundo");
        janela.TransparencyLevelHint = Catalogo.EmVidro(tema, Catalogo.Modo) && (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) || OperatingSystem.IsMacOS())
            ? [WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Mica, WindowTransparencyLevel.None]
            : [WindowTransparencyLevel.None];
        janela.TransparencyBackgroundFallback = new SolidColorBrush(Color.FromRgb(fundo.R, fundo.G, fundo.B));
        janela.Icon = IconeDoTema.Icone(tema);
    }
}
