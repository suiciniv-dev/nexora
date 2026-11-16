using System.Diagnostics;

namespace Nexora.Core.Nuvem;

public sealed class TransporteSsh(MaquinaNaNuvem maquina, string chave, string conhecidos, string? pastaDeControle = null, string? certificado = null, string? senha = null) : ITransporte
{
    public const string VariavelDaSenha = "NEXORA_SSH_SENHA";

    public static string? ProgramaDeSenha { get; set; }

    public static string Executavel
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                var doSistema = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe");
                return File.Exists(doSistema) ? doSistema : "ssh.exe";
            }
            return File.Exists("/usr/bin/ssh") ? "/usr/bin/ssh" : "ssh";
        }
    }

    public static bool UsaControlMaster => !OperatingSystem.IsWindows();

    public IReadOnlyList<string> Argumentos(string comandoRemoto)
    {
        var args = Base();
        args.Add(Destino);
        args.Add(comandoRemoto);
        return args;
    }

    public IReadOnlyList<string> ArgumentosDoTunel(int portaLocal, int portaRemota)
    {
        var args = Base();
        args.AddRange(["-N", "-o", "ExitOnForwardFailure=yes", "-L", "127.0.0.1:" + portaLocal.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":localhost:" + portaRemota.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        args.Add(Destino);
        return args;
    }

    string Destino => string.IsNullOrEmpty(maquina.Usuario) ? maquina.Host : maquina.Usuario + "@" + maquina.Host;

    List<string> Base()
    {
        List<string> args =
        [
            "-T",
            "-o", "BatchMode=" + (senha is null ? "yes" : "no"),
            "-o", "ConnectTimeout=15",
            "-o", "ServerAliveInterval=15",
            "-o", "ServerAliveCountMax=3",
            "-o", "StrictHostKeyChecking=" + (certificado is null ? "accept-new" : "yes"),
            "-o", "UserKnownHostsFile=" + conhecidos,
            "-o", "LogLevel=ERROR",
        ];
        if (senha is null) args.AddRange(["-o", "IdentitiesOnly=yes", "-i", chave]);
        else args.AddRange(["-o", "PreferredAuthentications=password,keyboard-interactive", "-o", "PubkeyAuthentication=no", "-o", "NumberOfPasswordPrompts=1"]);
        if (certificado is not null) args.AddRange(["-o", "CertificateFile=" + certificado]);
        if (UsaControlMaster && pastaDeControle is not null)
            args.AddRange(["-o", "ControlMaster=auto", "-o", "ControlPath=" + Path.Combine(pastaDeControle, "%C"), "-o", "ControlPersist=120"]);
        if (maquina.Porta is > 0 and not 22) args.AddRange(["-p", maquina.Porta.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        return args;
    }

    public ProcessStartInfo Comando(string comandoRemoto) => Montar(Argumentos(comandoRemoto));

    public ProcessStartInfo Tunel(int portaLocal, int portaRemota) => Montar(ArgumentosDoTunel(portaLocal, portaRemota));

    ProcessStartInfo Montar(IReadOnlyList<string> argumentos)
    {
        var info = new ProcessStartInfo(Executavel);
        foreach (var arg in argumentos) info.ArgumentList.Add(arg);
        if (senha is not null)
        {
            info.Environment["SSH_ASKPASS"] = ProgramaDeSenha ?? throw new InvalidOperationException(Texto.Idioma.T("O Nexora não achou o próprio programa para entregar a senha ao SSH."));
            info.Environment["SSH_ASKPASS_REQUIRE"] = "force";
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))) info.Environment["DISPLAY"] = "nexora:0";
            info.Environment[VariavelDaSenha] = senha;
        }
        return info;
    }
}

public sealed class TransporteLocal(string casa, string? shell = null) : ITransporte
{
    public string Casa { get; } = casa;

    public static string? ShellPadrao()
    {
        if (!OperatingSystem.IsWindows()) return "/bin/sh";
        string[] candidatos =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Git", "bin", "bash.exe"),
        ];
        return candidatos.FirstOrDefault(File.Exists);
    }

    public bool Disponivel => (shell ?? ShellPadrao()) is not null;

    public ProcessStartInfo Comando(string comandoRemoto)
    {
        var programa = shell ?? ShellPadrao() ?? throw new InvalidOperationException(Texto.Idioma.T("A máquina de teste precisa do Git Bash."));
        Directory.CreateDirectory(Casa);
        var info = new ProcessStartInfo(programa) { WorkingDirectory = Casa };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("cd \"$HOME\" && " + comandoRemoto);
        info.Environment["HOME"] = Casa;
        info.Environment["CHERE_INVOKING"] = "1";
        return info;
    }
}

public sealed class ProvedorSsh(string pasta) : IProvedorDeMaquinas
{
    public string Tipo => TiposDeMaquina.Ssh;
    public bool CriaMaquinas => false;
    public bool Pausa => false;
    public bool PublicaPortas => false;

    public string Chave => Path.Combine(pasta, "chaves", "id_ed25519");
    public string Conhecidos => Path.Combine(pasta, "known_hosts");
    public string? PastaDeControle => TransporteSsh.UsaControlMaster ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nexora-ssh") : null;

    public IMaquinaRemota Abrir(MaquinaNaNuvem dados)
    {
        if (PastaDeControle is { } controle)
        {
            Directory.CreateDirectory(controle);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(controle, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        return new MaquinaRemota(dados, new TransporteSsh(dados, Chave, Conhecidos, PastaDeControle));
    }

    public Task<MaquinaNaNuvem> CriarAsync(string nome, CancellationToken cancelamento = default) => throw new NotSupportedException();
    public Task RetomarAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default) => Task.CompletedTask;
    public Task PausarAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default) => throw new NotSupportedException();
    public Task<string> PublicarPortaAsync(MaquinaNaNuvem maquina, int porta, CancellationToken cancelamento = default) => throw new NotSupportedException();
    public Task<CotaDaMaquina?> CotaAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default) => Task.FromResult<CotaDaMaquina?>(null);
}

public sealed class ProvedorDeTeste(string pasta) : IProvedorDeMaquinas
{
    public string Tipo => TiposDeMaquina.Teste;
    public bool CriaMaquinas => false;
    public bool Pausa => false;
    public bool PublicaPortas => false;

    public string Casa(MaquinaNaNuvem dados) => Path.Combine(pasta, dados.Id);

    public IMaquinaRemota Abrir(MaquinaNaNuvem dados) => new MaquinaRemota(dados, new TransporteLocal(Casa(dados)));

    public Task<MaquinaNaNuvem> CriarAsync(string nome, CancellationToken cancelamento = default) => throw new NotSupportedException();
    public Task RetomarAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default) => Task.CompletedTask;
    public Task PausarAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default) => throw new NotSupportedException();
    public Task<string> PublicarPortaAsync(MaquinaNaNuvem maquina, int porta, CancellationToken cancelamento = default) => throw new NotSupportedException();
    public Task<CotaDaMaquina?> CotaAsync(MaquinaNaNuvem maquina, CancellationToken cancelamento = default) => Task.FromResult<CotaDaMaquina?>(null);
}
