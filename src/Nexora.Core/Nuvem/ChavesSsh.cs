using System.Diagnostics;

namespace Nexora.Core.Nuvem;

public static class ChavesSsh
{
    public static string GeradorPadrao
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                var doSistema = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh-keygen.exe");
                return File.Exists(doSistema) ? doSistema : "ssh-keygen.exe";
            }
            return File.Exists("/usr/bin/ssh-keygen") ? "/usr/bin/ssh-keygen" : "ssh-keygen";
        }
    }

    public static string? LinhaPublica(string chave)
    {
        var publica = chave + ".pub";
        return File.Exists(publica) ? File.ReadAllText(publica).Trim() : null;
    }

    public static async Task<string> GarantirAsync(string chave, string? gerador = null, CancellationToken cancelamento = default)
    {
        if (File.Exists(chave) && LinhaPublica(chave) is { } existente) return existente;
        var pasta = Path.GetDirectoryName(chave)!;
        Directory.CreateDirectory(pasta);
        if (File.Exists(chave)) File.Delete(chave);
        if (File.Exists(chave + ".pub")) File.Delete(chave + ".pub");
        Proteger(pasta, pasta: true);
        var info = new ProcessStartInfo(gerador ?? GeradorPadrao)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };
        foreach (var arg in new[] { "-q", "-t", "ed25519", "-N", "", "-C", "nexora@" + Environment.MachineName.ToLowerInvariant(), "-f", chave })
            info.ArgumentList.Add(arg);
        using var processo = Process.Start(info) ?? throw new InvalidOperationException(Texto.Idioma.T("O ssh-keygen não abriu."));
        processo.StandardInput.Close();
        var erros = processo.StandardError.ReadToEndAsync(cancelamento);
        await processo.StandardOutput.ReadToEndAsync(cancelamento).ConfigureAwait(false);
        await processo.WaitForExitAsync(cancelamento).ConfigureAwait(false);
        if (processo.ExitCode != 0 || !File.Exists(chave))
            throw new InvalidOperationException(Texto.Idioma.F("Não deu para criar a chave: {0}", (await erros.ConfigureAwait(false)).Trim()));
        Proteger(chave, pasta: false);
        return LinhaPublica(chave) ?? throw new InvalidOperationException(Texto.Idioma.T("A chave pública não foi criada."));
    }

    static void Proteger(string caminho, bool pasta)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(caminho, pasta
                ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                : UnixFileMode.UserRead | UnixFileMode.UserWrite);
            return;
        }
        try
        {
            var usuario = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
            if (pasta)
            {
                var info = new DirectoryInfo(caminho);
                var regras = new System.Security.AccessControl.DirectorySecurity();
                regras.SetAccessRuleProtection(true, false);
                regras.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(usuario,
                    System.Security.AccessControl.FileSystemRights.FullControl,
                    System.Security.AccessControl.InheritanceFlags.ContainerInherit | System.Security.AccessControl.InheritanceFlags.ObjectInherit,
                    System.Security.AccessControl.PropagationFlags.None,
                    System.Security.AccessControl.AccessControlType.Allow));
                info.SetAccessControl(regras);
            }
            else
            {
                var info = new FileInfo(caminho);
                var regras = new System.Security.AccessControl.FileSecurity();
                regras.SetAccessRuleProtection(true, false);
                regras.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(usuario,
                    System.Security.AccessControl.FileSystemRights.FullControl,
                    System.Security.AccessControl.AccessControlType.Allow));
                info.SetAccessControl(regras);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException or InvalidOperationException)
        {
        }
    }
}
