using System.ComponentModel;
using System.Diagnostics;
using Nexora.Core.Claude;
using Nexora.Core.Servidor;
using Nexora.Servicos;

namespace Nexora.Aplicativo;

public static class ProcessoDoNucleo
{
    static readonly TimeSpan EsperaPeloApp = TimeSpan.FromSeconds(30);
    static readonly TimeSpan EsperaPeloServidor = TimeSpan.FromSeconds(15);

    public static int? Pid { get; private set; }

    public static int Servir(string cano) =>
        ServidorDoNucleo.RodarAsync(cano, NucleoLocal.Instancia, EsperaPeloApp).GetAwaiter().GetResult();

    public static void Iniciar(string[] args)
    {
        if (args.Contains("--demo") || args.Contains("--isolada") || App.Config.NucleoNoProprioProcesso) return;
        _ = Task.Run(IniciarAsync);
    }

    public static IEnumerable<(string Programa, bool ComDll)> Formas(string? processo, bool windows, bool dependeDoDotnet, string? dotnet)
    {
        if (processo is null) yield break;
        if (Path.GetFileNameWithoutExtension(processo).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            yield return (processo, true);
            yield break;
        }
        if (windows && dependeDoDotnet && dotnet is not null) yield return (dotnet, true);
        yield return (processo, false);
    }

    static async Task IniciarAsync()
    {
        var cano = $"nexora-nucleo-{Environment.ProcessId}-{Guid.NewGuid():N}";
        var formas = Formas(Environment.ProcessPath, OperatingSystem.IsWindows(), Trampolim.ExecutavelDependeDoDotnet, Trampolim.DotnetInstalado());
        if (Abrir(formas, cano) is not { } processo) return;
        GrupoDeProcessos.Adicionar(processo);
        Pid = processo.Id;
        if (await NucleoRemoto.ConectarAsync(cano, EsperaPeloServidor).ConfigureAwait(false) is not { } remoto)
        {
            Erros.Registrar(new TimeoutException($"O núcleo não respondeu em {EsperaPeloServidor.TotalSeconds:0} s."), "núcleo");
            return;
        }
        remoto.Caiu += () =>
        {
            App.Nucleo = NucleoLocal.Instancia;
            Core.Git.ComandoGit.Nucleo = null;
        };
        App.Nucleo = remoto;
        Core.Git.ComandoGit.Nucleo = remoto;
    }

    static Process? Abrir(IEnumerable<(string Programa, bool ComDll)> formas, string cano)
    {
        Exception? falha = null;
        foreach (var (programa, comDll) in formas)
        {
            var info = new ProcessStartInfo(programa) { UseShellExecute = false, CreateNoWindow = true };
            if (comDll) info.ArgumentList.Add(typeof(ProcessoDoNucleo).Assembly.Location);
            info.ArgumentList.Add(ServidorDoNucleo.Argumento);
            info.ArgumentList.Add(cano);
            try
            {
                if (Process.Start(info) is { } processo) return processo;
            }
            catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException)
            {
                falha = ex;
            }
        }
        if (falha is not null) Erros.Registrar(falha, "núcleo");
        return null;
    }
}
