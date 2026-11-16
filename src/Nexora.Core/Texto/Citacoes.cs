namespace Nexora.Core.Texto;

public static class Citacoes
{
    public static string Montar(string? trecho, string texto)
    {
        if (string.IsNullOrWhiteSpace(trecho)) return texto;
        var linhas = trecho.Trim().Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd().Length == 0 ? ">" : "> " + l.TrimEnd());
        return string.Join("\n", linhas) + "\n\n" + texto;
    }

    public static (string? Trecho, string Texto) Separar(string texto)
    {
        if (!texto.StartsWith('>')) return (null, texto);
        var linhas = texto.Replace("\r\n", "\n").Split('\n');
        var citadas = new List<string>();
        var i = 0;
        for (; i < linhas.Length && linhas[i].StartsWith('>'); i++)
        {
            var linha = linhas[i][1..];
            citadas.Add(linha.StartsWith(' ') ? linha[1..] : linha);
        }
        if (i >= linhas.Length || linhas[i].Trim().Length != 0) return (null, texto);
        var resto = string.Join("\n", linhas.Skip(i + 1)).Trim();
        var citado = string.Join("\n", citadas).Trim();
        return resto.Length == 0 || citado.Length == 0 ? (null, texto) : (citado, resto);
    }
}
