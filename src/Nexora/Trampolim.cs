using System.ComponentModel;
using System.Diagnostics;

namespace Nexora;

static class Trampolim
{
    static readonly TimeSpan Carencia = TimeSpan.FromSeconds(4);

    public static string Executavel => Path.Combine(AppContext.BaseDirectory, "Nexora.exe");

    public static bool RodandoPeloDotnet =>
        Environment.ProcessPath is { } processo && Path.GetFileNameWithoutExtension(processo).Equals("dotnet", StringComparison.OrdinalIgnoreCase);

    public static bool ExecutavelDependeDoDotnet =>
        File.Exists(Executavel) && !File.Exists(Path.Combine(AppContext.BaseDirectory, "hostfxr.dll"));

    public static string? DotnetInstalado()
    {
        var raiz = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        foreach (var pasta in new[] { raiz, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet") })
            if (pasta is { Length: > 0 } && File.Exists(Path.Combine(pasta, "dotnet.exe"))) return Path.Combine(pasta, "dotnet.exe");
        return null;
    }

    public static bool Pular(string[] args)
    {
        if (!OperatingSystem.IsWindows() || !RodandoPeloDotnet || !File.Exists(Executavel)) return false;
        if (args.Contains("--sem-exe") || args.Contains("--isolada") || args.Contains("--demo")) return false;
        try
        {
            var inicio = new ProcessStartInfo(Executavel) { UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory };
            foreach (var argumento in args) inicio.ArgumentList.Add(argumento);
            using var filho = Process.Start(inicio);
            if (filho is null) return false;
            if (!filho.WaitForExit(Carencia)) return true;
            return filho.ExitCode == 0;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            return false;
        }
    }
}
