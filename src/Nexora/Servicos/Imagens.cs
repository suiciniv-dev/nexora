using Avalonia.Media.Imaging;
using Nexora.Core.Claude;
using SkiaSharp;

namespace Nexora.Servicos;

public static class Imagens
{
    const int LadoMaximo = 1800;
    const int TamanhoMaximo = 3_500_000;

    public static readonly HashSet<string> Extensoes = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp" };

    public static Anexo? Preparar(byte[] bytes, string nome)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(bytes));
        if (codec is null) return null;
        using var original = SKBitmap.Decode(codec);
        if (original is null) return null;
        var escala = Math.Min(1.0, (double)LadoMaximo / Math.Max(original.Width, original.Height));
        using var reduzida = escala < 1
            ? original.Resize(new SKImageInfo((int)(original.Width * escala), (int)(original.Height * escala)), new SKSamplingOptions(SKCubicResampler.Mitchell))
            : null;
        using var imagem = SKImage.FromBitmap(reduzida ?? original);
        using var png = imagem.Encode(SKEncodedImageFormat.Png, 100);
        if (png.Size <= TamanhoMaximo) return new Anexo(nome, "image/png", png.ToArray());
        using var jpeg = imagem.Encode(SKEncodedImageFormat.Jpeg, 85);
        return new Anexo(Path.ChangeExtension(nome, ".jpg"), "image/jpeg", jpeg.ToArray());
    }

    public static Anexo? DeBitmap(Bitmap bitmap, string nome)
    {
        using var memoria = new MemoryStream();
        bitmap.Save(memoria, PngBitmapEncoderOptions.Default);
        return Preparar(memoria.ToArray(), nome);
    }

    public static Bitmap Miniatura(Anexo anexo, int largura = 240)
    {
        using var memoria = new MemoryStream(anexo.Dados);
        return Bitmap.DecodeToWidth(memoria, largura);
    }
}
