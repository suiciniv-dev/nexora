using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Nexora.Core.Painel;

public sealed record Noticia(string Titulo, string Link, DateTimeOffset? Quando, string Fonte);

public static class Noticias
{
    public static readonly IReadOnlyList<string> Padrao =
    [
        "https://github.com/anthropics/claude-code/releases.atom",
        "https://github.com/openai/codex/releases.atom",
        "https://github.blog/changelog/feed/",
        "https://hnrss.org/frontpage?points=300",
    ];

    static readonly HttpClient Cliente = Criar();

    static HttpClient Criar()
    {
        var cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        cliente.DefaultRequestHeaders.UserAgent.ParseAdd("Nexora");
        return cliente;
    }

    public static IReadOnlyList<Noticia> Ler(string xml, string? fonte = null)
    {
        XDocument documento;
        try
        {
            documento = XDocument.Parse(xml, LoadOptions.None);
        }
        catch (XmlException)
        {
            return [];
        }
        if (documento.Root is not { } raiz) return [];
        var lista = new List<Noticia>();
        if (raiz.Name.LocalName == "feed")
        {
            var nome = fonte ?? Filho(raiz, "title")?.Value.Trim() ?? "";
            foreach (var entrada in raiz.Elements().Where(e => e.Name.LocalName == "entry"))
            {
                var link = entrada.Elements().Where(e => e.Name.LocalName == "link")
                    .OrderBy(e => (string?)e.Attribute("rel") is null or "alternate" ? 0 : 1)
                    .Select(e => (string?)e.Attribute("href")).FirstOrDefault(h => !string.IsNullOrEmpty(h)) ?? "";
                var quando = Data(Filho(entrada, "updated")?.Value ?? Filho(entrada, "published")?.Value);
                Acrescentar(lista, Filho(entrada, "title")?.Value, link, quando, nome);
            }
        }
        else if (raiz.Descendants().FirstOrDefault(e => e.Name.LocalName == "channel") is { } canal)
        {
            var nome = fonte ?? Filho(canal, "title")?.Value.Trim() ?? "";
            foreach (var item in canal.Elements().Where(e => e.Name.LocalName == "item"))
                Acrescentar(lista, Filho(item, "title")?.Value, Filho(item, "link")?.Value ?? "",
                    Data(Filho(item, "pubDate")?.Value ?? Filho(item, "date")?.Value), nome);
        }
        return lista;
    }

    static void Acrescentar(List<Noticia> lista, string? titulo, string link, DateTimeOffset? quando, string fonte)
    {
        if (string.IsNullOrWhiteSpace(titulo)) return;
        var limpo = string.Join(' ', titulo.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        lista.Add(new Noticia(limpo, link.Trim(), quando, fonte));
    }

    static XElement? Filho(XElement pai, string nome) => pai.Elements().FirstOrDefault(e => e.Name.LocalName == nome);

    static DateTimeOffset? Data(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        texto = texto.Trim();
        if (DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var iso)) return iso;
        var semZona = texto.Length > 6 && texto[^4] == ' ' && char.IsLetter(texto[^1]) ? texto[..^4] + " +0000" : texto;
        foreach (var formato in new[] { "ddd, dd MMM yyyy HH:mm:ss zzz", "ddd, d MMM yyyy HH:mm:ss zzz", "dd MMM yyyy HH:mm:ss zzz" })
            if (DateTimeOffset.TryParseExact(semZona.Replace("+0000", "+00:00").Replace("-0000", "+00:00"), formato, CultureInfo.InvariantCulture, DateTimeStyles.None, out var rfc))
                return rfc;
        return null;
    }

    public static string Fonte(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url;
        var partes = uri.AbsolutePath.Trim('/').Split('/');
        if (uri.Host == "github.com" && partes.Length >= 2) return partes[1];
        return uri.Host.StartsWith("www.", StringComparison.Ordinal) ? uri.Host[4..] : uri.Host;
    }

    public static async Task<IReadOnlyList<Noticia>> BuscarAsync(IEnumerable<string> feeds, int maximo = 8, CancellationToken cancelar = default)
    {
        var tarefas = feeds.Where(f => !string.IsNullOrWhiteSpace(f)).Distinct().Select(async url =>
        {
            try
            {
                var xml = await Cliente.GetStringAsync(url, cancelar);
                return Ler(xml, Fonte(url));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or UriFormatException)
            {
                return (IReadOnlyList<Noticia>)[];
            }
        });
        var todas = (await Task.WhenAll(tarefas)).SelectMany(l => l.Take(maximo)).ToList();
        return [.. todas.OrderByDescending(n => n.Quando ?? DateTimeOffset.MinValue).Take(maximo)];
    }
}
