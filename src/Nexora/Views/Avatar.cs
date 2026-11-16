using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Threading;

namespace Nexora.Views;

public sealed class Avatar : Control
{
    public static readonly StyledProperty<IImage?> ImagemProperty = AvaloniaProperty.Register<Avatar, IImage?>(nameof(Imagem));
    public static readonly StyledProperty<string?> IniciaisProperty = AvaloniaProperty.Register<Avatar, string?>(nameof(Iniciais));
    public static readonly StyledProperty<bool> TrabalhandoProperty = AvaloniaProperty.Register<Avatar, bool>(nameof(Trabalhando));
    public static readonly StyledProperty<bool> PrecisaProperty = AvaloniaProperty.Register<Avatar, bool>(nameof(Precisa));
    public static readonly StyledProperty<bool> ProntaProperty = AvaloniaProperty.Register<Avatar, bool>(nameof(Pronta));
    public static readonly StyledProperty<bool> ErroProperty = AvaloniaProperty.Register<Avatar, bool>(nameof(Erro));
    public static readonly StyledProperty<double> FracaoProperty = AvaloniaProperty.Register<Avatar, double>(nameof(Fracao));

    readonly Orbita _orbita = new() { IsVisible = false };
    bool _girando;
    int _tentativas;

    static Avatar()
    {
        AffectsRender<Avatar>(ImagemProperty, IniciaisProperty, TrabalhandoProperty, PrecisaProperty, ProntaProperty, ErroProperty, FracaoProperty);
        TrabalhandoProperty.Changed.AddClassHandler<Avatar>((a, _) => a.Reavaliar());
        ErroProperty.Changed.AddClassHandler<Avatar>((a, _) => a.Reavaliar());
        PrecisaProperty.Changed.AddClassHandler<Avatar>((a, _) => a.Reavaliar());
    }

    public Avatar() => VisualChildren.Add(_orbita);

    public IImage? Imagem { get => GetValue(ImagemProperty); set => SetValue(ImagemProperty, value); }
    public string? Iniciais { get => GetValue(IniciaisProperty); set => SetValue(IniciaisProperty, value); }
    public bool Trabalhando { get => GetValue(TrabalhandoProperty); set => SetValue(TrabalhandoProperty, value); }
    public bool Precisa { get => GetValue(PrecisaProperty); set => SetValue(PrecisaProperty, value); }
    public bool Pronta { get => GetValue(ProntaProperty); set => SetValue(ProntaProperty, value); }
    public bool Erro { get => GetValue(ErroProperty); set => SetValue(ErroProperty, value); }
    public double Fracao { get => GetValue(FracaoProperty); set => SetValue(FracaoProperty, value); }

    bool Girar => Trabalhando && !Precisa && !Erro;

    protected override Size MeasureOverride(Size disponivel)
    {
        _orbita.Measure(disponivel);
        return base.MeasureOverride(disponivel);
    }

    protected override Size ArrangeOverride(Size final)
    {
        _orbita.Arrange(new Rect(final));
        return final;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Pulso.Instancia.AtividadeMudou += Reavaliar;
        Reavaliar();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Pulso.Instancia.AtividadeMudou -= Reavaliar;
        Parar();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty) Centralizar();
    }

    void Reavaliar()
    {
        var girar = Girar && VisualRoot is not null && Pulso.Instancia.Animando;
        _orbita.IsVisible = Girar;
        _orbita.Cor = Recurso("Ciano", "#86C5E6");
        if (girar == _girando) return;
        if (!girar)
        {
            Parar();
            return;
        }
        if (ElementComposition.GetElementVisual(_orbita) is not { } composto)
        {
            if (_tentativas++ < 20) Dispatcher.UIThread.Post(Reavaliar, DispatcherPriority.Background);
            return;
        }
        _tentativas = 0;
        Centralizar();
        var giro = composto.Compositor.CreateScalarKeyFrameAnimation();
        giro.InsertKeyFrame(0f, 0f, new LinearEasing());
        giro.InsertKeyFrame(1f, (float)(Math.PI * 2), new LinearEasing());
        giro.Duration = TimeSpan.FromSeconds(1.6);
        giro.IterationBehavior = AnimationIterationBehavior.Forever;
        composto.StartAnimation("RotationAngle", giro);
        _girando = true;
    }

    void Parar()
    {
        if (!_girando) return;
        _girando = false;
        if (ElementComposition.GetElementVisual(_orbita) is not { } composto) return;
        composto.StopAnimation("RotationAngle");
        composto.RotationAngle = 0;
    }

    void Centralizar()
    {
        if (ElementComposition.GetElementVisual(_orbita) is { } composto)
            composto.CenterPoint = new Avalonia.Vector3D(Bounds.Width / 2, Bounds.Height / 2, 0);
    }

    IBrush Recurso(string chave, string reserva) =>
        this.TryFindResource(chave, ActualThemeVariant, out var r) && r is IBrush b ? b : new SolidColorBrush(Color.Parse(reserva));

    IBrush? CorDoEstado() =>
        Erro ? Recurso("Vermelho", "#E8806C")
        : Precisa ? Recurso("Laranja", "#F0A860")
        : Trabalhando ? Recurso("Ciano", "#86C5E6")
        : Pronta ? Recurso("Verde", "#7BCB9F")
        : null;

    public static double Espessura(double tamanho) => Math.Max(1.6, tamanho / 20);

    public override void Render(DrawingContext contexto)
    {
        var tamanho = Math.Min(Bounds.Width, Bounds.Height);
        if (tamanho <= 4) return;
        var centro = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var estado = CorDoEstado();
        var espessura = Espessura(tamanho);
        var raio = tamanho / 2 - espessura / 2;
        contexto.DrawEllipse(null, new Pen(Recurso("SuperficieAlta", "#33FFFFFF"), espessura), centro, raio, raio);
        var fracao = Math.Clamp(Fracao, 0, 1);
        var cor = estado ?? Recurso("TextoFraco", "#8F8A83");
        if (fracao >= 0.999) contexto.DrawEllipse(null, new Pen(cor, espessura), centro, raio, raio);
        else if (fracao > 0.005) contexto.DrawGeometry(null, new Pen(cor, espessura, lineCap: PenLineCap.Round), Arco(centro, raio, fracao));

        var lado = Math.Round(tamanho - espessura * 2 - Math.Max(3, tamanho * 0.1) * 2);
        var caixa = new Rect(centro.X - lado / 2, centro.Y - lado / 2, lado, lado);
        var forma = new RoundedRect(caixa, lado * 0.28);
        using (contexto.PushClip(forma))
        {
            if (Imagem is { } imagem)
            {
                var origem = imagem.Size;
                var escala = Math.Max(lado / origem.Width, lado / origem.Height);
                var recorte = new Rect((origem.Width - lado / escala) / 2, (origem.Height - lado / escala) / 2, lado / escala, lado / escala);
                contexto.DrawImage(imagem, recorte, caixa);
            }
            else
            {
                contexto.DrawRectangle(Recurso("MarcaGradiente", "#5B5470"), null, caixa);
                if (Iniciais is { Length: > 0 } iniciais)
                {
                    var familia = this.TryFindResource("FonteUi", ActualThemeVariant, out var f) && f is FontFamily ff ? ff : FontFamily.Default;
                    var texto = new FormattedText(iniciais, Core.Texto.Idioma.Cultura, FlowDirection.LeftToRight,
                        new Typeface(familia, FontStyle.Normal, FontWeight.SemiBold), Math.Max(8, lado * 0.38), Brushes.White);
                    contexto.DrawText(texto, new Point(centro.X - texto.Width / 2, centro.Y - texto.Height / 2));
                }
            }
        }
        if (estado is null) contexto.DrawRectangle(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), null, forma);
    }

    static StreamGeometry Arco(Point centro, double raio, double fracao)
    {
        var g = new StreamGeometry();
        using var c = g.Open();
        var angulo = fracao * 2 * Math.PI - Math.PI / 2;
        c.BeginFigure(new Point(centro.X, centro.Y - raio), false);
        c.ArcTo(new Point(centro.X + raio * Math.Cos(angulo), centro.Y + raio * Math.Sin(angulo)), new Size(raio, raio), 0, fracao > 0.5, SweepDirection.Clockwise);
        c.EndFigure(false);
        return g;
    }

    sealed class Orbita : Control
    {
        IBrush? _cor;

        public IBrush? Cor
        {
            get => _cor;
            set
            {
                if (ReferenceEquals(_cor, value)) return;
                _cor = value;
                InvalidateVisual();
            }
        }

        public override void Render(DrawingContext contexto)
        {
            var tamanho = Math.Min(Bounds.Width, Bounds.Height);
            if (tamanho <= 4 || _cor is null) return;
            var espessura = Espessura(tamanho);
            contexto.DrawEllipse(_cor, null, new Point(Bounds.Width / 2, (Bounds.Height - tamanho) / 2 + espessura / 2), espessura * 1.15, espessura * 1.15);
        }
    }
}
