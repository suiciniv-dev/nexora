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
        Core.Arquivos.ArquivoSeguro.SegundoPlano = true;
        ProcessoDoNucleo.Iniciar(args);
        PeloProcessador = DesenhoPeloProcessador(args);
        return BuildAvaloniaApp(PeloProcessador).StartWithClassicDesktopLifetime(args);
    }

    public static bool Rodando { get; private set; }

    public static bool PeloProcessador { get; private set; }

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
            .WithInterFont()
            .LogToTrace();
}
