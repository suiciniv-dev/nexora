using System.Runtime.CompilerServices;

namespace Nexora.Testes;

static class DadosIsolados
{
    public static string ClaudeReal { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    public static string Pasta { get; } = Path.Combine(Path.GetTempPath(), "nexora-testes-" + Environment.ProcessId);

    [ModuleInitializer]
    internal static void Isolar()
    {
        Directory.CreateDirectory(Pasta);
        Core.DadosDoNexora.Pasta = Pasta;
        Directory.CreateDirectory(Path.Combine(Pasta, "claude-projects"));
        Core.Historico.ConversasSalvas.PastaPadrao = Path.Combine(Pasta, "claude-projects");
        Directory.CreateDirectory(Path.Combine(Pasta, "claude-sessions"));
        Core.Historico.SessoesAtivas.PastaPadrao = Path.Combine(Pasta, "claude-sessions");
    }
}
