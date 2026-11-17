using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Nexora.Temas;

namespace Nexora.Controles;

public sealed class FundoIluminado : Control
{
    RenderTargetBitmap? _imagem;
    Size _medida;
    double _escala;
    bool _sujo = true;

    public FundoIluminado() => IsHitTestVisible = false;

    public int Desenhos { get; private set; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Catalogo.Mudou += Sujar;
        Sujar();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Catalogo.Mudou -= Sujar;
        _imagem?.Dispose();
        _imagem = null;
    }

    void Sujar()
    {
        _sujo = true;
        InvalidateVisual();
    }

    IBrush? Pincel(string chave) => this.TryFindResource(chave, out var valor) ? valor as IBrush : null;

    public override void Render(DrawingContext contexto)
    {
        var tamanho = Bounds.Size;
        if (tamanho.Width < 1 || tamanho.Height < 1) return;
        var escala = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        if (_sujo || _medida != tamanho || Math.Abs(_escala - escala) > 0.001) Desenhar(tamanho, escala);
        if (_imagem is not null) contexto.DrawImage(_imagem, new Rect(tamanho));
    }

    void Desenhar(Size tamanho, double escala)
    {
        _sujo = false;
        _medida = tamanho;
        _escala = escala;
        _imagem?.Dispose();
        _imagem = null;
        if (Catalogo.Atual.Luz is null) return;
        var area = new Rect(tamanho);
        var imagem = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(tamanho.Width * escala), (int)Math.Ceiling(tamanho.Height * escala)), new Vector(96 * escala, 96 * escala));
        using (var c = imagem.CreateDrawingContext())
        {
            if (Pincel("LuzDoCanto") is { } canto) c.FillRectangle(canto, area);
            if (Pincel("LuzDoFundo") is { } luz) c.FillRectangle(luz, area);
            if (Pincel("PontosDoFundo") is { } pontos)
            {
                if (Pincel("MascaraDosPontos") is { } mascara)
                    using (c.PushOpacityMask(mascara, area))
                        c.FillRectangle(pontos, area);
                else c.FillRectangle(pontos, area);
            }
        }
        _imagem = imagem;
        Desenhos++;
    }
}
