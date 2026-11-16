using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Nexora.Core.Texto;

namespace Nexora;

public sealed record Receita(string Id, string Site, string[] Passos, string? Pacote = null, bool Script = false, string? Brew = null)
{
    public bool Instalavel => Script || (OperatingSystem.IsMacOS()
        ? Brew is not null && Instalacao.BrewInstalado() is not null
        : Pacote is not null && Instalacao.Winget() is not null);

    public string Comando => Id switch
    {
        "claude" => OperatingSystem.IsMacOS() ? "curl -fsSL https://claude.ai/install.sh | bash" : "irm https://claude.ai/install.ps1 | iex",
        "dotnet" => OperatingSystem.IsMacOS() ? Idioma.T("Vem junto com o Nexora") : "winget install Microsoft.DotNet.Runtime.10",
        _ => OperatingSystem.IsMacOS() ? (Brew is null ? "" : $"brew install {Brew}") : $"winget install {Pacote}",
    };
}

public static partial class Instalacao
{
    static readonly bool Mac = OperatingSystem.IsMacOS();

    public static Receita Claude => field ??= new("claude", $"https://code.claude.com/docs/{Idioma.Codigo}/setup", Mac
        ? [
            Idioma.T("Abra o Terminal (Aplicativos, Utilitários)."),
            Idioma.T("Cole o comando abaixo e aperte Enter."),
            Idioma.T("Quando terminar, feche o Terminal e clique em Verificar de novo."),
        ]
        : [
            Idioma.T("Abra o PowerShell (menu Iniciar, digite PowerShell)."),
            Idioma.T("Cole o comando abaixo e aperte Enter."),
            Idioma.T("Quando terminar, feche o PowerShell e clique em Verificar de novo."),
        ], Script: true);

    public static Receita Git => field ??= new("git", Mac ? "https://git-scm.com/downloads/mac" : "https://git-scm.com/downloads/win", Mac
        ? [
            Idioma.T("No Mac, o git vem com as ferramentas de linha de comando da Apple: rode xcode-select --install no Terminal."),
            Idioma.T("Com o Homebrew (brew.sh), também dá para usar o comando abaixo."),
        ]
        : [
            Idioma.T("Abra o PowerShell ou o Terminal."),
            Idioma.T("Cole o comando abaixo e aperte Enter. O Windows pode pedir permissão."),
            Idioma.T("Sem o winget, baixe o instalador na página oficial e avance com as opções padrão."),
        ], "Git.Git", Brew: "git");

    public static Receita Node => field ??= new("node", Idioma.EmIngles ? "https://nodejs.org/en/download" : "https://nodejs.org/pt-br/download", Mac
        ? [
            Idioma.T("Abra o Terminal."),
            Idioma.T("Cole o comando abaixo e aperte Enter (precisa do Homebrew, de brew.sh)."),
            Idioma.T("Sem o Homebrew, baixe o instalador .pkg da versão LTS na página oficial."),
        ]
        : [
            Idioma.T("Abra o PowerShell ou o Terminal."),
            Idioma.T("Cole o comando abaixo e aperte Enter. O Windows pode pedir permissão."),
            Idioma.T("Sem o winget, baixe a versão LTS na página oficial."),
        ], "OpenJS.NodeJS.LTS", Brew: "node");

    public static Receita Gh => field ??= new("gh", "https://cli.github.com/", Mac
        ? [
            Idioma.T("Abra o Terminal."),
            Idioma.T("Cole o comando abaixo e aperte Enter (precisa do Homebrew, de brew.sh)."),
            Idioma.T("Depois, volte aqui e entre na sua conta do GitHub."),
        ]
        : [
            Idioma.T("Abra o PowerShell ou o Terminal."),
            Idioma.T("Cole o comando abaixo e aperte Enter."),
            Idioma.T("Depois, volte aqui e clique em Entrar numa conta."),
        ], "GitHub.cli", Brew: "gh");

    public static Receita DotNet => field ??= new("dotnet", Idioma.EmIngles ? "https://dotnet.microsoft.com/en-us/download/dotnet/10.0" : "https://dotnet.microsoft.com/pt-br/download/dotnet/10.0",
    [
        Idioma.T("Baixe o .NET Runtime 10 (x64) na página oficial."),
        Idioma.T("Ou cole o comando abaixo no PowerShell."),
    ]);

    public static string? BrewInstalado() =>
        new[] { "/opt/homebrew/bin/brew", "/usr/local/bin/brew" }.FirstOrDefault(File.Exists) ?? NoPath("brew");

    static bool _pathDoMac;

    public static void PathDoMac(bool deNovo = false)
    {
        if (!Mac || _pathDoMac && !deNovo) return;
        _pathDoMac = true;
        var casa = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var doShell = "";
        try
        {
            var shell = Environment.GetEnvironmentVariable("SHELL") is { Length: > 0 } s ? s : "/bin/zsh";
            var info = new ProcessStartInfo(shell) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
            info.ArgumentList.Add("-ilc");
            info.ArgumentList.Add("printf '<<NEXORA>>%s<<NEXORA>>' \"$PATH\"");
            using var processo = Process.Start(info);
            if (processo is not null)
            {
                processo.StandardInput.Close();
                var saida = processo.StandardOutput.ReadToEndAsync();
                _ = processo.StandardError.ReadToEndAsync();
                if (processo.WaitForExit(5000) && Regex.Match(saida.Result, "<<NEXORA>>(.*?)<<NEXORA>>", RegexOptions.Singleline) is { Success: true } m) doShell = m.Groups[1].Value;
                else if (!processo.HasExited) processo.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or AggregateException)
        {
        }
        var extras = new[] { Path.Combine(casa, ".local", "bin"), "/opt/homebrew/bin", "/opt/homebrew/sbin", "/usr/local/bin", Path.Combine(casa, ".dotnet", "tools"), "/usr/local/share/dotnet" };
        var atual = (Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin:/bin:/usr/sbin:/sbin").Split(':', StringSplitOptions.RemoveEmptyEntries);
        var juntas = doShell.Split(':', StringSplitOptions.RemoveEmptyEntries).Concat(atual).Concat(extras).Where(p => p.Length > 0).Distinct().ToList();
        Environment.SetEnvironmentVariable("PATH", string.Join(':', juntas));
    }

    public static string? Winget()
    {
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");
        return File.Exists(local) ? local : NoPath("winget.exe");
    }

    public static string? NoPath(string nome)
    {
        if (!OperatingSystem.IsWindows() && nome.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) nome = nome[..^4];
        return (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p =>
            {
                try
                {
                    return Path.Combine(Environment.ExpandEnvironmentVariables(p.Trim('"')), nome);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            })
            .FirstOrDefault(File.Exists);
    }

    public static void AtualizarPath()
    {
        if (Mac)
        {
            PathDoMac(deNovo: true);
            return;
        }
        var atual = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
        var novos = new[] { EnvironmentVariableTarget.Machine, EnvironmentVariableTarget.User }
            .SelectMany(alvo => (Environment.GetEnvironmentVariable("PATH", alvo) ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            .Select(p => Environment.ExpandEnvironmentVariables(p.Trim()))
            .Where(p => p.Length > 0 && !atual.Any(a => string.Equals(a.TrimEnd('\\'), p.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (novos.Count > 0) Environment.SetEnvironmentVariable("PATH", string.Join(Path.PathSeparator, atual.Concat(novos)));
    }

    public static async Task<bool> InstalarAsync(Receita receita, Action<string> progresso, CancellationToken ct = default)
    {
        ProcessStartInfo? info = null;
        if (Mac)
        {
            if (receita.Script)
            {
                info = new ProcessStartInfo("/bin/bash");
                foreach (var a in new[] { "-c", "curl -fsSL https://claude.ai/install.sh | bash" }) info.ArgumentList.Add(a);
            }
            else if (receita.Brew is { } formula && BrewInstalado() is { } brew)
            {
                info = new ProcessStartInfo(brew);
                foreach (var a in new[] { "install", formula }) info.ArgumentList.Add(a);
                info.Environment["HOMEBREW_NO_AUTO_UPDATE"] = "1";
                info.Environment["NONINTERACTIVE"] = "1";
            }
        }
        else if (receita.Script)
        {
            info = new ProcessStartInfo("powershell.exe");
            foreach (var a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", "[Console]::OutputEncoding=[Text.Encoding]::UTF8; irm https://claude.ai/install.ps1 | iex" })
                info.ArgumentList.Add(a);
        }
        else if (receita.Pacote is { } pacote && Winget() is { } winget)
        {
            info = new ProcessStartInfo(winget);
            foreach (var a in new[] { "install", "--id", pacote, "-e", "--source", "winget", "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity" })
                info.ArgumentList.Add(a);
        }
        if (info is null) return false;
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        info.StandardOutputEncoding = Encoding.UTF8;
        info.StandardErrorEncoding = Encoding.UTF8;
        try
        {
            using var processo = Sistema.Abrir(info);
            if (processo is null) return false;
            var leitura = Task.WhenAll(Ler(processo.StandardOutput, progresso), Ler(processo.StandardError, progresso));
            await processo.WaitForExitAsync(ct);
            await leitura;
            return processo.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            progresso(ex.Message);
            return false;
        }
    }

    static async Task Ler(StreamReader leitor, Action<string> progresso)
    {
        var buffer = new char[512];
        var linha = new StringBuilder();
        int lidos;
        while ((lidos = await leitor.ReadAsync(buffer)) > 0)
        {
            for (var i = 0; i < lidos; i++)
            {
                if (buffer[i] is '\r' or '\n') Emitir();
                else linha.Append(buffer[i]);
            }
        }
        Emitir();

        void Emitir()
        {
            var texto = Limpar(linha.ToString());
            linha.Clear();
            if (texto.Length > 1) progresso(texto);
        }
    }

    public static string Limpar(string texto)
    {
        var limpo = Blocos().Replace(Ansi().Replace(texto, ""), " ");
        limpo = Espacos().Replace(limpo, " ").Trim();
        return limpo.Length > 140 ? limpo[..140] + "…" : limpo;
    }

    [GeneratedRegex(@"\x1b\[[0-9;?]*[A-Za-z]")]
    private static partial Regex Ansi();

    [GeneratedRegex(@"[█▓▒░■□▏▎▍▌▋▊▉]+")]
    private static partial Regex Blocos();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Espacos();
}
