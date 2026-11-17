using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace Nexora.Servicos;

static class Avisos
{
    [StructLayout(LayoutKind.Sequential)]
    struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    [DllImport("user32.dll")] static extern bool FlashWindowEx(ref FLASHWINFO info);

    const uint PiscarBarra = 0x2 | 0xC;

    public static void Piscar(IntPtr hwnd)
    {
        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero) return;
        var info = new FLASHWINFO { cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(), hwnd = hwnd, dwFlags = PiscarBarra, uCount = 3 };
        FlashWindowEx(ref info);
    }

#if MAC
    public static void Notificar(string titulo, string texto, string? argumento = null, Action<string>? aoClicar = null, IReadOnlyList<(string Rotulo, string Argumento)>? acoes = null)
    {
        static string Aspas(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        try
        {
            var info = new ProcessStartInfo("/usr/bin/osascript") { UseShellExecute = false, CreateNoWindow = true };
            info.ArgumentList.Add("-e");
            info.ArgumentList.Add($"display notification {Aspas(texto)} with title {Aspas(titulo)}");
            Sistema.Abrir(info)?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }
#else
    static bool _registrado;
    static readonly List<Windows.UI.Notifications.ToastNotification> Vivos = [];

    public static void Notificar(string titulo, string texto, string? argumento = null, Action<string>? aoClicar = null, IReadOnlyList<(string Rotulo, string Argumento)>? acoes = null)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)) return;
        if (Sistema.SoAnotar is { } anotar)
        {
            anotar($"aviso {titulo}: {texto}");
            return;
        }
        if (!Sistema.AppDeVerdade) return;
        try
        {
            Registrar();
            var xml = new Windows.Data.Xml.Dom.XmlDocument();
            var lancar = argumento is null ? "" : $" launch=\"{SecurityElement.Escape(argumento)}\"";
            var botoes = acoes is { Count: > 0 }
                ? "<actions>" + string.Concat(acoes.Select(a => $"<action content=\"{SecurityElement.Escape(a.Rotulo)}\" arguments=\"{SecurityElement.Escape(a.Argumento)}\" activationType=\"foreground\"/>")) + "</actions>"
                : "";
            xml.LoadXml($"<toast{lancar}><visual><binding template=\"ToastGeneric\"><text>{SecurityElement.Escape(titulo)}</text><text>{SecurityElement.Escape(texto)}</text></binding></visual>{botoes}</toast>");
            var aviso = new Windows.UI.Notifications.ToastNotification(xml);
            if (aoClicar is not null)
                aviso.Activated += (_, args) => aoClicar((args as Windows.UI.Notifications.ToastActivatedEventArgs)?.Arguments ?? argumento ?? "");
            lock (Vivos)
            {
                Vivos.Add(aviso);
                if (Vivos.Count > 20) Vivos.RemoveAt(0);
            }
            Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier(IdentidadeJanela.Aumid).Show(aviso);
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
        }
    }

    static void Registrar()
    {
        if (_registrado || !OperatingSystem.IsWindows()) return;
        using var chave = Registry.CurrentUser.CreateSubKey($@"Software\Classes\AppUserModelId\{IdentidadeJanela.Aumid}");
        chave.SetValue("DisplayName", "Nexora");
        chave.SetValue("IconUri", Path.Combine(AppContext.BaseDirectory, "Assets", "nexora.png"));
        _registrado = true;
    }
#endif
}
