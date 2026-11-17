using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Nexora.Core.Claude;
using Nexora.Core.Servidor;
using Nexora.Servicos;

namespace Nexora.Aplicativo;

public static class ProcessoDoNucleo
{
    static readonly TimeSpan Ocioso = TimeSpan.FromSeconds(30);
    static readonly TimeSpan JaAberto = TimeSpan.FromMilliseconds(400);
    static readonly TimeSpan EsperaPeloServidor = TimeSpan.FromSeconds(15);

    static readonly TaskCompletionSource<NucleoRemoto?> _pronto = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static Guid Versao => typeof(ProcessoDoNucleo).Assembly.ManifestModule.ModuleVersionId;

    public static Task<NucleoRemoto?> Pronto => _pronto.Task;

    public static bool Iniciado { get; private set; }

    public static int Servir(string cano, string[] resto)
    {
        var ocioso = resto is [var segundos, ..] && double.TryParse(segundos, NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? TimeSpan.FromSeconds(s) : Ocioso;
        var sessoes = new SessoesDoServidor(FabricaDeSessoes.Criar);
        try
        {
            return ServidorDoNucleo.RodarAsync(cano, NucleoLocal.Instancia, ocioso, sessoes: sessoes).GetAwaiter().GetResult();
        }
        finally
        {
            sessoes.EncerrarTodasAsync().GetAwaiter().GetResult();
        }
    }

    public static void Iniciar(string[] args)
    {
        Iniciado = true;
        if (args.Contains("--demo") || args.Contains("--isolada") || App.Config.NucleoNoProprioProcesso)
        {
            _pronto.TrySetResult(null);
            return;
        }
        _ = Task.Run(async () =>
        {
            try
            {
                await IniciarAsync().ConfigureAwait(false);
            }
            finally
            {
                _pronto.TrySetResult(App.Nucleo as NucleoRemoto);
            }
        });
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
        var cano = ServidorDoNucleo.NomeDoCano(Environment.UserName, Core.DadosDoNexora.Pasta, Versao);
        var remoto = await NucleoRemoto.ConectarAsync(cano, JaAberto).ConfigureAwait(false);
        if (remoto is null)
        {
            var formas = Formas(Environment.ProcessPath, OperatingSystem.IsWindows(), Trampolim.ExecutavelDependeDoDotnet, Trampolim.DotnetInstalado());
            if (!Abrir(formas, cano)) return;
            remoto = await NucleoRemoto.ConectarAsync(cano, EsperaPeloServidor).ConfigureAwait(false);
        }
        if (remoto is null)
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

    static bool Abrir(IEnumerable<(string Programa, bool ComDll)> formas, string cano)
    {
        Exception? falha = null;
        foreach (var (programa, comDll) in formas)
        {
            var info = new ProcessStartInfo(programa) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = AppContext.BaseDirectory };
            if (comDll) info.ArgumentList.Add(typeof(ProcessoDoNucleo).Assembly.Location);
            info.ArgumentList.Add(ServidorDoNucleo.Argumento);
            info.ArgumentList.Add(cano);
            try
            {
                using var processo = Process.Start(info);
                if (processo is not null) return true;
            }
            catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException)
            {
                falha = ex;
            }
        }
        if (falha is not null) Erros.Registrar(falha, "núcleo");
        return false;
    }
}
