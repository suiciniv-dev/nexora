using System.Runtime.Versioning;

namespace Nexora.Core.Cli;

public sealed class ComandoNoTerminal(string casa, string? dotnet, string aplicativo)
{
    public static string Nome => OperatingSystem.IsWindows() ? "nexora.cmd" : "nexora";

    public string Pasta => Path.Combine(casa, ".local", "bin");
    public string Arquivo => Path.Combine(Pasta, Nome);

    public string Conteudo => OperatingSystem.IsWindows()
        ? $"@echo off\r\n\"{dotnet ?? "dotnet"}\" \"{aplicativo}\" --cli %*\r\n"
        : $"#!/bin/sh\nexec \"{aplicativo}\" --cli \"$@\"\n";

    public bool Instalado => File.Exists(Arquivo) && File.ReadAllText(Arquivo) == Conteudo;

    public bool NoPath => (Environment.GetEnvironmentVariable("PATH") ?? "")
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Any(p => string.Equals(p.TrimEnd('\\', '/'), Pasta, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));

    public string Instalar()
    {
        Directory.CreateDirectory(Pasta);
        File.WriteAllText(Arquivo, Conteudo);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(Arquivo, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        return OperatingSystem.IsWindows() ? ColocarNoPathDoWindows() : ColocarNoPerfilDoMac();
    }

    public void Remover()
    {
        if (File.Exists(Arquivo)) File.Delete(Arquivo);
    }

    [SupportedOSPlatform("windows")]
    string ColocarNoPathDoWindows()
    {
        var atual = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
        var partes = atual.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (partes.Any(p => string.Equals(p.TrimEnd('\\'), Pasta, StringComparison.OrdinalIgnoreCase)))
            return Texto.Idioma.T("Pronto. Abra um terminal e digite nexora.");
        partes.Add(Pasta);
        Environment.SetEnvironmentVariable("Path", string.Join(';', partes), EnvironmentVariableTarget.User);
        return Texto.Idioma.T("Pronto. Feche e abra o terminal e digite nexora.");
    }

    string ColocarNoPerfilDoMac()
    {
        var perfil = Path.Combine(casa, ".zprofile");
        const string Linha = "export PATH=\"$HOME/.local/bin:$PATH\"";
        if (NoPath || (File.Exists(perfil) && File.ReadAllText(perfil).Contains(".local/bin", StringComparison.Ordinal)))
            return Texto.Idioma.T("Pronto. Abra um terminal e digite nexora.");
        File.AppendAllText(perfil, "\n" + Linha + "\n");
        return Texto.Idioma.T("Pronto. Feche e abra o terminal e digite nexora.");
    }
}
