using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Nexora.Servicos;

public static partial class Sistema
{
    public static bool Mac => OperatingSystem.IsMacOS();

    public static KeyModifiers Comando => Mac ? KeyModifiers.Meta : KeyModifiers.Control;

    public static Action<string>? SoAnotar { get; set; }

    public static bool AppDeVerdade => Program.Rodando && Application.Current is App;

    public static Process? Abrir(ProcessStartInfo info)
    {
        if (SoAnotar is not { } anotar) return AppDeVerdade ? Process.Start(info) : null;
        anotar(string.Join(' ', [info.FileName, .. info.ArgumentList, info.Arguments]).Trim());
        return null;
    }

    public static string Aspas(string texto) => "'" + texto.Replace("'", "'\\''") + "'";

    public static bool Iniciar(string programa, IEnumerable<string> argumentos)
    {
        try
        {
            var info = new ProcessStartInfo(programa) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in argumentos) info.ArgumentList.Add(a);
            Abrir(info)?.Dispose();
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    public static Process? AbrirTerminal(string comando)
    {
        if (!Mac) return null;
        static string Apple(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        try
        {
            var info = new ProcessStartInfo("/usr/bin/osascript") { UseShellExecute = false, CreateNoWindow = true };
            foreach (var linha in new[] { "tell application \"Terminal\"", "activate", $"do script {Apple(comando)}", "end tell" })
            {
                info.ArgumentList.Add("-e");
                info.ArgumentList.Add(linha);
            }
            return Abrir(info);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    public static bool TemNoChaveiro(string servico)
    {
        if (!Mac) return false;
        try
        {
            var info = new ProcessStartInfo("/usr/bin/security") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var a in new[] { "find-generic-password", "-s", servico }) info.ArgumentList.Add(a);
            using var processo = Process.Start(info);
            if (processo is null) return false;
            _ = processo.StandardOutput.ReadToEndAsync();
            _ = processo.StandardError.ReadToEndAsync();
            if (!processo.WaitForExit(3000))
            {
                processo.Kill();
                return false;
            }
            return processo.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    public static void AbrirPasta(string pasta)
    {
        if (Mac) Iniciar("/usr/bin/open", [pasta]);
        else Iniciar("explorer.exe", [pasta]);
    }

    public static void Revelar(string caminho, bool ehPasta)
    {
        if (Mac)
        {
            Iniciar("/usr/bin/open", ehPasta ? [caminho] : ["-R", caminho]);
            return;
        }
        try
        {
            Abrir(new ProcessStartInfo("explorer.exe", ehPasta ? $"\"{caminho}\"" : $"/select,\"{caminho}\""))?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    public static void AbrirNoVsCode(string caminho)
    {
        if (!Mac)
        {
            Iniciar("cmd.exe", ["/c", "code", caminho]);
            return;
        }
        if (Instalacao.NoPath("code") is { } code) Iniciar(code, [caminho]);
        else Iniciar("/usr/bin/open", ["-a", "Visual Studio Code", caminho]);
    }

    [GeneratedRegex(@"\b((?:Ctrl|Alt|Shift)(?:[+ ](?:Ctrl|Alt|Shift))*)[+ ](Tab|Enter|Esc|D\d|F\d{1,2}|[A-Z0-9])\b")]
    private static partial Regex Gesto();

    [GeneratedRegex(@"^\s*(?:Ctrl|Alt|Shift)(?:[+ ](?:Ctrl|Alt|Shift))*[+ ](?:Tab|Enter|Esc|D\d|F\d{1,2}|[A-Z0-9])\s*$")]
    private static partial Regex SoGesto();

    public static string Atalho(string texto) => Atalho(texto, Mac);

    public static string Atalho(string texto, bool mac) => !mac ? texto : Gesto().Replace(texto, m =>
    {
        var mods = m.Groups[1].Value.Split('+', ' ');
        var tecla = m.Groups[2].Value;
        if (tecla.Length == 2 && tecla[0] == 'D' && char.IsDigit(tecla[1])) tecla = tecla[1..];
        var ctrl = mods.Contains("Ctrl");
        var comando = ctrl && tecla != "Tab";
        return (ctrl && !comando ? "⌃" : "") + (mods.Contains("Alt") ? "⌥" : "") + (mods.Contains("Shift") ? "⇧" : "") + (comando ? "⌘" : "") + tecla;
    });

    public static void PrepararMac()
    {
        if (!Mac) return;
        ToolTip.TipProperty.Changed.AddClassHandler<Control>((c, e) =>
        {
            if (e.NewValue is string s && Gesto().IsMatch(s)) ToolTip.SetTip(c, Atalho(s));
        });
        TextBlock.TextProperty.Changed.AddClassHandler<TextBlock>((t, e) =>
        {
            if (e.NewValue is string s && SoGesto().IsMatch(s)) t.Text = Atalho(s);
        });
        TextBox.PlaceholderTextProperty.Changed.AddClassHandler<TextBox>((t, e) =>
        {
            if (e.NewValue is string s && Gesto().IsMatch(s)) t.PlaceholderText = Atalho(s);
        });
    }

    public static void AjustarAtalhos(InputElement elemento)
    {
        if (!Mac) return;
        foreach (var kb in elemento.KeyBindings)
            if (kb.Gesture is { } g && g.KeyModifiers.HasFlag(KeyModifiers.Control) && g.Key is not (Key.Tab or Key.Space))
                kb.Gesture = new KeyGesture(g.Key, (g.KeyModifiers & ~KeyModifiers.Control) | KeyModifiers.Meta);
    }
}
