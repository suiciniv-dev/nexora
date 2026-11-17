using System.Text.RegularExpressions;

namespace Nexora.Testes;

public sealed partial class ArquiteturaTestes
{
    static readonly string App = Path.Combine(Raiz(), "src", "Nexora");

    static string Raiz()
    {
        for (var pasta = new DirectoryInfo(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
            if (Directory.Exists(Path.Combine(pasta.FullName, "src", "Nexora")) && Directory.Exists(Path.Combine(pasta.FullName, "tests")))
                return pasta.FullName;
        throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
    }

    static IEnumerable<string> Arquivos(string pasta, string padrao) =>
        Directory.EnumerateFiles(Path.Combine(App, pasta), padrao, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    static string Relativo(string arquivo) => Path.GetRelativePath(App, arquivo).Replace('\\', '/');

    [GeneratedRegex(@"^(?:public |internal )?(?:sealed |static |abstract |readonly )*(?:partial )?(?:class|record struct|record|enum|interface|struct) (\w+)", RegexOptions.Multiline)]
    private static partial Regex Tipo();

    [GeneratedRegex(@"Selector=""([^""]*)""")]
    private static partial Regex Seletor();

    [GeneratedRegex(@"(?<![\w|:#.(-])Border\.[A-Za-z]")]
    private static partial Regex BorderComClasse();

    [Fact]
    public void Nenhum_estilo_mira_Border_com_classe()
    {
        var errados = Arquivos(".", "*.axaml")
            .SelectMany(f => Seletor().Matches(File.ReadAllText(f)).Select(m => (Arquivo: Relativo(f), Seletor: m.Groups[1].Value)))
            .Where(s => BorderComClasse().IsMatch(s.Seletor))
            .Select(s => $"{s.Arquivo}: {s.Seletor}")
            .ToList();
        Assert.True(errados.Count == 0, "Use c|Caixa.classe em vez de Border.classe:\n" + string.Join('\n', errados));
    }

    [Fact]
    public void ViewModels_so_tem_view_models()
    {
        var errados = Arquivos("ViewModels", "*.cs")
            .SelectMany(f => Tipo().Matches(File.ReadAllText(f)).Select(m => (Arquivo: Relativo(f), Nome: m.Groups[1].Value)))
            .Where(t => !t.Nome.EndsWith("VM") && !t.Nome.StartsWith("Item") && !Regex.IsMatch(t.Nome, "^I[A-Z]") && t.Nome != "TemplatesDoApp")
            .Select(t => $"{t.Arquivo}: {t.Nome}")
            .ToList();
        Assert.True(errados.Count == 0, "Tipos fora do lugar em ViewModels (vão para Modelos ou Servicos):\n" + string.Join('\n', errados));
    }

    [Theory]
    [InlineData("Modelos")]
    [InlineData("Servicos")]
    [InlineData("Controles")]
    public void Camadas_de_baixo_nao_conhecem_telas_nem_view_models(string pasta)
    {
        var errados = Arquivos(pasta, "*.cs")
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"\bNexora\.(ViewModels|Views)\b"))
            .Select(Relativo)
            .ToList();
        Assert.True(errados.Count == 0, $"{pasta} não pode usar Nexora.ViewModels nem Nexora.Views:\n" + string.Join('\n', errados));
    }

    [Fact]
    public void Um_tipo_por_arquivo_com_o_nome_do_tipo()
    {
        var errados = new List<string>();
        foreach (var arquivo in Arquivos(".", "*.cs"))
        {
            var tipos = Tipo().Matches(File.ReadAllText(arquivo)).Select(m => m.Groups[1].Value).ToList();
            if (tipos.Count == 0) continue;
            var nome = Path.GetFileName(arquivo).Split('.')[0];
            if (tipos.Count > 1) errados.Add($"{Relativo(arquivo)}: {tipos.Count} tipos ({string.Join(", ", tipos)})");
            else if (tipos[0] != nome) errados.Add($"{Relativo(arquivo)}: o tipo se chama {tipos[0]}");
        }
        Assert.True(errados.Count == 0, "Um tipo por arquivo, com o nome do tipo:\n" + string.Join('\n', errados));
    }

    [Fact]
    public void Raiz_do_app_so_tem_a_entrada()
    {
        var soltos = Directory.EnumerateFiles(App, "*.cs").Select(Path.GetFileName).Order().ToList();
        Assert.Equal(["App.axaml.cs", "Program.cs"], soltos);
    }
}
