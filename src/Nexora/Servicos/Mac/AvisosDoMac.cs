using System.Runtime.InteropServices;

namespace Nexora.Servicos.Mac;

static unsafe class AvisosDoMac
{
    [StructLayout(LayoutKind.Sequential)]
    struct Bloco
    {
        public IntPtr Isa;
        public int Flags;
        public int Reservado;
        public IntPtr Invocar;
        public IntPtr Descritor;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Descritor
    {
        public ulong Reservado;
        public ulong Tamanho;
    }

    const int BlocoGlobal = 1 << 28;
    const nuint PedirAlertaSomESelo = 1 | 2 | 4;
    const nuint MostrarSomListaEFaixa = 2 | 8 | 16;

    static IntPtr _centro;
    static bool _tentou;
    static readonly Dictionary<string, (Action<string> AoClicar, string Argumento)> Cliques = [];

    public static bool Preparar() => Centro() != IntPtr.Zero;

    static IntPtr Centro()
    {
        if (_tentou) return _centro;
        _tentou = true;
        if (!Cocoa.Disponivel) return IntPtr.Zero;
        try
        {
            if (!Cocoa.NoPacote() || !Cocoa.Framework("UserNotifications")) return IntPtr.Zero;
            var classe = Cocoa.Classe("UNUserNotificationCenter");
            var centro = classe == IntPtr.Zero ? IntPtr.Zero : Cocoa.Enviar(classe, Cocoa.Sel("currentNotificationCenter"));
            if (centro == IntPtr.Zero) return IntPtr.Zero;
            Cocoa.Enviar(centro, Cocoa.Sel("setDelegate:"), Delegado());
            Cocoa.Enviar(centro, Cocoa.Sel("requestAuthorizationWithOptions:completionHandler:"), PedirAlertaSomESelo,
                NovoBloco((IntPtr)(delegate* unmanaged<IntPtr, byte, IntPtr, void>)&AoPermitir));
            _centro = centro;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
        }
        return _centro;
    }

    public static bool Mostrar(string titulo, string texto, string? argumento, Action<string>? aoClicar)
    {
        var centro = Centro();
        if (centro == IntPtr.Zero) return false;
        try
        {
            var conteudo = Cocoa.Novo("UNMutableNotificationContent");
            Cocoa.Enviar(conteudo, Cocoa.Sel("setTitle:"), Cocoa.Texto(titulo));
            Cocoa.Enviar(conteudo, Cocoa.Sel("setBody:"), Cocoa.Texto(texto));
            Cocoa.Enviar(conteudo, Cocoa.Sel("setSound:"), Cocoa.Enviar(Cocoa.Classe("UNNotificationSound"), Cocoa.Sel("defaultSound")));
            var id = Guid.NewGuid().ToString("N");
            if (aoClicar is not null)
                lock (Cliques)
                {
                    Cliques[id] = (aoClicar, argumento ?? "");
                    if (Cliques.Count > 50) Cliques.Remove(Cliques.Keys.First());
                }
            var pedido = Cocoa.Enviar(Cocoa.Classe("UNNotificationRequest"), Cocoa.Sel("requestWithIdentifier:content:trigger:"), Cocoa.Texto(id), conteudo, IntPtr.Zero);
            Cocoa.Enviar(centro, Cocoa.Sel("addNotificationRequest:withCompletionHandler:"), pedido, IntPtr.Zero);
            Cocoa.Enviar(conteudo, Cocoa.Sel("release"));
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    static IntPtr Delegado()
    {
        const string Nome = "NexoraDelegadoDeAvisos";
        var classe = Cocoa.Classe(Nome);
        if (classe == IntPtr.Zero)
        {
            classe = Cocoa.objc_allocateClassPair(Cocoa.Classe("NSObject"), Nome, 0);
            Cocoa.class_addMethod(classe, Cocoa.Sel("userNotificationCenter:willPresentNotification:withCompletionHandler:"),
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)&AoApresentar, "v@:@@@?");
            Cocoa.class_addMethod(classe, Cocoa.Sel("userNotificationCenter:didReceiveNotificationResponse:withCompletionHandler:"),
                (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void>)&AoResponder, "v@:@@@?");
            if (Cocoa.objc_getProtocol("UNUserNotificationCenterDelegate") is var protocolo && protocolo != IntPtr.Zero) Cocoa.class_addProtocol(classe, protocolo);
            Cocoa.objc_registerClassPair(classe);
        }
        return Cocoa.Enviar(Cocoa.Enviar(classe, Cocoa.Sel("alloc")), Cocoa.Sel("init"));
    }

    static IntPtr NovoBloco(IntPtr invocar)
    {
        var sistema = NativeLibrary.Load("/usr/lib/libSystem.B.dylib");
        var descritor = (Descritor*)NativeMemory.Alloc((nuint)sizeof(Descritor));
        *descritor = new Descritor { Tamanho = (ulong)sizeof(Bloco) };
        var bloco = (Bloco*)NativeMemory.Alloc((nuint)sizeof(Bloco));
        *bloco = new Bloco { Isa = NativeLibrary.GetExport(sistema, "_NSConcreteGlobalBlock"), Flags = BlocoGlobal, Invocar = invocar, Descritor = (IntPtr)descritor };
        return (IntPtr)bloco;
    }

    [UnmanagedCallersOnly]
    static void AoPermitir(IntPtr bloco, byte concedido, IntPtr erro)
    {
    }

    [UnmanagedCallersOnly]
    static void AoApresentar(IntPtr eu, IntPtr seletor, IntPtr centro, IntPtr aviso, IntPtr concluir)
    {
        if (concluir == IntPtr.Zero) return;
        ((delegate* unmanaged<IntPtr, nuint, void>)((Bloco*)concluir)->Invocar)(concluir, MostrarSomListaEFaixa);
    }

    [UnmanagedCallersOnly]
    static void AoResponder(IntPtr eu, IntPtr seletor, IntPtr centro, IntPtr resposta, IntPtr concluir)
    {
        try
        {
            var pedido = Cocoa.Enviar(Cocoa.Enviar(resposta, Cocoa.Sel("notification")), Cocoa.Sel("request"));
            if (Cocoa.Ler(Cocoa.Enviar(pedido, Cocoa.Sel("identifier"))) is { } id)
            {
                (Action<string> AoClicar, string Argumento) clique;
                bool achou;
                lock (Cliques) achou = Cliques.Remove(id, out clique);
                if (achou) clique.AoClicar(clique.Argumento);
            }
        }
        catch (Exception ex)
        {
            Erros.Registrar(ex, "aviso do macOS");
        }
        finally
        {
            if (concluir != IntPtr.Zero) ((delegate* unmanaged<IntPtr, void>)((Bloco*)concluir)->Invocar)(concluir);
        }
    }
}
