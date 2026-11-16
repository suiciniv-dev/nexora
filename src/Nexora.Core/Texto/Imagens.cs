using System.Globalization;
using System.Text.RegularExpressions;

namespace Nexora.Core.Texto;

public sealed record ImagemMd(string Url, int? Largura);

public static partial class ImagensMd
{
    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex Tag();

    [GeneratedRegex(@"\bsrc\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex Fonte();

    [GeneratedRegex(@"\bwidth\s*=\s*[""']?(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex Largura();

    [GeneratedRegex(@"!\[[^\]]*\]\(\s*<?([^)\s>]+)>?(?:\s+""[^""]*"")?\s*\)")]
    private static partial Regex MarkdownImagem();

    [GeneratedRegex(@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase)]
    private static partial Regex Uuid();

    public static (string Resto, IReadOnlyList<ImagemMd> Imagens) Separar(string texto)
    {
        var achadas = new List<(int Posicao, ImagemMd Imagem)>();
        foreach (Match m in Tag().Matches(texto))
            if (Fonte().Match(m.Value) is { Success: true } src)
                achadas.Add((m.Index, new ImagemMd(src.Groups[1].Value, Largura().Match(m.Value) is { Success: true } w && int.TryParse(w.Groups[1].Value, CultureInfo.InvariantCulture, out var n) ? n : null)));
        foreach (Match m in MarkdownImagem().Matches(texto)) achadas.Add((m.Index, new ImagemMd(m.Groups[1].Value, null)));
        if (achadas.Count == 0) return (texto, []);
        var resto = MarkdownImagem().Replace(Tag().Replace(texto, ""), "").Trim();
        return (resto, [.. achadas.OrderBy(a => a.Posicao).Select(a => a.Imagem)]);
    }

    public static IReadOnlyList<string> FontesDoHtml(string html) =>
        [.. Tag().Matches(html).Select(m => Fonte().Match(m.Value)).Where(s => s.Success).Select(s => System.Net.WebUtility.HtmlDecode(s.Groups[1].Value))];

    public static string? Chave(string url) => Uuid().Match(url) is { Success: true } m ? m.Value.ToLowerInvariant() : null;
}

public static partial class Mencoes
{
    [GeneratedRegex(@"(?<![\w@])@([^\s@]+)")]
    private static partial Regex Mencao();

    public static IReadOnlyList<string> Arquivos(string texto) =>
        [.. Mencao().Matches(texto)
            .Select(m => m.Groups[1].Value.TrimEnd(',', ';', ':', ')', ']', '!', '?', '.', '"', '\''))
            .Where(c => c.Contains('/') || c.Contains('\\') || Path.HasExtension(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    public static string Remover(string texto, string caminho)
    {
        var sem = Regex.Replace(texto, @"(?<![\w@])@" + Regex.Escape(caminho) + @"(?=$|[\s,;:)\].!?""'])", "");
        return Regex.Replace(sem, @"[ \t]{2,}", " ").Replace(" ,", ",").Trim(' ');
    }
}
