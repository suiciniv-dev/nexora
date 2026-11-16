using System.Text;
using System.Text.RegularExpressions;

namespace Nexora.Core.Texto;

public sealed class TemplateDeTarefa
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Texto { get; set; } = "";

    public TemplateDeTarefa Copia() => new() { Id = Id, Nome = Nome, Texto = Texto };
}

public static partial class Templates
{
    public const string IdIssue = "issue";
    const char Marca = '\u0001';

    public static IReadOnlyList<string> Variaveis { get; } =
        ["saudacao", "tipo", "url", "numero", "titulo", "corpo", "branch", "handoff", "projeto", "links", "dias", "url2", "corpo2"];

    public static List<TemplateDeTarefa> Padroes()
    {
        var issue = Idioma.T("{saudacao} claude, {tipo}: {url}\n\n{corpo}");
        var semCommit = Idioma.T("Pode criar a branch, mas não faça commit sem o meu ok. Qualquer dúvida, me pergunte.");
        return
        [
            new() { Id = IdIssue, Nome = Idioma.T("Issue nova"), Texto = $"{issue}\n\n{Idioma.T("Analise e já ajuste.")} {semCommit}" },
            new() { Id = "issue-investigar", Nome = Idioma.T("Issue: investigar antes"), Texto = $"{issue}\n\n{Idioma.T("Analise e tente reproduzir. Me passe o que encontrou antes de mudar o código.")} {semCommit}" },
            new() { Id = "issue-levantamento", Nome = Idioma.T("Issue: levantamento"), Texto = $"{issue}\n\n{Idioma.T("Faça primeiro um levantamento: como funciona hoje, o que muda e em que fases. Me passe antes de mexer.")} {semCommit}" },
            new() { Id = "reviews", Nome = Idioma.T("Reviews do PR"), Texto = Idioma.T("{handoff}, pediram ajustes no PR.\n\n{links}\n\nNão responda o revisor, só faça os ajustes. Sem commit até o meu ok.") },
            new() { Id = "handoff", Nome = Idioma.T("Continuar handoff"), Texto = Idioma.T("{handoff}, podemos dar andamento. O que está pendente? Me passe e espere o meu ok para seguir.") },
            new() { Id = "duas-issues", Nome = Idioma.T("Duas issues no mesmo PR"), Texto = $"{Idioma.T("{saudacao} claude, temos 2 issues que vamos fazer no mesmo PR.\n\n{url}\n{corpo}\n\n{url2}\n{corpo2}")}\n\n{semCommit}" },
            new() { Id = "fluxo-issue-pr", Nome = Idioma.T("Fluxo: da issue ao PR"), Texto = string.Join($"\n\n{SeparadorDePassos}\n\n",
                $"{issue}\n\n{Idioma.T("Analise e implemente. Não faça commit ainda.")}",
                Idioma.T("Rode os testes do projeto e corrija o que falhar. Se não houver testes para o que mudou, escreva."),
                Idioma.T("Revise o diff como um revisor exigente e ajuste o que precisar."),
                Idioma.T("Faça o commit e abra o PR em rascunho, com um resumo do que mudou.")) },
            new() { Id = "fluxo-revisar", Nome = Idioma.T("Fluxo: revisar e testar"), Texto = string.Join($"\n\n{SeparadorDePassos}\n\n",
                Idioma.T("Revise as alterações não commitadas desta branch e aponte bugs e riscos."),
                Idioma.T("Corrija o que você apontou."),
                Idioma.T("Rode os testes e me diga o resultado.")) },
            new() { Id = "relatorio-prs", Nome = Idioma.T("Relatório de PRs"), Texto = Idioma.T("Analise meus PRs dos últimos {dias} dias e gere uma lista para apresentação, do maior impacto para o menor, em markdown. Separe em Novos e Melhorias/Ajustes, só com os tópicos e um emoji no início. Títulos com ###### e tópicos com -. Em cada item: título do PR, link do PR e link da tela do sistema.") },
        ];
    }

    public static (string Texto, int Cursor) Preencher(string modelo, IReadOnlyDictionary<string, string?> valores)
    {
        var faltou = false;
        var texto = Variavel().Replace(modelo.Replace("\r", ""), m =>
        {
            var nome = m.Groups[1].Value.ToLowerInvariant();
            if (!Variaveis.Contains(nome)) return m.Value;
            if (valores.TryGetValue(nome, out var valor) && !string.IsNullOrWhiteSpace(valor)) return valor.Replace("\r", "").Trim('\n');
            if (faltou) return "";
            faltou = true;
            return Marca.ToString();
        });
        var linhas = texto.Split('\n').Select(l => l.TrimEnd()).ToList();
        texto = LinhasDemais().Replace(string.Join("\n", linhas), "\n\n").Trim('\n', ' ');
        var cursor = texto.IndexOf(Marca);
        if (cursor < 0) return (texto, texto.Length);
        return (texto.Remove(cursor, 1), cursor);
    }

    public static string SeparadorDePassos => Idioma.T("--- próximo passo ---");

    public static IReadOnlyList<string> Passos(string texto)
    {
        var passos = new List<string>();
        var atual = new StringBuilder();
        foreach (var linha in texto.Replace("\r", "").Split('\n'))
        {
            if (EhSeparador(linha))
            {
                if (atual.ToString().Trim() is { Length: > 0 } passo) passos.Add(passo);
                atual.Clear();
                continue;
            }
            atual.Append(linha).Append('\n');
        }
        if (atual.ToString().Trim() is { Length: > 0 } ultimo) passos.Add(ultimo);
        return passos;
    }

    static bool EhSeparador(string linha)
    {
        var t = linha.Trim().ToLowerInvariant();
        return t is "--- próximo passo ---" or "--- proximo passo ---" or "--- next step ---";
    }

    public static string Saudacao(DateTime agora) => agora.Hour switch
    {
        >= 5 and < 12 => Idioma.T("Bom dia"),
        >= 12 and < 18 => Idioma.T("Boa tarde"),
        _ => Idioma.T("Boa noite"),
    };

    public static string Handoff(string? branch) => branch is { Length: > 0 } b ? $"Handoff: {b}" : "";

    [GeneratedRegex(@"\{([a-zA-Z0-9]+)\}")]
    private static partial Regex Variavel();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex LinhasDemais();
}
