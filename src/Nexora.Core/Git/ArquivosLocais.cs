using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using Nexora.Core.Texto;

namespace Nexora.Core.Git;

public sealed record CopiaLocal(IReadOnlyList<string> Arquivos, int? PortaAntiga, int? PortaNova)
{
    public string Resumo => Arquivos.Count == 0 ? ""
        : (Arquivos.Count == 1
            ? Idioma.F("Copiei 1 arquivo local do principal ({0})", string.Join(", ", Arquivos.Select(Path.GetFileName).Distinct()))
            : Idioma.F("Copiei {0} arquivos locais do principal ({1})", Arquivos.Count, string.Join(", ", Arquivos.Select(Path.GetFileName).Distinct())))
          + (PortaNova is { } p ? Idioma.F(" e troquei a porta {0} por {1}.", PortaAntiga, p) : ".");
}

public static partial class ArquivosLocais
{
    const int Passo = 5;
    static readonly string[] Padroes =
    [
        ":(glob)**/appsettings.Development.json",
        ":(glob)**/appsettings.*.local.json",
        ":(glob)**/appsettings.Local.json",
        ":(glob)**/*.local.json",
        ":(glob)**/.env",
        ":(glob)**/.env.*",
    ];
    static readonly HashSet<string> Ignoradas = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj", "node_modules", ".vs", "packages" };

    public static async Task<IReadOnlyList<string>> ListarAsync(string repositorio)
    {
        var r = await ComandoGit.ExecutarAsync(repositorio, ["ls-files", "--others", "--ignored", "--exclude-standard", "-z", "--", .. Padroes]);
        if (!r.Ok) return [];
        return [.. r.Saida.Split('\0', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(caminho => !caminho.EndsWith('/') && !caminho.Split('/').Any(Ignoradas.Contains))
            .Order(StringComparer.OrdinalIgnoreCase)];
    }

    public static async Task<CopiaLocal> CopiarAsync(string origem, string destino)
    {
        var arquivos = await ListarAsync(origem);
        if (arquivos.Count == 0) return new CopiaLocal([], null, null);
        var usadas = (await Repositorio.WorktreesAsync(origem)).Select(w => w.Caminho).Append(origem)
            .Where(w => !string.Equals(Path.GetFullPath(w).TrimEnd('\\'), Path.GetFullPath(destino).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            .SelectMany(w => arquivos.Select(a => Path.Combine(w, a)))
            .Select(PortaDoArquivo).OfType<int>().ToHashSet();
        int? antiga = null, nova = null;
        var copiados = new List<string>();
        foreach (var relativo in arquivos)
        {
            var de = Path.Combine(origem, relativo);
            var para = Path.Combine(destino, relativo);
            if (!File.Exists(de) || File.Exists(para)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(para)!);
            var bytes = await File.ReadAllBytesAsync(de);
            var bom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
            var texto = Encoding.UTF8.GetString(bom ? bytes[Encoding.UTF8.Preamble.Length..] : bytes);
            if (Porta(texto) is { } porta && (antiga is null || antiga == porta))
            {
                antiga = porta;
                nova ??= Proxima(porta, usadas);
                texto = Regex.Replace(texto, $@":{porta}(?!\d)", $":{nova}");
                await File.WriteAllTextAsync(para, texto, new UTF8Encoding(bom));
            }
            else
                await File.WriteAllBytesAsync(para, bytes);
            copiados.Add(relativo);
        }
        return new CopiaLocal(copiados, nova is null ? null : antiga, nova);
    }

    static int? PortaDoArquivo(string caminho)
    {
        try
        {
            return File.Exists(caminho) ? Porta(File.ReadAllText(caminho)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    static int? Porta(string texto) =>
        SelfUrl().Match(texto) is { Success: true } achou && int.TryParse(achou.Groups[1].Value, out var porta) ? porta : null;

    static int Proxima(int porta, HashSet<int> usadas)
    {
        var tentativa = porta + Passo;
        var ocupadas = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(e => e.Port).ToHashSet();
        while (usadas.Contains(tentativa) || ocupadas.Contains(tentativa)) tentativa += Passo;
        return tentativa;
    }

    [GeneratedRegex(@"""SelfUrl""\s*:\s*""[^""]*?:(\d{2,5})")]
    private static partial Regex SelfUrl();
}
