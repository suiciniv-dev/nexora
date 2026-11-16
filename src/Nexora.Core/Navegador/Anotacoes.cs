using System.Globalization;
using System.Text;
using System.Text.Json;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.Core.Navegador;

public sealed record Anotacao(
    int Id, string Seletor, string Tag, string Texto, string Html,
    int X, int Y, int Largura, int Altura, int LarguraJanela, int AlturaJanela, string Comentario, string Url = "")
{
    public string Resumo => Texto.Length > 0 ? Resumos.UmaLinha(Texto, 60) : Seletor;
}

public static class Anotacoes
{
    public const int MaximoHtml = 400;
    public const int MaximoTexto = 200;

    public static Anotacao? Ler(JsonElement m)
    {
        if (m.ValueKind != JsonValueKind.Object) return null;
        var seletor = Protocolo.Texto(m, "seletor")?.Trim();
        var comentario = Protocolo.Texto(m, "comentario")?.Trim();
        if (string.IsNullOrEmpty(seletor) || string.IsNullOrEmpty(comentario) || Inteiro(m, "id") is not { } id) return null;
        return new Anotacao(
            id,
            Resumos.Limitar(seletor, 300),
            Protocolo.Texto(m, "tag")?.ToLowerInvariant() ?? "",
            Resumos.UmaLinha(System.Text.RegularExpressions.Regex.Replace(Protocolo.Texto(m, "texto") ?? "", @"\s+", " ").Trim(), MaximoTexto),
            Resumos.Limitar((Protocolo.Texto(m, "html") ?? "").Trim(), MaximoHtml),
            Inteiro(m, "x") ?? 0, Inteiro(m, "y") ?? 0, Inteiro(m, "largura") ?? 0, Inteiro(m, "altura") ?? 0,
            Inteiro(m, "larguraJanela") ?? 0, Inteiro(m, "alturaJanela") ?? 0,
            Resumos.Limitar(comentario, 2000),
            Protocolo.Texto(m, "url") ?? "");
    }

    static int? Inteiro(JsonElement m, string nome) =>
        m.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) && double.IsFinite(d)
            ? (int)Math.Round(Math.Clamp(d, int.MinValue, int.MaxValue))
            : null;

    public static bool MesmaPagina(string a, string b)
    {
        static string Sem(string u) => u.Split('#')[0].TrimEnd('/');
        return string.Equals(Sem(a), Sem(b), StringComparison.OrdinalIgnoreCase);
    }

    public static string Mensagem(string url, IReadOnlyList<Anotacao> anotacoes, bool comPrint)
    {
        var s = new StringBuilder();
        s.AppendLine(comPrint
            ? Idioma.F("Anotações na página {0}. O print anexo mostra os marcadores numerados.", url)
            : Idioma.F("Anotações na página {0}.", url));
        if (anotacoes.FirstOrDefault(a => a.LarguraJanela > 0) is { } primeira)
            s.AppendLine(Idioma.F("Janela do navegador: {0}×{1} px.", primeira.LarguraJanela, primeira.AlturaJanela));
        for (var i = 0; i < anotacoes.Count; i++)
        {
            var a = anotacoes[i];
            s.AppendLine();
            s.Append(CultureInfo.InvariantCulture, $"{i + 1}. {a.Comentario.Replace("\n", "\n   ")}").AppendLine();
            if (a.Url.Length > 0 && !MesmaPagina(a.Url, url)) s.Append("   ").Append(Idioma.T("Página:")).Append(' ').AppendLine(a.Url);
            s.Append("   ").Append(Idioma.T("Elemento:")).Append(" `").Append(a.Seletor).AppendLine("`");
            if (a.Texto.Length > 0) s.Append("   ").Append(Idioma.T("Texto:")).Append(" \"").Append(a.Texto).AppendLine("\"");
            s.Append("   ").AppendLine(Idioma.F("Tamanho: {0}×{1} px, na posição ({2}, {3}) da página.", a.Largura, a.Altura, a.X, a.Y));
            if (a.Html.Length > 0)
            {
                s.Append("   ").AppendLine(Idioma.T("HTML:"));
                s.AppendLine("   ```html");
                foreach (var linha in a.Html.Split('\n')) s.Append("   ").AppendLine(linha.TrimEnd('\r'));
                s.AppendLine("   ```");
            }
        }
        s.AppendLine();
        s.Append(Idioma.T("Ache esses elementos no código do projeto e faça as mudanças pedidas em cada anotação."));
        return s.ToString();
    }
}
