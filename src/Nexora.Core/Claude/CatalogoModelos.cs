using System.Text.Json;

namespace Nexora.Core.Claude;

public static class CatalogoModelos
{
    public const string Padrao = "default";

    static readonly object Trava = new();
    static IReadOnlyList<ModeloDisponivel>? _atual;

    public static string? Arquivo { get; set; }

    public static event Action? Mudou;

    public static IReadOnlyList<ModeloDisponivel> Atual
    {
        get
        {
            lock (Trava) return _atual ??= Carregar();
        }
    }

    public static void Guardar(IReadOnlyList<ModeloDisponivel>? modelos)
    {
        if (modelos is not { Count: > 0 }) return;
        var json = JsonSerializer.Serialize(modelos);
        lock (Trava)
        {
            if (_atual is not null && JsonSerializer.Serialize(_atual) == json) return;
            _atual = modelos;
        }
        try
        {
            if (Arquivo is { } arquivo)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
                File.WriteAllText(arquivo, json);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        Mudou?.Invoke();
    }

    public static ModeloDisponivel? Achar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var lista = Atual;
        return lista.FirstOrDefault(m => string.Equals(m.Valor, valor, StringComparison.OrdinalIgnoreCase))
            ?? lista.FirstOrDefault(m => m.Valor != Padrao && string.Equals(m.Resolvido, valor, StringComparison.OrdinalIgnoreCase));
    }

    public static string ValorPara(string idDoModelo) =>
        Achar(idDoModelo) is { } achado && achado.Valor != Padrao ? achado.Valor : idDoModelo;

    public static string Nome(string valor)
    {
        if (Achar(valor) is not { } modelo) return NomeDoId(valor);
        return modelo.Valor == Padrao && modelo.Resolvido is { } resolvido ? NomeDoId(resolvido) : modelo.Nome;
    }

    public static string NomeDoId(string id)
    {
        var partes = id.Replace("claude-", "").Split('-').Where(p => p.Length > 0 && !(p.Length == 8 && p.All(char.IsDigit))).ToList();
        if (partes.Count == 0) return id;
        var familia = char.ToUpperInvariant(partes[0][0]) + partes[0][1..];
        return partes.Count > 1 ? $"{familia} {string.Join('.', partes.Skip(1))}" : familia;
    }

    static IReadOnlyList<ModeloDisponivel> Carregar()
    {
        try
        {
            if (Arquivo is { } arquivo && File.Exists(arquivo))
                return JsonSerializer.Deserialize<List<ModeloDisponivel>>(File.ReadAllText(arquivo)) ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
        return [];
    }
}
