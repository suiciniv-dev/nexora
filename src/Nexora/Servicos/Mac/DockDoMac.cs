namespace Nexora.Servicos.Mac;

static class DockDoMac
{
    public static void Selo(string? texto)
    {
        if (!Cocoa.Disponivel) return;
        try
        {
            var app = Cocoa.Enviar(Cocoa.Classe("NSApplication"), Cocoa.Sel("sharedApplication"));
            var doca = app == IntPtr.Zero ? IntPtr.Zero : Cocoa.Enviar(app, Cocoa.Sel("dockTile"));
            if (doca == IntPtr.Zero) return;
            Cocoa.Enviar(doca, Cocoa.Sel("setBadgeLabel:"), Cocoa.Texto(texto));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
        }
    }
}
