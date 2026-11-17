using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

using Nexora.ViewModels;

namespace Nexora.Views;

public interface IAoMostrar
{
    void AoMostrar();
}

public interface IAoEsconder
{
    void AoEsconder();
}

public sealed class AbasVivas : Panel
{
    public static readonly StyledProperty<object?> ConteudoProperty = AvaloniaProperty.Register<AbasVivas, object?>(nameof(Conteudo));
    public static readonly StyledProperty<IReadOnlyList<object>?> ConteudosProperty = AvaloniaProperty.Register<AbasVivas, IReadOnlyList<object>?>(nameof(Conteudos));
    public static readonly StyledProperty<IDataTemplate?> CabecalhoTemplateProperty = AvaloniaProperty.Register<AbasVivas, IDataTemplate?>(nameof(CabecalhoTemplate));
    public static readonly AttachedProperty<bool> EmPainelProperty = AvaloniaProperty.RegisterAttached<AbasVivas, Control, bool>("EmPainel", inherits: true);

    const double Divisor = 7;
    const double LarguraMinima = 260;
    const double AlturaMinima = 180;

    readonly List<Vista> _vistas = [];
    readonly List<Border> _divisores = [];
    readonly Queue<object> _fila = new();
    HashSet<object> _desejados = new(ReferenceEqualityComparer.Instance);
    Vista? _preparando;
    bool _agendado;
    List<Vista> _lado = [];
    int[] _linhas = [];
    double[] _pesosLinha = [];
    double[][] _pesosColuna = [];

    public object? Conteudo { get => GetValue(ConteudoProperty); set => SetValue(ConteudoProperty, value); }
    public IReadOnlyList<object>? Conteudos { get => GetValue(ConteudosProperty); set => SetValue(ConteudosProperty, value); }
    public IDataTemplate? CabecalhoTemplate { get => GetValue(CabecalhoTemplateProperty); set => SetValue(CabecalhoTemplateProperty, value); }

    public static bool GetEmPainel(Control alvo) => alvo.GetValue(EmPainelProperty);
    public static void SetEmPainel(Control alvo, bool valor) => alvo.SetValue(EmPainelProperty, valor);

    public int Limite { get; set; } = 6;

    public event Action<object>? FocoPedido;

    public static int[] Linhas(int paineis)
    {
        if (paineis <= 3) return [paineis];
        var colunas = (int)Math.Ceiling(Math.Sqrt(paineis));
        var linhas = (int)Math.Ceiling(paineis / (double)colunas);
        var resto = paineis % linhas;
        return [.. Enumerable.Range(0, linhas).Select(i => paineis / linhas + (i < resto ? 1 : 0))];
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ConteudoProperty || change.Property == ConteudosProperty) Mostrar();
    }

    void Mostrar()
    {
        var conteudo = Conteudo;
        var alvos = Conteudos is { Count: > 1 } varios ? varios : conteudo is null ? [] : new[] { conteudo };
        var visiveis = new List<Vista>();
        foreach (var alvo in alvos)
        {
            var vista = _vistas.FirstOrDefault(v => ReferenceEquals(v.Alvo, alvo));
            if (vista is null)
            {
                vista = Criar(alvo);
                Children.Add(vista);
            }
            else if (ReferenceEquals(vista, _preparando)) Revelar(vista);
            visiveis.Add(vista);
        }
        for (var i = visiveis.Count - 1; i >= 0; i--)
        {
            _vistas.Remove(visiveis[i]);
            _vistas.Insert(0, visiveis[i]);
        }
        var foco = conteudo is null ? null : visiveis.FirstOrDefault(v => ReferenceEquals(v.Alvo, conteudo));
        var dividido = visiveis.Count > 1;
        var area = DataContext as AreaVM;
        foreach (var v in _vistas)
        {
            var visivel = visiveis.Contains(v);
            if (!visivel && v.IsVisible && v.Corpo.Presenter?.Child is IAoEsconder escondido) escondido.AoEsconder();
            if (visivel && !v.IsVisible && !dividido && IsLoaded) Animacoes.Surgir(v, "baixo");
            v.IsVisible = visivel;
            v.Dividida(dividido, dividido && ReferenceEquals(v, foco), dividido ? area?.Divisao.FirstOrDefault(g => ReferenceEquals(g.Conteudo, v.Alvo)) : null, CabecalhoTemplate);
        }
        while (_vistas.Count > Math.Max(Limite, visiveis.Count))
        {
            var sobra = _vistas.LastOrDefault(v => !visiveis.Contains(v));
            if (sobra is null) break;
            Tirar(sobra);
        }
        if (visiveis.Count != _lado.Count) Reorganizar(visiveis.Count);
        _lado = visiveis;
        InvalidateMeasure();
        if (foco?.Corpo.Presenter?.Child is IAoMostrar alvoVivo) alvoVivo.AoMostrar();
    }

    void Reorganizar(int paineis)
    {
        _linhas = paineis > 1 ? Linhas(paineis) : [];
        _pesosLinha = [.. _linhas.Select(_ => 1.0)];
        _pesosColuna = [.. _linhas.Select(c => Enumerable.Repeat(1.0, c).ToArray())];
        foreach (var d in _divisores) Children.Remove(d);
        _divisores.Clear();
        for (var linha = 0; linha < _linhas.Length; linha++)
            for (var coluna = 0; coluna < _linhas[linha] - 1; coluna++)
                _divisores.Add(NovoDivisor(linha, coluna));
        for (var linha = 0; linha < _linhas.Length - 1; linha++) _divisores.Add(NovoDivisor(linha, -1));
    }

    Vista Criar(object alvo)
    {
        var vista = new Vista(alvo);
        vista.AddHandler(PointerPressedEvent, (_, _) =>
        {
            if (_lado.Count > 1 && !ReferenceEquals(vista.Alvo, Conteudo)) FocoPedido?.Invoke(vista.Alvo);
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        return vista;
    }

    Border NovoDivisor(int linha, int coluna)
    {
        var horizontal = coluna < 0;
        var traco = new Border
        {
            IsHitTestVisible = false,
            Width = horizontal ? double.NaN : 1,
            Height = horizontal ? 1 : double.NaN,
            HorizontalAlignment = horizontal ? HorizontalAlignment.Stretch : HorizontalAlignment.Center,
            VerticalAlignment = horizontal ? VerticalAlignment.Center : VerticalAlignment.Stretch,
        };
        traco.Bind(Border.BackgroundProperty, this.GetResourceObservable("Borda"));
        var divisor = new Border
        {
            Background = Brushes.Transparent,
            Cursor = new Cursor(horizontal ? StandardCursorType.SizeNorthSouth : StandardCursorType.SizeWestEast),
            Child = traco,
        };
        double? origem = null;
        double[]? medidas = null;
        divisor.PointerPressed += (_, e) =>
        {
            var p = e.GetPosition(this);
            origem = horizontal ? p.Y : p.X;
            medidas = horizontal ? Alturas(Bounds.Height) : Larguras(Bounds.Width, linha);
            e.Pointer.Capture(divisor);
            e.Handled = true;
        };
        divisor.PointerMoved += (_, e) =>
        {
            if (origem is not { } o || medidas is null) return;
            var i = horizontal ? linha : coluna;
            if (i + 1 >= medidas.Length) return;
            var p = e.GetPosition(this);
            var minimo = horizontal ? AlturaMinima : LarguraMinima;
            var par = medidas[i] + medidas[i + 1];
            if (par < minimo * 2) return;
            var a = Math.Clamp(medidas[i] + (horizontal ? p.Y : p.X) - o, minimo, par - minimo);
            var novos = (double[])medidas.Clone();
            novos[i] = a;
            novos[i + 1] = par - a;
            if (horizontal) _pesosLinha = novos;
            else _pesosColuna[linha] = novos;
            InvalidateArrange();
        };
        divisor.PointerReleased += (_, e) =>
        {
            origem = null;
            medidas = null;
            e.Pointer.Capture(null);
        };
        Children.Add(divisor);
        return divisor;
    }

    static double[] Repartir(double total, double[] pesos)
    {
        var livre = Math.Max(0, total - (pesos.Length - 1) * Divisor);
        var soma = pesos.Sum();
        return soma <= 0 ? [.. pesos.Select(_ => livre / pesos.Length)] : [.. pesos.Select(p => livre * p / soma)];
    }

    double[] Alturas(double total) => Repartir(total, _pesosLinha);

    double[] Larguras(double total, int linha) => Repartir(total, _pesosColuna[linha]);

    IEnumerable<(Control Controle, Rect Lugar)> Lugares(Size tamanho)
    {
        var alturas = Alturas(tamanho.Height);
        var y = 0.0;
        var indice = 0;
        var divisor = 0;
        for (var linha = 0; linha < _linhas.Length; linha++)
        {
            var larguras = Larguras(tamanho.Width, linha);
            var x = 0.0;
            for (var coluna = 0; coluna < _linhas[linha]; coluna++)
            {
                if (indice < _lado.Count) yield return (_lado[indice++], new Rect(x, y, larguras[coluna], alturas[linha]));
                x += larguras[coluna];
                if (coluna < _linhas[linha] - 1)
                {
                    yield return (_divisores[divisor++], new Rect(x, y, Divisor, alturas[linha]));
                    x += Divisor;
                }
            }
            y += alturas[linha];
            if (linha < _linhas.Length - 1) y += Divisor;
        }
        y = 0;
        for (var linha = 0; linha < _linhas.Length - 1; linha++)
        {
            y += alturas[linha];
            yield return (_divisores[divisor++], new Rect(0, y, tamanho.Width, Divisor));
            y += Divisor;
        }
    }

    protected override Size MeasureOverride(Size disponivel)
    {
        if (_lado.Count <= 1)
        {
            foreach (var v in _vistas.Where(v => v.IsVisible)) v.Measure(disponivel);
            return disponivel;
        }
        var tamanho = new Size(
            double.IsInfinity(disponivel.Width) ? LarguraMinima * 3 : disponivel.Width,
            double.IsInfinity(disponivel.Height) ? AlturaMinima * 2 : disponivel.Height);
        foreach (var (controle, lugar) in Lugares(tamanho)) controle.Measure(lugar.Size);
        return disponivel;
    }

    protected override Size ArrangeOverride(Size final)
    {
        if (_lado.Count <= 1)
        {
            foreach (var v in _vistas.Where(v => v.IsVisible)) v.Arrange(new Rect(final));
            return final;
        }
        foreach (var (controle, lugar) in Lugares(final)) controle.Arrange(lugar);
        return final;
    }

    public void Podar(IEnumerable<object> abertos)
    {
        var manter = new HashSet<object>(abertos, ReferenceEqualityComparer.Instance);
        manter.UnionWith(_desejados);
        foreach (var vista in _vistas.Where(v => !manter.Contains(v.Alvo)).ToList()) Tirar(vista);
    }

    public void Preparar(IEnumerable<object> alvos)
    {
        _desejados = new HashSet<object>(alvos, ReferenceEqualityComparer.Instance);
        _fila.Clear();
        foreach (var alvo in _desejados)
            if (!_vistas.Any(v => ReferenceEquals(v.Alvo, alvo))) _fila.Enqueue(alvo);
        Agendar();
    }

    void Agendar()
    {
        if (_agendado || _preparando is not null || _fila.Count == 0) return;
        _agendado = true;
        DispatcherTimer.RunOnce(() => Dispatcher.UIThread.Post(PrepararProxima, DispatcherPriority.ApplicationIdle), TimeSpan.FromMilliseconds(250));
    }

    void PrepararProxima()
    {
        _agendado = false;
        if (_preparando is not null || !IsLoaded || _lado.Count > 1) return;
        while (_fila.TryDequeue(out var alvo))
        {
            if (_vistas.Count >= Limite) break;
            if (!_desejados.Contains(alvo) || _vistas.Any(v => ReferenceEquals(v.Alvo, alvo))) continue;
            var vista = Criar(alvo);
            vista.Opacity = 0;
            vista.IsHitTestVisible = false;
            _preparando = vista;
            _vistas.Add(vista);
            Children.Insert(0, vista);
            InvalidateMeasure();
            Dispatcher.UIThread.Post(() =>
            {
                if (!ReferenceEquals(_preparando, vista)) return;
                _preparando = null;
                vista.IsVisible = false;
                Revelar(vista);
                Agendar();
            }, DispatcherPriority.Background);
            return;
        }
    }

    void Revelar(Vista vista)
    {
        if (ReferenceEquals(_preparando, vista)) _preparando = null;
        vista.ClearValue(OpacityProperty);
        vista.ClearValue(IsHitTestVisibleProperty);
    }

    void Tirar(Vista vista)
    {
        if (ReferenceEquals(_preparando, vista)) _preparando = null;
        _vistas.Remove(vista);
        _lado.Remove(vista);
        Children.Remove(vista);
        vista.Soltar();
    }

    sealed class Vista : Border
    {
        readonly ContentControl _cabecalho = new() { IsVisible = false };

        public Vista(object alvo)
        {
            Alvo = alvo;
            Corpo = new ContentControl { Content = alvo };
            ClipToBounds = true;
            var pilha = new DockPanel();
            DockPanel.SetDock(_cabecalho, Dock.Top);
            pilha.Children.Add(_cabecalho);
            pilha.Children.Add(Corpo);
            Child = pilha;
        }

        public object Alvo { get; }
        public ContentControl Corpo { get; }

        public void Dividida(bool dividida, bool focada, GuiaVM? guia, IDataTemplate? cabecalho)
        {
            SetEmPainel(this, dividida);
            Classes.Set("painel", dividida);
            Classes.Set("focado", focada);
            _cabecalho.ContentTemplate = cabecalho;
            _cabecalho.Content = guia;
            _cabecalho.IsVisible = dividida && guia is not null;
        }

        public void Soltar()
        {
            _cabecalho.Content = null;
            Corpo.Content = null;
        }
    }
}
