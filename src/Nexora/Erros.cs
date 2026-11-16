using System.Text;
using Avalonia.Threading;
using Nexora.Core.Texto;

namespace Nexora;

public static class Erros
{
    const long Limite = 512 * 1024;
    static readonly object Trava = new();
    static DateTime _ultimoAviso = DateTime.MinValue;

    public static string Arquivo { get; set; } =
        Path.Combine(global::Nexora.Core.DadosDoNexora.Pasta, "erros.log");

    public static Action<string, string>? Avisar { get; set; } = (titulo, texto) => Avisos.Notificar(titulo, texto);

    public static void Ligar()
    {
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            e.Handled = true;
            Tratar(e.Exception, "interface");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            Registrar(e.Exception, "tarefa");
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) Registrar(ex, "fatal");
        };
    }

    public static void Tratar(Exception erro, string onde)
    {
        Registrar(erro, onde);
        if (DateTime.UtcNow - _ultimoAviso < TimeSpan.FromSeconds(30)) return;
        _ultimoAviso = DateTime.UtcNow;
        try
        {
            Avisar?.Invoke(Idioma.T("Algo deu errado"), Idioma.T("O Nexora continuou aberto. O erro ficou anotado em erros.log, na pasta de dados."));
        }
        catch (Exception)
        {
        }
    }

    public static void Registrar(Exception erro, string onde)
    {
        try
        {
            lock (Trava)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Arquivo)!);
                var info = new FileInfo(Arquivo);
                if (info.Exists && info.Length > Limite)
                {
                    var resto = File.ReadAllText(Arquivo);
                    File.WriteAllText(Arquivo, resto[(resto.Length / 2)..], new UTF8Encoding(false));
                }
                var versao = typeof(Erros).Assembly.GetName().Version?.ToString(3) ?? "?";
                File.AppendAllText(Arquivo, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {versao}  {onde}\n{erro}\n\n", new UTF8Encoding(false));
            }
        }
        catch (Exception)
        {
        }
    }
}
