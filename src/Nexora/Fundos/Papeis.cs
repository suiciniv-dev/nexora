namespace Nexora.Fundos;

public static class Papeis
{
    public const string Nenhum = "nenhum";
    const string Prefixo = "nexora:";

    public static readonly IReadOnlyList<Papel> Todos =
    [
        new("nexora:grafo", "Grafo", "nós e estrelas à deriva", true, "#BDB4DA", "#0E0D0C"),
        new("nexora:grafo-claro", "Grafo claro", "nós à deriva no papel", false, "#5B5470", "#F6F4F1"),
        new("nexora:constelacao", "Constelação", "céu que gira devagar", true, "#A9B6F2", "#0C0D1E"),
        new("nexora:carta-celeste", "Carta celeste", "constelações a tinta", false, "#4A4E86", "#F6F3EA"),
        new("nexora:via-lactea", "Via Láctea", "faixa de poeira estelar", true, "#CDBFEA", "#0C0B13"),
        new("nexora:nebulosa", "Nebulosa", "nuvens de cor à deriva", true, "#E59AC4", "#0E0B12"),
        new("nexora:pulso", "Pulso", "sinais entre estrelas", true, "#86C5E6", "#0B0D12"),
        new("nexora:aurora", "Aurora", "cortinas de luz no céu", true, "#7BCB9F", "#0A0F14"),
        new("nexora:mosaico", "Mosaico", "grade de commits que acende", true, "#E0A048", "#0F0E0C"),
        new("nexora:mosaico-estatico", "Mosaico estático", "a mesma grade, sem movimento", true, "#E0A048", "#0F0E0C", Parado: true),
    ];

    static readonly Dictionary<string, string> Antigos = new()
    {
        ["claude"] = "grafo",
        ["clouds"] = "grafo-claro",
        ["lagoon"] = "grafo-claro",
        ["koi"] = "aurora",
        ["jelly"] = "pulso",
        ["boats"] = "grafo-claro",
        ["fireflies"] = "pulso",
        ["moonlit"] = "constelacao",
        ["lareira"] = "nebulosa",
        ["chuva"] = "grafo",
        ["galaxia"] = "via-lactea",
        ["sakura"] = "carta-celeste",
        ["aurora"] = "aurora",
        ["codigo"] = "pulso",
        ["arcade"] = "pulso",
        ["starfield-warp"] = "constelacao",
        ["spiral-galaxy"] = "via-lactea",
        ["nebula-drift"] = "nebulosa",
        ["northern-lights"] = "aurora",
        ["neon-city-rain"] = "pulso",
        ["retro-synth-sunset"] = "nebulosa",
        ["moonlit-sea"] = "constelacao",
        ["rainy-window"] = "grafo",
        ["snowfall"] = "constelacao",
        ["underwater-reef"] = "aurora",
        ["flow-field"] = "grafo",
        ["game-of-life"] = "pulso",
    };

    public static Papel? Encontrar(string? id) => Todos.FirstOrDefault(p => p.Id == id);

    public static string Nome(string? id) => Encontrar(id)?.Nome ?? "Sólido";

    public static string TemaDe(Papel papel) => papel.Id[Prefixo.Length..];

    public static string Migrar(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return Nenhum;
        var separador = id.IndexOf(':');
        if (separador < 0 || id[..separador] is not ("claudinho" or "magia")) return id;
        return Prefixo + (Antigos.TryGetValue(id[(separador + 1)..], out var novo) ? novo : "grafo");
    }

    public static string? TemaAntigo(string? id) => id is not null && Antigos.TryGetValue(id, out var novo) ? novo : null;
}
