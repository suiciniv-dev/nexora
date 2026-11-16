using System.Text.RegularExpressions;

namespace Nexora.Core.Desempenho;

public static partial class Pilha
{
    public const int MaximoDoNome = 160;

    [GeneratedRegex(@"\+<(?<m>[^<>+]+)>d__[\d_]+(\.MoveNext)?$")]
    private static partial Regex MaquinaDeEstado();

    [GeneratedRegex(@"\+<>c(__DisplayClass[\d_]+)?(`\d+)?(?=[.<\[]|$)")]
    private static partial Regex ClasseDeLambda();

    [GeneratedRegex(@"<(?<m>[^<>]+)>g__(?<l>[^|<>]+)\|[\d_]+")]
    private static partial Regex FuncaoLocal();

    [GeneratedRegex(@"<(?<m>[^<>]+)>b__[\d_]+")]
    private static partial Regex Lambda();

    [GeneratedRegex(@"`\d+")]
    private static partial Regex Aridade();

    [GeneratedRegex(@"<[^<>]*>|\[[^\[\]]*\]")]
    private static partial Regex ArgumentosGenericos();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)+$")]
    private static partial Regex SoIdentificadores();

    public static bool EhNomeDeCodigo(string nome) => nome.Length <= MaximoDoNome && SoIdentificadores().IsMatch(nome);

    public static string Limpar(string tipo, string metodo)
    {
        var nome = $"{tipo}.{metodo switch { ".ctor" => "ctor", ".cctor" => "cctor", _ => metodo }}";
        nome = MaquinaDeEstado().Replace(nome, ".${m}");
        nome = ClasseDeLambda().Replace(nome, "");
        nome = FuncaoLocal().Replace(nome, "${m}.${l}");
        nome = Lambda().Replace(nome, "${m}");
        nome = Aridade().Replace(nome, "");
        for (var antes = ""; antes != nome;)
        {
            antes = nome;
            nome = ArgumentosGenericos().Replace(nome, "");
        }
        nome = nome.Replace('+', '.');
        while (nome.Contains("..")) nome = nome.Replace("..", ".");
        return nome.Trim('.');
    }

    public static string? Ponta(IReadOnlyList<string> quadros) => quadros.FirstOrDefault(EhNomeDeCodigo);

    public static string? Causa(IReadOnlyList<string> quadros) =>
        quadros.FirstOrDefault(q => q.StartsWith("Nexora.", StringComparison.Ordinal) && EhNomeDeCodigo(q))
        ?? quadros.FirstOrDefault(q => !q.StartsWith("System.", StringComparison.Ordinal) && EhNomeDeCodigo(q))
        ?? Ponta(quadros);
}
