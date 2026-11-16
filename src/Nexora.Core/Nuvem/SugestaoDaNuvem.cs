using System.Globalization;
using System.Runtime.InteropServices;

namespace Nexora.Core.Nuvem;

public static partial class SugestaoDaNuvem
{
    public const double Limite = 0.85;

    public static Func<double?> Medir { get; set; } = MemoriaEmUso;

    public static string? Texto(double? memoriaEmUso, bool projetoPode, string? maquina) =>
        memoriaEmUso is { } uso && uso >= Limite && projetoPode && maquina is { Length: > 0 }
            ? Nexora.Core.Texto.Idioma.F("Este PC está com {0}% da memória em uso. Esta conversa pode rodar em {1} e deixar o PC livre.", Math.Round(uso * 100).ToString(CultureInfo.CurrentCulture), maquina)
            : null;

    public static double? MemoriaEmUso()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var estado = new EstadoDaMemoria { Tamanho = (uint)Marshal.SizeOf<EstadoDaMemoria>() };
                return GlobalMemoryStatusEx(ref estado) && estado.Total > 0 ? 1 - (double)estado.Livre / estado.Total : null;
            }
            if (OperatingSystem.IsLinux())
            {
                long total = 0, livre = 0;
                foreach (var linha in File.ReadLines("/proc/meminfo"))
                {
                    var partes = linha.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (partes.Length < 2 || !long.TryParse(partes[1], out var valor)) continue;
                    if (partes[0] == "MemTotal:") total = valor;
                    else if (partes[0] == "MemAvailable:") livre = valor;
                }
                return total > 0 ? 1 - (double)livre / total : null;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EntryPointNotFoundException or DllNotFoundException)
        {
        }
        return null;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct EstadoDaMemoria
    {
        public uint Tamanho;
        public uint Carga;
        public ulong Total;
        public ulong Livre;
        public ulong TotalPaginacao;
        public ulong LivrePaginacao;
        public ulong TotalVirtual;
        public ulong LivreVirtual;
        public ulong LivreEstendida;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref EstadoDaMemoria estado);
}
