using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Nexora.Core.Avatares;

namespace Nexora.Views;

public sealed class AvatarVivo : Control
{
    public static readonly StyledProperty<string?> AvatarProperty = AvaloniaProperty.Register<AvatarVivo, string?>(nameof(Avatar));
    public static readonly StyledProperty<string?> CorProperty = AvaloniaProperty.Register<AvatarVivo, string?>(nameof(Cor));
    public static readonly StyledProperty<string?> AgenteProperty = AvaloniaProperty.Register<AvatarVivo, string?>(nameof(Agente));
    public static readonly StyledProperty<string?> SubagenteProperty = AvaloniaProperty.Register<AvatarVivo, string?>(nameof(Subagente));
    public static readonly StyledProperty<HumorAvatar> HumorProperty = AvaloniaProperty.Register<AvatarVivo, HumorAvatar>(nameof(Humor));
    public static readonly StyledProperty<bool> SegueMouseProperty = AvaloniaProperty.Register<AvatarVivo, bool>(nameof(SegueMouse), true);
    public static readonly StyledProperty<bool> ParadoProperty = AvaloniaProperty.Register<AvatarVivo, bool>(nameof(Parado));
    public static readonly StyledProperty<bool> DesbotadoProperty = AvaloniaProperty.Register<AvatarVivo, bool>(nameof(Desbotado));
    public static readonly StyledProperty<string?> RotuloProperty = AvaloniaProperty.Register<AvatarVivo, string?>(nameof(Rotulo));
    public static readonly StyledProperty<bool> ComemorarProperty = AvaloniaProperty.Register<AvatarVivo, bool>(nameof(Comemorar));

    const double TamanhoPequeno = 128;
    const double QuadrosPequeno = 30;
    const double Folga = 0.004;
    static readonly TimeSpan TempoDeComemorar = TimeSpan.FromSeconds(4);

    static readonly Stopwatch Relogio = Stopwatch.StartNew();

    readonly Animador _animador = new();
    DefinicaoAvatar? _definicao;
    string _cor = Quadro.CorPadrao;
    AvatarPreparado? _preparado;
    QuadroAvatar? _quadro;
    TopLevel? _topo;
    Point? _ponteiro;
    bool _pedido;
    bool _continuar;
    bool _ouvindo;
    double _ultimoDesenho = double.NegativeInfinity;
    int _geracao;
    double _comemorarAte = double.NegativeInfinity;

    public string? Avatar { get => GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }
    public string? Cor { get => GetValue(CorProperty); set => SetValue(CorProperty, value); }
    public string? Agente { get => GetValue(AgenteProperty); set => SetValue(AgenteProperty, value); }
    public string? Subagente { get => GetValue(SubagenteProperty); set => SetValue(SubagenteProperty, value); }
    public HumorAvatar Humor { get => GetValue(HumorProperty); set => SetValue(HumorProperty, value); }
    public bool SegueMouse { get => GetValue(SegueMouseProperty); set => SetValue(SegueMouseProperty, value); }
    public bool Parado { get => GetValue(ParadoProperty); set => SetValue(ParadoProperty, value); }
    public bool Desbotado { get => GetValue(DesbotadoProperty); set => SetValue(DesbotadoProperty, value); }
    public string? Rotulo { get => GetValue(RotuloProperty); set => SetValue(RotuloProperty, value); }
    public bool Comemorar { get => GetValue(ComemorarProperty); set => SetValue(ComemorarProperty, value); }

    public HumorAvatar HumorAtual => Relogio.Elapsed.TotalSeconds < _comemorarAte ? HumorAvatar.Feliz : Humor;

    public AvatarPreparado? Preparado => _preparado;
    public QuadroAvatar? UltimoQuadro => _quadro;
    public int QuadrosMontados { get; private set; }

    public AvatarVivo()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
        EffectiveViewportChanged += (_, _) => PedirQuadro();
    }

    bool Reduzido => Parado || Aparencia.AnimacoesReduzidas;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topo = TopLevel.GetTopLevel(this);
        _animador.Reiniciar();
        AtualizarPonteiro();
        Carregar();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Desligar();
        _topo = null;
        _pedido = false;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AvatarProperty || change.Property == CorProperty || change.Property == AgenteProperty || change.Property == SubagenteProperty)
            Carregar();
        else if (change.Property == SegueMouseProperty || change.Property == ParadoProperty)
        {
            AtualizarPonteiro();
            Redesenhar();
        }
        else if (change.Property == HumorProperty || change.Property == DesbotadoProperty)
            Redesenhar();
        else if (change.Property == RotuloProperty)
            AtualizarRotulo();
        else if (change.Property == ComemorarProperty && change.NewValue is true && _topo is not null)
        {
            _comemorarAte = Relogio.Elapsed.TotalSeconds + TempoDeComemorar.TotalSeconds;
            Redesenhar();
            DispatcherTimer.RunOnce(Redesenhar, TempoDeComemorar + TimeSpan.FromMilliseconds(50));
        }
    }

    void Carregar()
    {
        var escolha = Subagente is not null ? CatalogoDeAvatares.Subagente(Subagente) : Agente is not null ? CatalogoDeAvatares.Agente(Agente) : null;
        var definicao = Avatar is { Length: > 0 } id ? CatalogoDeAvatares.Definicao(id) : escolha?.Definicao;
        _cor = Quadro.CorHex(Cor ?? escolha?.Cor, Quadro.CorPadrao);
        AtualizarRotulo(definicao);
        if (definicao == _definicao && _preparado is not null)
        {
            Redesenhar();
            return;
        }
        _definicao = definicao;
        _preparado = null;
        _quadro = null;
        var geracao = ++_geracao;
        InvalidateVisual();
        if (definicao is null) return;
        Task.Run(() => Quadro.Preparar(definicao)).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
                Dispatcher.UIThread.Post(() =>
                {
                    if (geracao != _geracao) return;
                    _preparado = t.Result;
                    Redesenhar();
                });
        }, TaskScheduler.Default);
    }

    void AtualizarRotulo(DefinicaoAvatar? definicao = null)
    {
        definicao ??= _definicao;
        if (Rotulo == "")
        {
            AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
            ClearValue(AutomationProperties.NameProperty);
            return;
        }
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
        AutomationProperties.SetName(this, Rotulo ?? definicao?.Nome);
    }

    void AtualizarPonteiro()
    {
        var querer = _topo is not null && SegueMouse && !Parado;
        if (querer == _ouvindo) return;
        if (querer) Ligar();
        else Desligar();
    }

    void Ligar()
    {
        if (_topo is null) return;
        _topo.AddHandler(PointerMovedEvent, AoMover, RoutingStrategies.Tunnel, handledEventsToo: true);
        _topo.AddHandler(PointerPressedEvent, AoMover, RoutingStrategies.Tunnel, handledEventsToo: true);
        _topo.PointerExited += AoSair;
        _ouvindo = true;
    }

    void Desligar()
    {
        if (_topo is not null && _ouvindo)
        {
            _topo.RemoveHandler(PointerMovedEvent, AoMover);
            _topo.RemoveHandler(PointerPressedEvent, AoMover);
            _topo.PointerExited -= AoSair;
        }
        _ouvindo = false;
        _ponteiro = null;
    }

    void AoMover(object? remetente, PointerEventArgs e)
    {
        if (_topo is null) return;
        _ponteiro = e.GetPosition(_topo);
        if (Reduzido) PedirQuadro();
    }

    void AoSair(object? remetente, PointerEventArgs e)
    {
        _ponteiro = null;
        PedirQuadro();
    }

    void Redesenhar()
    {
        _quadro = null;
        InvalidateVisual();
        PedirQuadro();
    }

    void PedirQuadro()
    {
        if (_pedido || _topo is null || _preparado is null || !IsEffectivelyVisible) return;
        _pedido = true;
        _topo.RequestAnimationFrame(AoQuadro);
    }

    void AoQuadro(TimeSpan _)
    {
        _pedido = false;
        if (_topo is null || _preparado is null || !IsEffectivelyVisible) return;
        var agora = Relogio.Elapsed.TotalSeconds;
        var lado = Math.Min(Bounds.Width, Bounds.Height);
        if (_quadro is not null && lado > 0 && lado < TamanhoPequeno && agora - _ultimoDesenho < 1 / QuadrosPequeno - Folga)
        {
            PedirQuadro();
            return;
        }
        _ultimoDesenho = agora;
        Montar();
        InvalidateVisual();
        if (_continuar) PedirQuadro();
    }

    public void Avancar()
    {
        if (_preparado is null) return;
        _ultimoDesenho = Relogio.Elapsed.TotalSeconds;
        Montar();
        InvalidateVisual();
    }

    (double X, double Y)? Alvo(double lado, double ox, double oy)
    {
        if (!SegueMouse || Parado || _ponteiro is not { } p || _topo is null || _definicao is null || lado <= 0) return null;
        if (this.TranslatePoint(new Point(ox, oy), _topo) is not { } canto) return null;
        var origemX = Math.Clamp(_definicao.Olhos.X / 100, 0, 1);
        var origemY = Math.Clamp(_definicao.Olhos.Y / 100, 0, 1);
        return Seguir.OlharParaPonteiro(p.X, p.Y, canto.X, canto.Y, lado, lado, origemX, origemY);
    }

    void Montar()
    {
        if (_preparado is null) return;
        var lado = Math.Min(Bounds.Width, Bounds.Height);
        double ox = (Bounds.Width - lado) / 2, oy = (Bounds.Height - lado) / 2;
        var reduzido = Reduzido;
        var humor = HumorAtual;
        var passo = _animador.Passo(Relogio.Elapsed.TotalMilliseconds, humor, Alvo(lado, ox, oy), false, reduzido);
        _quadro = Quadro.Montar(_preparado, new OpcoesDoQuadro
        {
            Cor = _cor,
            Tamanho = lado > 0 ? lado : null,
            Tempo = Relogio.Elapsed.TotalSeconds,
            Olhar = passo.Olhar,
            Humor = humor,
            Mistura = passo.Mistura,
            Reduzido = reduzido,
        });
        _continuar = !reduzido || passo.Movendo;
        QuadrosMontados++;
    }

    public override void Render(DrawingContext contexto)
    {
        if (_preparado is null) return;
        if (_quadro is null) Montar();
        if (_quadro is not { } quadro) return;
        if (!_pedido && _continuar) PedirQuadro();
        var lado = Math.Min(Bounds.Width, Bounds.Height);
        if (lado <= 0) return;
        double ox = (Bounds.Width - lado) / 2, oy = (Bounds.Height - lado) / 2;
        PintorDeAvatar.Pintar(contexto, _preparado, quadro, new Rect(ox, oy, lado, lado), Desbotado);
    }
}
