using System.Text;
using System.Text.Json;
using Nexora.Core.Orquestra;
using Nexora.Core.Texto;

namespace Nexora.Core.Claude;

public static class Resumos
{
    static readonly JsonSerializerOptions Indentado = new() { WriteIndented = true };

    public static string DaFerramenta(string nome, JsonElement entrada, string? pasta = null)
    {
        string? Campo(string c) => Protocolo.Texto(entrada, c);
        var resumo = nome switch
        {
            "Bash" or "PowerShell" => Campo("command"),
            "Read" or "Write" or "Edit" or "MultiEdit" => Relativo(Campo("file_path"), pasta),
            "NotebookEdit" => Relativo(Campo("notebook_path"), pasta),
            "Glob" => Campo("pattern"),
            "Grep" => Campo("pattern") is { } p ? Campo("path") is { } onde ? Idioma.F("{0}  em {1}", p, Relativo(onde, pasta)) : p : null,
            "WebFetch" => Campo("url"),
            "WebSearch" or "ToolSearch" => Campo("query"),
            "Task" or "Agent" => Campo("description"),
            "Skill" => Campo("skill"),
            "TodoWrite" => Idioma.T("lista de tarefas"),
            Ferramentas.Prefixo + Ferramentas.CriarAgente => Campo("titulo") ?? Campo("tarefa") ?? Campo("mensagem"),
            Ferramentas.Prefixo + Ferramentas.EnviarMensagem => $"{Campo("id")}: {Campo("mensagem")}",
            Ferramentas.Prefixo + Ferramentas.ResponderPermissao => $"{Campo("id")}: {Campo("decisao")}",
            Ferramentas.Prefixo + Ferramentas.Esperar => Protocolo.Itens(Protocolo.Objeto(entrada, "ids")).Select(i => i.GetString()).ToList() is { Count: > 0 } ids
                ? string.Join(", ", ids)
                : Idioma.T("os agentes"),
            _ => PrimeiroTexto(entrada),
        };
        return UmaLinha(resumo ?? "", 140);
    }

    public static string? DetalheDaPermissao(string nome, JsonElement entrada, string? pasta = null)
    {
        string? Campo(string c) => Protocolo.Texto(entrada, c);
        return nome switch
        {
            "Bash" or "PowerShell" => Campo("command"),
            "Write" => $"{Relativo(Campo("file_path"), pasta)}\n\n{Linhas(Campo("content"), 24)}",
            "Edit" => $"{Relativo(Campo("file_path"), pasta)}\n\n{Prefixar(Linhas(Campo("old_string"), 12), "- ")}\n{Prefixar(Linhas(Campo("new_string"), 12), "+ ")}",
            "ExitPlanMode" or "AskUserQuestion" => null,
            _ => entrada.ValueKind == JsonValueKind.Object ? Limitar(JsonSerializer.Serialize(entrada, Indentado), 2000) : null,
        };
    }

    public static string? Relativo(string? caminho, string? pasta)
    {
        if (string.IsNullOrEmpty(caminho) || string.IsNullOrEmpty(pasta)) return caminho;
        try
        {
            var completo = Path.GetFullPath(caminho);
            var raiz = Path.GetFullPath(pasta);
            return completo.StartsWith(raiz, StringComparison.OrdinalIgnoreCase) ? Path.GetRelativePath(raiz, completo) : caminho;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return caminho;
        }
    }

    public static string NomeDaFerramenta(string nome)
    {
        if (!nome.StartsWith("mcp__", StringComparison.Ordinal)) return nome;
        var partes = nome["mcp__".Length..].Split("__", 2);
        if (partes.Length < 2) return nome;
        var servidor = partes[0] is ServidorMcp.Nome or Navegador.FerramentasDoNavegador.Servidor ? "Nexora" : partes[0];
        return $"{servidor} · {partes[1].Replace('_', ' ')}";
    }

    public static string UmaLinha(string texto, int maximo)
    {
        var linha = texto.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
        return linha.Length > maximo ? linha[..(maximo - 1)] + "…" : linha;
    }

    public static string ResultadoDeSubagente(string conteudo)
    {
        var texto = System.Text.RegularExpressions.Regex.Replace(conteudo, "<usage>.*?</usage>", "", System.Text.RegularExpressions.RegexOptions.Singleline);
        var linhas = texto.Replace("\r", "").TrimEnd().Split('\n').ToList();
        while (linhas.Count > 0 && (string.IsNullOrWhiteSpace(linhas[^1]) || linhas[^1].TrimStart().StartsWith("agentId:", StringComparison.Ordinal)))
            linhas.RemoveAt(linhas.Count - 1);
        return string.Join("\n", linhas).Trim();
    }

    public static (int? Ferramentas, long? DuracaoMs) UsoDoSubagente(string conteudo)
    {
        var uso = System.Text.RegularExpressions.Regex.Match(conteudo, "<usage>(.*?)</usage>", System.Text.RegularExpressions.RegexOptions.Singleline);
        if (!uso.Success) return (null, null);
        long? Valor(string nome) =>
            System.Text.RegularExpressions.Regex.Match(uso.Groups[1].Value, nome + @":\s*(\d+)") is { Success: true } m && long.TryParse(m.Groups[1].Value, out var v) ? v : null;
        return ((int?)Valor("tool_uses"), Valor("duration_ms"));
    }

    public static string SemMarcas(string linha)
    {
        var limpa = System.Text.RegularExpressions.Regex.Replace(linha, @"^\s*(#{1,6}\s+|[-*+]\s+|\d+[.)]\s+|>\s*)", "");
        return limpa.Replace("**", "").Replace("__", "").Replace("`", "").Trim();
    }

    public static string Limitar(string texto, int maximo) =>
        texto.Length > maximo ? texto[..maximo] + "\n…" : texto;

    static string Linhas(string? texto, int maximo)
    {
        if (string.IsNullOrEmpty(texto)) return "";
        var linhas = texto.Replace("\r", "").Split('\n');
        return linhas.Length <= maximo ? texto.Replace("\r", "") : string.Join("\n", linhas.Take(maximo)) + "\n" + Idioma.F("… mais {0} linhas", linhas.Length - maximo);
    }

    static string Prefixar(string texto, string prefixo)
    {
        var sb = new StringBuilder();
        foreach (var linha in texto.Split('\n')) sb.Append(prefixo).AppendLine(linha);
        return sb.ToString().TrimEnd('\n', '\r');
    }

    static string? PrimeiroTexto(JsonElement entrada) =>
        entrada.ValueKind == JsonValueKind.Object
            && entrada.EnumerateObject().Select(p => p.Value).FirstOrDefault(v => v.ValueKind == JsonValueKind.String) is { ValueKind: JsonValueKind.String } texto
            ? texto.GetString()
            : null;
}
