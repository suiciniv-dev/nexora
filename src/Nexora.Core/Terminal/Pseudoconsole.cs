using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Nexora.Core.Terminal;

public sealed class Pseudoconsole : IDisposable
{
    readonly IntPtr _console;
    readonly IntPtr _processo;
    readonly IntPtr _listaDeAtributos;
    readonly FileStream _entrada;
    readonly FileStream _saida;
    readonly Thread _leitura;
    readonly Thread _vigia;
    int _encerrado;
    int _consoleFechado;
    long _ultimaSaida = Environment.TickCount64;

    public event Action<string>? Saida;
    public event Action<int>? Encerrou;

    public int Pid { get; }

    Pseudoconsole(IntPtr console, IntPtr processo, int pid, IntPtr listaDeAtributos, SafeFileHandle escrita, SafeFileHandle leitura)
    {
        _console = console;
        _processo = processo;
        _listaDeAtributos = listaDeAtributos;
        Pid = pid;
        _entrada = new FileStream(escrita, FileAccess.Write, 1);
        _saida = new FileStream(leitura, FileAccess.Read, 1);
        _leitura = new Thread(Ler) { IsBackground = true, Name = "Nexora terminal" };
        _vigia = new Thread(Vigiar) { IsBackground = true, Name = "Nexora terminal (processo)" };
    }

    public static string ShellPadrao()
    {
        foreach (var pasta in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            if (File.Exists(Path.Combine(pasta, "pwsh.exe"))) return "pwsh.exe -NoLogo";
        return "powershell.exe -NoLogo";
    }

    public static Pseudoconsole Abrir(string linhaDeComando, string pasta, short colunas, short linhas, IReadOnlyDictionary<string, string>? ambiente = null)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("O terminal integrado ainda é só para Windows.");
        if (!CreatePipe(out var leituraDaEntrada, out var escritaDaEntrada, IntPtr.Zero, 0)) throw new Win32Exception();
        if (!CreatePipe(out var leituraDaSaida, out var escritaDaSaida, IntPtr.Zero, 0)) throw new Win32Exception();
        var resultado = CreatePseudoConsole(new Coordenada { X = colunas, Y = linhas }, leituraDaEntrada, escritaDaSaida, 0, out var console);
        leituraDaEntrada.Dispose();
        escritaDaSaida.Dispose();
        if (resultado != 0) throw new Win32Exception(resultado);

        var tamanho = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref tamanho);
        var lista = Marshal.AllocHGlobal(tamanho);
        if (!InitializeProcThreadAttributeList(lista, 1, 0, ref tamanho)) throw new Win32Exception();
        if (!UpdateProcThreadAttribute(lista, 0, (IntPtr)AtributoPseudoconsole, console, IntPtr.Size, IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception();

        var inicio = new InicioEstendido { Lista = lista };
        inicio.Inicio.Tamanho = Marshal.SizeOf<InicioEstendido>();
        inicio.Inicio.Opcoes = UsarAlcasProprias;
        var comando = new StringBuilder(linhaDeComando);
        var bloco = ambiente is { Count: > 0 } ? Marshal.StringToHGlobalUni(BlocoDeAmbiente(ambiente)) : IntPtr.Zero;
        var criou = CreateProcessW(null, comando, IntPtr.Zero, IntPtr.Zero, false, InicioEstendidoPresente | CriarUnicode, bloco, pasta, ref inicio, out var info);
        var erroAoCriar = Marshal.GetLastWin32Error();
        if (bloco != IntPtr.Zero)
        {
            for (var i = 0; Marshal.ReadInt16(bloco, i) != 0 || Marshal.ReadInt16(bloco, i + 2) != 0; i += 2) Marshal.WriteInt16(bloco, i, 0);
            Marshal.FreeHGlobal(bloco);
        }
        if (!criou)
        {
            var erro = erroAoCriar;
            ClosePseudoConsole(console);
            DeleteProcThreadAttributeList(lista);
            Marshal.FreeHGlobal(lista);
            throw new Win32Exception(erro);
        }
        CloseHandle(info.Linha);
        var terminal = new Pseudoconsole(console, info.Processo, info.Pid, lista, escritaDaEntrada, leituraDaSaida);
        terminal._leitura.Start();
        terminal._vigia.Start();
        return terminal;
    }

    public static string BlocoDeAmbiente(IReadOnlyDictionary<string, string> extras)
    {
        var todas = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
            if (e.Key is string k && k.Length > 0 && !k.StartsWith('=')) todas[k] = e.Value as string ?? "";
        foreach (var (k, v) in extras) todas[k] = v;
        var texto = new StringBuilder();
        foreach (var (k, v) in todas) texto.Append(k).Append('=').Append(v).Append('\0');
        return texto.Append('\0').ToString();
    }

    public void Escrever(string texto)
    {
        if (_encerrado != 0) return;
        try
        {
            var bytes = Encoding.UTF8.GetBytes(texto);
            _entrada.Write(bytes);
            _entrada.Flush();
        }
        catch (IOException)
        {
        }
    }

    public void Redimensionar(short colunas, short linhas)
    {
        if (_encerrado == 0 && colunas > 0 && linhas > 0) ResizePseudoConsole(_console, new Coordenada { X = colunas, Y = linhas });
    }

    void Ler()
    {
        var decodificador = Encoding.UTF8.GetDecoder();
        var bytes = new byte[16 * 1024];
        var caracteres = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
        try
        {
            int lidos;
            while ((lidos = _saida.Read(bytes, 0, bytes.Length)) > 0)
            {
                Interlocked.Exchange(ref _ultimaSaida, Environment.TickCount64);
                var n = decodificador.GetChars(bytes, 0, lidos, caracteres, 0);
                if (n > 0) Saida?.Invoke(new string(caracteres, 0, n));
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
        }
    }

    void Vigiar()
    {
        WaitForSingleObject(_processo, uint.MaxValue);
        GetExitCodeProcess(_processo, out var codigo);
        var limite = Environment.TickCount64 + 3000;
        if (Volatile.Read(ref _encerrado) == 0) Thread.Sleep(100);
        while (Volatile.Read(ref _encerrado) == 0 && Environment.TickCount64 - Interlocked.Read(ref _ultimaSaida) < 300 && Environment.TickCount64 < limite) Thread.Sleep(50);
        FecharConsole();
        _leitura.Join(TimeSpan.FromSeconds(2));
        if (Interlocked.Exchange(ref _encerrado, 1) == 0) Encerrou?.Invoke((int)codigo);
    }

    void FecharConsole()
    {
        if (Interlocked.Exchange(ref _consoleFechado, 1) == 0) ClosePseudoConsole(_console);
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _encerrado, 1);
        TerminateProcess(_processo, 0);
        FecharConsole();
        _vigia.Join(TimeSpan.FromSeconds(1));
        _entrada.Dispose();
        _saida.Dispose();
        CloseHandle(_processo);
        DeleteProcThreadAttributeList(_listaDeAtributos);
        Marshal.FreeHGlobal(_listaDeAtributos);
    }

    const uint InicioEstendidoPresente = 0x00080000;
    const uint CriarUnicode = 0x00000400;
    const int AtributoPseudoconsole = 0x00020016;
    const int UsarAlcasProprias = 0x00000100;

    [StructLayout(LayoutKind.Sequential)]
    struct Coordenada
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct Inicio
    {
        public int Tamanho;
        public string? Reservado;
        public string? Area;
        public string? Titulo;
        public int X, Y, Largura, Altura, Colunas, Linhas, Cor, Opcoes;
        public short Mostrar;
        public short Reservado2;
        public IntPtr Reservado3, Entrada, Saida, Erro;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct InicioEstendido
    {
        public Inicio Inicio;
        public IntPtr Lista;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct InfoDoProcesso
    {
        public IntPtr Processo;
        public IntPtr Linha;
        public int Pid;
        public int Tid;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CreatePipe(out SafeFileHandle leitura, out SafeFileHandle escrita, IntPtr atributos, int tamanho);

    [DllImport("kernel32.dll")]
    static extern int CreatePseudoConsole(Coordenada tamanho, SafeFileHandle entrada, SafeFileHandle saida, uint opcoes, out IntPtr console);

    [DllImport("kernel32.dll")]
    static extern int ResizePseudoConsole(IntPtr console, Coordenada tamanho);

    [DllImport("kernel32.dll")]
    static extern void ClosePseudoConsole(IntPtr console);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool InitializeProcThreadAttributeList(IntPtr lista, int quantidade, int opcoes, ref IntPtr tamanho);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool UpdateProcThreadAttribute(IntPtr lista, uint opcoes, IntPtr atributo, IntPtr valor, IntPtr tamanho, IntPtr anterior, IntPtr retorno);

    [DllImport("kernel32.dll")]
    static extern void DeleteProcThreadAttributeList(IntPtr lista);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CreateProcessW(string? aplicacao, StringBuilder comando, IntPtr atributosDoProcesso, IntPtr atributosDaLinha, bool herdar,
        uint opcoes, IntPtr ambiente, string? pasta, ref InicioEstendido inicio, out InfoDoProcesso info);

    [DllImport("kernel32.dll")]
    static extern uint WaitForSingleObject(IntPtr alca, uint milissegundos);

    [DllImport("kernel32.dll")]
    static extern bool GetExitCodeProcess(IntPtr processo, out uint codigo);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool TerminateProcess(IntPtr processo, uint codigo);

    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr alca);
}
