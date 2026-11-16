using System.Globalization;

namespace Nexora.Core.Uso;

public readonly record struct TokensDaResposta(long Entrada, long Saida, long Escrita5m, long Escrita1h, long Lida, bool Rapido = false)
{
    public long Total => Entrada + Saida + Escrita5m + Escrita1h + Lida;
}

public static class PesoDoModelo
{
    public static string Familia(string? modelo) => modelo?.ToLowerInvariant() switch
    {
        null or "" => "outro",
        var m when m.Contains("fable") => "Fable",
        var m when m.Contains("mythos") => "Mythos",
        var m when m.Contains("opus") => "Opus",
        var m when m.Contains("sonnet") => "Sonnet",
        var m when m.Contains("haiku") => "Haiku",
        var m when m.Contains("gemini") && m.Contains("flash") => "Gemini Flash",
        var m when m.Contains("gemini") && m.Contains("pro") => "Gemini Pro",
        var m when m.Contains("gemini") => "Gemini",
        var m when m.StartsWith("gpt") || m.StartsWith("o3") || m.StartsWith("o4") || m.Contains("codex") => NomeOpenAi(m),
        _ => "outro",
    };

    static string NomeOpenAi(string m)
    {
        var partes = m.Split('-', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (partes.Count == 0) return "outro";
        var nome = partes[0] == "gpt" && partes.Count > 1 ? "GPT-" + partes[1] : partes[0];
        var resto = partes.Skip(partes[0] == "gpt" && partes.Count > 1 ? 2 : 1)
            .Where(p => !p.All(char.IsDigit))
            .Select(p => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(p));
        return string.Join(' ', new[] { nome }.Concat(resto));
    }

    public static double Peso(string? modelo, TokensDaResposta t)
    {
        var (entrada, saida, lida) = Tabela(modelo?.ToLowerInvariant() ?? "", t.Entrada + t.Escrita5m + t.Escrita1h + t.Lida);
        var rapido = t.Rapido && (modelo ?? "").Contains("opus", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        var dolares = t.Entrada * entrada + t.Saida * saida + t.Escrita5m * entrada * 1.25 + t.Escrita1h * entrada * 2 + t.Lida * lida;
        return dolares * rapido / 1_000_000;
    }

    static (double Entrada, double Saida, double Lida) Tabela(string m, long prompt) => m switch
    {
        _ when m.Contains("fable-5-1") || m.Contains("mythos-5-1") => (10, 50, 0.25),
        _ when m.Contains("fable") || m.Contains("mythos") => (10, 50, 1.0),
        _ when m.Contains("opus-5-5") => (4, 20, 0.20),
        _ when m.Contains("opus-4-1") || m.EndsWith("opus-4") || m.Contains("opus-4-2025") => (15, 75, 1.5),
        _ when m.Contains("opus") => (5, 25, 0.5),
        _ when m.Contains("sonnet-5") => (2, 10, 0.2),
        _ when m.Contains("sonnet") => (3, 15, 0.3),
        _ when m.Contains("haiku-5-5") => prompt > 100_000 ? (0.5, 2.5, 0.05) : (0.1, 0.5, 0.01),
        _ when m.Contains("haiku-4-5") => (1, 5, 0.1),
        _ when m.Contains("haiku") => (0.8, 4, 0.08),
        _ when m.Contains("gemini") && m.Contains("flash") => (0.3, 2.5, 0.075),
        _ when m.Contains("gemini") => prompt > 200_000 ? (2.5, 15, 0.625) : (1.25, 10, 0.31),
        _ when m.Contains("mini") => (0.25, 2, 0.025),
        _ when m.StartsWith("gpt") || m.Contains("codex") || m.StartsWith("o3") || m.StartsWith("o4") => (1.25, 10, 0.125),
        _ => (3, 15, 0.3),
    };
}
