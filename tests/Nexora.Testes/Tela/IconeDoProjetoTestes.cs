using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Nexora.Views;

namespace Nexora.Testes.Tela;

public sealed class IconeDoProjetoTestes
{
    static WriteableBitmap Imagem(bool transparente)
    {
        var bitmap = new WriteableBitmap(new PixelSize(64, 64), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var buffer = bitmap.Lock();
        var dados = new byte[64 * 64 * 4];
        for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
            {
                var i = (y * 64 + x) * 4;
                var d = Math.Sqrt((x - 32) * (x - 32) + (y - 32) * (y - 32));
                var anel = d is > 18 and < 26;
                var alfa = transparente && !anel ? (byte)0 : (byte)255;
                (dados[i], dados[i + 1], dados[i + 2], dados[i + 3]) = anel ? ((byte)70, (byte)40, (byte)20, alfa) : ((byte)200, (byte)200, (byte)200, alfa);
                if (alfa == 0) (dados[i], dados[i + 1], dados[i + 2]) = ((byte)0, (byte)0, (byte)0);
            }
        System.Runtime.InteropServices.Marshal.Copy(dados, 0, buffer.Address, dados.Length);
        return bitmap;
    }

    static Color Centro(Avatar avatar)
    {
        var tela = new Avalonia.Controls.Border { Width = 64, Height = 64, Background = new SolidColorBrush(Color.Parse("#080D1C")), Child = avatar };
        tela.Measure(new Size(64, 64));
        tela.Arrange(new Rect(0, 0, 64, 64));
        using var alvo = new RenderTargetBitmap(new PixelSize(64, 64));
        alvo.Render(tela);
        using var copia = new WriteableBitmap(new PixelSize(64, 64), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var trava = copia.Lock();
        alvo.CopyPixels(new PixelRect(0, 0, 64, 64), trava.Address, 64 * 64 * 4, 64 * 4);
        var bytes = new byte[64 * 64 * 4];
        System.Runtime.InteropServices.Marshal.Copy(trava.Address, bytes, 0, bytes.Length);
        var i = (32 * 64 + 32) * 4;
        return Color.FromRgb(bytes[i + 2], bytes[i + 1], bytes[i]);
    }

    [Fact]
    public Task IconeComFundoTransparenteGanhaUmaPlacaClara() => Sessao.Rodar(() =>
    {
        Assert.True(FundoDoIcone.Medir(Imagem(transparente: true)));
        Assert.False(FundoDoIcone.Medir(Imagem(transparente: false)));
        Assert.Same(FundoDoIcone.Placa, FundoDoIcone.Instancia.Convert(Imagem(true), typeof(IBrush), null, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Null(FundoDoIcone.Instancia.Convert(Imagem(false), typeof(IBrush), null, System.Globalization.CultureInfo.InvariantCulture));

        var centro = Centro(new Avatar { Width = 64, Height = 64, Imagem = Imagem(true), Trabalhando = true });
        Assert.True(centro.R + centro.G + centro.B > 450, $"O meio do ícone ficou escuro: {centro}");
    });
}
