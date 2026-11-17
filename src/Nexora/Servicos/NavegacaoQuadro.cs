namespace Nexora.Servicos;

public static class NavegacaoQuadro
{
    public static T? Vizinho<T>(IReadOnlyList<IReadOnlyList<T>> colunas, T? atual, int dx, int dy) where T : class
    {
        var col = atual is null ? -1 : Enumerable.Range(0, colunas.Count).FirstOrDefault(i => colunas[i].Contains(atual), -1);
        if (col < 0) return colunas.SelectMany(c => c).FirstOrDefault();
        var linha = colunas[col].ToList().IndexOf(atual!);
        if (dy != 0) return colunas[col].ElementAtOrDefault(linha + dy) ?? atual;
        if (dx == 0) return atual;
        for (var c = col + Math.Sign(dx); c >= 0 && c < colunas.Count; c += Math.Sign(dx))
            if (colunas[c].Count > 0) return colunas[c][Math.Min(linha, colunas[c].Count - 1)];
        return atual;
    }

    public static T? Passo<T>(IReadOnlyList<T> ordem, T? atual, int passo) where T : class
    {
        if (ordem.Count == 0) return null;
        var i = atual is null ? -1 : ordem.ToList().IndexOf(atual);
        if (i < 0) return ordem[0];
        return ordem[Math.Clamp(i + Math.Sign(passo), 0, ordem.Count - 1)];
    }
}
