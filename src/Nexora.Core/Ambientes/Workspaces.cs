using System.Text.Json;
using System.Text.Json.Nodes;
using Nexora.Core.Claude;

namespace Nexora.Core.Ambientes;

public sealed record Ambiente(string Nome, string Principal, IReadOnlyList<string> Vinculados, string? Arquivo)
{
    public Ambiente ParaWorktree(string pasta) =>
        new(Path.GetFileName(pasta.TrimEnd('\\', '/')), pasta, Vinculados.Where(v => !Workspaces.MesmaPasta(v, pasta)).ToList(), null);
}

public static class Workspaces
{
    static readonly JsonDocumentOptions Leitura = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    static readonly JsonSerializerOptions Escrita = new() { WriteIndented = true, IndentCharacter = '\t', IndentSize = 1 };

    public static Ambiente? Ler(string arquivo)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(arquivo), Leitura);
            var dir = Path.GetDirectoryName(Path.GetFullPath(arquivo))!;
            var pastas = Protocolo.Itens(Protocolo.Objeto(doc.RootElement, "folders"))
                .Select(f => Protocolo.Texto(f, "path"))
                .OfType<string>()
                .Select(p => Path.GetFullPath(Path.IsPathRooted(p) ? p : Path.Combine(dir, p)).TrimEnd('\\', '/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return pastas.Count == 0 ? null : new Ambiente(Path.GetFileNameWithoutExtension(arquivo), pastas[0], pastas.Skip(1).ToList(), arquivo);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public static IReadOnlyList<Ambiente> Procurar(IEnumerable<string> raizes)
    {
        var achados = new List<Ambiente>();
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raiz in raizes.Where(Directory.Exists))
            foreach (var arquivo in Arquivos(raiz))
                if (Ler(arquivo) is { } ambiente && vistos.Add(ambiente.Principal))
                    achados.Add(ambiente);
        return achados.OrderBy(a => a.Nome, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static IReadOnlyList<Ambiente> ProcurarProjetos(IEnumerable<string> raizes)
    {
        var existentes = raizes.Where(Directory.Exists).ToList();
        var achados = Procurar(existentes).ToList();
        var ocupadas = achados.SelectMany(a => new[] { a.Principal }.Concat(a.Vinculados)).ToList();
        foreach (var raiz in existentes.Where(r => !Arquivos(r).Any()))
            foreach (var pasta in Repositorios(raiz))
            {
                if (ocupadas.Any(o => MesmaPasta(o, pasta))) continue;
                achados.Add(DaPasta(pasta));
                ocupadas.Add(pasta);
            }
        return achados.OrderBy(a => a.Nome, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static Ambiente DaPasta(string pasta)
    {
        var limpa = Path.GetFullPath(pasta).TrimEnd('\\', '/');
        var nome = Path.GetFileName(limpa);
        return new Ambiente(nome.Length > 0 ? nome : limpa, limpa, [], null);
    }

    public static bool EhRepositorio(string pasta) =>
        Directory.Exists(Path.Combine(pasta, ".git")) || File.Exists(Path.Combine(pasta, ".git"));

    static IEnumerable<string> Repositorios(string raiz)
    {
        var limpa = Path.GetFullPath(raiz).TrimEnd('\\', '/');
        if (EhRepositorio(limpa)) return [limpa];
        try
        {
            return Directory.EnumerateDirectories(limpa)
                .Where(d => !Path.GetFileName(d).StartsWith('.') && EhRepositorio(d))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static string Escrever(Ambiente origem, string novoPrincipal)
    {
        var principal = novoPrincipal.TrimEnd('\\', '/');
        var arquivo = Path.Combine(principal, Path.GetFileName(principal) + ".code-workspace");
        var pastas = new JsonArray(new JsonObject { ["path"] = "." });
        foreach (var vinculado in origem.Vinculados.Where(v => !MesmaPasta(v, principal)))
            pastas.Add(new JsonObject { ["path"] = Path.GetRelativePath(principal, vinculado).Replace('\\', '/') });
        Nexora.Core.Arquivos.ArquivoSeguro.Gravar(arquivo, new JsonObject { ["folders"] = pastas, ["settings"] = new JsonObject() }.ToJsonString(Escrita));
        return arquivo;
    }

    public static Ambiente Salvar(Ambiente ambiente)
    {
        var arquivo = ambiente.Arquivo ?? Path.Combine(ambiente.Principal, ambiente.Nome + ".code-workspace");
        var dir = Path.GetDirectoryName(Path.GetFullPath(arquivo))!;
        JsonObject raiz;
        try
        {
            raiz = File.Exists(arquivo) ? JsonNode.Parse(File.ReadAllText(arquivo), documentOptions: Leitura) as JsonObject ?? [] : [];
        }
        catch (JsonException ex)
        {
            throw new Nexora.Core.Arquivos.ArquivoIlegivelException(arquivo, ex.Message);
        }
        var pastas = new JsonArray();
        foreach (var pasta in new[] { ambiente.Principal }.Concat(ambiente.Vinculados).Distinct(StringComparer.OrdinalIgnoreCase))
            pastas.Add(new JsonObject { ["path"] = Relativo(dir, pasta) });
        raiz["folders"] = pastas;
        raiz["settings"] ??= new JsonObject();
        Nexora.Core.Arquivos.ArquivoSeguro.Gravar(arquivo, raiz.ToJsonString(Escrita));
        return ambiente with { Arquivo = arquivo };
    }

    static string Relativo(string dir, string pasta)
    {
        var relativo = Path.GetRelativePath(dir, pasta);
        return Path.IsPathRooted(relativo) ? relativo : relativo.Replace('\\', '/');
    }

    public static bool MesmaPasta(string a, string b) =>
        string.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    static IEnumerable<string> Arquivos(string raiz)
    {
        List<string> lista = [];
        try
        {
            lista.AddRange(Directory.EnumerateFiles(raiz, "*.code-workspace"));
            foreach (var sub in Directory.EnumerateDirectories(raiz).Where(d => !Path.GetFileName(d).StartsWith('.')))
            {
                try
                {
                    lista.AddRange(Directory.EnumerateFiles(sub, "*.code-workspace"));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return lista.Order(StringComparer.OrdinalIgnoreCase);
    }
}
