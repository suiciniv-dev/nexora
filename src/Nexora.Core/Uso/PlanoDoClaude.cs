using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nexora.Core.Uso;

public sealed record JanelaDoPlano(string Nome, double Percentual, DateTime? Reinicia);

public sealed record Plano(JanelaDoPlano? Sessao, JanelaDoPlano? Semana, IReadOnlyList<JanelaDoPlano> PorModelo, DateTime LidoEm);

public static partial class PlanoDoClaude
{
    static readonly CultureInfo Ingles = CultureInfo.GetCultureInfo("en-US");

    public static Plano? Interpretar(string texto, DateTime agora)
    {
        JanelaDoPlano? sessao = null, semana = null;
        if (Sessao().Match(texto) is { Success: true } s) sessao = new JanelaDoPlano("Sessão", Numero(s.Groups[1].Value), Reinicio(s.Groups[2].Value, agora));
        if (Semana().Match(texto) is { Success: true } w) semana = new JanelaDoPlano("Semana", Numero(w.Groups[1].Value), Reinicio(w.Groups[2].Value, agora));
        var porModelo = SemanaDoModelo().Matches(texto)
            .Select(m => new JanelaDoPlano(m.Groups[1].Value.Trim(), Numero(m.Groups[2].Value), Reinicio(m.Groups[3].Value, agora)))
            .ToList();
        return sessao is null && semana is null && porModelo.Count == 0 ? null : new Plano(sessao, semana, porModelo, agora);
    }

    static double Numero(string texto) => double.Parse(texto, NumberStyles.Float, CultureInfo.InvariantCulture);

    static DateTime? Reinicio(string texto, DateTime agora)
    {
        var t = texto.Trim();
        if (t.StartsWith("in ", StringComparison.OrdinalIgnoreCase))
        {
            var quando = agora;
            if (Regex.Match(t, @"(\d+)\s*d") is { Success: true } d) quando = quando.AddDays(int.Parse(d.Groups[1].Value, CultureInfo.InvariantCulture));
            if (Regex.Match(t, @"(\d+)\s*h") is { Success: true } h) quando = quando.AddHours(int.Parse(h.Groups[1].Value, CultureInfo.InvariantCulture));
            if (Regex.Match(t, @"(\d+)\s*m") is { Success: true } m) quando = quando.AddMinutes(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            return quando;
        }
        foreach (var formato in new[] { "MMM d, h:mmtt", "MMM d, htt", "MMM d, h:mm tt", "MMM d, h tt", "h:mmtt", "htt", "h:mm tt", "h tt" })
        {
            if (!DateTime.TryParseExact(t, formato, Ingles, DateTimeStyles.AllowWhiteSpaces, out var lido)) continue;
            if (!formato.StartsWith('M')) lido = agora.Date + lido.TimeOfDay;
            else lido = new DateTime(agora.Year, lido.Month, lido.Day, lido.Hour, lido.Minute, 0);
            if (lido < agora.AddHours(-1)) lido = formato.StartsWith('M') ? lido.AddYears(1) : lido.AddDays(1);
            return lido;
        }
        return null;
    }

    public static async Task<Plano?> LerAsync(string claude, CancellationToken cancelar = default)
    {
        var info = new ProcessStartInfo(claude)
        {
            WorkingDirectory = Path.GetTempPath(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in new[] { "-p", "/usage", "--output-format", "json", "--no-session-persistence", "--settings", "{\"disableAllHooks\":true}" })
            info.ArgumentList.Add(a);
        try
        {
            using var processo = Process.Start(info);
            if (processo is null) return null;
            var saida = processo.StandardOutput.ReadToEndAsync(cancelar);
            _ = processo.StandardError.ReadToEndAsync(cancelar);
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelar);
            limite.CancelAfter(TimeSpan.FromSeconds(60));
            try
            {
                await processo.WaitForExitAsync(limite.Token);
            }
            catch (OperationCanceledException)
            {
                processo.Kill(true);
                return null;
            }
            using var doc = JsonDocument.Parse(await saida);
            return doc.RootElement.TryGetProperty("result", out var r) && r.GetString() is { } texto ? Interpretar(texto, DateTime.Now) : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or JsonException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"Current session:\s*([0-9.]+)%\s*used\W+resets\s+([^(\r\n]+)")]
    private static partial Regex Sessao();

    [GeneratedRegex(@"Current week \(all models\):\s*([0-9.]+)%\s*used\W+resets\s+([^(\r\n]+)")]
    private static partial Regex Semana();

    [GeneratedRegex(@"Current week \((?!all models)([^)]+)\):\s*([0-9.]+)%\s*used\W+resets\s+([^(\r\n]+)")]
    private static partial Regex SemanaDoModelo();
}
