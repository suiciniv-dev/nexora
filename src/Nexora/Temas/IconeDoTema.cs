using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Nexora.Temas;

public static class IconeDoTema
{
    const string Glifo = "F1 M51,208L51,66.5L73,66.5L73,208ZM82.26,55.27L190.71,186.73L173.74,200.73L65.29,69.27ZM183,189.5L183,48L205,48L205,189.5Z" +
        "M36,208A26,26 0 1 1 88,208A26,26 0 1 1 36,208ZM168,48A26,26 0 1 1 220,48A26,26 0 1 1 168,48ZM36,48A26,26 0 1 1 88,48A26,26 0 1 1 36,48Z" +
        "M51,48A11,11 0 1 0 73,48A11,11 0 1 0 51,48ZM168,208A26,26 0 1 1 220,208A26,26 0 1 1 168,208ZM183,208A11,11 0 1 0 205,208A11,11 0 1 0 183,208Z";

    static readonly Dictionary<string, WindowIcon> Feitos = [];

    public static (Color Topo, Color Base) Cores(Tema tema) =>
        (Misturar(tema.Cor("Marca"), Color.Parse("#121014"), 0.62), Misturar(tema.Cor("MarcaFim"), Color.Parse("#0B0A0D"), 0.68));

    public static WindowIcon Icone(Tema tema)
    {
        var (topo, baseCor) = Cores(tema);
        var chave = $"{topo}{baseCor}";
        if (Feitos.TryGetValue(chave, out var feito)) return feito;
        using var bitmap = Desenhar(topo, baseCor, 256);
        using var fluxo = new MemoryStream();
        bitmap.Save(fluxo);
        fluxo.Position = 0;
        return Feitos[chave] = new WindowIcon(fluxo);
    }

    public static RenderTargetBitmap Desenhar(Color topo, Color baseCor, int lado)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(lado, lado), new Vector(96, 96));
        using var ctx = bitmap.CreateDrawingContext();
        using (ctx.PushTransform(Matrix.CreateScale(lado / 1024.0, lado / 1024.0)))
        {
            var ladrilho = Squircle(512, 512, 480);
            ctx.DrawGeometry(Vertical(topo, baseCor), null, ladrilho);
            ctx.DrawGeometry(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.FromArgb(56, 255, 255, 255), 0), new GradientStop(Color.FromArgb(0, 255, 255, 255), 1) },
            }, null, ladrilho);
            ctx.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)), 4), Squircle(512, 512, 478));
            var glifo = Geometry.Parse(Glifo);
            var escala = 0.36 * 1024 / 184;
            var dx = 512 - (36 + 92) * escala;
            var dy = 492 - (22 + 106) * escala;
            glifo.Transform = new MatrixTransform(Matrix.CreateScale(escala, escala) * Matrix.CreateTranslation(dx, dy));
            ctx.DrawGeometry(Vertical(Colors.White, Color.Parse("#EEEAF5")), null, glifo);
        }
        return bitmap;
    }

    static LinearGradientBrush Vertical(Color a, Color b) => new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(a, 0), new GradientStop(b, 1) },
    };

    static StreamGeometry Squircle(double cx, double cy, double a, double n = 5, int amostras = 180)
    {
        var geometria = new StreamGeometry();
        using var g = geometria.Open();
        for (var i = 0; i < amostras; i++)
        {
            var t = 2 * Math.PI * i / amostras;
            var (c, s) = (Math.Cos(t), Math.Sin(t));
            var p = new Point(cx + a * Math.CopySign(Math.Pow(Math.Abs(c), 2 / n), c), cy + a * Math.CopySign(Math.Pow(Math.Abs(s), 2 / n), s));
            if (i == 0) g.BeginFigure(p, true);
            else g.LineTo(p);
        }
        g.EndFigure(true);
        return geometria;
    }

    static Color Misturar(Color a, Color b, double k) =>
        Color.FromRgb((byte)Math.Round(a.R + (b.R - a.R) * k), (byte)Math.Round(a.G + (b.G - a.G) * k), (byte)Math.Round(a.B + (b.B - a.B) * k));
}
