using Avalonia.Media.Imaging;
using Nexora.Core.Git;

namespace Nexora.Servicos;

public static class IconesDeRepositorio
{
    static readonly Dictionary<string, Task<IconeDoProjeto>> Lidos = new(StringComparer.OrdinalIgnoreCase);
    static readonly string Guardados = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexora", "icones");
    static readonly string[] Ignorar = ["node_modules/", "/bin/", "/obj/", "test", "vendor/", "packages/", "/dist/", "/build/", "third_party", "thirdparty", "docs/"];
    static readonly HttpClient Http = Criar();

    static HttpClient Criar()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Nexora/{Atualizacoes.Atual.ToString(3)}");
        return http;
    }

    public static Task<IconeDoProjeto> DeAsync(string pasta)
    {
        lock (Lidos)
        {
            if (!Lidos.TryGetValue(pasta, out var tarefa)) Lidos[pasta] = tarefa = Task.Run(() => CarregarAsync(pasta));
            return tarefa;
        }
    }

    static async Task<IconeDoProjeto> CarregarAsync(string pasta)
    {
        try
        {
            if (!Directory.Exists(pasta)) return new(null, true);
            var lista = await ComandoGit.ExecutarAsync(pasta, ["ls-files", "--", "*.png", "*.ico"], TimeSpan.FromSeconds(10));
            if (!lista.Ok) return new(null, false);
            if (Escolher(lista.Saida.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) is { } arquivo
                && Abrir(Path.Combine(pasta, arquivo.Replace('/', Path.DirectorySeparatorChar))) is { } doRepo)
                return new(doRepo, true);
            if (await GitHub.RepositorioDaPastaAsync(pasta) is not { } repo || repo.Split('/')[0] is not { Length: > 0 } dono) return new(null, true);
            return new(await AvatarAsync(dono), true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return new(null, true);
        }
    }

    static async Task<Bitmap?> AvatarAsync(string dono)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(dono, "^[A-Za-z0-9-]+$")) return null;
        var arquivo = Path.Combine(Guardados, dono + ".png");
        if (!File.Exists(arquivo) || DateTime.UtcNow - File.GetLastWriteTimeUtc(arquivo) > TimeSpan.FromDays(7))
        {
            try
            {
                var bytes = await Http.GetByteArrayAsync($"https://github.com/{dono}.png?size=64");
                Directory.CreateDirectory(Guardados);
                await File.WriteAllBytesAsync(arquivo, bytes);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                if (!File.Exists(arquivo)) return null;
            }
        }
        return Abrir(arquivo);
    }

    static Bitmap? Abrir(string arquivo)
    {
        try
        {
            using var fluxo = File.OpenRead(arquivo);
            return Bitmap.DecodeToWidth(fluxo, 64, BitmapInterpolationMode.HighQuality);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    public static string? Escolher(IEnumerable<string> arquivos) =>
        arquivos
            .Select(a => a.Replace('\\', '/'))
            .Where(a => !Ignorar.Any(i => ("/" + a).Contains(i, StringComparison.OrdinalIgnoreCase)))
            .Select(a => (Arquivo: a, Nota: Nota(a)))
            .Where(x => x.Nota > 0)
            .OrderByDescending(x => x.Nota)
            .ThenBy(x => x.Arquivo.Count(c => c == '/'))
            .ThenByDescending(x => Tamanho(x.Arquivo))
            .ThenBy(x => x.Arquivo.Length)
            .Select(x => x.Arquivo)
            .FirstOrDefault();

    static int Nota(string arquivo)
    {
        var nome = Path.GetFileNameWithoutExtension(arquivo).ToLowerInvariant();
        var ico = Path.GetExtension(arquivo).Equals(".ico", StringComparison.OrdinalIgnoreCase);
        if (nome == "favicon") return ico ? 95 : 100;
        if (nome.Contains("appicon") || nome.Contains("app-icon") || nome.Contains("app_icon") || nome == "ic_launcher") return 90;
        if (nome.Contains("touch-icon")) return 85;
        if (ico) return 80;
        if (nome.StartsWith("icon")) return 70;
        if (nome is "logo" || nome.StartsWith("logo-") || nome.StartsWith("logo_")) return 60;
        return 0;
    }

    static int Tamanho(string arquivo) =>
        System.Text.RegularExpressions.Regex.Matches(Path.GetFileNameWithoutExtension(arquivo), @"\d+")
            .Select(m => int.TryParse(m.Value, out var n) ? n : 0).DefaultIfEmpty(0).Max();
}
