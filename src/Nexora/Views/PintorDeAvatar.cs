using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Nexora.Core.Avatares;

namespace Nexora.Views;

public static class PintorDeAvatar
{
    static readonly ConditionalWeakTable<AvatarPreparado, Dictionary<string, StreamGeometry>> Geometrias = new();

    static Avalonia.Matrix Para(Core.Avatares.Matriz m) => new(m.A, m.B, m.C, m.D, m.E, m.F);

    static StreamGeometry Geometria(AvatarPreparado p, CaminhoDoQuadro caminho)
    {
        var mapa = Geometrias.GetValue(p, _ => []);
        if (mapa.TryGetValue(caminho.Id, out var pronta)) return pronta;
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.SetFillRule(caminho.ParOuImpar ? FillRule.EvenOdd : FillRule.NonZero);
            var aberto = false;
            foreach (var s in caminho.Desenho)
            {
                switch (s.Tipo)
                {
                    case TipoSegmento.M:
                        if (aberto) c.EndFigure(true);
                        c.BeginFigure(new Point(s.X, s.Y), true);
                        aberto = true;
                        break;
                    case TipoSegmento.L: c.LineTo(new Point(s.X, s.Y)); break;
                    case TipoSegmento.C: c.CubicBezierTo(new Point(s.X1, s.Y1), new Point(s.X2, s.Y2), new Point(s.X, s.Y)); break;
                    case TipoSegmento.Q: c.QuadraticBezierTo(new Point(s.X1, s.Y1), new Point(s.X, s.Y)); break;
                    case TipoSegmento.A:
                        c.ArcTo(new Point(s.X, s.Y), new Size(s.Rx, s.Ry), s.Rotacao, s.Grande == 1, s.Horario == 1 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
                        break;
                    case TipoSegmento.Z:
                        if (aberto) c.EndFigure(true);
                        aberto = false;
                        break;
                }
            }
            if (aberto) c.EndFigure(true);
        }
        mapa[caminho.Id] = g;
        return g;
    }

    static Color Desbotar(Color c)
    {
        var cinza = (byte)(0.3 * c.R + 0.59 * c.G + 0.11 * c.B);
        return Color.FromRgb((byte)((c.R + cinza * 2) / 3), (byte)((c.G + cinza * 2) / 3), (byte)((c.B + cinza * 2) / 3));
    }

    const double Contorno = 5.5;
    const double BordaDoOlho = 2.2;
    static readonly ConcurrentDictionary<(uint Cor, bool Escuro), WriteableBitmap> Ladrilhos = new();

    static readonly ConditionalWeakTable<AvatarPreparado, Dictionary<string, Geometry>> Contornos = new();
    static readonly ImmutablePen CanetaDoContorno = new(Brushes.Black, Contorno, lineJoin: PenLineJoin.Round);

    static Geometry ContornoDe(AvatarPreparado p, CaminhoDoQuadro caminho)
    {
        var mapa = Contornos.GetValue(p, _ => []);
        if (mapa.TryGetValue(caminho.Id, out var pronto)) return pronto;
        var largo = Geometria(p, caminho).GetWidenedGeometry(CanetaDoContorno) ?? Geometria(p, caminho);
        mapa[caminho.Id] = largo;
        return largo;
    }

    static Color Vezes(Color c, double k) => Color.FromRgb((byte)Js.Arredondar(c.R * k), (byte)Js.Arredondar(c.G * k), (byte)Js.Arredondar(c.B * k));

    static Color Clarear(Color c, double k) => Color.FromRgb((byte)Js.Arredondar(c.R + (255 - c.R) * k), (byte)Js.Arredondar(c.G + (255 - c.G) * k), (byte)Js.Arredondar(c.B + (255 - c.B) * k));

    static Color Misturar(Color a, Color b, double t) =>
        Color.FromRgb((byte)Js.Arredondar(a.R + (b.R - a.R) * t), (byte)Js.Arredondar(a.G + (b.G - a.G) * t), (byte)Js.Arredondar(a.B + (b.B - a.B) * t));

    static unsafe WriteableBitmap Ladrilho(Color cor, bool escuro) => Ladrilhos.GetOrAdd((cor.ToUInt32(), escuro), _ =>
    {
        var bitmap = new WriteableBitmap(new PixelSize(2, 2), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var buffer = bitmap.Lock();
        var pixels = (uint*)buffer.Address;
        var cheio = 0xFF000000u | ((uint)cor.R << 16) | ((uint)cor.G << 8) | cor.B;
        var linha = buffer.RowBytes / 4;
        pixels[0] = escuro ? 0 : cheio;
        pixels[1] = escuro ? cheio : 0;
        pixels[linha] = escuro ? cheio : 0;
        pixels[linha + 1] = 0;
        return bitmap;
    });

    static void Pontilhar(DrawingContext contexto, Rect area, Rect alvo, Color cor, bool escuro, double opacidade)
    {
        var celula = Math.Max(1, Js.Arredondar(area.Width / 48));
        var pincel = new ImageBrush(Ladrilho(cor, escuro))
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.Fill,
            DestinationRect = new RelativeRect(area.X - alvo.X, area.Y - alvo.Y, 2 * celula, 2 * celula, RelativeUnit.Absolute),
            Opacity = opacidade,
        };
        contexto.DrawRectangle(pincel, null, alvo);
    }

    public static void Pintar(DrawingContext contexto, AvatarPreparado preparado, QuadroAvatar quadro, Rect area, bool desbotado)
    {
        var vista = Avalonia.Matrix.CreateScale(area.Width / 100, area.Height / 100) * Avalonia.Matrix.CreateTranslation(area.X, area.Y);
        var raiz = Para(quadro.Raiz) * vista;
        var cor = Color.Parse(quadro.Cor);
        if (desbotado) cor = Desbotar(cor);
        var escuro = Vezes(cor, 0.38);
        var sombra = Vezes(cor, 0.62);
        var luz = Clarear(cor, 0.45);
        var caminhos = new Dictionary<string, CaminhoDoQuadro>(quadro.Caminhos.Count);
        foreach (var c in quadro.Caminhos) caminhos[c.Id] = c;
        var fatias = quadro.Formas.Count(f => !f.Olho);
        var branco = new ImmutableSolidColorBrush(Colors.White);
        var bordaDoOlho = new ImmutablePen(new ImmutableSolidColorBrush(escuro), BordaDoOlho);
        using var opacidade = contexto.PushOpacity(desbotado ? 0.6 : 1);
        using var opcoes = contexto.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None });
        var tracoEscuro = new ImmutableSolidColorBrush(escuro);
        foreach (var forma in quadro.Formas)
        {
            if (forma.Olho || forma.Caminho is not { } id || !caminhos.TryGetValue(id, out var caminho)) continue;
            using var transformacao = contexto.PushTransform(Para(forma.Matriz) * raiz);
            contexto.DrawGeometry(tracoEscuro, null, ContornoDe(preparado, caminho));
        }
        var i = 0;
        foreach (var forma in quadro.Formas)
        {
            var matriz = Para(forma.Matriz) * raiz;
            if (!forma.Olho)
            {
                if (forma.Caminho is not { } id || !caminhos.TryGetValue(id, out var caminho)) continue;
                var t = fatias > 1 ? (double)i / (fatias - 1) : 1;
                var geometria = Geometria(preparado, caminho);
                using (contexto.PushTransform(matriz))
                    contexto.DrawGeometry(new ImmutableSolidColorBrush(Misturar(sombra, cor, Math.Pow(t, 0.7))), null, geometria);
                i++;
                if (i == fatias)
                {
                    var recorte = geometria.Clone();
                    recorte.Transform = new MatrixTransform(matriz);
                    using (contexto.PushGeometryClip(recorte))
                    {
                        Pontilhar(contexto, area, new Rect(area.X, area.Y, area.Width * 0.62, area.Height * 0.5), luz, false, 0.7);
                        Pontilhar(contexto, area, new Rect(area.X, area.Y + area.Height * 0.58, area.Width, area.Height * 0.42), Colors.Black, true, 0.24);
                    }
                }
                continue;
            }
            if (forma.Marca is not { } marca) continue;
            using var olho = contexto.PushTransform(matriz);
            if (!marca.Arco)
            {
                var pilula = new Rect(marca.X, marca.Y, marca.Largura, marca.Altura);
                contexto.DrawRectangle(null, bordaDoOlho, pilula, marca.Raio, marca.Raio);
                contexto.DrawRectangle(branco, null, pilula, marca.Raio, marca.Raio);
                continue;
            }
            var arco = new StreamGeometry();
            using (var c = arco.Open())
            {
                c.BeginFigure(new Point(marca.Inicio.X, marca.Inicio.Y), false);
                c.QuadraticBezierTo(new Point(marca.Controle.X, marca.Controle.Y), new Point(marca.Fim.X, marca.Fim.Y));
                c.EndFigure(false);
            }
            contexto.DrawGeometry(null, new ImmutablePen(new ImmutableSolidColorBrush(escuro), marca.Traco, lineCap: PenLineCap.Round), arco);
        }
    }
}
