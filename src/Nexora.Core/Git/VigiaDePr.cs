using System.Globalization;
using System.Text;
using System.Text.Json;
using Nexora.Core.Texto;

namespace Nexora.Core.Git;

public sealed record ComentarioPr(string Id, string Autor, string Texto, string? Arquivo, int? Linha, DateTimeOffset? Quando, string? Url);

public sealed record RetratoPr(int Numero, string Sha, bool Aberto, IReadOnlyList<string> ChecksFalhos, bool Conflito, IReadOnlyList<ComentarioPr> Comentarios);

public enum TipoEventoPr
{
    CheckFalhou,
    Conflito,
    Comentarios,
}

public sealed record EventoPr(TipoEventoPr Tipo, int Numero, IReadOnlyList<string> Chaves, IReadOnlyList<string> Checks, IReadOnlyList<ComentarioPr> Comentarios);

public static class VigiaDePr
{
    public const string Desligado = "desligado";
    public const string Avisar = "avisar";
    public const string Agir = "agir";
    public const string AgirEResponder = "agir-e-responder";

    public static bool EhRobo(string autor) =>
        autor.EndsWith("[bot]", StringComparison.OrdinalIgnoreCase) || autor.Equals("github-actions", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<EventoPr> Comparar(RetratoPr agora, string? eu, Func<string, bool> visto, bool primeiraVez)
    {
        var eventos = new List<EventoPr>();
        if (!agora.Aberto) return eventos;
        var falhos = agora.ChecksFalhos.Where(c => !visto($"check:{agora.Sha}:{c}")).ToList();
        if (falhos.Count > 0)
            eventos.Add(new EventoPr(TipoEventoPr.CheckFalhou, agora.Numero, [.. falhos.Select(c => $"check:{agora.Sha}:{c}")], falhos, []));
        if (agora.Conflito && !visto($"conflito:{agora.Sha}"))
            eventos.Add(new EventoPr(TipoEventoPr.Conflito, agora.Numero, [$"conflito:{agora.Sha}"], [], []));
        var novos = agora.Comentarios
            .Where(c => !visto($"comentario:{c.Id}"))
            .Where(c => !EhRobo(c.Autor) && !string.Equals(c.Autor, eu, StringComparison.OrdinalIgnoreCase) && c.Texto.Trim().Length > 0)
            .ToList();
        if (novos.Count > 0)
        {
            var chaves = novos.Select(c => $"comentario:{c.Id}").ToList();
            eventos.Add(primeiraVez
                ? new EventoPr(TipoEventoPr.Comentarios, agora.Numero, chaves, [], [])
                : new EventoPr(TipoEventoPr.Comentarios, agora.Numero, chaves, [], novos));
        }
        return eventos;
    }

    public static string Pedido(EventoPr evento, string modo)
    {
        switch (evento.Tipo)
        {
            case TipoEventoPr.CheckFalhou:
                return Idioma.F("O PR #{0} tem checks falhando: {1}. Veja o log com `gh pr checks {0}` e `gh run view --log-failed`, corrija a causa, rode os testes e faça commit e push.",
                    evento.Numero, string.Join(", ", evento.Checks));
            case TipoEventoPr.Conflito:
                return Idioma.F("O PR #{0} está com conflito com a branch base. Traga as mudanças da base do jeito que o projeto costuma fazer, resolva os conflitos, rode os testes e faça push.", evento.Numero);
            default:
                var texto = new StringBuilder(Idioma.F("Chegaram comentários de revisão no PR #{0}:", evento.Numero)).Append('\n');
                foreach (var c in evento.Comentarios)
                {
                    var onde = c.Arquivo is { Length: > 0 } arquivo ? c.Linha is { } linha ? $" ({arquivo}:{linha.ToString(CultureInfo.InvariantCulture)})" : $" ({arquivo})" : "";
                    texto.Append("\n- @").Append(c.Autor).Append(onde).Append(": ").Append(c.Texto.Trim().Replace("\r", "").Replace("\n", "\n  "));
                }
                texto.Append("\n\n").Append(modo == AgirEResponder
                    ? Idioma.T("Faça as mudanças pedidas, rode os testes e faça commit e push. Depois responda cada comentário no GitHub dizendo o que mudou.")
                    : Idioma.T("Faça as mudanças pedidas, rode os testes e faça commit e push. Não responda aos comentários no GitHub."));
                return texto.ToString();
        }
    }

    public static string Aviso(EventoPr evento) => evento.Tipo switch
    {
        TipoEventoPr.CheckFalhou => evento.Checks.Count == 1
            ? Idioma.F("PR #{0}: o check {1} falhou", evento.Numero, evento.Checks[0])
            : Idioma.F("PR #{0}: {1} checks falharam", evento.Numero, evento.Checks.Count),
        TipoEventoPr.Conflito => Idioma.F("PR #{0}: conflito com a branch base", evento.Numero),
        _ => evento.Comentarios.Count == 1
            ? Idioma.F("PR #{0}: comentário novo de @{1}", evento.Numero, evento.Comentarios[0].Autor)
            : Idioma.F("PR #{0}: {1} comentários novos de revisão", evento.Numero, evento.Comentarios.Count),
    };

    public static RetratoPr RetratoDe(DetalhePr pr)
    {
        var comentarios = new List<ComentarioPr>();
        comentarios.AddRange(pr.Revisoes.Where(r => r.Corpo.Trim().Length > 0).Select(r => new ComentarioPr("r" + r.Id, r.Autor, r.Corpo, null, null, r.Quando, null)));
        comentarios.AddRange(pr.Comentarios.Select(c => new ComentarioPr("c" + c.Id, c.Autor, c.Corpo, null, null, c.Quando, c.Url)));
        foreach (var conversa in pr.Conversas.Where(c => !c.Resolvida))
            comentarios.AddRange(conversa.Comentarios.Select(c => new ComentarioPr("l" + c.Id, c.Autor, c.Corpo, conversa.Arquivo, conversa.Linha, c.Quando, c.Url)));
        return new RetratoPr(pr.Numero, pr.ShaDoHead, pr.Aberto,
            [.. pr.Checks.Where(c => c.Estado == EstadoCheck.Falhou).Select(c => c.Chave).Distinct()],
            pr.TemConflito, [.. comentarios.OrderBy(c => c.Quando ?? DateTimeOffset.MinValue)]);
    }

    public static async Task<RetratoPr?> BuscarAsync(string pasta, string branch) =>
        (await GitHub.DetalhePrAsync(pasta, branch)).Pr is { } pr ? RetratoDe(pr) : null;
}
