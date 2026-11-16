namespace Nexora.Core.Navegador;

public static class Enderecos
{
    static readonly string[] Locais = ["localhost", "127.0.0.1", "0.0.0.0", "[::1]"];
    static readonly string[] SufixosLocais = [".localhost", ".local", ".test", ".internal"];

    public static string? Normalizar(string? entrada)
    {
        var texto = entrada?.Trim() ?? "";
        if (texto.Length == 0 || texto.Any(char.IsWhiteSpace)) return null;
        if (texto.StartsWith(':')) texto = texto[1..];
        if (int.TryParse(texto, out var so) && texto.All(char.IsAsciiDigit))
            return so is > 0 and < 65536 ? $"http://localhost:{so}/" : null;
        if (texto.Equals("about:blank", StringComparison.OrdinalIgnoreCase)) return "about:blank";
        var temEsquema = texto.Contains("://", StringComparison.Ordinal);
        if (!temEsquema) texto = (EhLocal(Hospedeiro(texto)) ? "http://" : "https://") + texto;
        if (!Uri.TryCreate(texto, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
        if (string.IsNullOrEmpty(uri.Host)) return null;
        if (!temEsquema && !EhLocal(uri.Host) && !uri.Host.Contains('.')) return null;
        return uri.AbsoluteUri;
    }

    static string Hospedeiro(string semEsquema)
    {
        var fim = semEsquema.IndexOfAny(['/', '?', '#']);
        var autoridade = fim < 0 ? semEsquema : semEsquema[..fim];
        if (autoridade.StartsWith('[')) return autoridade[..(autoridade.IndexOf(']') is var f and >= 0 ? f + 1 : autoridade.Length)];
        var doisPontos = autoridade.LastIndexOf(':');
        return doisPontos < 0 ? autoridade : autoridade[..doisPontos];
    }

    public static bool EhLocal(string? hospedeiro)
    {
        if (string.IsNullOrEmpty(hospedeiro)) return false;
        var h = hospedeiro.ToLowerInvariant();
        return Locais.Contains(h) || SufixosLocais.Any(h.EndsWith);
    }

    public static bool EhLocal(Uri uri) => EhLocal(uri.Host) || uri.IsLoopback;

    public static string Curto(string? url)
    {
        if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url ?? "";
        var caminho = uri.PathAndQuery == "/" ? "" : uri.PathAndQuery;
        return uri.IsDefaultPort ? uri.Host + caminho : $"{uri.Host}:{uri.Port}{caminho}";
    }
}
