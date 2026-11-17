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
        string? Opcao(string nome) => Array.IndexOf(resto, nome) is var i and >= 0 && i + 1 < resto.Length ? resto[i + 1] : null;
        var ocioso = double.TryParse(Opcao("--ocioso"), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? TimeSpan.FromSeconds(s) : Ocioso;
        Core.Texto.Idioma.Registrar(Traducao.Ingles.Pares);
        Core.Texto.Idioma.Definir(Opcao("--idioma"));
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

    public static IEnumerable<(string Programa, string? Dll)> Formas(string? processo, string pasta, bool windows, bool dependeDoDotnet, string? dotnet)
    {
        if (processo is null) yield break;
        var dll = Path.Combine(pasta, "Nexora.dll");
        if (Path.GetFileNameWithoutExtension(processo).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            yield return (processo, dll);
            yield break;
        }
        if (windows && dependeDoDotnet && dotnet is not null) yield return (dotnet, dll);
        yield return (Path.Combine(pasta, Path.GetFileName(processo)), null);
    }

    static string PastaDoServidor()
    {
        if (!OperatingSystem.IsWindows()) return AppContext.BaseDirectory;
        try
        {
            var pasta = CopiaDoNucleo.Preparar(AppContext.BaseDirectory, CopiaDoNucleo.Raiz, Versao);
            _ = Task.Run(() => CopiaDoNucleo.Limpar(CopiaDoNucleo.Raiz, pasta));
            return pasta;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Erros.Registrar(ex, "núcleo");
            return AppContext.BaseDirectory;
        }
    }

    static async Task IniciarAsync()
    {
        var cano = ServidorDoNucleo.NomeDoCano(Environment.UserName, Core.DadosDoNexora.Pasta, Versao);
        var remoto = await NucleoRemoto.ConectarAsync(cano, JaAberto).ConfigureAwait(false);
        if (remoto is null)
        {
            var pasta = PastaDoServidor();
            var formas = Formas(Environment.ProcessPath, pasta, OperatingSystem.IsWindows(), Trampolim.ExecutavelDependeDoDotnet, Trampolim.DotnetInstalado());
            if (!Abrir(formas, pasta, cano)) return;
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
            Core.Orquestra.ServidorMcp.Ponte = null;
        };
        App.Nucleo = remoto;
        Core.Git.ComandoGit.Nucleo = remoto;
        Core.Orquestra.ServidorMcp.Ponte = new PonteRemota(remoto);
    }

    static bool Abrir(IEnumerable<(string Programa, string? Dll)> formas, string pasta, string cano)
    {
        Exception? falha = null;
        foreach (var (programa, dll) in formas)
        {
            var info = new ProcessStartInfo(programa) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = pasta };
            if (dll is not null) info.ArgumentList.Add(dll);
            info.ArgumentList.Add(ServidorDoNucleo.Argumento);
            info.ArgumentList.Add(cano);
            info.ArgumentList.Add("--idioma");
            info.ArgumentList.Add(Core.Texto.Idioma.Codigo);
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
