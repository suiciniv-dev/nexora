namespace Nexora.Core.Texto;

public enum TipoLinha { Igual, Removida, Adicionada, Cabecalho }

public sealed record LinhaDiff(TipoLinha Tipo, string Texto, int? Antiga = null, int? Nova = null);

public static class Diff
{
    const int MaiorComparacao = 4000;

    public static IReadOnlyList<LinhaDiff> Comparar(string antes, string depois)
    {
        var a = Linhas(antes);
        var b = Linhas(depois);
        if ((long)a.Length * b.Length > (long)MaiorComparacao * MaiorComparacao)
            return [.. a.Select((l, i) => new LinhaDiff(TipoLinha.Removida, l, i + 1)), .. b.Select((l, i) => new LinhaDiff(TipoLinha.Adicionada, l, null, i + 1))];

        var lcs = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
            for (var j = b.Length - 1; j >= 0; j--)
                lcs[i, j] = a[i] == b[j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

        var linhas = new List<LinhaDiff>();
        int x = 0, y = 0;
        while (x < a.Length || y < b.Length)
        {
            if (x < a.Length && y < b.Length && a[x] == b[y])
            {
                linhas.Add(new LinhaDiff(TipoLinha.Igual, a[x], x + 1, y + 1));
                x++;
                y++;
            }
            else if (x < a.Length && (y == b.Length || lcs[x + 1, y] >= lcs[x, y + 1]))
            {
                linhas.Add(new LinhaDiff(TipoLinha.Removida, a[x], x + 1));
                x++;
            }
            else
            {
                linhas.Add(new LinhaDiff(TipoLinha.Adicionada, b[y], null, y + 1));
                y++;
            }
        }
        return linhas;
    }

    public static IReadOnlyList<LinhaDiff> LerUnificado(string diff)
    {
        var linhas = new List<LinhaDiff>();
        int antiga = 0, nova = 0;
        var dentro = false;
        foreach (var linha in diff.Replace("\r", "").Split('\n'))
        {
            if (linha.StartsWith("@@"))
            {
                dentro = true;
                var partes = linha.Split(' ');
                antiga = Inicio(partes.ElementAtOrDefault(1));
                nova = Inicio(partes.ElementAtOrDefault(2));
                linhas.Add(new LinhaDiff(TipoLinha.Cabecalho, linha));
                continue;
            }
            if (!dentro || linha.StartsWith('\\')) continue;
            if (linha.StartsWith('+')) linhas.Add(new LinhaDiff(TipoLinha.Adicionada, linha[1..], null, nova++));
            else if (linha.StartsWith('-')) linhas.Add(new LinhaDiff(TipoLinha.Removida, linha[1..], antiga++));
            else if (linha.Length > 0 || antiga > 0) linhas.Add(new LinhaDiff(TipoLinha.Igual, linha.Length > 0 ? linha[1..] : "", antiga++, nova++));
        }
        while (linhas.Count > 0 && linhas[^1] is { Tipo: TipoLinha.Igual, Texto: "" }) linhas.RemoveAt(linhas.Count - 1);
        return linhas;
    }

    static int Inicio(string? faixa)
    {
        if (string.IsNullOrEmpty(faixa)) return 0;
        var numero = faixa.TrimStart('-', '+').Split(',')[0];
        return int.TryParse(numero, out var n) ? n : 0;
    }

    static string[] Linhas(string texto) => texto.Length == 0 ? [] : texto.Replace("\r\n", "\n").Split('\n');
}
