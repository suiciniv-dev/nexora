using System.Globalization;

namespace Nexora.Core.Texto;

public static class Idioma
{
    static readonly Dictionary<string, string> Traducoes = new(StringComparer.Ordinal);
    static readonly CultureInfo Portugues = CultureInfo.GetCultureInfo("pt-BR");
    static readonly CultureInfo Ingles = CultureInfo.GetCultureInfo("en-US");

    public static bool EmIngles { get; private set; }

    public static string Codigo => EmIngles ? "en" : "pt";

    public static CultureInfo Cultura => EmIngles ? Ingles : Portugues;

    public static void Definir(string? escolha) => EmIngles = escolha?.ToLowerInvariant() switch
    {
        "en" => true,
        "pt" => false,
        _ => !CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("pt", StringComparison.OrdinalIgnoreCase),
    };

    public static void Registrar(IEnumerable<(string Pt, string En)> pares)
    {
        foreach (var (pt, en) in pares) Traducoes[pt] = en;
    }

    public static bool TemTraducao(string pt) => Traducoes.ContainsKey(pt);

    public static IReadOnlyCollection<string> Chaves => Traducoes.Keys;

    public static string T(string pt) => EmIngles && Traducoes.TryGetValue(pt, out var en) ? en : pt;

    public static string F(string pt, params object?[] valores) => string.Format(Cultura, T(pt), valores);
}
