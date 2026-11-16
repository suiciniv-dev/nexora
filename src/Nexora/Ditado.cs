using System.Runtime.InteropServices;

namespace Nexora;

public static class Ditado
{
    const byte TeclaWindows = 0x5B;
    const byte TeclaH = 0x48;
    const uint Soltar = 0x0002;

    [DllImport("user32.dll")]
    static extern void keybd_event(byte tecla, byte varredura, uint opcoes, UIntPtr extra);

    public static bool Disponivel => OperatingSystem.IsWindows();

    public static void Abrir()
    {
        if (!Disponivel) return;
        if (Sistema.SoAnotar is { } anotar)
        {
            anotar("ditado por voz (Win+H)");
            return;
        }
        if (!Sistema.AppDeVerdade) return;
        keybd_event(TeclaWindows, 0, 0, UIntPtr.Zero);
        keybd_event(TeclaH, 0, 0, UIntPtr.Zero);
        keybd_event(TeclaH, 0, Soltar, UIntPtr.Zero);
        keybd_event(TeclaWindows, 0, Soltar, UIntPtr.Zero);
    }
}
