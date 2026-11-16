using System.Collections.Concurrent;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Nexora.Fundos;
using SkiaSharp;

namespace Nexora.Views;

public sealed class FundoVivo : Control
{
    public static readonly StyledProperty<string?> EstiloProperty =
        AvaloniaProperty.Register<FundoVivo, string?>(nameof(Estilo));

    public static readonly StyledProperty<bool> AgitadoProperty =
        AvaloniaProperty.Register<FundoVivo, bool>(nameof(Agitado));

    public static readonly StyledProperty<string> ModoProperty =
        AvaloniaProperty.Register<FundoVivo, string>(nameof(Modo), "animado");

    sealed class Foto(SKPicture imagem, float dpr)
    {
        int _usos = 1;
        public SKPicture Imagem { get; } = imagem;
        public float Dpr { get; } = dpr;
        public void Pegar() => Interlocked.Increment(ref _usos);

        public void Soltar()
        {
            if (Interlocked.Decrement(ref _usos) == 0) Imagem.Dispose();
        }
    }

    sealed class Desenho(Foto foto, Rect limites) : ICustomDrawOperation
    {
        public Rect Bounds => limites;
        public bool HitTest(Point p) => false;
        public bool Equals(ICustomDrawOperation? outro) => false;

        public void Render(ImmediateDrawingContext contexto)
        {
            if (contexto.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } recurso) return;
            using var emprestimo = recurso.Lease();
            var tela = emprestimo.SkCanvas;
            tela.Save();
            tela.ClipRect(SKRect.Create((float)limites.Width, (float)limites.Height));
            tela.Scale(1 / foto.Dpr);
            tela.DrawPicture(foto.Imagem);
            tela.Restore();
        }

        public void Dispose() => foto.Soltar();
    }

    public static string? UltimoErro { get; private set; }

    readonly object _trava = new();
    readonly ConcurrentQueue<Action<IMotorFundo, double>> _acoes = new();
    readonly DispatcherTimer _vigia;
    Foto? _foto;
    Thread? _laco;
    volatile bool _ligado;
    volatile string? _pedido;
    volatile bool _agitado;
    volatile bool _pausado;
    volatile bool _focado = true;
    volatile string _modo = "animado";
    float _largura;
    float _altura;
    float _dpr = 1;
    SKPoint? _mouse;
    int _pendente;
    TopLevel? _janela;

    public FundoVivo()
    {
        IsHitTestVisible = false;
        _vigia = new DispatcherTimer(TimeSpan.FromMilliseconds(300), DispatcherPriority.Background, (_, _) => Vigiar());
    }

    static FundoVivo()
    {
        EstiloProperty.Changed.AddClassHandler<FundoVivo>((f, _) => f._pedido = Normalizar(f.Estilo));
        AgitadoProperty.Changed.AddClassHandler<FundoVivo>((f, _) => f._agitado = f.Agitado);
        ModoProperty.Changed.AddClassHandler<FundoVivo>((f, _) => f._modo = f.Modo);
    }

    public string? Estilo { get => GetValue(EstiloProperty); set => SetValue(EstiloProperty, value); }
    public bool Agitado { get => GetValue(AgitadoProperty); set => SetValue(AgitadoProperty, value); }
    public string Modo { get => GetValue(ModoProperty); set => SetValue(ModoProperty, value); }
    bool Interativo => _pedido is not null && _modo == "animado" && Papeis.Encontrar(_pedido)?.Parado != true && !Aparencia.AnimacoesReduzidas;

    static string? Normalizar(string? estilo) => Papeis.Encontrar(estilo)?.Id;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _janela = TopLevel.GetTopLevel(this);
        if (_janela is not null)
        {
            _janela.AddHandler(PointerMovedEvent, AoMover, RoutingStrategies.Tunnel, handledEventsToo: true);
            _janela.AddHandler(PointerPressedEvent, AoClicar, RoutingStrategies.Bubble);
            _janela.PointerExited += AoSair;
        }
        _pedido = Normalizar(Estilo);
        _agitado = Agitado;
        _modo = Modo;
        Vigiar();
        _vigia.Start();
        _ligado = true;
        _laco = new Thread(Laco) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "Fundo vivo" };
        _laco.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _ligado = false;
        _vigia.Stop();
        if (_janela is null) return;
        _janela.RemoveHandler(PointerMovedEvent, AoMover);
        _janela.RemoveHandler(PointerPressedEvent, AoClicar);
        _janela.PointerExited -= AoSair;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        lock (_trava) (_largura, _altura) = ((float)e.NewSize.Width, (float)e.NewSize.Height);
    }

    void Vigiar()
    {
        var janela = _janela as Window;
        _pausado = !IsEffectivelyVisible || janela?.WindowState == WindowState.Minimized;
        _focado = janela?.IsActive ?? true;
        var escala = (float)Math.Min(1.5, _janela?.RenderScaling ?? 1);
        lock (_trava)
        {
            _dpr = escala;
            (_largura, _altura) = ((float)Bounds.Width, (float)Bounds.Height);
        }
    }

    void AoMover(object? sender, PointerEventArgs e)
    {
        var p = e.GetPosition(this);
        lock (_trava) _mouse = new SKPoint((float)p.X, (float)p.Y);
    }

    void AoSair(object? sender, PointerEventArgs e)
    {
        lock (_trava) _mouse = null;
    }

    void AoClicar(object? sender, PointerPressedEventArgs e)
    {
        if (!Interativo || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var p = e.GetPosition(this);
        _acoes.Enqueue((m, t) => m.Clique((float)p.X, (float)p.Y, t));
    }

    void Laco()
    {
        IMotorFundo? motor = null;
        string? atual = null;
        var relogio = Stopwatch.StartNew();
        var antes = 0.0;
        (float, float, float)? parado = null;
        while (_ligado)
        {
            var modo = _modo;
            var pedido = modo is "solido" or "vidro" ? null : _pedido;
            if (modo == "animado" && pedido is not null && (Papeis.Encontrar(pedido)?.Parado == true || Aparencia.AnimacoesReduzidas)) modo = "parado";
            if (pedido != atual)
            {
                motor?.Dispose();
                motor = null;
                atual = pedido;
                parado = null;
                _acoes.Clear();
                Publicar(null);
                if (pedido is not null) motor = Criar(pedido);
                antes = relogio.Elapsed.TotalSeconds;
            }
            float w, h, dpr;
            SKPoint? mouse;
            lock (_trava) (w, h, dpr, mouse) = (_largura, _altura, _dpr, _mouse);
            if (motor is null || _pausado || w < 2 || h < 2 || modo == "parado" && parado == (w, h, dpr))
            {
                Thread.Sleep(150);
                antes = relogio.Elapsed.TotalSeconds;
                continue;
            }
            if (modo == "parado")
            {
                _acoes.Clear();
                try
                {
                    Publicar(Congelar(motor, w, h, dpr));
                    parado = (w, h, dpr);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    UltimoErro = $"{atual}: {ex}";
                    Trace.WriteLine(UltimoErro);
                    motor.Dispose();
                    motor = null;
                }
                continue;
            }
            parado = null;
            var agora = relogio.Elapsed.TotalSeconds;
            var dt = Math.Min(0.05, agora - antes);
            antes = agora;
            try
            {
                while (_acoes.TryDequeue(out var acao)) acao(motor, agora);
                motor.Agito(agora, _agitado);
                using var gravador = new SKPictureRecorder();
                var tela = gravador.BeginRecording(SKRect.Create(w * dpr, h * dpr));
                motor.Quadro(tela, w, h, dpr, agora, dt, mouse);
                Publicar(new Foto(gravador.EndRecording(), dpr));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                UltimoErro = $"{atual}: {ex}";
                Trace.WriteLine(UltimoErro);
                motor.Dispose();
                motor = null;
                Publicar(null);
                continue;
            }
            var fps = _focado ? motor.Fps : Math.Min(12, motor.Fps);
            var resta = 1.0 / fps - (relogio.Elapsed.TotalSeconds - agora);
            if (resta > 0.001) Thread.Sleep(TimeSpan.FromSeconds(resta));
        }
        motor?.Dispose();
        Publicar(null);
    }

    static Foto Congelar(IMotorFundo motor, float w, float h, float dpr)
    {
        SKPicture? ultima = null;
        for (var q = 0; q < 3; q++)
        {
            ultima?.Dispose();
            using var gravador = new SKPictureRecorder();
            var tela = gravador.BeginRecording(SKRect.Create(w * dpr, h * dpr));
            motor.Quadro(tela, w, h, dpr, 2.4 + q * 0.04, 0.04, null);
            ultima = gravador.EndRecording();
        }
        return new Foto(ultima!, dpr);
    }

    static IMotorFundo? Criar(string pedido)
    {
        try
        {
            return Motores.Criar(pedido);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            UltimoErro = $"{pedido}: {ex}";
            Trace.WriteLine(UltimoErro);
            return null;
        }
    }

    void Publicar(Foto? foto)
    {
        Foto? velha;
        lock (_trava) (velha, _foto) = (_foto, foto);
        velha?.Soltar();
        if (Interlocked.Exchange(ref _pendente, 1) == 0)
            Dispatcher.UIThread.Post(() =>
            {
                Volatile.Write(ref _pendente, 0);
                InvalidateVisual();
            }, DispatcherPriority.Render);
    }

    public override void Render(DrawingContext contexto)
    {
        Foto? foto;
        lock (_trava)
        {
            foto = _foto;
            foto?.Pegar();
        }
        if (foto is not null) contexto.Custom(new Desenho(foto, new Rect(Bounds.Size)));
    }
}
