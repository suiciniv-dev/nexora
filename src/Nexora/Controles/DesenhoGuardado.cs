using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Nexora.Core.Texto;

namespace Nexora.Controles;

public abstract class DesenhoGuardado : Control
{
    RenderTargetBitmap? _imagem;
    Size _medida;
    double _escala;
    bool _sujo = true;

    public int Desenhos { get; private set; }

    protected abstract void Pintar(DrawingContext contexto, Size tamanho);

    protected virtual void PintarPorCima(DrawingContext contexto, Size tamanho) { }

    protected FormattedText Rotulo(string texto, IBrush? cor, double tamanho = 11)
    {
        var familia = this.TryFindResource("FonteUi", ActualThemeVariant, out var f) && f is FontFamily ff ? ff : FontFamily.Default;
        return new FormattedText(texto, Idioma.Cultura, FlowDirection.LeftToRight, new Typeface(familia, FontStyle.Normal, FontWeight.Medium), tamanho, cor ?? Brushes.Gray);
    }

    protected static Point NoPixel(double x, double y) => new(Math.Round(x), Math.Round(y));

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property.OwnerType == GetType())
        {
            _sujo = true;
            InvalidateVisual();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _imagem?.Dispose();
        _imagem = null;
        _sujo = true;
    }

    public sealed override void Render(DrawingContext contexto)
    {
        var tamanho = Bounds.Size;
        if (tamanho.Width < 1 || tamanho.Height < 1) return;
        var escala = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        if (_sujo || _imagem is null || _medida != tamanho || Math.Abs(_escala - escala) > 0.001)
        {
            _imagem?.Dispose();
            _imagem = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(tamanho.Width * escala), (int)Math.Ceiling(tamanho.Height * escala)), new Vector(96 * escala, 96 * escala));
            using (var c = _imagem.CreateDrawingContext()) Pintar(c, tamanho);
            (_medida, _escala, _sujo) = (tamanho, escala, false);
            Desenhos++;
        }
        contexto.DrawImage(_imagem, new Rect(tamanho));
        PintarPorCima(contexto, tamanho);
    }
}
