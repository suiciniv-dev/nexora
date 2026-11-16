using System.Text.RegularExpressions;
using Nexora.Core.Texto;
using Nexora.Traducao;

namespace Nexora.Testes;

[CollectionDefinition("Idioma", DisableParallelization = true)]
public sealed class ColecaoDoIdioma;

[Collection("Idioma")]
public sealed partial class TraducaoTestes
{
    static string Raiz()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !Directory.Exists(Path.Combine(pasta.FullName, "src", "Nexora"))) pasta = pasta.Parent;
        return pasta?.FullName ?? throw new DirectoryNotFoundException("Não achei a raiz do repositório.");
    }

    [GeneratedRegex(@"\{l:T\s+'((?:[^']|'')*)'\s*\}")]
    private static partial Regex NoXamlComAspas();

    [GeneratedRegex(@"\{l:T\s+([^'}][^}]*)\}")]
    private static partial Regex NoXamlSemAspas();

    [GeneratedRegex(@"Idioma\.[TF]\(\s*""((?:[^""\\]|\\.)*)""")]
    private static partial Regex NoCodigo();

    [GeneratedRegex(@"\{(\d+)(?:[,:][^}]*)?\}")]
    private static partial Regex Marcador();

    static IEnumerable<(string Texto, string Onde)> Usados()
    {
        var src = Path.Combine(Raiz(), "src");
        foreach (var arquivo in Directory.EnumerateFiles(src, "*.axaml", SearchOption.AllDirectories).Where(Fonte))
        {
            var conteudo = File.ReadAllText(arquivo);
            foreach (Match m in NoXamlComAspas().Matches(conteudo)) yield return (System.Net.WebUtility.HtmlDecode(m.Groups[1].Value.Replace("''", "'")), arquivo);
            foreach (Match m in NoXamlSemAspas().Matches(conteudo)) yield return (System.Net.WebUtility.HtmlDecode(m.Groups[1].Value.Trim()), arquivo);
        }
        foreach (var arquivo in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories).Where(Fonte))
            foreach (Match m in NoCodigo().Matches(File.ReadAllText(arquivo)))
                yield return (Regex.Unescape(m.Groups[1].Value), arquivo);
    }

    static bool Fonte(string arquivo) => !arquivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !arquivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}");

    [Fact]
    public void TodoTextoMarcadoTemIngles()
    {
        var ingles = Ingles.Pares.Select(p => p.Pt).ToHashSet(StringComparer.Ordinal);
        var faltando = Usados().Where(u => !ingles.Contains(u.Texto)).Select(u => $"{Path.GetFileName(u.Onde)}: {u.Texto}").Distinct().ToList();
        Assert.True(faltando.Count == 0, "Sem tradução:\n" + string.Join("\n", faltando.Take(80)));
    }

    [Fact]
    public void TraducaoTemOsMesmosMarcadoresEUmaVersaoSo()
    {
        var problemas = new List<string>();
        foreach (var grupo in Ingles.Pares.GroupBy(p => p.Pt, StringComparer.Ordinal))
        {
            if (grupo.Select(p => p.En).Distinct().Count() > 1) problemas.Add($"duas traduções para: {grupo.Key}");
            var pt = Marcador().Matches(grupo.Key).Select(m => m.Groups[1].Value).Order().ToList();
            foreach (var (_, en) in grupo)
            {
                var deEn = Marcador().Matches(en).Select(m => m.Groups[1].Value).Order().ToList();
                if (!pt.SequenceEqual(deEn)) problemas.Add($"marcadores diferentes: {grupo.Key} → {en}");
                if (string.IsNullOrWhiteSpace(en)) problemas.Add($"tradução vazia: {grupo.Key}");
            }
        }
        Assert.True(problemas.Count == 0, string.Join("\n", problemas));
    }

    [Fact]
    public void EmInglesTraduzEFormataComCulturaAmericana()
    {
        Idioma.Registrar([("{0} sessões, {1:N0} respostas", "{0} sessions, {1:N0} replies")]);
        try
        {
            Idioma.Definir("en");
            Assert.Equal("2 sessions, 1,515 replies", Idioma.F("{0} sessões, {1:N0} respostas", 2, 1515));
            Assert.Equal("Texto sem tradução", Idioma.T("Texto sem tradução"));
        }
        finally
        {
            Idioma.Definir("pt");
        }
        Assert.Equal("2 sessões, 1.515 respostas", Idioma.F("{0} sessões, {1:N0} respostas", 2, 1515));
    }
}
