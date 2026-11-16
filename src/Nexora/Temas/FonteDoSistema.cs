using Avalonia.Media;
using Avalonia.Media.Fonts;

namespace Nexora.Temas;

public sealed class FonteDoSistema : FontCollectionBase
{
    public const string Familia = "fonts:Sistema#Segoe UI";
    const string Nome = "Segoe UI";
    static readonly string[] Arquivos = ["segoeui.ttf", "segoeuil.ttf", "segoeuib.ttf", "segoeuii.ttf", "segoeuiz.ttf"];

    public override Uri Key { get; } = new("fonts:Sistema");

    public static bool Disponivel => OperatingSystem.IsWindows() && File.Exists(Caminho(Arquivos[0]));

    static string Caminho(string arquivo) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), arquivo);

    public FonteDoSistema()
    {
        foreach (var arquivo in Arquivos) Carregar(arquivo);
        if (Carregar("seguisb.ttf") is { } seminegrito)
            TryAddGlyphTypeface(Nome, new FontCollectionKey(FontStyle.Normal, FontWeight.Medium, FontStretch.Normal), seminegrito);
        if (Carregar("seguisbi.ttf") is { } seminegritoItalico)
            TryAddGlyphTypeface(Nome, new FontCollectionKey(FontStyle.Italic, FontWeight.Medium, FontStretch.Normal), seminegritoItalico);
    }

    GlyphTypeface? Carregar(string arquivo)
    {
        var caminho = Caminho(arquivo);
        if (!File.Exists(caminho)) return null;
        try
        {
            using var fluxo = File.OpenRead(caminho);
            return TryAddGlyphTypeface(fluxo, out var fonte) ? fonte : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return null;
        }
    }
}
