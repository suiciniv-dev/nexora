using System.Text.RegularExpressions;

namespace Nexora.Core.Texto;

public abstract record Bloco;

public sealed record Paragrafo(string Texto) : Bloco;

public sealed record Titulo(int Nivel, string Texto) : Bloco;

public sealed record ItemLista(int Nivel, string Marcador, string Texto);

public sealed record Lista(IReadOnlyList<ItemLista> Itens) : Bloco;

public sealed record Codigo(string? Linguagem, string Texto) : Bloco;

public sealed record Citacao(string Texto) : Bloco;

public sealed record Tabela(IReadOnlyList<IReadOnlyList<string>> Linhas) : Bloco;

public sealed record Regua : Bloco;

public sealed record Trecho(string Texto, bool Negrito = false, bool Italico = false, bool Codigo = false, string? Link = null);

public static partial class Markdown
{
    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex TituloRegex();

    [GeneratedRegex(@"^(\s*)([-*+]|\d+[.)])\s+(.*)$")]
    private static partial Regex ItemRegex();

    [GeneratedRegex(@"^\|?\s*:?-{2,}")]
    private static partial Regex SeparadorTabelaRegex();

    [GeneratedRegex(@"`([^`]+)`|\*\*(.+?)\*\*|\[([^\]]+)\]\(([^)\s]+)\)|(?<![\w*])\*(?!\s)([^*\n]+?)\*(?![\w*])")]
    private static partial Regex InlineRegex();

    [GeneratedRegex(@"https?://[^\s<>()\[\]{}""'`]+")]
    private static partial Regex EnderecoRegex();

    public static IReadOnlyList<Bloco> Blocos(string texto)
    {
        var linhas = texto.Replace("\r\n", "\n").Split('\n');
        var blocos = new List<Bloco>();
        var paragrafo = new List<string>();

        void FecharParagrafo()
        {
            if (paragrafo.Count == 0) return;
            blocos.Add(new Paragrafo(string.Join("\n", paragrafo)));
            paragrafo.Clear();
        }

        for (var i = 0; i < linhas.Length; i++)
        {
            var linha = linhas[i];
            var t = linha.Trim();

            if (t.StartsWith("```"))
            {
                FecharParagrafo();
                var linguagem = t[3..].Trim();
                var codigo = new List<string>();
                for (i++; i < linhas.Length && !linhas[i].TrimStart().StartsWith("```"); i++) codigo.Add(linhas[i]);
                blocos.Add(new Codigo(linguagem.Length > 0 ? linguagem : null, string.Join("\n", codigo)));
                continue;
            }
            if (t.Length == 0)
            {
                FecharParagrafo();
                continue;
            }
            if (TituloRegex().Match(t) is { Success: true } titulo)
            {
                FecharParagrafo();
                blocos.Add(new Titulo(titulo.Groups[1].Length, titulo.Groups[2].Value.Trim()));
                continue;
            }
            if (t is "---" or "***" or "___")
            {
                FecharParagrafo();
                blocos.Add(new Regua());
                continue;
            }
            if (t.StartsWith('|'))
            {
                FecharParagrafo();
                var tabela = new List<IReadOnlyList<string>>();
                for (; i < linhas.Length && linhas[i].TrimStart().StartsWith('|'); i++)
                    if (!SeparadorTabelaRegex().IsMatch(linhas[i].Trim())) tabela.Add(Celulas(linhas[i]));
                i--;
                blocos.Add(new Tabela(tabela));
                continue;
            }
            if (t.StartsWith('>'))
            {
                FecharParagrafo();
                var citacao = new List<string>();
                for (; i < linhas.Length && linhas[i].TrimStart().StartsWith('>'); i++) citacao.Add(linhas[i].TrimStart()[1..].Trim());
                i--;
                blocos.Add(new Citacao(string.Join("\n", citacao)));
                continue;
            }
            if (ItemRegex().IsMatch(linha))
            {
                FecharParagrafo();
                var itens = new List<ItemLista>();
                for (; i < linhas.Length; i++)
                {
                    if (ItemRegex().Match(linhas[i]) is { Success: true } item)
                    {
                        var marcador = item.Groups[2].Value;
                        itens.Add(new ItemLista(item.Groups[1].Value.Replace("\t", "    ").Length / 2, char.IsDigit(marcador[0]) ? marcador : "•", item.Groups[3].Value.Trim()));
                    }
                    else if (linhas[i].Length > 0 && char.IsWhiteSpace(linhas[i][0]) && itens.Count > 0)
                        itens[^1] = itens[^1] with { Texto = itens[^1].Texto + " " + linhas[i].Trim() };
                    else
                        break;
                }
                i--;
                blocos.Add(new Lista(itens));
                continue;
            }
            paragrafo.Add(t);
        }
        FecharParagrafo();
        return blocos;
    }

    public static IReadOnlyList<Trecho> Trechos(string texto)
    {
        var trechos = new List<Trecho>();
        var posicao = 0;
        foreach (Match m in InlineRegex().Matches(texto))
        {
            if (m.Index > posicao) trechos.AddRange(Simples(texto[posicao..m.Index]));
            if (m.Groups[1].Success) trechos.Add(new Trecho(m.Groups[1].Value, Codigo: true));
            else if (m.Groups[2].Success) trechos.AddRange(Trechos(m.Groups[2].Value).Select(t => t with { Negrito = true }));
            else if (m.Groups[3].Success) trechos.Add(new Trecho(m.Groups[3].Value, Link: m.Groups[4].Value));
            else trechos.Add(new Trecho(m.Groups[5].Value, Italico: true));
            posicao = m.Index + m.Length;
        }
        if (posicao < texto.Length) trechos.AddRange(Simples(texto[posicao..]));
        return trechos;
    }

    static IEnumerable<Trecho> Simples(string texto)
    {
        var posicao = 0;
        foreach (Match m in EnderecoRegex().Matches(texto))
        {
            var endereco = m.Value.TrimEnd('.', ',', ';', ':', '!', '?');
            if (m.Index > posicao) yield return new Trecho(texto[posicao..m.Index]);
            yield return new Trecho(endereco, Link: endereco);
            posicao = m.Index + endereco.Length;
        }
        if (posicao < texto.Length) yield return new Trecho(texto[posicao..]);
    }

    static List<string> Celulas(string linha)
    {
        var t = linha.Trim();
        if (t.StartsWith('|')) t = t[1..];
        if (t.EndsWith('|')) t = t[..^1];
        return t.Split('|').Select(c => c.Trim()).ToList();
    }
}
