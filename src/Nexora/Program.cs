using Avalonia;

namespace Nexora;

static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable(Core.Nuvem.TransporteSsh.VariavelDaSenha) is { Length: > 0 } senha && Environment.GetEnvironmentVariable("SSH_ASKPASS_REQUIRE") == "force")
        {
            Console.Out.Write(senha + "\n");
            Console.Out.Flush();
            return 0;
        }
        if (args is ["--cli", ..]) return Cli.Rodar(args[1..]);
        if (App.Config.AbrirPeloExe && Trampolim.Pular(args)) return 0;
        IdentidadeJanela.DoProcesso();
        Instalacao.PathDoMac();
        Sistema.PrepararMac();
        Rodando = true;
        PeloProcessador = DesenhoPeloProcessador(args);
        return BuildAvaloniaApp(PeloProcessador).StartWithClassicDesktopLifetime(args);
    }

    public static bool Rodando { get; private set; }

    public static bool PeloProcessador { get; private set; }

    public static bool MudaODesenho(string modo) => OperatingSystem.IsWindows() && (modo == "solido") != PeloProcessador;

    static bool DesenhoPeloProcessador(string[] args)
    {
        var i = Array.IndexOf(args, "--fundo");
        return (i >= 0 && i + 1 < args.Length ? args[i + 1] : App.Config.ModoFundo) == "solido";
    }

    public static AppBuilder BuildAvaloniaApp(bool peloProcessador = false) =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
                OverlayPopups = true,
                RenderingMode = peloProcessador ? [Win32RenderingMode.Software] : [Win32RenderingMode.AngleEgl, Win32RenderingMode.Software],
            })
            .WithInterFont()
            .LogToTrace();
}
