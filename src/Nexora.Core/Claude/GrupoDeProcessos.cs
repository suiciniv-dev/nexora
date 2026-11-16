using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Nexora.Core.Claude;

static partial class GrupoDeProcessos
{
    const uint MatarAoFechar = 0x2000;
    const int LimitesEstendidos = 9;

    static readonly Lazy<SafeFileHandle?> Grupo = new(Criar);
    static readonly List<Process> Lembrados = [];
    static bool _ganchoNaSaida;

    public static void Adicionar(Process processo)
    {
        if (!OperatingSystem.IsWindows())
        {
            Lembrar(processo);
            return;
        }
        if (Grupo.Value is not { IsInvalid: false } grupo) return;
        AssignProcessToJobObject(grupo, processo.Handle);
    }

    static void Lembrar(Process processo)
    {
        lock (Lembrados)
        {
            Lembrados.RemoveAll(Saiu);
            Lembrados.Add(processo);
            if (_ganchoNaSaida) return;
            _ganchoNaSaida = true;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => EncerrarLembrados();
        }
    }

    static bool Saiu(Process p)
    {
        try
        {
            return p.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    public static void EncerrarLembrados()
    {
        lock (Lembrados)
        {
            foreach (var p in Lembrados)
                try
                {
                    if (!p.HasExited) p.Kill(entireProcessTree: true);
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                }
            Lembrados.Clear();
        }
    }

    internal static SafeFileHandle? Novo() => Criar();

    internal static void Adicionar(SafeFileHandle? grupo, Process processo)
    {
        if (grupo is { IsInvalid: false }) AssignProcessToJobObject(grupo, processo.Handle);
    }

    internal static void Terminar(SafeFileHandle? grupo)
    {
        if (grupo is { IsInvalid: false, IsClosed: false }) TerminateJobObject(grupo, 1);
    }

    internal static unsafe int[] Processos(SafeFileHandle? grupo)
    {
        if (grupo is not { IsInvalid: false, IsClosed: false }) return [];
        const int capacidade = 256;
        var tamanho = 8 + capacidade * IntPtr.Size;
        var memoria = stackalloc byte[tamanho];
        if (!QueryInformationJobObject(grupo, 3, (IntPtr)memoria, (uint)tamanho, IntPtr.Zero)) return [];
        var quantos = Math.Min(*(uint*)(memoria + 4), (uint)capacidade);
        var lista = new int[quantos];
        for (var i = 0; i < quantos; i++) lista[i] = (int)*(nint*)(memoria + 8 + i * IntPtr.Size);
        return lista;
    }

    static SafeFileHandle? Criar()
    {
        if (!OperatingSystem.IsWindows()) return null;
        var grupo = CreateJobObjectW(IntPtr.Zero, null);
        if (grupo.IsInvalid) return null;
        var info = new LimitesDoGrupo { Basicos = new LimitesBasicos { Flags = MatarAoFechar } };
        var tamanho = Marshal.SizeOf<LimitesDoGrupo>();
        var memoria = Marshal.AllocHGlobal(tamanho);
        try
        {
            Marshal.StructureToPtr(info, memoria, false);
            SetInformationJobObject(grupo, LimitesEstendidos, memoria, (uint)tamanho);
        }
        finally
        {
            Marshal.FreeHGlobal(memoria);
        }
        return grupo;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct LimitesBasicos
    {
        public long TempoPorProcesso;
        public long TempoPorGrupo;
        public uint Flags;
        public UIntPtr MemoriaMinima;
        public UIntPtr MemoriaMaxima;
        public uint ProcessosAtivos;
        public UIntPtr Afinidade;
        public uint Prioridade;
        public uint Escalonamento;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ContadoresIo
    {
        public ulong Leituras, Escritas, Outras, BytesLidos, BytesEscritos, OutrosBytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct LimitesDoGrupo
    {
        public LimitesBasicos Basicos;
        public ContadoresIo Io;
        public UIntPtr MemoriaPorProcesso;
        public UIntPtr MemoriaDoGrupo;
        public UIntPtr PicoPorProcesso;
        public UIntPtr PicoDoGrupo;
    }

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateJobObjectW(IntPtr atributos, string? nome);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(SafeFileHandle grupo, int classe, IntPtr info, uint tamanho);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(SafeFileHandle grupo, IntPtr processo);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TerminateJobObject(SafeFileHandle grupo, uint codigo);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryInformationJobObject(SafeFileHandle grupo, int classe, IntPtr info, uint tamanho, IntPtr devolvido);
}
