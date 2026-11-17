using System.Diagnostics;
using System.Text;
using Nexora.Core.Texto;

namespace Nexora.Core.Git;

public sealed record ResultadoGit(int Codigo, string Saida, string Erro)
{
    public bool Ok => Codigo == 0;
    public string Mensagem => (string.IsNullOrWhiteSpace(Erro) ? Saida : Erro).Trim();
}

public static class ComandoGit
{
    public static Task<ResultadoGit> ExecutarAsync(string pasta, IEnumerable<string> args, TimeSpan? limite = null) =>
        RodarAsync("git", pasta, ["-c", "core.quotepath=off", .. args], limite);

    public static Task<ResultadoGit> ExecutarComAmbienteAsync(string pasta, IEnumerable<string> args, IReadOnlyDictionary<string, string>? ambiente, string? entrada = null, TimeSpan? limite = null) =>
        RodarAsync("git", pasta, ["-c", "core.quotepath=off", .. args], limite, ambiente, entrada);

    public static Task<ResultadoGit> ExecutarGhAsync(string pasta, IEnumerable<string> args, TimeSpan? limite = null, IReadOnlyDictionary<string, string>? ambiente = null) =>
        RodarAsync("gh", pasta, args, limite, ambiente);

    public static Servidor.INucleo? Nucleo { get; set; }

    static Task<ResultadoGit> RodarAsync(string programa, string pasta, IEnumerable<string> args, TimeSpan? limite, IReadOnlyDictionary<string, string>? ambiente = null, string? entrada = null)
    {
        var pedido = new PedidoDeComando(programa, pasta, [.. args], limite, ambiente, entrada);
        return Nucleo is { } nucleo ? nucleo.RodarComandoAsync(pedido) : RodarAquiAsync(pedido);
    }

    public static async Task<ResultadoGit> RodarAquiAsync(PedidoDeComando pedido)
    {
        var (programa, pasta, args, limite, ambiente, entrada) = pedido;
        if (pasta.Length > 0 && !Directory.Exists(pasta)) return new ResultadoGit(-1, "", Idioma.F("A pasta {0} não existe neste computador.", pasta));
        var info = new ProcessStartInfo(programa)
        {
            WorkingDirectory = pasta,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = entrada is not null,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        info.Environment["GH_PROMPT_DISABLED"] = "1";
        info.Environment["NO_COLOR"] = "1";
        if (ambiente is not null)
            foreach (var (nome, valor) in ambiente) info.Environment[nome] = valor;
        try
        {
            using var processo = await Task.Run(() => Process.Start(info)).ConfigureAwait(false) ?? throw new InvalidOperationException(Idioma.F("O {0} não abriu.", programa));
            var saida = processo.StandardOutput.ReadToEndAsync();
            var erro = processo.StandardError.ReadToEndAsync();
            if (entrada is not null)
            {
                var bytes = new UTF8Encoding(false).GetBytes(entrada);
                await processo.StandardInput.BaseStream.WriteAsync(bytes);
                processo.StandardInput.Close();
            }
            using var tempo = new CancellationTokenSource(limite ?? TimeSpan.FromSeconds(30));
            try
            {
                await processo.WaitForExitAsync(tempo.Token);
            }
            catch (OperationCanceledException)
            {
                processo.Kill(entireProcessTree: true);
                return new ResultadoGit(-1, "", Idioma.F("O {0} demorou demais e foi interrompido.", programa));
            }
            return new ResultadoGit(processo.ExitCode, await saida, await erro);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            return new ResultadoGit(-1, "", Idioma.F("O {0} não foi encontrado neste computador.", programa));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return new ResultadoGit(-1, "", ex.Message);
        }
    }

    public static Task<ResultadoGit> ExecutarAsync(string pasta, params string[] args) => ExecutarAsync(pasta, args, null);
}
