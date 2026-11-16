using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Nexora.Core.Recursos;

public sealed record ServidorDeBuild(int Pid, string Tipo, long Memoria);

public static class ServidoresDeBuild
{
    public static string? Classificar(string nomeDoProcesso, string? linhaDeComando)
    {
        if (nomeDoProcesso.Equals("VBCSCompiler", StringComparison.OrdinalIgnoreCase)) return "Compilador do C#";
        if (linhaDeComando is not { Length: > 0 } linha) return null;
        if (linha.Contains("MSBuild.dll", StringComparison.OrdinalIgnoreCase) && linha.Contains("/nodemode:", StringComparison.OrdinalIgnoreCase)) return "Nó do MSBuild";
        if (linha.Contains("VBCSCompiler.dll", StringComparison.OrdinalIgnoreCase)) return "Compilador do C#";
        if (linha.Contains("rzc.dll", StringComparison.OrdinalIgnoreCase) && linha.Contains("server", StringComparison.OrdinalIgnoreCase)) return "Servidor do Razor";
        return null;
    }

    public static IReadOnlyList<ServidorDeBuild> Listar()
    {
        if (!OperatingSystem.IsWindows()) return [];
        var lista = new List<ServidorDeBuild>();
        foreach (var processo in Process.GetProcesses())
            using (processo)
            {
                try
                {
                    var nome = processo.ProcessName;
                    if (!nome.Equals("dotnet", StringComparison.OrdinalIgnoreCase) && !nome.Equals("VBCSCompiler", StringComparison.OrdinalIgnoreCase)) continue;
                    if (Classificar(nome, nome.Equals("dotnet", StringComparison.OrdinalIgnoreCase) ? LinhaDeComando(processo.Id) : null) is { } tipo)
                        lista.Add(new ServidorDeBuild(processo.Id, tipo, processo.WorkingSet64));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                }
            }
        return lista;
    }

    public static async Task<long> DesligarAsync(CancellationToken cancelar = default)
    {
        var antes = Listar();
        if (antes.Count == 0) return 0;
        try
        {
            var inicio = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argumento in new[] { "build-server", "shutdown" }) inicio.ArgumentList.Add(argumento);
            using var processo = Process.Start(inicio);
            if (processo is not null)
            {
                var saida = processo.StandardOutput.ReadToEndAsync(cancelar);
                var erros = processo.StandardError.ReadToEndAsync(cancelar);
                await processo.WaitForExitAsync(cancelar).WaitAsync(TimeSpan.FromSeconds(60), cancelar);
                await Task.WhenAll(saida, erros);
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or TimeoutException)
        {
        }
        var depois = Listar().Select(s => s.Pid).ToHashSet();
        return antes.Where(s => !depois.Contains(s.Pid)).Sum(s => s.Memoria);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct TextoUnicode
    {
        public ushort Tamanho;
        public ushort Capacidade;
        public IntPtr Buffer;
    }

    [DllImport("ntdll.dll")]
    static extern int NtQueryInformationProcess(IntPtr processo, int classe, IntPtr dados, int tamanho, out int devolvido);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint acesso, bool herdar, int pid);

    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr alca);

    const int ProcessCommandLineInformation = 60;
    const uint ConsultaLimitada = 0x1000;

    internal static string? LinhaDeComando(int pid)
    {
        var alca = OpenProcess(ConsultaLimitada, false, pid);
        if (alca == IntPtr.Zero) return null;
        try
        {
            NtQueryInformationProcess(alca, ProcessCommandLineInformation, IntPtr.Zero, 0, out var precisa);
            if (precisa <= 0) return null;
            var memoria = Marshal.AllocHGlobal(precisa);
            try
            {
                if (NtQueryInformationProcess(alca, ProcessCommandLineInformation, memoria, precisa, out _) != 0) return null;
                var texto = Marshal.PtrToStructure<TextoUnicode>(memoria);
                return texto.Buffer == IntPtr.Zero ? null : Marshal.PtrToStringUni(texto.Buffer, texto.Tamanho / 2);
            }
            finally
            {
                Marshal.FreeHGlobal(memoria);
            }
        }
        finally
        {
            CloseHandle(alca);
        }
    }
}
