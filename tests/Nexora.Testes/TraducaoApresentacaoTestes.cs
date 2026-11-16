using System.Text.RegularExpressions;
using Nexora.Traducao;

namespace Nexora.Testes;

public sealed partial class TraducaoTestes
{
    static readonly string[] SemTraducao = ["Nexora", "Esc", "Migration", "GitHub CLI"];

    [GeneratedRegex("\"((?:[^\"\\\\]|\\\\.)*)\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"")]
    private static partial Regex ParDoDicionario();

    [GeneratedRegex("(?<![\\w$.])[tf]\\(")]
    private static partial Regex ChamadaDeTraducao();

    [GeneratedRegex("[?:(]\\s*\"((?:[^\"\\\\]|\\\\.)*)\"")]
    private static partial Regex LiteralDeResultado();

    [GeneratedRegex("<(script|style)\\b.*?</\\1>", RegexOptions.Singleline)]
    private static partial Regex ScriptOuEstilo();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Etiqueta();

    [GeneratedRegex("\\btitle=\"([^\"]*)\"")]
    private static partial Regex Titulo();

    static string HtmlDaApresentacao() => File.ReadAllText(Path.Combine(Raiz(), "src", "Nexora", "Assets", "Apresentacao", "index.html"));

    static List<(string Pt, string En)> DicionarioDaApresentacao(string html)
    {
        var inicio = html.IndexOf("const EN = {", StringComparison.Ordinal);
        var fim = html.IndexOf("\n  };", inicio, StringComparison.Ordinal);
        Assert.True(inicio >= 0 && fim > inicio, "Não achei o dicionário EN na apresentação.");
        return ParDoDicionario().Matches(html[inicio..fim]).Select(m => (Regex.Unescape(m.Groups[1].Value), Regex.Unescape(m.Groups[2].Value))).ToList();
    }

    static IEnumerable<string> ArgumentosDeTraducao(string script)
    {
        foreach (Match m in ChamadaDeTraducao().Matches(script))
        {
            var profundidade = 1;
            var i = m.Index + m.Length;
            var comeco = i;
            for (; i < script.Length && profundidade > 0; i++)
            {
                switch (script[i])
                {
                    case '"':
                        for (i++; i < script.Length && script[i] != '"'; i++)
                            if (script[i] == '\\') i++;
                        break;
                    case '(':
                        profundidade++;
                        break;
                    case ')':
                        profundidade--;
                        break;
                }
            }
            yield return script[comeco..(i - 1)];
        }
    }

    static IEnumerable<string> UsadosNaApresentacao(string html)
    {
        var script = html[(html.IndexOf("const idioma", StringComparison.Ordinal))..];
        var dicionario = script.IndexOf("const EN = {", StringComparison.Ordinal);
        var fimDicionario = script.IndexOf("\n  };", dicionario, StringComparison.Ordinal);
        script = script.Remove(dicionario, fimDicionario - dicionario);
        foreach (var argumento in ArgumentosDeTraducao(script))
            foreach (Match m in LiteralDeResultado().Matches("(" + argumento))
                yield return Regex.Unescape(m.Groups[1].Value);

        var corpo = html[html.IndexOf("<body>", StringComparison.Ordinal)..];
        corpo = ScriptOuEstilo().Replace(corpo, "");
        foreach (Match m in Titulo().Matches(corpo)) yield return System.Net.WebUtility.HtmlDecode(m.Groups[1].Value);
        foreach (var trecho in Etiqueta().Replace(corpo, "\n").Split('\n'))
        {
            var texto = System.Net.WebUtility.HtmlDecode(trecho).Trim();
            if (texto.Length == 0 || !texto.Any(char.IsLetter) || texto.StartsWith("Ctrl+", StringComparison.Ordinal) || SemTraducao.Contains(texto)) continue;
            yield return texto;
        }
        yield return System.Net.WebUtility.HtmlDecode(Regex.Match(html, "<title>(.*?)</title>").Groups[1].Value);
    }

    [Fact]
    public void ApresentacaoTemInglesParaTodoTexto()
    {
        var html = HtmlDaApresentacao();
        var ingles = DicionarioDaApresentacao(html).Select(p => p.Pt).ToHashSet(StringComparer.Ordinal);
        var faltando = UsadosNaApresentacao(html).Where(t => !ingles.Contains(t)).Distinct().ToList();
        Assert.True(faltando.Count == 0, "Apresentação sem tradução:\n" + string.Join("\n", faltando));
    }

    [Fact]
    public void ApresentacaoTemDicionarioConsistenteComOCodigo()
    {
        var pares = DicionarioDaApresentacao(HtmlDaApresentacao());
        Assert.True(pares.Count > 100, "O dicionário da apresentação veio quase vazio.");
        var problemas = new List<string>();
        foreach (var grupo in pares.GroupBy(p => p.Pt, StringComparer.Ordinal))
        {
            if (grupo.Count() > 1) problemas.Add($"chave repetida: {grupo.Key}");
            var pt = Marcador().Matches(grupo.Key).Select(m => m.Groups[1].Value).Order().ToList();
            foreach (var (_, en) in grupo)
            {
                if (string.IsNullOrWhiteSpace(en)) problemas.Add($"tradução vazia: {grupo.Key}");
                if (!pt.SequenceEqual(Marcador().Matches(en).Select(m => m.Groups[1].Value).Order())) problemas.Add($"marcadores diferentes: {grupo.Key}");
            }
        }
        var doCodigo = Ingles.Pares.GroupBy(p => p.Pt, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().En, StringComparer.Ordinal);
        foreach (var (pt, en) in pares)
            if (doCodigo.TryGetValue(pt, out var versao) && versao != en) problemas.Add($"a apresentação e o código traduzem diferente: {pt}");
        Assert.True(problemas.Count == 0, string.Join("\n", problemas));
    }
}

