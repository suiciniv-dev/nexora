using System.Runtime.CompilerServices;

namespace Nexora.Testes;

static class DadosIsolados
{
    public static string Pasta { get; } = Path.Combine(Path.GetTempPath(), "nexora-testes-" + Environment.ProcessId);

    [ModuleInitializer]
    internal static void Isolar()
    {
        Directory.CreateDirectory(Pasta);
        Core.DadosDoNexora.Pasta = Pasta;
    }
}
