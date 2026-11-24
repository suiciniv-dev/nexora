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
        if (args is [Core.Servidor.ServidorDoNucleo.Argumento, var cano, .. var resto]) return ProcessoDoNucleo.Servir(cano, resto);
        if (App.Config.AbrirPeloExe && Trampolim.Pular(args)) return 0;
        IdentidadeJanela.DoProcesso();
        Instalacao.PathDoMac();
        Sistema.PrepararMac();
        Rodando = true;
        System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;
        Core.Arquivos.ArquivoSeguro.SegundoPlano = true;
        ProcessoDoNucleo.Iniciar(args);
        PeloProcessador = DesenhoPeloProcessador(args);
        DesenhoDoMac = ModosDoMac(args);
        return BuildAvaloniaApp(PeloProcessador).StartWithClassicDesktopLifetime(args);
    }

    public static bool Rodando { get; private set; }

    public static bool PeloProcessador { get; private set; }

    static AvaloniaNativeRenderingMode[] DesenhoDoMac = ModosDoMac([]);

    static AvaloniaNativeRenderingMode[] ModosDoMac(string[] args)
    {
        var i = Array.IndexOf(args, "--desenho-mac");
        return (i >= 0 && i + 1 < args.Length ? args[i + 1] : null) switch
        {
            "software" => [AvaloniaNativeRenderingMode.Software],
            "opengl" => [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software],
            _ => [AvaloniaNativeRenderingMode.Metal, AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software],
        };
    }

    public static bool MudaODesenho(string modo) => false;

    static bool DesenhoPeloProcessador(string[] args) => args.Contains("--software") || App.Config.DesenhoPeloProcessador;

    public static AppBuilder BuildAvaloniaApp(bool peloProcessador = false) =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
                OverlayPopups = true,
                RenderingMode = peloProcessador ? [Win32RenderingMode.Software] : [Win32RenderingMode.AngleEgl, Win32RenderingMode.Software],
            })
            .With(new AvaloniaNativePlatformOptions { RenderingMode = peloProcessador ? [AvaloniaNativeRenderingMode.Software] : DesenhoDoMac })
            .WithInterFont()
            .LogToTrace();
}
