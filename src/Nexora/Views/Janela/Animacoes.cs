using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Nexora.ViewModels;

namespace Nexora.Views;

public static class Animacoes
{
    public static readonly AttachedProperty<bool> GirarProperty =
        AvaloniaProperty.RegisterAttached<Visual, bool>("Girar", typeof(Animacoes));

    public static readonly AttachedProperty<bool> PulsarProperty =
        AvaloniaProperty.RegisterAttached<Visual, bool>("Pulsar", typeof(Animacoes));

    public static readonly AttachedProperty<bool> EntradaProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("Entrada", typeof(Animacoes));

    public static readonly AttachedProperty<string?> AparecerProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Aparecer", typeof(Animacoes));

    static readonly AttachedProperty<Movimento?> MovimentoProperty =
        AvaloniaProperty.RegisterAttached<Visual, Movimento?>("Movimento", typeof(Animacoes));


    static Animacoes()
    {
        GirarProperty.Changed.AddClassHandler<Visual>((v, _) => Acompanhar(v));
        PulsarProperty.Changed.AddClassHandler<Visual>((v, _) => Acompanhar(v));
        AparecerProperty.Changed.AddClassHandler<Control>((c, e) =>
        {
            c.PropertyChanged -= AoMudarVisivel;
            if (e.NewValue is string) c.PropertyChanged += AoMudarVisivel;
        });
        EntradaProperty.Changed.AddClassHandler<Control>((c, e) =>
        {
            c.AttachedToVisualTree -= Entrar;
            if (e.NewValue is true) c.AttachedToVisualTree += Entrar;
        });
    }

    public static string? GetAparecer(Control c) => c.GetValue(AparecerProperty);
    public static void SetAparecer(Control c, string? valor) => c.SetValue(AparecerProperty, valor);

    static void AoMudarVisivel(object? remetente, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty && e.NewValue is true && remetente is Control c && GetAparecer(c) is { } direcao) Surgir(c, direcao);
    }

    public static void Surgir(Control controle, string direcao)
    {
        if (Aparencia.AnimacoesReduzidas) return;
        AnimarNoCompositor(controle, direcao == "esquerda" ? new Vector3D(-22, 0, 0) : new Vector3D(0, 10, 0), 180, 220);
    }

    static void AnimarNoCompositor(Control controle, Vector3D deslocamento, int msOpacidade, int msMovimento)
    {
        if (ElementComposition.GetElementVisual(controle) is { } antes) antes.Opacity = 0;
        void Comecar(object? remetente, EventArgs e)
        {
            controle.LayoutUpdated -= Comecar;
            if (ElementComposition.GetElementVisual(controle) is not { } composto) return;
            var compositor = composto.Compositor;
            var opacidade = compositor.CreateScalarKeyFrameAnimation();
            opacidade.InsertKeyFrame(0f, 0f, new LinearEasing());
            opacidade.InsertKeyFrame(1f, (float)controle.Opacity, new CubicEaseOut());
            opacidade.Duration = TimeSpan.FromMilliseconds(msOpacidade);
            composto.StartAnimation("Opacity", opacidade);
            var final = new Vector3D(controle.Bounds.X, controle.Bounds.Y, 0);
            var movimento = compositor.CreateVector3DKeyFrameAnimation();
            movimento.InsertKeyFrame(0f, final + deslocamento, new LinearEasing());
            movimento.InsertKeyFrame(1f, final, new CubicEaseOut());
            movimento.Duration = TimeSpan.FromMilliseconds(msMovimento);
            composto.StartAnimation("Offset", movimento);
        }
        controle.LayoutUpdated += Comecar;
        controle.InvalidateArrange();
    }

    public static void Deslizar(params Visual[] alvos)
    {
        if (Aparencia.AnimacoesReduzidas) return;
        foreach (var alvo in alvos)
        {
            if (ElementComposition.GetElementVisual(alvo) is not { } composto) continue;
            var compositor = composto.Compositor;
            var deslize = compositor.CreateVector3DKeyFrameAnimation();
            deslize.Target = "Offset";
            deslize.InsertExpressionKeyFrame(1f, "this.FinalValue", new CubicEaseOut());
            deslize.Duration = TimeSpan.FromMilliseconds(220);
            var implicitas = compositor.CreateImplicitAnimationCollection();
            implicitas["Offset"] = deslize;
            composto.ImplicitAnimations = implicitas;
            DispatcherTimer.RunOnce(() =>
            {
                if (composto.ImplicitAnimations == implicitas) composto.ImplicitAnimations = null;
            }, TimeSpan.FromMilliseconds(450));
        }
    }

    public static bool GetGirar(Visual v) => v.GetValue(GirarProperty);
    public static void SetGirar(Visual v, bool valor) => v.SetValue(GirarProperty, valor);
    public static bool GetPulsar(Visual v) => v.GetValue(PulsarProperty);
    public static void SetPulsar(Visual v, bool valor) => v.SetValue(PulsarProperty, valor);
    public static bool GetEntrada(Control c) => c.GetValue(EntradaProperty);
    public static void SetEntrada(Control c, bool valor) => c.SetValue(EntradaProperty, valor);

    static void Acompanhar(Visual visual)
    {
        var movimento = visual.GetValue(MovimentoProperty);
        if (movimento is null)
        {
            if (!GetGirar(visual) && !GetPulsar(visual)) return;
            movimento = new Movimento(visual);
            visual.SetValue(MovimentoProperty, movimento);
        }
        movimento.Reavaliar();
    }

    static void Entrar(object? remetente, VisualTreeAttachmentEventArgs e)
    {
        if (remetente is not Control controle) return;
        if (controle.DataContext is ItemChat item)
        {
            if (!item.Animar) return;
            item.Animar = false;
        }
        if (Aparencia.AnimacoesReduzidas) return;
        AnimarNoCompositor(controle, new Vector3D(0, 6, 0), 200, 240);
    }

    sealed class Movimento
    {
        const string Rotacao = "RotationAngle";
        const string Escala = "Scale";
        readonly Visual _visual;
        bool _girando, _pulsando, _ligado;
        int _tentativas;

        public Movimento(Visual visual)
        {
            _visual = visual;
            visual.AttachedToVisualTree += (_, _) =>
            {
                if (!_ligado) Pulso.Instancia.AtividadeMudou += Reavaliar;
                _ligado = true;
                Reavaliar();
            };
            visual.DetachedFromVisualTree += (_, _) =>
            {
                Pulso.Instancia.AtividadeMudou -= Reavaliar;
                _ligado = false;
                Parar();
            };
            visual.PropertyChanged += (_, e) =>
            {
                if (e.Property == Visual.BoundsProperty) Centralizar();
            };
            if (visual.IsAttachedToVisualTree())
            {
                Pulso.Instancia.AtividadeMudou += Reavaliar;
                _ligado = true;
            }
        }

        public void Reavaliar()
        {
            var ativo = _visual.IsAttachedToVisualTree() && Pulso.Instancia.Animando;
            var girar = ativo && GetGirar(_visual);
            var pulsar = ativo && GetPulsar(_visual);
            if (girar == _girando && pulsar == _pulsando) return;
            if (ElementComposition.GetElementVisual(_visual) is not { } composto)
            {
                if (_tentativas++ < 20) Dispatcher.UIThread.Post(Reavaliar, DispatcherPriority.Background);
                return;
            }
            _tentativas = 0;
            Centralizar();
            var compositor = composto.Compositor;
            if (girar != _girando)
            {
                if (girar)
                {
                    var giro = compositor.CreateScalarKeyFrameAnimation();
                    giro.InsertKeyFrame(0f, 0f, new LinearEasing());
                    giro.InsertKeyFrame(1f, (float)(Math.PI * 2), new LinearEasing());
                    giro.Duration = TimeSpan.FromSeconds(2.6);
                    giro.IterationBehavior = AnimationIterationBehavior.Forever;
                    composto.StartAnimation(Rotacao, giro);
                }
                else
                {
                    composto.StopAnimation(Rotacao);
                    composto.RotationAngle = 0;
                }
                _girando = girar;
            }
            if (pulsar != _pulsando || girar != _pulsandoComGiro)
            {
                var respirar = pulsar || girar;
                if (respirar)
                {
                    var minimo = pulsar && !girar ? 0.55 : 0.82;
                    var escala = compositor.CreateVector3DKeyFrameAnimation();
                    escala.InsertKeyFrame(0f, new Vector3D(1, 1, 1), new SineEaseInOut());
                    escala.InsertKeyFrame(0.5f, new Vector3D(minimo, minimo, 1), new SineEaseInOut());
                    escala.InsertKeyFrame(1f, new Vector3D(1, 1, 1), new SineEaseInOut());
                    escala.Duration = TimeSpan.FromSeconds(girar ? 1.6 : 1.4);
                    escala.IterationBehavior = AnimationIterationBehavior.Forever;
                    composto.StartAnimation(Escala, escala);
                }
                else
                {
                    composto.StopAnimation(Escala);
                    composto.Scale = new Vector3D(1, 1, 1);
                }
                _pulsando = pulsar;
                _pulsandoComGiro = girar;
            }
        }

        bool _pulsandoComGiro;

        void Centralizar()
        {
            if (ElementComposition.GetElementVisual(_visual) is { } composto)
                composto.CenterPoint = new Vector3D(_visual.Bounds.Width / 2, _visual.Bounds.Height / 2, 0);
        }

        void Parar()
        {
            if (ElementComposition.GetElementVisual(_visual) is { } composto)
            {
                composto.StopAnimation(Rotacao);
                composto.StopAnimation(Escala);
                composto.RotationAngle = 0;
                composto.Scale = new Vector3D(1, 1, 1);
            }
            _girando = false;
            _pulsando = false;
            _pulsandoComGiro = false;
        }
    }
}
