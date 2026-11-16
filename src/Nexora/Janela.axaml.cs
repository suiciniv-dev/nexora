using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora;

public partial class Janela : Window
{
    JanelaVM? _vm;

    public Janela()
    {
        InitializeComponent();
        IdentidadeJanela.Aplicar(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
        Temas.VidroDaJanela.Acompanhar(this);
        FecharPopups.Ligar(this);
        AddHandler(KeyDownEvent, AntesDaPagina, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, (_, _) => Realcar(false), Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(GotFocusEvent, (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(PosicionarRealce, Avalonia.Threading.DispatcherPriority.Render));
        LayoutUpdated += (_, _) =>
        {
            if (_realcando) PosicionarRealce();
        };
        AddHandler(GotFocusEvent, (_, e) =>
        {
            if (_vm?.TelaGrande is { Ligada: true, Aberta: false, NoMenu: true } tela && e.Source is Avalonia.Visual v && v != TopoGrande && !TopoGrande.IsVisualAncestorOf(v)) tela.NoMenu = false;
        });
PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty && _vm is { } vm) vm.TelaCheia = WindowState == WindowState.FullScreen;
        };
        SizeChanged += (_, e) =>
        {
            AjustarBarra(e.NewSize.Width);
            AjustarDisposicao(e.NewSize);
        };
        if (Sistema.Mac)
        {
            Sistema.AjustarAtalhos(this);
            BarraTitulo.Margin = new Avalonia.Thickness(84, 0, 0, 0);
            BarraTitulo.ColumnDefinitions[3].Width = new GridLength(14);
        }
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null)
            {
                _vm.PedirBusca -= FocarBusca;
                _vm.PedirApresentacao -= MostrarApresentacao;
                _vm.TelaGrande.PedirFocoMenu -= FocarMenuGrande;
                _vm.TelaGrande.PedirFocoPagina -= FocarPagina;
            }
            if (_vm is not null)
            {
                _vm.PropertyChanged -= AoMudarVm;
                _vm.Abertas.CollectionChanged -= AoMudarAbertas;
            }
            _vm = DataContext as JanelaVM;
            if (_vm is not null)
            {
                _vm.PropertyChanged += AoMudarVm;
                _vm.Abertas.CollectionChanged += AoMudarAbertas;
                _vm.Vertical = Disposicao.GetVertical(this);
                _vm.PedirBusca += FocarBusca;
                _vm.PedirApresentacao += MostrarApresentacao;
                _vm.TelaGrande.PedirFocoMenu += FocarMenuGrande;
                _vm.TelaGrande.PedirFocoPagina += FocarPagina;
            }
            LigarAtalhos();
        };
        Opened += (_, _) =>
        {
            var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            IdentidadeJanela.Aplicar(hwnd);
            if (DataContext is JanelaVM vm) vm.Janela = hwnd;
            Janelas.RestaurarExtras();
            PrepararConversas();
        };
        Aparencia.Mudou += AplicarEscala;
        Closed += (_, _) => Aparencia.Mudou -= AplicarEscala;
        AplicarEscala();
        Closing += (_, _) =>
        {
            Janelas.Salvar();
            Janelas.FecharExtras();
            (DataContext as JanelaVM)?.EncerrarTudo();
        };
    }

    public AbasView FaixaDeAbas => Abas;

    readonly List<Avalonia.Input.KeyBinding> _atalhos = [];

    void LigarAtalhos()
    {
        foreach (var antigo in _atalhos) KeyBindings.Remove(antigo);
        _atalhos.Clear();
        if (_vm is not { } vm) return;
        foreach (var atalho in CatalogoDeAtalhos.Todos())
        {
            if (atalho.Comando is not { } comando) continue;
            foreach (var gesto in atalho.Gestos)
            {
                var ligacao = new Avalonia.Input.KeyBinding
                {
                    Gesture = Avalonia.Input.KeyGesture.Parse(gesto),
                    Command = new ComandoAdiado(() => comando(vm)),
                    CommandParameter = atalho.Parametro,
                };
                KeyBindings.Add(ligacao);
                _atalhos.Add(ligacao);
            }
        }
        Sistema.AjustarAtalhos(this);
    }

    void AplicarEscala()
    {
        var escala = Aparencia.Zoom * (_vm?.TelaCheia == true ? 1.25 : 1);
        Escalador.LayoutTransform = Math.Abs(escala - 1) < 0.001 ? null : new Avalonia.Media.ScaleTransform(escala, escala);
        if (Sistema.Mac) BarraTitulo.Margin = new Avalonia.Thickness(84 / escala, 0, 0, 0);
        else BarraTitulo.ColumnDefinitions[5].Width = new GridLength(156 / escala);
    }

    WindowState _antesDaTelaCheia = WindowState.Normal;

    void AoMudarAbertas(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => PrepararConversas();

    void PrepararConversas()
    {
        if (_vm is null) return;
        Area.Vivas.Preparar(_vm.Abertas.Take(Area.Vivas.Limite - 1));
    }

    void AoMudarVm(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(JanelaVM.LateralFixa))
        {
            Animacoes.Deslizar(CartaoConteudo, Abas);
            return;
        }
        if (e.PropertyName != nameof(JanelaVM.TelaCheia) || _vm is null) return;
        AplicarEscala();
        Moldura.RowDefinitions[0].Height = new GridLength(_vm.TelaCheia ? 84 : 54);
        Moldura.RowDefinitions[2].Height = new GridLength(_vm.TelaCheia ? 18 : 28);
        if (_vm.TelaCheia)
        {
            if (WindowState == WindowState.FullScreen) return;
            _antesDaTelaCheia = WindowState;
            WindowState = WindowState.FullScreen;
        }
        else if (WindowState == WindowState.FullScreen) WindowState = _antesDaTelaCheia;
    }

    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _vm is not { } vm) return;
        var tela = vm.TelaGrande;
        if (tela.Ligada && !vm.TemSobreposicao && !vm.LateralAberta)
        {
            if (tela.Aberta || tela.NoMenu)
            {
                if (TelaGrandeView.Teclar(tela, e)) e.Handled = true;
                return;
            }
            if (e.Key == Avalonia.Input.Key.Escape && e.KeyModifiers == Avalonia.Input.KeyModifiers.None)
            {
                tela.IrAoMenu();
                e.Handled = true;
                return;
            }
            if (Direcao(e.Key) is { } direcao && e.KeyModifiers == Avalonia.Input.KeyModifiers.None && !VisaoGeralView.EmTexto(e.Source))
            {
                if (MoverNaPagina(direcao)) e.Handled = true;
                else if (direcao == Avalonia.Input.NavigationDirection.Up)
                {
                    tela.IrAoMenu();
                    e.Handled = true;
                }
                return;
            }
        }
        if (e.KeyModifiers != Avalonia.Input.KeyModifiers.None) return;
        if (e.Key == Avalonia.Input.Key.Escape)
        {
            if (!vm.LateralAberta) return;
            vm.LateralAberta = false;
            e.Handled = true;
            return;
        }
        if (!vm.NaVisaoGeral || vm.TemSobreposicao || VisaoGeralView.EmTexto(e.Source)) return;
        if (this.GetVisualDescendants().OfType<VisaoGeralView>().FirstOrDefault(v => v.IsEffectivelyVisible) is { } visao && visao.Navegar(e.Key)) e.Handled = true;
    }

    void AjustarDisposicao(Avalonia.Size tamanho)
    {
        var vertical = Disposicao.Aplicar(this, tamanho);
        if (_vm is not null) _vm.Vertical = vertical;
    }

    void AjustarBarra(double largura)
    {
        var estreita = largura < 1600;
        var apertada = largura < 1200;
        TextoFoco.IsVisible = !estreita;
        CaixaPlano.IsVisible = largura >= 1000;
    }

#if MAC
    void MostrarApresentacao()
    {
        if (_vm is { } vm) vm.MostraBoasVindas = true;
    }
#else
    Apresentacao? _apresentacao;

    void MostrarApresentacao()
    {
        if (_vm is not { } vm) return;
        if (!IsVisible)
        {
            void AoAbrir(object? s, EventArgs e)
            {
                Opened -= AoAbrir;
                MostrarApresentacao();
            }
            Opened += AoAbrir;
            return;
        }
        if (_apresentacao is not null)
        {
            _apresentacao.Activate();
            return;
        }
        _apresentacao = new Apresentacao(vm.BoasVindas, this);
        _apresentacao.Falhou += () => vm.MostraBoasVindas = true;
        _apresentacao.Closed += (_, _) => _apresentacao = null;
        _apresentacao.Show(this);
    }
#endif

    void FocarBusca() => PainelBusca.Focar();

    void FocarMenuGrande() => TopoGrande.Focus();

    static Avalonia.Input.NavigationDirection? Direcao(Avalonia.Input.Key tecla) => tecla switch
    {
        Avalonia.Input.Key.Up => Avalonia.Input.NavigationDirection.Up,
        Avalonia.Input.Key.Down => Avalonia.Input.NavigationDirection.Down,
        Avalonia.Input.Key.Left => Avalonia.Input.NavigationDirection.Left,
        Avalonia.Input.Key.Right => Avalonia.Input.NavigationDirection.Right,
        _ => null,
    };

    bool NaPaginaGrande => _vm is { TemSobreposicao: false, LateralAberta: false, TelaGrande: { Ligada: true, Aberta: false, NoMenu: false } };

    void AntesDaPagina(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (!NaPaginaGrande) return;
        if (e.Key is Avalonia.Input.Key.Tab || Direcao(e.Key) is not null) Realcar(true);
        if (e.KeyModifiers != Avalonia.Input.KeyModifiers.None || Direcao(e.Key) is not { } direcao) return;
        switch (FocusManager?.GetFocusedElement())
        {
            case ComboBox { IsDropDownOpen: false } or GridSplitter:
                if (MoverNaPagina(direcao)) e.Handled = true;
                break;
            case TextBox caixa when NaBorda(caixa, direcao):
                if (MoverNaPagina(direcao)) e.Handled = true;
                else if (direcao == Avalonia.Input.NavigationDirection.Up)
                {
                    _vm!.TelaGrande.IrAoMenu();
                    e.Handled = true;
                }
                break;
        }
    }

    static bool NaBorda(TextBox caixa, Avalonia.Input.NavigationDirection direcao)
    {
        if (caixa.FindAncestorOfType<ChatView>() is { SugestoesAbertas: true }) return false;
        var texto = caixa.Text ?? "";
        if (texto.Length == 0) return true;
        var cursor = Math.Clamp(caixa.CaretIndex, 0, texto.Length);
        return direcao switch
        {
            Avalonia.Input.NavigationDirection.Up => texto.LastIndexOf('\n', Math.Max(0, cursor - 1)) < 0 || cursor == 0,
            Avalonia.Input.NavigationDirection.Down => texto.IndexOf('\n', cursor) < 0,
            _ => false,
        };
    }

    bool _realcando;

    void Realcar(bool ligar)
    {
        _realcando = ligar;
        PosicionarRealce();
    }

    void PosicionarRealce()
    {
        var foco = FocusManager?.GetFocusedElement() as Control;
        var alvo = _realcando && NaPaginaGrande && foco is not null && CartaoConteudo.IsVisualAncestorOf(foco) && foco.IsEffectivelyVisible
            ? foco.TranslatePoint(default, CamadaRealce) : null;
        if (alvo is not { } ponto)
        {
            Realce.Opacity = 0;
            return;
        }
        var visivel = Realce.Opacity > 0;
        if (!visivel) Realce.Transitions = null;
        Canvas.SetLeft(Realce, ponto.X - 4);
        Canvas.SetTop(Realce, ponto.Y - 4);
        Realce.Width = foco!.Bounds.Width + 8;
        Realce.Height = foco.Bounds.Height + 8;
        if (!visivel) Realce.ClearValue(Avalonia.Animation.Animatable.TransitionsProperty);
        Realce.Opacity = 1;
    }

    public bool MoverNaPagina(Avalonia.Input.NavigationDirection direcao)
    {
        if (FocusManager is not { } focos) return false;
        Realcar(true);
        var foco = focos.GetFocusedElement() as Avalonia.Visual;
        if (foco is null || !CartaoConteudo.IsVisualAncestorOf(foco))
        {
            var primeiro = CartaoConteudo.GetVisualDescendants().OfType<Avalonia.Input.InputElement>()
                .Where(i => i.Focusable && i.IsEffectivelyVisible && i.IsEffectivelyEnabled && i is not TextBox && i.TranslatePoint(default, CartaoConteudo) is not null)
                .OrderBy(i => Math.Round(i.TranslatePoint(default, CartaoConteudo)!.Value.Y / 8))
                .ThenBy(i => i.TranslatePoint(default, CartaoConteudo)!.Value.X)
                .FirstOrDefault();
            if (primeiro is null) return false;
            primeiro.Focus(Avalonia.Input.NavigationMethod.Directional);
            (primeiro as Control)?.BringIntoView();
            return true;
        }
        var opcoes = new Avalonia.Input.FindNextElementOptions { SearchRoot = CartaoConteudo };
        for (var tentativa = 0; tentativa < 4; tentativa++)
        {
            if (!focos.TryMoveFocus(direcao, opcoes))
            {
                if (tentativa > 0 && foco is Avalonia.Input.InputElement volta) volta.Focus(Avalonia.Input.NavigationMethod.Directional);
                return false;
            }
            if (focos.GetFocusedElement() is not GridSplitter) break;
        }
        (focos.GetFocusedElement() as Control)?.BringIntoView();
        return true;
    }

    void FocarPagina()
    {
        if (_vm is null) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (NaPagina()) return;
            _vm.Principal.Focar();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (NaPagina() || _vm?.TelaGrande is not { Ligada: true, Aberta: false, NoMenu: false }) return;
                if (this.GetVisualDescendants().OfType<VisaoGeralView>().FirstOrDefault(v => v.IsEffectivelyVisible) is { } visao && visao.Navegar(Avalonia.Input.Key.Right)) return;
                MoverNaPagina(Avalonia.Input.NavigationDirection.Down);
            }, Avalonia.Threading.DispatcherPriority.Input);
        }, Avalonia.Threading.DispatcherPriority.Input);

        bool NaPagina() => FocusManager?.GetFocusedElement() is Avalonia.Visual foco && CartaoConteudo.IsVisualAncestorOf(foco) && foco.IsEffectivelyVisible;
    }

    async void Importar_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        var arquivos = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Core.Texto.Idioma.T("Importar um .code-workspace"),
            FileTypeFilter = [new FilePickerFileType(Core.Texto.Idioma.T("Workspace do VS Code")) { Patterns = ["*.code-workspace"] }],
        });
        if (arquivos.FirstOrDefault()?.TryGetLocalPath() is { } arquivo && _vm.ImportarWorkspace(arquivo) is { } ambiente) _vm.Navegar(ambiente);
    }
}
