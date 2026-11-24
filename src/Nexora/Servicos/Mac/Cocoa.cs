using System.Runtime.InteropServices;

namespace Nexora.Servicos.Mac;

static class Cocoa
{
    const string Objc = "/usr/lib/libobjc.A.dylib";

    [DllImport(Objc)] static extern IntPtr objc_getClass(string nome);
    [DllImport(Objc)] static extern IntPtr sel_registerName(string nome);
    [DllImport(Objc)] public static extern IntPtr objc_allocateClassPair(IntPtr pai, string nome, nint extras);
    [DllImport(Objc)] public static extern void objc_registerClassPair(IntPtr classe);
    [DllImport(Objc)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool class_addMethod(IntPtr classe, IntPtr seletor, IntPtr implementacao, string tipos);
    [DllImport(Objc)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool class_addProtocol(IntPtr classe, IntPtr protocolo);
    [DllImport(Objc)] public static extern IntPtr objc_getProtocol(string nome);

    [DllImport(Objc, EntryPoint = "objc_msgSend")] public static extern IntPtr Enviar(IntPtr alvo, IntPtr seletor);
    [DllImport(Objc, EntryPoint = "objc_msgSend")] public static extern IntPtr Enviar(IntPtr alvo, IntPtr seletor, IntPtr a);
    [DllImport(Objc, EntryPoint = "objc_msgSend")] public static extern IntPtr Enviar(IntPtr alvo, IntPtr seletor, IntPtr a, IntPtr b);
    [DllImport(Objc, EntryPoint = "objc_msgSend")] public static extern IntPtr Enviar(IntPtr alvo, IntPtr seletor, IntPtr a, IntPtr b, IntPtr c);
    [DllImport(Objc, EntryPoint = "objc_msgSend")] public static extern IntPtr Enviar(IntPtr alvo, IntPtr seletor, nuint a, IntPtr b);

    static readonly Dictionary<string, IntPtr> Classes = [];
    static readonly Dictionary<string, IntPtr> Seletores = [];

    public static bool Disponivel => OperatingSystem.IsMacOS();

    public static IntPtr Classe(string nome)
    {
        lock (Classes)
        {
            if (!Classes.TryGetValue(nome, out var classe) || classe == IntPtr.Zero) Classes[nome] = classe = objc_getClass(nome);
            return classe;
        }
    }

    public static IntPtr Sel(string nome)
    {
        lock (Seletores)
        {
            if (!Seletores.TryGetValue(nome, out var seletor)) Seletores[nome] = seletor = sel_registerName(nome);
            return seletor;
        }
    }

    public static IntPtr Novo(string classe) => Enviar(Enviar(Classe(classe), Sel("alloc")), Sel("init"));

    public static IntPtr Texto(string? texto)
    {
        if (texto is null) return IntPtr.Zero;
        var utf8 = Marshal.StringToCoTaskMemUTF8(texto);
        try
        {
            return Enviar(Classe("NSString"), Sel("stringWithUTF8String:"), utf8);
        }
        finally
        {
            Marshal.FreeCoTaskMem(utf8);
        }
    }

    public static string? Ler(IntPtr texto) => texto == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(Enviar(texto, Sel("UTF8String")));

    public static bool Framework(string nome) =>
        NativeLibrary.TryLoad($"/System/Library/Frameworks/{nome}.framework/{nome}", out _);

    public static bool NoPacote()
    {
        var pacote = Enviar(Classe("NSBundle"), Sel("mainBundle"));
        return pacote != IntPtr.Zero && Enviar(pacote, Sel("bundleIdentifier")) != IntPtr.Zero;
    }
}
