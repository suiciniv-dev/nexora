using System.Diagnostics;
using Nexora.Core.Claude;
using Nexora.Core.Servidor;

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

    static async Task IniciarAsync()
    {
        try
        {
            if (Environment.ProcessPath is not { } executavel) return;
            var cano = $"nexora-nucleo-{Environment.ProcessId}-{Guid.NewGuid():N}";
            var info = new ProcessStartInfo(executavel) { UseShellExecute = false, CreateNoWindow = true };
            if (Path.GetFileNameWithoutExtension(executavel).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                info.ArgumentList.Add(typeof(ProcessoDoNucleo).Assembly.Location);
            info.ArgumentList.Add(ServidorDoNucleo.Argumento);
            info.ArgumentList.Add(cano);
            using var processo = Process.Start(info);
            if (processo is null) return;
            GrupoDeProcessos.Adicionar(processo);
            Pid = processo.Id;
            if (await NucleoRemoto.ConectarAsync(cano, EsperaPeloServidor).ConfigureAwait(false) is not { } remoto) return;
            remoto.Caiu += () =>
            {
                App.Nucleo = NucleoLocal.Instancia;
                Core.Git.ComandoGit.Nucleo = null;
            };
            App.Nucleo = remoto;
            Core.Git.ComandoGit.Nucleo = remoto;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
        }
    }
}
