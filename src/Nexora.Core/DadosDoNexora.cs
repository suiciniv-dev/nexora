namespace Nexora.Core;

public static class DadosDoNexora
{
    public static string Pasta { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nexora");
}
