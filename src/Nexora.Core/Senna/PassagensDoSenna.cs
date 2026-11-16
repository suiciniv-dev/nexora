using System.Text.Json;

namespace Nexora.Core.Senna;

public sealed record Passagem(string Titulo, string Projeto, DateTimeOffset Quando)
{
    const string Prefixo = "Handoff: ";

    public string Rotulo => Titulo.StartsWith(Prefixo, StringComparison.OrdinalIgnoreCase) ? Titulo[Prefixo.Length..] : Titulo;
}

public static class PassagensDoSenna
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };
    static (DateTime Em, IReadOnlyList<Passagem> Lista)? _ultima;

    public static string ArquivoDoPainel { get; set; } = @"D:\SennaDados\painel.url";

    public static bool Disponivel => File.Exists(ArquivoDoPainel);

    public static async Task<IReadOnlyList<Passagem>> ListarAsync(bool forcar = false, CancellationToken cancelar = default)
    {
        if (!forcar && _ultima is { } ultima && DateTime.UtcNow - ultima.Em < TimeSpan.FromSeconds(30)) return ultima.Lista;
        try
        {
            var painel = new Uri((await File.ReadAllTextAsync(ArquivoDoPainel, cancelar)).Trim());
            var token = System.Web.HttpUtility.ParseQueryString(painel.Query)["t"];
            if (string.IsNullOrEmpty(token)) return [];
            var endereco = $"{painel.Scheme}://{painel.Authority}/api/indice?projeto=&n=2000&t={Uri.EscapeDataString(token)}";
            using var resposta = await Http.GetAsync(endereco, cancelar);
            if (!resposta.IsSuccessStatusCode) return [];
            using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(cancelar));
            var lista = Ler(doc.RootElement);
            _ultima = (DateTime.UtcNow, lista);
            return lista;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or JsonException or UriFormatException or TaskCanceledException)
        {
            return [];
        }
    }

    public static IReadOnlyList<Passagem> Ler(JsonElement raiz)
    {
        if (!raiz.TryGetProperty("itens", out var itens) || itens.ValueKind != JsonValueKind.Array) return [];
        var lista = new List<Passagem>();
        foreach (var item in itens.EnumerateArray())
        {
            if (Texto(item, "tipo") != "handoff" || Texto(item, "titulo") is not { Length: > 0 } titulo) continue;
            var segundos = item.TryGetProperty("atualizado_em", out var em) && em.ValueKind == JsonValueKind.Number ? em.GetDouble() : 0;
            lista.Add(new Passagem(titulo, Texto(item, "projeto") ?? "", DateTimeOffset.FromUnixTimeMilliseconds((long)(segundos * 1000))));
        }
        return [.. lista.OrderByDescending(p => p.Quando)];
    }

    public static IReadOnlyList<Passagem> DoProjeto(IReadOnlyList<Passagem> todas, string? pasta)
    {
        if (string.IsNullOrWhiteSpace(pasta)) return [];
        var nome = Path.GetFileName(pasta.TrimEnd('\\', '/'));
        return [.. todas.Where(p => MesmoProjeto(p.Projeto, nome))
            .OrderByDescending(p => string.Equals(p.Projeto, nome, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(p => p.Quando)];
    }

    public static Passagem? DaBranch(IReadOnlyList<Passagem> todas, string? pasta, string? branch)
    {
        if (string.IsNullOrWhiteSpace(branch)) return null;
        var doProjeto = DoProjeto(todas, pasta);
        bool Igual(Passagem p) => string.Equals(p.Rotulo.Trim(), branch, StringComparison.OrdinalIgnoreCase);
        bool Cita(Passagem p) => p.Rotulo.Contains(branch, StringComparison.OrdinalIgnoreCase);
        return doProjeto.Where(Igual).MaxBy(p => p.Quando)
            ?? todas.Where(Igual).MaxBy(p => p.Quando)
            ?? doProjeto.Where(Cita).MaxBy(p => p.Quando);
    }

    public static IReadOnlyList<Passagem> Filtrar(IReadOnlyList<Passagem> lista, string? busca)
    {
        var palavras = SemAcento(busca ?? "").Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (palavras.Length == 0) return lista;
        return [.. lista.Where(p =>
        {
            var alvo = SemAcento($"{p.Titulo} {p.Projeto}").Replace('-', ' ').Replace('_', ' ');
            return palavras.All(palavra => alvo.Contains(palavra.Replace('-', ' ').Replace('_', ' '), StringComparison.Ordinal));
        })];
    }

    static string SemAcento(string texto)
    {
        var sb = new System.Text.StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(System.Text.NormalizationForm.FormD))
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    public static bool MesmoProjeto(string a, string b) =>
        a.Length > 0 && b.Length > 0
        && (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)
            || a.StartsWith(b + "-", StringComparison.OrdinalIgnoreCase)
            || b.StartsWith(a + "-", StringComparison.OrdinalIgnoreCase));

    static string? Texto(JsonElement e, string nome) =>
        e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
