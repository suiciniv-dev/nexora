namespace Nexora.ViewModels;

public static class Siglas
{
    public static string De(string? nome)
    {
        var partes = (nome ?? "").Split('.', '-', '_', ' ').Where(p => p.Length > 0).ToList();
        return partes.Count switch
        {
            0 => "?",
            1 => partes[0][..Math.Min(2, partes[0].Length)].ToUpperInvariant(),
            _ => $"{char.ToUpperInvariant(partes[0][0])}{char.ToUpperInvariant(partes[^1][0])}",
        };
    }
}
