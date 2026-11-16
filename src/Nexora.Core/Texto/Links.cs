using System.Text.RegularExpressions;

namespace Nexora.Core.Texto;

public sealed record AlvoDeLink(string Caminho, int? Linha);

public static partial class Links
{
    [GeneratedRegex(@"^(?<caminho>.+?)(?:#L(?<linha>\d+)(?:-L?\d+)?|:(?<linha>\d+)(?::\d+)?)$")]
    private static partial Regex ComLinhaRegex();

    public static AlvoDeLink? Arquivo(string destino, string? pasta)
    {
        if (string.IsNullOrWhiteSpace(destino)) return null;
        var texto = Uri.UnescapeDataString(destino.Trim());
        if (texto.StartsWith("file:///", StringComparison.OrdinalIgnoreCase)) texto = texto["file:///".Length..];
        int? linha = null;
        if (ComLinhaRegex().Match(texto) is { Success: true } m && !(m.Groups["caminho"].Value.Length == 1 && char.IsLetter(m.Groups["caminho"].Value[0])))
        {
            texto = m.Groups["caminho"].Value;
            linha = int.Parse(m.Groups["linha"].Value);
        }
        texto = texto.Replace('/', Path.DirectorySeparatorChar);
        foreach (var candidato in Candidatos(texto, pasta))
            if (File.Exists(candidato)) return new AlvoDeLink(Path.GetFullPath(candidato), linha);
        return null;
    }

    static IEnumerable<string> Candidatos(string texto, string? pasta)
    {
        if (Path.IsPathRooted(texto)) yield return texto;
        else if (pasta is { Length: > 0 }) yield return Path.Combine(pasta, texto.TrimStart('.', Path.DirectorySeparatorChar));
    }
}
