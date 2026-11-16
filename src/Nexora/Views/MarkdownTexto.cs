using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Nexora.Core.Texto;

namespace Nexora.Views;

public sealed class MarkdownTexto : StackPanel
{
    public static readonly StyledProperty<string?> TextoProperty =
        AvaloniaProperty.Register<MarkdownTexto, string?>(nameof(Texto));

    public static readonly StyledProperty<Func<string, Task<Bitmap?>>?> CarregarImagemProperty =
        AvaloniaProperty.Register<MarkdownTexto, Func<string, Task<Bitmap?>>?>(nameof(CarregarImagem));

    static MarkdownTexto()
    {
        TextoProperty.Changed.AddClassHandler<MarkdownTexto>((m, _) => m.Montar());
        CarregarImagemProperty.Changed.AddClassHandler<MarkdownTexto>((m, _) => m.Montar());
    }

    public Func<string, Task<Bitmap?>>? CarregarImagem
    {
        get => GetValue(CarregarImagemProperty);
        set => SetValue(CarregarImagemProperty, value);
    }

    public static event Action<MarkdownTexto, string>? LinkClicado;

    const double Corpo = 14;
    const double Entrelinha = 1.6;

    public MarkdownTexto() => Spacing = 12;

    public string? Texto
    {
        get => GetValue(TextoProperty);
        set => SetValue(TextoProperty, value);
    }

    void Montar()
    {
        Children.Clear();
        _links.Clear();
        foreach (var bloco in Markdown.Blocos(Texto ?? "")) Children.Add(Criar(bloco));
    }

    Control Criar(Bloco bloco) => bloco switch
    {
        Titulo t => Frase(t.Texto, tamanho: t.Nivel switch { 1 => 20, 2 => 17, _ => 15 }, negrito: true, margem: new Thickness(0, 8, 0, -4)),
        Paragrafo p => CriarParagrafo(p.Texto),
        Lista l => CriarLista(l),
        Codigo c => CriarCodigo(c),
        Citacao c => new Border { Classes = { "mdCitacao" }, Child = Frase(c.Texto, suave: true) },
        Tabela t => CriarTabela(t),
        _ => new Border { Classes = { "mdRegua" } },
    };

    SelectableTextBlock Frase(string texto, double? tamanho = null, bool negrito = false, bool suave = false, Thickness? margem = null)
    {
        var bloco = new SelectableTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Inlines = Inlines(texto),
            Margin = margem ?? default,
            LineHeight = Math.Round(tamanho is { } titulo ? titulo * 1.3 : Corpo * Entrelinha),
        };
        if (tamanho is { } t) bloco.FontSize = t;
        if (negrito) bloco.FontWeight = FontWeight.SemiBold;
        if (suave) bloco.Classes.Add("suave");
        LigarLinks(bloco);
        return bloco;
    }

    void LigarLinks(SelectableTextBlock bloco)
    {
        if (bloco.Inlines is not { } inlines || !inlines.OfType<Run>().Any(r => _links.ContainsKey(r))) return;
        var texto = inlines.Text ?? "";
        var faixas = new List<(int Inicio, int Fim, string Destino)>();
        var posicao = 0;
        foreach (var run in inlines.OfType<Run>())
        {
            if (string.IsNullOrEmpty(run.Text)) continue;
            var inicio = texto.IndexOf(run.Text, posicao, StringComparison.Ordinal);
            if (inicio < 0) continue;
            if (_links.TryGetValue(run, out var destino)) faixas.Add((inicio, inicio + run.Text.Length, destino));
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

    readonly Dictionary<Run, string> _links = [];
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

    InlineCollection Inlines(string texto)
    {
        var inlines = new InlineCollection();
        var linhas = texto.Split('\n');
        for (var i = 0; i < linhas.Length; i++)
        {
            if (i > 0) inlines.Add(new LineBreak());
            foreach (var trecho in Markdown.Trechos(linhas[i])) inlines.Add(Pedaco(trecho));
        }
        return inlines;
    }

    Run Pedaco(Trecho trecho)
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
            _links[run] = trecho.Link;
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

    Control CriarCodigo(Codigo codigo)
    {
        var texto = new SelectableTextBlock { Text = codigo.Texto, FontSize = 12.5, TextWrapping = TextWrapping.NoWrap };
        texto.Bind(TextBlock.FontFamilyProperty, this.GetResourceObservable("Mono"));
        var borda = new Border
        {
            Classes = { "mdCodigo" },
            Child = RolagemHorizontal(texto),
        };
        if (codigo.Linguagem is null) return borda;
        var rotulo = new TextBlock { Text = codigo.Linguagem, Margin = new Thickness(2, 0, 0, 4) };
        rotulo.Classes.Add("fraco");
        return new StackPanel { Children = { rotulo, borda } };
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
                var celula = new Border
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
        return RolagemHorizontal(new Border { Classes = { "mdTabela" }, Child = grade, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left });
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
