#if !MAC
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Microsoft.Web.WebView2.Core;

namespace Nexora.Controles;

public sealed partial class WebViewNativo : NativeControlHost
{
    const uint FilhaVisivel = 0x40000000 | 0x10000000 | 0x02000000;

    CoreWebView2Controller? _controle;
    IntPtr _hwnd;

    public string PastaDados { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexora", "WebView2");

    public System.Drawing.Color Fundo { get; init; } = System.Drawing.Color.FromArgb(255, 18, 17, 16);
    public CoreWebView2? Web => _controle?.CoreWebView2;
    public event Action<CoreWebView2>? Pronto;
    public event Action<string>? Falhou;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        if (!OperatingSystem.IsWindows()) return base.CreateNativeControlCore(parent);
        _hwnd = CreateWindowExW(0, "Static", "", FilhaVisivel, 0, 0, 10, 10, parent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        _ = IniciarAsync();
        return new PlatformHandle(_hwnd, "HWND");
    }

    async Task IniciarAsync()
    {
        try
        {
            var ambiente = await CoreWebView2Environment.CreateAsync(null, PastaDados);
            _controle = await ambiente.CreateCoreWebView2ControllerAsync(_hwnd);
            _controle.DefaultBackgroundColor = Fundo;
            var web = _controle.CoreWebView2;
            web.Settings.AreDefaultContextMenusEnabled = false;
            web.Settings.AreDevToolsEnabled = false;
            web.Settings.IsZoomControlEnabled = false;
            web.Settings.IsStatusBarEnabled = false;
            web.Settings.AreBrowserAcceleratorKeysEnabled = false;
            web.Settings.IsPinchZoomEnabled = false;
            web.NewWindowRequested += (_, e) => e.Handled = true;
            Ajustar();
            _controle.MoveFocus(CoreWebView2MoveFocusReason.Programmatic);
            Pronto?.Invoke(web);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Falhou?.Invoke(ex.Message);
        }
    }

    public void Focar() => _controle?.MoveFocus(CoreWebView2MoveFocusReason.Programmatic);

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Ajustar();
    }

    void Ajustar()
    {
        if (_controle is null) return;
        var escala = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        _controle.Bounds = new System.Drawing.Rectangle(0, 0, (int)Math.Ceiling(Bounds.Width * escala), (int)Math.Ceiling(Bounds.Height * escala));
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        try
        {
            _controle?.Close();
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
        }
        _controle = null;
        if (OperatingSystem.IsWindows()) DestroyWindow(control.Handle);
        else base.DestroyNativeControlCore(control);
    }

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial IntPtr CreateWindowExW(uint estiloEx, string classe, string titulo, uint estilo, int x, int y, int largura, int altura, IntPtr pai, IntPtr menu, IntPtr instancia, IntPtr parametro);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(IntPtr hwnd);
}
#endif
