using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Nexora;

static class IdentidadeJanela
{
    public const string Aumid = "Suiciniv.Nexora";

    [StructLayout(LayoutKind.Sequential)]
    struct PROPERTYKEY { public Guid fmtid; public uint pid; }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    struct PROPVARIANT { [FieldOffset(0)] public ushort vt; [FieldOffset(8)] public IntPtr p; }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint n);
        [PreserveSig] int GetAt(uint i, out PROPERTYKEY k);
        [PreserveSig] int GetValue(ref PROPERTYKEY k, out PROPVARIANT v);
        [PreserveSig] int SetValue(ref PROPERTYKEY k, ref PROPVARIANT v);
        [PreserveSig] int Commit();
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    class ShellLink { }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arquivo, int tamanho, IntPtr dados, uint flags);
        void GetIDList(out IntPtr lista);
        void SetIDList(IntPtr lista);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder nome, int tamanho);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string nome);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pasta, int tamanho);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pasta);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder argumentos, int tamanho);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string argumentos);
        void GetHotkey(out short tecla);
        void SetHotkey(short tecla);
        void GetShowCmd(out int modo);
        void SetShowCmd(int modo);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder icone, int tamanho, out int indice);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string icone, int indice);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string caminho, uint reservado);
        void Resolve(IntPtr janela, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string arquivo);
    }

    [DllImport("shell32.dll")] static extern int SHGetPropertyStoreForWindow(IntPtr h, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object? store);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SetCurrentProcessExplicitAppUserModelID(string id);
    [DllImport("ole32.dll")] static extern int PropVariantClear(ref PROPVARIANT v);

    static readonly Guid Formato = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");

    public static string AtalhoDoMenu => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Nexora.lnk");

    public static void DoProcesso()
    {
        if (OperatingSystem.IsWindows()) SetCurrentProcessExplicitAppUserModelID(Aumid);
    }

    public static (string Alvo, string Argumentos) Abertura()
    {
        var pasta = AppContext.BaseDirectory;
        var processo = Environment.ProcessPath ?? "dotnet.exe";
        var dotnet = Trampolim.RodandoPeloDotnet ? processo : Trampolim.ExecutavelDependeDoDotnet ? Trampolim.DotnetInstalado() : null;
        if (dotnet is null) return (processo, "");
        return (Path.Combine(Environment.SystemDirectory, "conhost.exe"), $"--headless \"{dotnet}\" \"{Path.Combine(pasta, "Nexora.dll")}\"");
    }

    public static void Aplicar(IntPtr hwnd)
    {
        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero) return;
        var (alvo, argumentos) = Abertura();
        var icone = Path.Combine(AppContext.BaseDirectory, "Assets", "nexora.ico");
        var iid = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
        if (SHGetPropertyStoreForWindow(hwnd, ref iid, out var obj) != 0 || obj is not IPropertyStore ps) return;
        try
        {
            Definir(ps, 5, Aumid);
            Definir(ps, 2, $"\"{alvo}\" {argumentos}".TrimEnd());
            Definir(ps, 3, icone + ",0");
            Definir(ps, 4, "Nexora");
            ps.Commit();
        }
        catch (COMException)
        {
        }
        finally
        {
            Marshal.ReleaseComObject(obj);
        }
    }

    public static void GarantirAtalhos()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (alvo, argumentos) = Abertura();
        foreach (var (arquivo, criar) in new[] { (AtalhoDoMenu, true), (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Nexora.lnk"), false) })
        {
            try { Garantir(arquivo, criar, alvo, argumentos); }
            catch (Exception e) when (e is COMException or IOException or UnauthorizedAccessException or InvalidCastException)
            {
            }
        }
    }

    static void Garantir(string arquivo, bool criar, string alvo, string argumentos)
    {
        var existe = File.Exists(arquivo);
        if (!existe && !criar) return;
        var link = new ShellLink();
        try
        {
            var sl = (IShellLinkW)link;
            var ps = (IPropertyStore)link;
            if (existe)
            {
                ((IPersistFile)link).Load(arquivo, 2);
                if (!DesteNexora(sl, alvo, argumentos) || Ler(ps, 5) == Aumid) return;
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
                sl.SetPath(alvo);
                sl.SetArguments(argumentos);
                sl.SetWorkingDirectory(AppContext.BaseDirectory.TrimEnd('\\'));
                sl.SetIconLocation(Path.Combine(AppContext.BaseDirectory, "Assets", "nexora.ico"), 0);
                sl.SetDescription("Nexora");
            }
            Definir(ps, 5, Aumid);
            ps.Commit();
            ((IPersistFile)link).Save(arquivo, true);
        }
        finally
        {
            Marshal.ReleaseComObject(link);
        }
    }

    static bool DesteNexora(IShellLinkW sl, string alvo, string argumentos)
    {
        var caminho = new StringBuilder(1024);
        sl.GetPath(caminho, caminho.Capacity, IntPtr.Zero, 0);
        if (!string.Equals(caminho.ToString(), alvo, StringComparison.OrdinalIgnoreCase)) return false;
        if (argumentos.Length == 0) return true;
        var lidos = new StringBuilder(2048);
        sl.GetArguments(lidos, lidos.Capacity);
        return lidos.ToString().Contains(Path.Combine(AppContext.BaseDirectory, "Nexora.dll"), StringComparison.OrdinalIgnoreCase);
    }

    static string? Ler(IPropertyStore ps, uint pid)
    {
        var k = new PROPERTYKEY { fmtid = Formato, pid = pid };
        if (ps.GetValue(ref k, out var v) != 0) return null;
        try { return v.vt == 31 ? Marshal.PtrToStringUni(v.p) : null; }
        finally { PropVariantClear(ref v); }
    }

    static void Definir(IPropertyStore ps, uint pid, string valor)
    {
        var k = new PROPERTYKEY { fmtid = Formato, pid = pid };
        var v = new PROPVARIANT { vt = 31, p = Marshal.StringToCoTaskMemUni(valor) };
        try { ps.SetValue(ref k, ref v); }
        finally { Marshal.FreeCoTaskMem(v.p); }
    }
}
