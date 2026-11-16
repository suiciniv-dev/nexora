using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.Core.Texto;
using Nexora.Core.Arquivos;
using Nexora.ViewModels;

namespace Nexora.Views;

public partial class ChatView : UserControl, IAoMostrar, IAoEsconder
{
    public static readonly Avalonia.StyledProperty<bool> CompactoProperty = Avalonia.AvaloniaProperty.Register<ChatView, bool>(nameof(Compacto));

    public bool Compacto { get => GetValue(CompactoProperty); set => SetValue(CompactoProperty, value); }

    bool Enxuto => Compacto || AbasVivas.GetEmPainel(this);

    public static readonly Avalonia.StyledProperty<bool> LargaProperty = Avalonia.AvaloniaProperty.Register<ChatView, bool>(nameof(Larga), true);

    public bool Larga { get => GetValue(LargaProperty); set => SetValue(LargaProperty, value); }

    const double LarguraParaDetalhes = 760;

    const double LarguraParaCabecalhoEmLinha = 640;

    const double Folga = 48;
    const double PertoDoTopo = 320;
    const int LotePorQuadro = 6;
    bool _grudado = true;
    bool _montando;
    bool _segurando;
    ChatVM? _vm;
    int _inicioMencao = -1;
    static ChatView? _ativa;
    readonly (Avalonia.Controls.Primitives.Popup Popup, string Nome, Func<ChatVM, bool> Ler, Action<ChatVM, bool> Gravar)[] _menus;

    public ChatView()
    {
        InitializeComponent();
        Composer.AddHandler(KeyDownEvent, AoTeclar, RoutingStrategies.Tunnel);
        Composer.TextChanged += (_, _) => _ = AtualizarMencoesAsync();
        ListaMencoes.AddHandler(PointerReleasedEvent, (_, _) => InserirMencao(), RoutingStrategies.Bubble, handledEventsToo: true);
        ListaComandos.AddHandler(PointerReleasedEvent, (_, _) => InserirComando(), RoutingStrategies.Bubble, handledEventsToo: true);
        Rolagem.ScrollChanged += AoRolar;
        Composer.PastingFromClipboard += AoColar;
        MenuPassagens.Opened += (_, _) => Dispatcher.UIThread.Post(() => BuscaPassagens.Focus(), DispatcherPriority.Input);
        Caixa.AddHandler(DragDrop.DragOverEvent, AoArrastarPorCima);
        Caixa.AddHandler(DragDrop.DropEvent, AoSoltar);
        _menus =
        [
            (MenuAgente, nameof(ChatVM.MenuAgenteAberto), vm => vm.MenuAgenteAberto, (vm, v) => vm.MenuAgenteAberto = v),
            (MenuOndeRoda, nameof(ChatVM.MenuOndeRodaAberto), vm => vm.MenuOndeRodaAberto, (vm, v) => vm.MenuOndeRodaAberto = v),
            (MenuModelo, nameof(ChatVM.MenuModeloAberto), vm => vm.MenuModeloAberto, (vm, v) => vm.MenuModeloAberto = v),
            (MenuModo, nameof(ChatVM.MenuModoAberto), vm => vm.MenuModoAberto, (vm, v) => vm.MenuModoAberto = v),
            (MenuPassagens, nameof(ChatVM.MenuPassagensAberto), vm => vm.MenuPassagensAberto, (vm, v) => vm.MenuPassagensAberto = v),
            (MenuTemplates, nameof(ChatVM.MenuTemplatesAberto), vm => vm.MenuTemplatesAberto, (vm, v) => vm.MenuTemplatesAberto = v),
            (MenuSubagentes, nameof(ChatVM.MenuSubagentesAberto), vm => vm.MenuSubagentesAberto, (vm, v) => vm.MenuSubagentesAberto = v),
        ];
        foreach (var menu in _menus)
            menu.Popup.Closed += (_, _) =>
            {
                if (_vm is { } vm && EmUso && menu.Ler(vm)) menu.Gravar(vm, false);
            };
        AddHandler(PointerPressedEvent, (_, _) => _ativa = this, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(GotFocusEvent, (_, _) => _ativa = this, RoutingStrategies.Bubble, handledEventsToo: true);
        DataContextChanged += (_, _) => Ligar(DataContext as ChatVM);
        SizeChanged += (_, e) =>
        {
            Larga = e.NewSize.Width >= LarguraParaDetalhes;
            Classes.Set("estreita", e.NewSize.Width < LarguraParaCabecalhoEmLinha);
            AtualizarDisposicao();
        };
        Cabecalho.SizeChanged += (_, _) => AjustarCamada();
    }

    public bool FecharDetalhes()
    {
        if (_vm is not { DetalhesPorCima: true } vm) return false;
        vm.DetalhesAbertos = false;
        return true;
    }

    protected override void OnPropertyChanged(Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CompactoProperty || change.Property == AbasVivas.EmPainelProperty)
        {
            Cabecalho.IsVisible = !Enxuto;
            Caixa.Margin = AbasVivas.GetEmPainel(this) ? new Avalonia.Thickness(12, 6, 16, 12) : new Avalonia.Thickness(24, 8, 24, 18);
        }
        if (change.Property == Disposicao.VerticalProperty) AtualizarDisposicao();

    }

    void AtualizarDisposicao()
    {
        var vertical = Disposicao.GetVertical(this);
        Cabecalho.Classes.Set("estreito", !Larga && (vertical || Bounds.Width < LarguraParaCabecalhoEmLinha));
        if (_vm is not null && !Enxuto)
        {
            _vm.DetalhesSobrepostos = vertical;
            _vm.DetalhesCabem = Larga;
        }
        AjustarColunas();
        AjustarCamada();
    }

    void AjustarCamada()
    {
        var topo = _vm is { DetalhesSobrepostos: true } && Cabecalho.IsVisible ? Cabecalho.Bounds.Bottom : 0;
        if (Camada.Margin.Top != topo) Camada.Margin = new Avalonia.Thickness(0, topo, 0, 0);
    }

    void Ligar(ChatVM? vm)
    {
        if (_vm is not null)
        {
            _vm.Editor.PropertyChanged -= AoMudarEditor;
            _vm.PropertyChanged -= AoMudarVM;
            _vm.PedirFoco -= Focar;
            _vm.PedirCursor -= PorCursor;
            _vm.MontagemPendente -= PedirMontagem;
        }
        _vm = vm;
        _grudado = true;
        Mencoes.IsOpen = false;
        Comandos.IsOpen = false;
        foreach (var menu in _menus) menu.Popup.IsOpen = false;
        if (vm is null) return;
        vm.Editor.PropertyChanged += AoMudarEditor;
        vm.PropertyChanged += AoMudarVM;
        vm.PedirFoco += Focar;
        vm.PedirCursor += PorCursor;
        vm.MontagemPendente += PedirMontagem;
        PedirMontagem();
        AtualizarDisposicao();
        AjustarColunas();
        Dispatcher.UIThread.Post(() =>
        {
            Rolagem.ScrollToEnd();
            Composer.Focus();
        }, DispatcherPriority.Background);
    }

    public void AoEsconder()
    {
        if (_vm is not null && !Enxuto) _vm.DetalhesAbertos = false;
    }

    public void AoMostrar()
    {
        PedirMontagem();
        Dispatcher.UIThread.Post(() =>
        {
            if (_grudado) Rolagem.ScrollToEnd();
            Composer.Focus();
        }, DispatcherPriority.Background);
    }

    void PedirMontagem()
    {
        if (_montando || _vm is not { Montando: true }) return;
        if (TopLevel.GetTopLevel(this) is not { } janela) return;
        _montando = true;
        janela.RequestAnimationFrame(MontarLote);
    }

    void MontarLote(TimeSpan _)
    {
        _montando = false;
        if (_vm is not { Montando: true } vm)
        {
            _segurando = false;
            return;
        }
        if (!IsEffectivelyVisible)
        {
            DispatcherTimer.RunOnce(PedirMontagem, TimeSpan.FromMilliseconds(250), DispatcherPriority.Background);
            return;
        }
        _segurando = !_grudado;
        vm.MontarMais(LotePorQuadro);
        _montando = true;
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(MontarLote);
    }

    void AoMudarEditor(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorVM.TemAbas)) AjustarColunas();
        else if (e.PropertyName == nameof(EditorVM.Atual) && _vm is { Editor.Atual: not null } vm && !Enxuto) vm.DetalhesAbertos = false;
    }

    void AjustarColunas()
    {
        var comEditor = _vm?.Editor.TemAbas == true;
        var empilhado = comEditor && Disposicao.GetVertical(this);
        var colunas = Divisao.ColumnDefinitions;
        var linhas = Divisao.RowDefinitions;
        if (empilhado)
        {
            colunas[0].Width = GridLength.Star;
            colunas[0].MinWidth = 0;
            colunas[1].Width = new GridLength(0);
            colunas[2].Width = new GridLength(0);
            if (linhas.Count != 3)
            {
                linhas.Clear();
                linhas.Add(new RowDefinition(2, GridUnitType.Star) { MinHeight = 280 });
                linhas.Add(new RowDefinition(5, GridUnitType.Pixel));
                linhas.Add(new RowDefinition(3, GridUnitType.Star) { MinHeight = 220 });
            }
            Grid.SetColumn(Alca, 0);
            Grid.SetRow(Alca, 1);
            Alca.ResizeDirection = GridResizeDirection.Rows;
            Grid.SetColumn(PainelEditor, 0);
            Grid.SetRow(PainelEditor, 2);
            Grid.SetRowSpan(Camada, 3);
            return;
        }
        linhas.Clear();
        Grid.SetColumn(Alca, 1);
        Grid.SetRow(Alca, 0);
        Alca.ResizeDirection = GridResizeDirection.Columns;
        Grid.SetColumn(PainelEditor, 2);
        Grid.SetRow(PainelEditor, 0);
        Grid.SetRowSpan(Camada, 1);
        colunas[0].Width = comEditor ? new GridLength(2, GridUnitType.Star) : GridLength.Star;
        colunas[0].MinWidth = comEditor ? 380 : 0;
        colunas[1].Width = comEditor ? new GridLength(5) : new GridLength(0);
        colunas[2].Width = comEditor ? new GridLength(3, GridUnitType.Star) : new GridLength(0);
    }

    void PorCursor(int posicao) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
    {
        if (!EmUso) return;
        Composer.Focus();
        Composer.CaretIndex = Math.Clamp(posicao, 0, Composer.Text?.Length ?? 0);
    }, Avalonia.Threading.DispatcherPriority.Input);

    bool EmUso => _ativa == this || (_ativa?._vm != _vm && IsEffectivelyVisible);

    void AoMudarVM(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is not { } vm) return;
        foreach (var menu in _menus)
            if (menu.Nome == e.PropertyName) menu.Popup.IsOpen = menu.Ler(vm) && EmUso;
    }

    void Focar()
    {
        if (!EmUso) return;
        Composer.Focus();
        Composer.CaretIndex = Composer.Text?.Length ?? 0;
        _ = AtualizarMencoesAsync();
    }

    async void AoColar(object? sender, RoutedEventArgs e)
    {
        if (_vm is null || TopLevel.GetTopLevel(this)?.Clipboard is not { } area) return;
        e.Handled = true;
        if (await area.TryGetFilesAsync() is { Length: > 0 } arquivos)
        {
            foreach (var arquivo in arquivos)
                if (arquivo.TryGetLocalPath() is { } caminho) _vm.AnexarArquivo(caminho);
            return;
        }
        if (await area.TryGetBitmapAsync() is { } imagem && _vm.Anexar(imagem)) return;
        if (await area.TryGetTextAsync() is not { Length: > 0 } texto) return;
        Substituir(Math.Min(Composer.SelectionStart, Composer.SelectionEnd), Math.Max(Composer.SelectionStart, Composer.SelectionEnd), texto);
    }

    void Substituir(int inicio, int fim, string texto)
    {
        var tamanho = Composer.Text?.Length ?? 0;
        Composer.SelectionStart = Math.Clamp(inicio, 0, tamanho);
        Composer.SelectionEnd = Math.Clamp(fim, 0, tamanho);
        Composer.SelectedText = texto;
        Composer.ClearSelection();
    }

    void AoArrastarPorCima(object? sender, DragEventArgs e) =>
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) || e.DataTransfer.Contains(DataFormat.Bitmap) ? DragDropEffects.Copy : DragDropEffects.None;

    void AoSoltar(object? sender, DragEventArgs e)
    {
        if (_vm is null) return;
        if (e.DataTransfer.TryGetFiles() is { Length: > 0 } arquivos)
            foreach (var arquivo in arquivos)
            {
                if (arquivo.TryGetLocalPath() is { } caminho) _vm.AnexarArquivo(caminho);
            }
        else if (e.DataTransfer.TryGetBitmap() is { } imagem)
            _vm.Anexar(imagem);
        e.Handled = true;
        Composer.Focus();
    }

    public static bool TemDitado => Ditado.Disponivel;

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        MarkdownTexto.LinkClicado += AbrirLink;
        AtualizarDisposicao();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        MarkdownTexto.LinkClicado -= AbrirLink;
        if (_vm is not null && !Enxuto) _vm.DetalhesAbertos = false;
        base.OnDetachedFromVisualTree(e);
    }

    async void CopiarPassagem_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not PassagemVM passagem || TopLevel.GetTopLevel(this)?.Clipboard is not { } area) return;
        await area.SetTextAsync(passagem.Titulo);
        passagem.MarcarCopiado();
    }

    async void CopiarMensagem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button botao || TopLevel.GetTopLevel(this)?.Clipboard is not { } area) return;
        var texto = botao.DataContext switch
        {
            ItemTexto item => item.Texto,
            ItemUsuario item => item.Texto,
            _ => null,
        };
        if (string.IsNullOrEmpty(texto)) return;
        await area.SetTextAsync(texto);
        if (botao.Content is not StackPanel { Children: [Icone icone, TextBlock rotulo] }) return;
        icone.Nome = "ok";
        rotulo.Text = Idioma.T("Copiado");
        botao.Classes.Add("copiado");
        await Task.Delay(1500);
        icone.Nome = "copiar";
        rotulo.Text = Idioma.T("Copiar");
        botao.Classes.Remove("copiado");
    }

    void ResponderMensagem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ItemTexto item } botao || _vm is null) return;
        var mensagem = botao.GetVisualAncestors().OfType<StackPanel>().FirstOrDefault(p => p.Classes.Contains("mensagem"));
        var selecionado = mensagem?.GetVisualDescendants().OfType<SelectableTextBlock>()
            .Select(b => b.SelectedText)
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
        _vm.ResponderA(selecionado ?? item.Texto);
    }

    void AbrirLink(MarkdownTexto origem, string destino)
    {
        if (_vm is null || origem.FindAncestorOfType<ChatView>() != this) return;
        if (Links.Arquivo(destino, _vm.Pasta) is { } alvo) _vm.Editor.Abrir(alvo.Caminho, alvo.Linha);
    }

    void AlcaTerminal_DragDelta(object? sender, Avalonia.Input.VectorEventArgs e)
    {
        if (_vm is null) return;
        var maximo = Math.Max(160, Bounds.Height - 260);
        _vm.AlturaTerminal = Math.Clamp(_vm.AlturaTerminal - e.Vector.Y, 120, maximo);
    }

    void AlcaTerminal_DragCompleted(object? sender, Avalonia.Input.VectorEventArgs e) => _vm?.GuardarAlturaTerminal();

    void Ditar_Click(object? sender, RoutedEventArgs e)
    {
        Composer.Focus();
        Dispatcher.UIThread.Post(Ditado.Abrir, DispatcherPriority.Background);
    }

    async void Anexar_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } janela || _vm is null) return;
        var arquivos = await janela.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Idioma.T("Anexar à mensagem"),
            AllowMultiple = true,
        });
        foreach (var arquivo in arquivos)
            if (arquivo.TryGetLocalPath() is { } caminho) _vm.AnexarArquivo(caminho);
        Composer.Focus();
    }

    async void OutraPasta_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } janela || DataContext is not ChatVM chat) return;
        var pastas = await janela.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Idioma.T("Em que pasta o agente vai trabalhar?"),
            AllowMultiple = false,
        });
        if (pastas.Count > 0 && pastas[0].TryGetLocalPath() is { } caminho) chat.UsarPasta(caminho);
    }

    async Task AtualizarMencoesAsync()
    {
        if (_vm is null) return;
        if (!Composer.IsFocused)
        {
            Comandos.IsOpen = false;
            Mencoes.IsOpen = false;
            return;
        }
        var texto = Composer.Text ?? "";
        var cursor = Math.Min(Composer.CaretIndex, texto.Length);
        var inicioComando = InicioDoGatilho(texto, cursor, '/');
        if (inicioComando >= 0)
        {
            Mencoes.IsOpen = false;
            _inicioMencao = -1;
            await _vm.BuscarComandosAsync(texto[inicioComando..cursor]);
            var atual = Composer.Text ?? "";
            Comandos.IsOpen = _vm.ComandosSugeridos.Count > 0 && InicioDoGatilho(atual, Math.Min(Composer.CaretIndex, atual.Length), '/') == inicioComando;
            if (Comandos.IsOpen) ListaComandos.SelectedIndex = 0;
            return;
        }
        Comandos.IsOpen = false;
        _inicioMencao = InicioDoGatilho(texto, cursor, '@');
        if (_inicioMencao < 0)
        {
            Mencoes.IsOpen = false;
            return;
        }
        Mencoes.IsOpen = true;
        await _vm.BuscarMencoesAsync(texto[(_inicioMencao + 1)..cursor]);
        if (_inicioMencao < 0) return;
        Mencoes.IsOpen = _vm.Sugestoes.Count > 0;
        if (Mencoes.IsOpen) ListaMencoes.SelectedIndex = 0;
    }

    static int InicioDoGatilho(string texto, int cursor, char gatilho)
    {
        for (var i = cursor - 1; i >= 0; i--)
        {
            if (texto[i] == gatilho) return i == 0 || char.IsWhiteSpace(texto[i - 1]) ? i : -1;
            if (char.IsWhiteSpace(texto[i])) return -1;
        }
        return -1;
    }

    void InserirComando()
    {
        if (ListaComandos.SelectedItem is not Nexora.Core.Claude.ComandoBarra comando) return;
        var texto = Composer.Text ?? "";
        var cursor = Math.Min(Composer.CaretIndex, texto.Length);
        var inicio = Math.Max(InicioDoGatilho(texto, cursor, '/'), 0);
        var espacos = texto[cursor..].Length - texto[cursor..].TrimStart().Length;
        Substituir(inicio, cursor + espacos, "/" + comando.Nome + " ");
        Comandos.IsOpen = false;
        Composer.Focus();
    }

    bool ComandoJaDigitado()
    {
        var texto = Composer.Text ?? "";
        var cursor = Math.Min(Composer.CaretIndex, texto.Length);
        var inicio = InicioDoGatilho(texto, cursor, '/');
        return inicio >= 0 && ListaComandos.SelectedItem is Nexora.Core.Claude.ComandoBarra comando
            && texto[inicio..cursor].Equals("/" + comando.Nome, StringComparison.OrdinalIgnoreCase);
    }

    void InserirMencao()
    {
        if (_vm is null || _inicioMencao < 0 || ListaMencoes.SelectedItem is not ArquivoIndexado arquivo) return;
        var texto = Composer.Text ?? "";
        var cursor = Math.Min(Composer.CaretIndex, texto.Length);
        Substituir(_inicioMencao, cursor, "@" + _vm.TextoDaMencao(arquivo) + " ");
        _inicioMencao = -1;
        Mencoes.IsOpen = false;
        Composer.Focus();
    }

    void AoTeclar(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.M && e.KeyModifiers == KeyModifiers.Alt)
        {
            _vm?.ProximoModo();
            e.Handled = true;
            return;
        }
        if (Comandos.IsOpen)
        {
            switch (e.Key)
            {
                case Key.Down:
                    ListaComandos.SelectedIndex = Math.Min(ListaComandos.SelectedIndex + 1, ListaComandos.ItemCount - 1);
                    e.Handled = true;
                    return;
                case Key.Up:
                    ListaComandos.SelectedIndex = Math.Max(ListaComandos.SelectedIndex - 1, 0);
                    e.Handled = true;
                    return;
                case Key.Enter when ComandoJaDigitado():
                    Comandos.IsOpen = false;
                    break;
                case Key.Enter or Key.Tab:
                    InserirComando();
                    e.Handled = true;
                    return;
                case Key.Escape:
                    Comandos.IsOpen = false;
                    e.Handled = true;
                    return;
            }
        }
        if (Mencoes.IsOpen)
        {
            switch (e.Key)
            {
                case Key.Down:
                    ListaMencoes.SelectedIndex = Math.Min(ListaMencoes.SelectedIndex + 1, ListaMencoes.ItemCount - 1);
                    e.Handled = true;
                    return;
                case Key.Up:
                    ListaMencoes.SelectedIndex = Math.Max(ListaMencoes.SelectedIndex - 1, 0);
                    e.Handled = true;
                    return;
                case Key.Enter or Key.Tab:
                    InserirMencao();
                    e.Handled = true;
                    return;
                case Key.Escape:
                    Mencoes.IsOpen = false;
                    e.Handled = true;
                    return;
            }
        }
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift)) return;
        e.Handled = true;
        if (_vm is null || !_vm.EnviarCommand.CanExecute(null)) return;
        _grudado = true;
        _vm.EnviarCommand.Execute(null);
    }

    public bool SugestoesAbertas => Comandos.IsOpen || Mencoes.IsOpen;

    void AoRolar(object? sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentDelta.Y > 0 && _grudado)
        {
            Rolagem.ScrollToEnd();
            return;
        }
        if (e.ExtentDelta.Y > 0 && _segurando)
        {
            Rolagem.Offset = Rolagem.Offset.WithY(Rolagem.Offset.Y + e.ExtentDelta.Y);
            return;
        }
        if (e.OffsetDelta.Y == 0) return;
        _grudado = Rolagem.Offset.Y >= Rolagem.Extent.Height - Rolagem.Viewport.Height - Folga;
        if (_vm is null) return;
        if (_grudado) _vm.ChegouAoFim();
        else _vm.NoFim = false;
        if (e.OffsetDelta.Y < 0 && Rolagem.Offset.Y < PertoDoTopo && _vm is { TemEscondidas: true, Montando: false, CarregandoAnteriores: false })
            _vm.MostrarAnterioresCommand.Execute(null);
    }
}
