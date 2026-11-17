using System.Collections.Concurrent;
using System.Text.Json;
using Avalonia.Platform;
using Nexora.Core.Avatares;

namespace Nexora.Servicos;

public sealed record EscolhaDeAvatar(DefinicaoAvatar Definicao, string Cor);

public static class CatalogoDeAvatares
{
    const string Pasta = "avares://Nexora/Assets/Avatares/";
    static readonly ConcurrentDictionary<string, DefinicaoAvatar?> Definicoes = new();
    static readonly Lazy<Catalogo> Dados = new(Carregar);

    sealed record Item(string Avatar, string Cor);

    sealed record Catalogo(Dictionary<string, Item> Agentes, Dictionary<string, Item> Subagentes, Item? Orquestrador);

    static string? Ler(string arquivo)
    {
        try
        {
            using var fluxo = AssetLoader.Open(new Uri(Pasta + arquivo));
            using var leitor = new StreamReader(fluxo);
            return leitor.ReadToEnd();
        }
        catch (Exception ex) when (ex is FileNotFoundException or IOException or InvalidOperationException or UriFormatException)
        {
            return null;
        }
    }

    static Catalogo Carregar()
    {
        var vazio = new Catalogo([], [], null);
        if (Ler("catalogo.json") is not { } json) return vazio;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            static Item? ItemDe(JsonElement e) =>
                e.ValueKind == JsonValueKind.Object && e.TryGetProperty("avatar", out var a) && a.GetString() is { Length: > 0 } avatar
                    ? new Item(avatar, e.TryGetProperty("cor", out var c) && c.GetString() is { } cor ? cor : Quadro.CorPadrao)
                    : null;
            Dictionary<string, Item> Mapa(string nome)
            {
                var mapa = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
                if (raiz.TryGetProperty(nome, out var grupo) && grupo.ValueKind == JsonValueKind.Object)
                    foreach (var p in grupo.EnumerateObject())
                        if (ItemDe(p.Value) is { } item) mapa[p.Name] = item;
                return mapa;
            }
            return new Catalogo(Mapa("agentes"), Mapa("subagentes"), raiz.TryGetProperty("orquestrador", out var o) ? ItemDe(o) : null);
        }
        catch (JsonException)
        {
            return vazio;
        }
    }

    public static DefinicaoAvatar? Definicao(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return Definicoes.GetOrAdd(id, chave => Ler(chave + ".json") is { } json ? Corpo.Ler(json).Definicao : null);
    }

    static EscolhaDeAvatar? Escolher(Item? item) =>
        item is not null && Definicao(item.Avatar) is { } def ? new EscolhaDeAvatar(def, item.Cor) : null;

    static Item? Procurar(Dictionary<string, Item> mapa, string? chave) =>
        chave is not null && mapa.TryGetValue(chave, out var item) ? item : mapa.GetValueOrDefault("*");

    public const string IdOrquestrador = "orquestrador";

    public static EscolhaDeAvatar? Agente(string? id) => id == IdOrquestrador
        ? Orquestrador
        : Escolher(Procurar(Dados.Value.Agentes, id) ?? Procurar(Dados.Value.Subagentes, "*"));

    public static EscolhaDeAvatar? Subagente(string? tipo) => Escolher(Procurar(Dados.Value.Subagentes, tipo));

    public static EscolhaDeAvatar? Orquestrador => Escolher(Dados.Value.Orquestrador);
}
