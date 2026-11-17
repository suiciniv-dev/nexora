using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Nexora.Core.Texto;

namespace Nexora.Controles;

public sealed class MarkdownTexto : StackPanel
{
    public static readonly StyledProperty<string?> TextoProperty =
        AvaloniaProperty.Register<MarkdownTexto, string?>(nameof(Texto));

    public static readonly StyledProperty<Func<string, Task<Bitmap?>>?> CarregarImagemProperty =
        AvaloniaProperty.Register<MarkdownTexto, Func<string, Task<Bitmap?>>?>(nameof(CarregarImagem));

    static MarkdownTexto()
    {
        TextoProperty.Changed.AddClassHandler<MarkdownTexto>((m, _) => m.Montar());
        CarregarImagemProperty.Changed.AddClassHandler<MarkdownTexto>((m, _) => m.Montar(tudo: true));
    }

    sealed record Montado(Bloco Bloco, Control Controle, SelectableTextBlock? Codigo);

    readonly List<Montado> _montados = [];
    bool _pendente;
    bool _refazer;

    public Func<string, Task<Bitmap?>>? CarregarImagem
    {
        get => GetValue(CarregarImagemProperty);
        set => SetValue(CarregarImagemProperty, value);
    }

    public static event Action<MarkdownTexto, string>? LinkClicado;

    const double Corpo = 14;
    const double Entrelinha = 1.6;

    public MarkdownTexto() => Spacing = 12;

    void AoAtualizarLayout(object? sender, EventArgs e)
    {
        if (!IsEffectivelyVisible) return;
        LayoutUpdated -= AoAtualizarLayout;
        if (_pendente) Montar();
    }

    public string? Texto
    {
        get => GetValue(TextoProperty);
        set => SetValue(TextoProperty, value);
    }

    void Montar(bool tudo = false)
    {
        _refazer |= tudo;
        if (!IsEffectivelyVisible)
        {
            if (!_pendente) LayoutUpdated += AoAtualizarLayout;
            _pendente = true;
            return;
        }
        if (_pendente) LayoutUpdated -= AoAtualizarLayout;
        _pendente = false;
        if (_refazer)
        {
            _refazer = false;
            Children.Clear();
            _montados.Clear();
        }
        var blocos = Markdown.Blocos(Texto ?? "");
        var iguais = 0;
        while (iguais < blocos.Count && iguais < _montados.Count && Iguais(blocos[iguais], _montados[iguais].Bloco)) iguais++;
        if (iguais < blocos.Count && iguais == _montados.Count - 1 && Atualizar(iguais, blocos[iguais])) iguais++;
        for (var i = _montados.Count - 1; i >= iguais; i--)
        {
            Children.RemoveAt(i);
            _montados.RemoveAt(i);
        }
        for (var i = iguais; i < blocos.Count; i++)
        {
            var montado = Criar(blocos[i]);
            _montados.Add(montado);
            Children.Add(montado.Controle);
        }
    }

    bool Atualizar(int indice, Bloco novo)
    {
        if (_montados[indice] is not { Bloco: Codigo antigo, Codigo: { } texto } montado || novo is not Codigo codigo || codigo.Linguagem != antigo.Linguagem) return false;
        texto.Text = codigo.Texto;
        _montados[indice] = montado with { Bloco = codigo };
        return true;
    }

    static bool Iguais(Bloco a, Bloco b) => (a, b) switch
    {
        (Lista x, Lista y) => x.Itens.SequenceEqual(y.Itens),
        (Tabela x, Tabela y) => x.Linhas.Count == y.Linhas.Count && x.Linhas.Zip(y.Linhas).All(p => p.First.SequenceEqual(p.Second)),
        _ => a.Equals(b),
    };

    Montado Criar(Bloco bloco)
    {
        if (bloco is Codigo codigo)
        {
            var (controle, texto) = CriarCodigo(codigo);
            return new Montado(bloco, controle, texto);
        }
        return new Montado(bloco, bloco switch
        {
            Titulo t => Frase(t.Texto, tamanho: t.Nivel switch { 1 => 20, 2 => 17, _ => 15 }, negrito: true, margem: new Thickness(0, 8, 0, -4)),
            Paragrafo p => CriarParagrafo(p.Texto),
            Lista l => CriarLista(l),
            Citacao c => new Caixa { Classes = { "mdCitacao" }, Child = Frase(c.Texto, suave: true) },
            Tabela t => CriarTabela(t),
            _ => new Caixa { Classes = { "mdRegua" } },
        }, null);
    }

    SelectableTextBlock Frase(string texto, double? tamanho = null, bool negrito = false, bool suave = false, Thickness? margem = null)
    {
        var links = new Dictionary<Run, string>();
        var bloco = new SelectableTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Inlines = Inlines(texto, links),
            Margin = margem ?? default,
            LineHeight = Math.Round(tamanho is { } titulo ? titulo * 1.3 : Corpo * Entrelinha),
        };
        if (tamanho is { } t) bloco.FontSize = t;
        if (negrito) bloco.FontWeight = FontWeight.SemiBold;
        if (suave) bloco.Classes.Add("suave");
        LigarLinks(bloco, links);
        return bloco;
    }

    void LigarLinks(SelectableTextBlock bloco, Dictionary<Run, string> links)
    {
        if (links.Count == 0 || bloco.Inlines is not { } inlines) return;
        var texto = inlines.Text ?? "";
        var faixas = new List<(int Inicio, int Fim, string Destino)>();
        var posicao = 0;
        foreach (var run in inlines.OfType<Run>())
        {
            if (string.IsNullOrEmpty(run.Text)) continue;
            var inicio = texto.IndexOf(run.Text, posicao, StringComparison.Ordinal);
            if (inicio < 0) continue;
            if (links.TryGetValue(run, out var destino)) faixas.Add((inicio, inicio + run.Text.Length, destino));
            posicao = inicio + run.Text.Length;
        }
        string? LinkEm(PointerEventArgs e)
        {
            var ponto = e.GetPosition(bloco) - new Point(bloco.Padding.Left, bloco.Padding.Top);
            var indice = bloco.TextLayout.HitTestPoint(ponto) is { IsInside: true } achado ? achado.TextPosition : -1;
            foreach (var (inicio, fim, destino) in faixas)
                if (indice >= inicio && indice < fim) return destino;
            return null;
        }
        bloco.PointerMoved += (_, e) =>
        {
            var sobreLink = LinkEm(e) is not null;
            if (sobreLink != (bloco.Cursor is not null)) bloco.Cursor = sobreLink ? (_mao ??= new Cursor(StandardCursorType.Hand)) : null;
        };
        bloco.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (e.InitialPressMouseButton != MouseButton.Left || bloco.SelectionStart != bloco.SelectionEnd || LinkEm(e) is not { } destino) return;
            e.Handled = true;
            if (!AbrirNoNavegador(destino)) LinkClicado?.Invoke(this, destino);
        }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
    }

    static Cursor? _mao;

    public static bool AbrirNoNavegador(string destino)
    {
        if (!Uri.TryCreate(destino, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https" or "mailto")) return false;
        try
        {
            Sistema.Abrir(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    InlineCollection Inlines(string texto, Dictionary<Run, string> links)
    {
        var inlines = new InlineCollection();
        var linhas = texto.Split('\n');
        for (var i = 0; i < linhas.Length; i++)
        {
            if (i > 0) inlines.Add(new LineBreak());
            foreach (var trecho in Markdown.Trechos(linhas[i])) inlines.Add(Pedaco(trecho, links));
        }
        return inlines;
    }

    Run Pedaco(Trecho trecho, Dictionary<Run, string> links)
    {
        var run = new Run(trecho.Texto);
        if (trecho.Negrito) run.FontWeight = FontWeight.SemiBold;
        if (trecho.Italico) run.FontStyle = FontStyle.Italic;
        if (trecho.Codigo)
        {
            run.Bind(TextElement.FontFamilyProperty, this.GetResourceObservable("Mono"));
            run.FontSize = 13;
            run.Bind(TextElement.ForegroundProperty, this.GetResourceObservable("CodigoTexto"));
        }
        if (trecho.Link is not null)
        {
            links[run] = trecho.Link;
            run.TextDecorations = TextDecorations.Underline;
            run.Bind(TextElement.ForegroundProperty, this.GetResourceObservable("Acento"));
        }
        return run;
    }

    Control CriarParagrafo(string texto)
    {
        var (resto, imagens) = ImagensMd.Separar(texto);
        if (imagens.Count == 0) return Frase(texto);
        var painel = new StackPanel { Spacing = 10 };
        if (resto.Length > 0) painel.Children.Add(Frase(resto));
        foreach (var imagem in imagens) painel.Children.Add(CriarImagem(imagem));
        return painel;
    }

    Control CriarImagem(ImagemMd imagem)
    {
        var largura = Math.Min(imagem.Largura ?? 860, 860);
        var aviso = new TextBlock { Text = Idioma.T("Carregando a imagem…"), Margin = new Thickness(12, 10), TextWrapping = TextWrapping.Wrap };
        aviso.Classes.Add("fraco");
        var borda = new Border
        {
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            MaxWidth = largura,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = aviso,
        };
        borda.Bind(Border.BorderBrushProperty, this.GetResourceObservable("Borda"));
        ToolTip.SetTip(borda, Idioma.T("Abrir a imagem no navegador"));
        borda.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            if (!imagem.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                Sistema.Abrir(new System.Diagnostics.ProcessStartInfo(imagem.Url) { UseShellExecute = true })?.Dispose();
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
        };
        if (CarregarImagem is { } carregar) _ = Preencher(carregar);
        else aviso.Text = imagem.Url;
        return borda;

        async Task Preencher(Func<string, Task<Bitmap?>> carregar)
        {
            var bitmap = await carregar(imagem.Url);
            if (bitmap is null)
            {
                aviso.Text = Idioma.T("A imagem não carregou. Clique para abrir no navegador.");
                return;
            }
            borda.Child = new Image { Source = bitmap, Stretch = Stretch.Uniform, MaxWidth = largura };
        }
    }

    Control CriarLista(Lista lista)
    {
        var painel = new StackPanel { Spacing = 6 };
        foreach (var item in lista.Itens)
        {
            var linha = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(item.Nivel * 18, 0, 0, 0) };
            var marcador = new TextBlock { Text = item.Marcador, MinWidth = 18, Margin = new Thickness(4, 0, 8, 0), LineHeight = Math.Round(Corpo * Entrelinha) };
            marcador.Classes.Add("suave");
            var texto = Frase(item.Texto);
            Grid.SetColumn(texto, 1);
            linha.Children.Add(marcador);
            linha.Children.Add(texto);
            painel.Children.Add(linha);
        }
        return painel;
    }

    (Control, SelectableTextBlock) CriarCodigo(Codigo codigo)
    {
        var texto = new SelectableTextBlock { Text = codigo.Texto, FontSize = 12.5, TextWrapping = TextWrapping.NoWrap };
        texto.Bind(TextBlock.FontFamilyProperty, this.GetResourceObservable("Mono"));
        var borda = new Caixa
        {
            Classes = { "mdCodigo" },
            Child = RolagemHorizontal(texto),
        };
        if (codigo.Linguagem is null) return (borda, texto);
        var rotulo = new TextBlock { Text = codigo.Linguagem, Margin = new Thickness(2, 0, 0, 4) };
        rotulo.Classes.Add("fraco");
        return (new StackPanel { Children = { rotulo, borda } }, texto);
    }

    Control CriarTabela(Tabela tabela)
    {
        if (tabela.Linhas.Count == 0) return new Panel();
        var colunas = tabela.Linhas.Max(l => l.Count);
        var grade = new Grid();
        for (var c = 0; c < colunas; c++) grade.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var l = 0; l < tabela.Linhas.Count; l++)
        {
            grade.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var c = 0; c < colunas; c++)
            {
                var texto = c < tabela.Linhas[l].Count ? tabela.Linhas[l][c] : "";
                var celula = new Caixa
                {
                    Classes = { "mdCelula" },
                    BorderThickness = new Thickness(c == 0 ? 0 : 1, l == 0 ? 0 : 1, 0, 0),
                    Child = Frase(texto, negrito: l == 0),
                };
                if (l == 0) celula.Classes.Add("mdCabecalho");
                Grid.SetRow(celula, l);
                Grid.SetColumn(celula, c);
                grade.Children.Add(celula);
            }
        }
        return RolagemHorizontal(new Caixa { Classes = { "mdTabela" }, Child = grade, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left });
    }

    static ScrollViewer RolagemHorizontal(Control conteudo)
    {
        var rolagem = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = conteudo,
        };
        rolagem.ScrollChanged += (_, _) =>
        {
            var espaco = new Thickness(0, 0, 0, rolagem.Extent.Width > rolagem.Viewport.Width + 0.5 ? 14 : 0);
            if (rolagem.Padding != espaco) rolagem.Padding = espaco;
        };
        return rolagem;
    }
}
