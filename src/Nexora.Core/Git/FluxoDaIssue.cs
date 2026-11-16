using System.Globalization;
using System.Text;

namespace Nexora.Core.Git;

public enum Etapa { Andamento, Revisao }

public sealed record IssueDaSessao(int Numero, string Repositorio, string Url, string Titulo)
{
    public bool EhPr => Url.Contains("/pull/", StringComparison.Ordinal);
}

public static class FluxoDaIssue
{
    public static OpcaoStatus? Opcao(IReadOnlyList<OpcaoStatus> opcoes, Etapa etapa) =>
        opcoes.FirstOrDefault(o => Combina(Normalizar(o.Nome), etapa));

    static bool Combina(string nome, Etapa etapa) => etapa switch
    {
        Etapa.Andamento => nome.Contains("progress") || nome.Contains("andamento") || nome.Contains("doing") || nome.Contains("progresso")
            || nome.Contains("desenvolvimento") || nome is "wip" or "fazendo",
        _ => nome.Contains("review") || nome.Contains("revis"),
    };

    public static bool Avanca(IReadOnlyList<OpcaoStatus> opcoes, string? atualId, string alvoId)
    {
        var alvo = Indice(opcoes, alvoId);
        if (alvo < 0) return false;
        if (atualId is null) return true;
        var atual = Indice(opcoes, atualId);
        return atual < 0 || atual < alvo;
    }

    static int Indice(IReadOnlyList<OpcaoStatus> opcoes, string id)
    {
        for (var i = 0; i < opcoes.Count; i++)
            if (opcoes[i].Id == id) return i;
        return -1;
    }

    public static string Normalizar(string nome)
    {
        var sb = new StringBuilder();
        foreach (var c in nome.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else if (char.IsWhiteSpace(c) && sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
        }
        return sb.ToString().Trim();
    }

    public static async Task<string?> MoverAsync(string pasta, string url, Etapa etapa)
    {
        var status = await GitHub.StatusAsync(pasta, url);
        if (status.SemPermissao) return null;
        var movidos = new List<string>();
        foreach (var quadro in status.Quadros)
        {
            if (Opcao(quadro.Opcoes, etapa) is not { } alvo || !Avanca(quadro.Opcoes, quadro.AtualId, alvo.Id)) continue;
            var r = await GitHub.MudarStatusAsync(pasta, quadro, alvo.Id);
            if (r.Ok) movidos.Add($"{alvo.Nome} no quadro {quadro.Quadro}");
        }
        return movidos.Count == 0 ? null : string.Join(" e ", movidos);
    }
}
