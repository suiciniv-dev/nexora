using System.Runtime.InteropServices;

namespace Nexora.Desempenho;

public static class Presenca
{
    [StructLayout(LayoutKind.Sequential)]
    struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr janela, out uint processo);

    public static readonly TimeSpan ParadoComOutraJanela = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan ParadoDeVez = TimeSpan.FromMinutes(10);

    public static bool Ausente(TimeSpan parado, bool naFrente) => parado > ParadoDeVez || parado > ParadoComOutraJanela && !naFrente;

    public static bool Ausente()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (!GetLastInputInfo(ref info)) return false;
            var parado = TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime));
            var janela = GetForegroundWindow();
            var naFrente = janela != IntPtr.Zero && GetWindowThreadProcessId(janela, out var processo) != 0 && processo == (uint)Environment.ProcessId;
            return Ausente(parado, naFrente);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }
}
