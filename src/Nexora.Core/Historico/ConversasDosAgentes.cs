using System.Text.Json;

namespace Nexora.Core.Historico;

public sealed record ConversaDeAgente(
    string Agente,
    string SessaoId,
    string Pasta,
    string Titulo,
    string? Branch,
    DateTime Atualizada,
    string? PrimeiraMensagem = null,
    string? UltimaMensagem = null)
{
    public ConversaSalva ParaSalva() => new(SessaoId, "", Titulo, Pasta, Branch, Atualizada, PrimeiraMensagem, UltimaMensagem, Agente);
}

public static class ConversasDosAgentes
{
    const int Maximo = 500;
    static readonly object Trava = new();
    static readonly JsonSerializerOptions Opcoes = new() { WriteIndented = false };

    public static string ArquivoPadrao =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nexora", "conversas-agentes.json");

    public static IReadOnlyList<ConversaDeAgente> Listar(string? arquivo = null)
    {
        arquivo ??= ArquivoPadrao;
        lock (Trava) return Ler(arquivo);
    }

    public static ConversaDeAgente? Achar(string sessaoId, string? arquivo = null) =>
        Listar(arquivo).FirstOrDefault(c => c.SessaoId == sessaoId);

    public static void Guardar(ConversaDeAgente conversa, string? arquivo = null)
    {
        arquivo ??= ArquivoPadrao;
        lock (Trava)
        {
            var lista = Ler(arquivo).Where(c => c.SessaoId != conversa.SessaoId).ToList();
            var antiga = Ler(arquivo).FirstOrDefault(c => c.SessaoId == conversa.SessaoId);
            lista.Insert(0, conversa with { PrimeiraMensagem = antiga?.PrimeiraMensagem ?? conversa.PrimeiraMensagem });
            Gravar(arquivo, [.. lista.OrderByDescending(c => c.Atualizada).Take(Maximo)]);
        }
    }

    public static void Remover(string sessaoId, string? arquivo = null)
    {
        arquivo ??= ArquivoPadrao;
        lock (Trava)
        {
            var lista = Ler(arquivo);
            if (lista.Any(c => c.SessaoId == sessaoId)) Gravar(arquivo, [.. lista.Where(c => c.SessaoId != sessaoId)]);
        }
    }

    public static void RemoverDaPasta(string pasta, string? arquivo = null)
    {
        arquivo ??= ArquivoPadrao;
        var chave = Normalizar(pasta);
        lock (Trava)
        {
            var lista = Ler(arquivo);
            var restantes = lista.Where(c => !string.Equals(Normalizar(c.Pasta), chave, StringComparison.OrdinalIgnoreCase)).ToList();
            if (restantes.Count != lista.Count) Gravar(arquivo, restantes);
        }
    }

    static string Normalizar(string pasta) => Path.GetFullPath(pasta).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    static List<ConversaDeAgente> Ler(string arquivo)
    {
        try
        {
            return File.Exists(arquivo) ? JsonSerializer.Deserialize<List<ConversaDeAgente>>(File.ReadAllText(arquivo), Opcoes) ?? [] : [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    static void Gravar(string arquivo, List<ConversaDeAgente> lista)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
            var temporario = arquivo + ".tmp";
            File.WriteAllText(temporario, JsonSerializer.Serialize(lista, Opcoes));
            File.Move(temporario, arquivo, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
