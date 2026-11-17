using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform;

namespace Nexora.Views;

public sealed class FundoDoIcone : IValueConverter
{
    public static readonly FundoDoIcone Instancia = new();
    public static readonly IBrush Placa = new ImmutableSolidColorBrush(Color.Parse("#F3F6FC"));
    static readonly ConditionalWeakTable<Bitmap, object> Lidos = [];

    public static bool Transparente(IImage? imagem)
    {
        if (imagem is not Bitmap bitmap) return false;
        if (Lidos.TryGetValue(bitmap, out var guardado)) return (bool)guardado;
        var resposta = Medir(bitmap);
        Lidos.AddOrUpdate(bitmap, resposta);
        return resposta;
    }

    public static bool Medir(Bitmap bitmap)
    {
        var tamanho = bitmap.PixelSize;
        if (tamanho.Width <= 0 || tamanho.Height <= 0) return false;
        var passo = tamanho.Width * 4;
        var dados = new byte[passo * tamanho.Height];
        var alca = GCHandle.Alloc(dados, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(tamanho), alca.AddrOfPinnedObject(), dados.Length, passo);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException)
        {
            return false;
        }
        finally
        {
            alca.Free();
        }
        var transparentes = 0;
        for (var i = 3; i < dados.Length; i += 4)
            if (dados[i] < 200) transparentes++;
        return transparentes > tamanho.Width * tamanho.Height * 0.06;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Transparente(value as IImage) ? Placa : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
