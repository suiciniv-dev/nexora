using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Nexora.Core.Claude;

namespace Nexora.Core.Servidor;

public static class JsonDoNucleo
{
    public const string Discriminador = "$tipo";

    public static IReadOnlyList<Type> TiposDeEvento { get; } =
        [.. typeof(EventoClaude).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(EventoClaude)) && !t.IsAbstract).OrderBy(t => t.Name, StringComparer.Ordinal)];

    public static JsonSerializerOptions Opcoes { get; } = new(JsonSerializerDefaults.General)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { Polimorfismo } },
    };

    static void Polimorfismo(JsonTypeInfo info)
    {
        if (info.Type != typeof(EventoClaude)) return;
        info.PolymorphismOptions = new JsonPolymorphismOptions { TypeDiscriminatorPropertyName = Discriminador };
        foreach (var tipo in TiposDeEvento) info.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(tipo, tipo.Name));
    }
}
