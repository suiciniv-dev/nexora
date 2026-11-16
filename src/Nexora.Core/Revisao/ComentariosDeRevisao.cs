using System.Text;
using Nexora.Core.Git;
using Nexora.Core.Senna;
using Nexora.Core.Texto;

namespace Nexora.Core.Revisao;

public sealed record ComentarioDeLinha(string Arquivo, int Inicio, int Fim, bool Antiga, string Trecho, string Texto);

public enum DestinoDaRevisao { Original, Nova }

public sealed record SituacaoDaOriginal(bool Existe, DateTime? UltimaAtividade, double Contexto, DateTimeOffset? PassagemEm);

public static class ComentariosDeRevisao
{
    public static readonly TimeSpan ParadaDepoisDe = TimeSpan.FromHours(8);
    const int LinhasDoTrecho = 8;

    public static string Faixa(int inicio, int fim, bool antiga = false)
    {
        var faixa = inicio == fim ? Idioma.F("linha {0}", inicio) : Idioma.F("linhas {0}-{1}", Math.Min(inicio, fim), Math.Max(inicio, fim));
        return antiga ? Idioma.F("{0} da versão antiga", faixa) : faixa;
    }

    public static ComentarioDeLinha? Comentario(string arquivo, IReadOnlyList<LinhaDiff> linhas, int de, int ate, string texto)
    {
        if (linhas.Count == 0) return null;
        var (a, b) = (Math.Clamp(Math.Min(de, ate), 0, linhas.Count - 1), Math.Clamp(Math.Max(de, ate), 0, linhas.Count - 1));
        var faixa = linhas.Skip(a).Take(b - a + 1).Where(l => l.Tipo != TipoLinha.Cabecalho).ToList();
        if (faixa.Count == 0) return null;
        var novas = faixa.Select(l => l.Nova).OfType<int>().ToList();
        var antigas = faixa.Select(l => l.Antiga).OfType<int>().ToList();
        var usaAntiga = novas.Count == 0;
        var numeros = usaAntiga ? antigas : novas;
        if (numeros.Count == 0) return null;
        var trecho = string.Join("\n", faixa.Select(l => (l.Tipo switch { TipoLinha.Adicionada => "+", TipoLinha.Removida => "-", _ => " " }) + l.Texto));
        return new ComentarioDeLinha(arquivo, numeros.Min(), numeros.Max(), usaAntiga, trecho, texto.Trim());
    }

    public static string Linguagem(string arquivo) => Path.GetExtension(arquivo).TrimStart('.').ToLowerInvariant() switch
    {
        "cs" => "csharp",
        "ts" or "tsx" => "ts",
        "js" or "jsx" or "mjs" => "js",
        "py" => "python",
        "kt" or "kts" => "kotlin",
        "md" => "markdown",
        "axaml" or "xaml" or "csproj" => "xml",
        "yml" => "yaml",
        "" => "",
        var e => e,
    };

    static void Bloco(StringBuilder sb, string trecho, string linguagem)
    {
        if (trecho.Trim().Length == 0) return;
        var cerca = trecho.Contains("```", StringComparison.Ordinal) ? "````" : "```";
        sb.Append("   ").Append(cerca).Append(linguagem).Append('\n');
        foreach (var linha in trecho.Replace("\r", "").Split('\n')) sb.Append("   ").Append(linha).Append('\n');
        sb.Append("   ").Append(cerca).Append('\n');
    }

    static void Corpo(StringBuilder sb, string texto)
    {
        foreach (var linha in texto.Replace("\r", "").Trim().Split('\n')) sb.Append("   ").Append(linha).Append('\n');
    }

    public static string PedidoDoDiff(IReadOnlyList<ComentarioDeLinha> comentarios)
    {
        var sb = new StringBuilder();
        sb.Append(comentarios.Count == 1
            ? Idioma.T("Revisei o diff e deixei 1 comentário. Ajuste o código conforme ele e me diga o que mudou.")
            : Idioma.F("Revisei o diff e deixei {0} comentários. Ajuste o código conforme cada um e me diga o que mudou.", comentarios.Count));
        sb.Append("\n\n");
        var n = 0;
        foreach (var c in comentarios.OrderBy(c => c.Arquivo, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Inicio))
        {
            sb.Append(++n).Append(". `").Append(c.Arquivo).Append("` (").Append(Faixa(c.Inicio, c.Fim, c.Antiga)).Append("):\n");
            Bloco(sb, c.Trecho, "diff");
            Corpo(sb, c.Texto);
            sb.Append('\n');
        }
        return sb.ToString().TrimEnd();
    }

    public static string UltimasLinhasDoHunk(string hunk, int maximo = LinhasDoTrecho)
    {
        var linhas = hunk.Replace("\r", "").Split('\n').Where(l => !l.StartsWith("@@", StringComparison.Ordinal)).ToList();
        while (linhas.Count > 0 && linhas[^1].Length == 0) linhas.RemoveAt(linhas.Count - 1);
        return string.Join("\n", linhas.Skip(Math.Max(0, linhas.Count - maximo)));
    }

    public static string PedidoDoPr(DetalhePr pr, IReadOnlyList<ConversaDoPr> conversas, IReadOnlyList<ComentarioDoPr> gerais, IReadOnlyList<RevisaoDoPr> revisoes,
        DestinoDaRevisao destino, Passagem? passagem)
    {
        var sb = new StringBuilder();
        if (destino == DestinoDaRevisao.Nova)
        {
            if (passagem is not null)
                sb.Append(Idioma.F("Retome o handoff \"{0}\".", passagem.Titulo)).Append(' ')
                    .Append(Idioma.F("Depois, cuide dos comentários de revisão do PR #{0} abaixo, que chegaram depois da passagem.", pr.Numero));
            else
                sb.Append(Idioma.F("Esta conversa é nova e não tem o contexto da branch {0}. Antes de mexer, leia o PR #{1} com `gh pr view {1}` e o diff com `gh pr diff {1}`.",
                    pr.HeadBranch, pr.Numero));
            sb.Append("\n\n");
        }
        sb.Append(Idioma.F("Resolva estes comentários de revisão do PR #{0}. Mude o código, rode os testes, faça o commit e o push. Não responda nem comente no PR e não marque as conversas como resolvidas: só o código muda.", pr.Numero));
        sb.Append("\n\n");
        var n = 0;
        foreach (var c in conversas.OrderBy(c => c.Arquivo, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Linha ?? 0))
        {
            var faixa = c.Linha is { } l ? Faixa(c.Inicio ?? l, l) : Idioma.T("arquivo");
            sb.Append(++n).Append(". `").Append(c.Arquivo).Append("` (").Append(faixa).Append("), ").Append(Idioma.F("de {0}", c.Primeiro?.Autor ?? "?")).Append(":\n");
            Bloco(sb, UltimasLinhasDoHunk(c.Trecho), "diff");
            Corpo(sb, c.Primeiro?.Corpo ?? "");
            foreach (var resposta in c.Comentarios.Skip(1)) Corpo(sb, Idioma.F("Resposta de {0}: {1}", resposta.Autor, resposta.Corpo.Trim()));
            sb.Append('\n');
        }
        foreach (var r in revisoes.Where(r => r.Corpo.Trim().Length > 0))
        {
            sb.Append(++n).Append(". ").Append(Idioma.F("Revisão de {0}:", r.Autor)).Append('\n');
            Corpo(sb, r.Corpo);
            sb.Append('\n');
        }
        foreach (var g in gerais.Where(g => g.Corpo.Trim().Length > 0))
        {
            sb.Append(++n).Append(". ").Append(Idioma.F("Comentário geral de {0}:", g.Autor)).Append('\n');
            Corpo(sb, g.Corpo);
            sb.Append('\n');
        }
        return sb.ToString().TrimEnd();
    }

    public static (DestinoDaRevisao Destino, string Motivo) Padrao(SituacaoDaOriginal original, bool temPassagem, DateTime agora)
    {
        if (!original.Existe || original.UltimaAtividade is not { } ultima)
            return (DestinoDaRevisao.Nova, temPassagem
                ? Idioma.T("Não há conversa desta branch com histórico aqui. A nova começa pela passagem.")
                : Idioma.T("Não há conversa desta branch com histórico aqui. A nova começa lendo o PR."));
        if (original.PassagemEm is { } passagem && passagem.LocalDateTime >= ultima.AddMinutes(-30))
            return (DestinoDaRevisao.Nova, Idioma.T("A passagem desta branch já foi feita, então a conversa original pode estar sem contexto."));
        if (agora - ultima > ParadaDepoisDe)
            return (DestinoDaRevisao.Nova, Idioma.F("A conversa original está parada há {0}.", Tempo.Duracao((long)(agora - ultima).TotalMilliseconds)));
        if (original.Contexto >= 0.8)
            return (DestinoDaRevisao.Nova, Idioma.T("O contexto da conversa original está quase cheio."));
        return (DestinoDaRevisao.Original, Idioma.T("A conversa original está ativa e ainda tem o contexto da branch."));
    }

    public static string? ExtrairArquivo(string diff, string arquivo)
    {
        var alvo = arquivo.Replace('\\', '/').TrimStart('/');
        var sb = new StringBuilder();
        var dentro = false;
        foreach (var linha in diff.Replace("\r", "").Split('\n'))
        {
            if (linha.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                if (dentro) break;
                dentro = linha.EndsWith(" b/" + alvo, StringComparison.Ordinal) || linha.Contains(" a/" + alvo + " ", StringComparison.Ordinal);
            }
            if (dentro) sb.Append(linha).Append('\n');
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }

    public static string? AgenteDoLogin(string? login)
    {
        if (string.IsNullOrWhiteSpace(login)) return null;
        var l = login.ToLowerInvariant();
        return l.Contains("claude", StringComparison.Ordinal) || l.Contains("anthropic", StringComparison.Ordinal) ? "claude"
            : l.Contains("codex", StringComparison.Ordinal) || l.Contains("chatgpt", StringComparison.Ordinal) || l.Contains("openai", StringComparison.Ordinal) ? "codex"
            : l.Contains("copilot", StringComparison.Ordinal) ? "copilot"
            : l.Contains("gemini", StringComparison.Ordinal) ? "gemini"
            : l.Contains("cursor", StringComparison.Ordinal) ? "cursor"
            : null;
    }

    public static string Iniciais(string? login)
    {
        var limpo = (login ?? "").Replace("[bot]", "", StringComparison.OrdinalIgnoreCase);
        var partes = limpo.Split(['-', '_', '.', ' '], StringSplitOptions.RemoveEmptyEntries);
        return partes.Length switch
        {
            0 => "?",
            1 => partes[0][..1].ToUpperInvariant(),
            _ => (partes[0][..1] + partes[1][..1]).ToUpperInvariant(),
        };
    }
}
