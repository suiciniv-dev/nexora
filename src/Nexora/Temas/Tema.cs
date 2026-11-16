using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Nexora.Fundos;

namespace Nexora.Temas;

public sealed class Tema
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    public bool Escuro { get; set; } = true;
    public Dictionary<string, string> Cores { get; set; } = [];
    public string Fonte { get; set; } = Catalogo.FonteEmbutida;
    public string FonteCodigo { get; set; } = "Cascadia Mono";
    public string FundoAnimado { get; set; } = Papeis.Nenhum;
    public string? Imagem { get; set; }
    public double Escurecer { get; set; } = 0.75;
    public string? Editor { get; set; }

    [JsonIgnore]
    public bool Embutido { get; set; }

    [JsonIgnore]
    public string NomeExibido => Embutido ? Nexora.Core.Texto.Idioma.T(Nome) : Nome;

    public Color Cor(string chave)
    {
        if (Cores.TryGetValue(chave, out var hex) && Color.TryParse(hex, out var cor)) return cor;
        if (chave is "Marca" or "MarcaFim") return Cor("Acento");
        var reserva = (Escuro ? Catalogo.Dracula : Catalogo.Alucard).Cores[chave];
        return Color.Parse(reserva);
    }

    public Tema Copiar(string id, string nome) => new()
    {
        Id = id,
        Nome = nome,
        Escuro = Escuro,
        Cores = new Dictionary<string, string>(Cores),
        Fonte = Fonte,
        FonteCodigo = FonteCodigo,
        FundoAnimado = FundoAnimado,
        Imagem = Imagem,
        Escurecer = Escurecer,
        Editor = Editor,
    };
}

public static class Catalogo
{
    public const string FonteEmbutida = "Inter";
    public static readonly string[] Chaves =
    [
        "Fundo", "Lateral", "Superficie", "SuperficieAlta", "Borda", "Texto", "TextoSuave", "TextoFraco",
        "Acento", "AcentoHover", "SobreAcento", "Rosa", "Verde", "Laranja", "Vermelho", "Ciano",
        "CodigoFundo", "CodigoTexto", "BolhaUsuario", "Marca", "MarcaFim",
    ];

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static event Action? Mudou;
    public static Tema Atual { get; private set; } = Dracula;

    public static string Pasta =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nexora", "temas");

    public static Tema Dracula => Novo("dracula", "Dracula", true, "Dracula", "nenhum",
        "#282A36", "#21222C", "#2F3140", "#44475A", "#383A4C", "#F8F8F2", "#B9BDD6", "#939BC3",
        "#BD93F9", "#CBA6FB", "#282A36", "#FF79C6", "#50FA7B", "#FFB86C", "#FF6B6B", "#8BE9FD", "#21222C", "#FF79C6", "#3A3C4E");

    public static Tema Alucard => Novo("alucard", "Alucard", false, "QuietLight", "nenhum",
        "#FFFBEB", "#F4EEDA", "#F8F3E1", "#E9E3CD", "#E0D9C0", "#1F1F1F", "#4F4B69", "#645F86",
        "#644AC9", "#7560D3", "#FFFBEB", "#A3144D", "#14710A", "#A04B14", "#B73426", "#036A96", "#F3EDD7", "#A3144D", "#E8E4F4");

    const string RosaClaro = "#A14679", VerdeClaro = "#2B7349", LaranjaClaro = "#965600", VermelhoClaro = "#B23E2E", CianoClaro = "#1E6C99";

    public static IReadOnlyList<Tema> Embutidos { get; } =
    [
        Novo("padrao", "Padrão", true, "DarkPlus", Papeis.Nenhum,
            "#181716", "#121110", "#24221F", "#2E2B28", "#24FFFFFF", "#F2F0EC", "#C4C0B9", "#9C978F",
            "#F2F0EC", "#FFFFFF", "#161514", "#E59AC4", "#7BCB9F", "#F0A860", "#E8806C", "#86C5E6", "#0F0E0D", "#E6C38F", "#332F2C",
            "#948BAE", "#5B5470"),
        Novo("padrao-claro", "Padrão claro", false, "LightPlus", Papeis.Nenhum,
            "#F7F6F4", "#EFEDEA", "#FFFFFF", "#E9E6E2", "#26000000", "#1C1B1A", "#45423E", "#615D58",
            "#1C1B1A", "#000000", "#FFFFFF", RosaClaro, VerdeClaro, LaranjaClaro, VermelhoClaro, CianoClaro, "#EFEDE9", "#8A4B12", "#ECE9E4",
            "#5B5470", "#3A3548"),
        Dracula,
        Alucard,
        .. Papeis.Todos.Select(DoPapel),
    ];

    static readonly Dictionary<string, double> Transparencia = new()
    {
        ["Fundo"] = 0.80,
        ["Lateral"] = 0.85,
        ["Superficie"] = 0.60,
        ["SuperficieAlta"] = 0.72,
        ["CodigoFundo"] = 0.65,
        ["BolhaUsuario"] = 0.72,
    };

    public static string Modo { get; set; } = "animado";

    public static bool EmVidro(Tema tema, string modo) =>
        modo != "solido" && tema.Imagem is null && (Papeis.Encontrar(tema.FundoAnimado) is null || modo == "vidro");

    static Color NoModo(Tema tema, string chave, bool vidro)
    {
        var cor = tema.Cor(chave);
        if (vidro && cor.A == 255 && Transparencia.TryGetValue(chave, out var alfa))
            return Color.FromArgb((byte)Math.Round(alfa * 255), cor.R, cor.G, cor.B);
        if (!vidro && chave is "Fundo" or "Lateral" && Papeis.Encontrar(tema.FundoAnimado) is null)
            return Color.FromRgb(cor.R, cor.G, cor.B);
        return cor;
    }

    static Tema DoPapel(Papel papel)
    {
        var baseCor = Color.Parse(papel.Base);
        var acento = Color.Parse(papel.Acento);
        Color preto = Colors.Black, branco = Colors.White;
        string[] cores;
        if (papel.Escuro)
        {
            var vidro = Misturar(baseCor, Color.Parse("#121110"), 0.55);
            cores =
            [
                Hex(Misturar(vidro, preto, 0.25)), Hex(vidro, 0.88), Hex(Misturar(vidro, branco, 0.05), 0.9), Hex(Misturar(vidro, branco, 0.11), 0.94),
                "#1FFFFFFF", "#ECEBE7", "#C9C6BF", "#9C998F",
                Hex(acento), Hex(Misturar(acento, branco, 0.18)), Luminancia(acento) > 0.55 ? "#141312" : "#FFFFFF",
                "#E59AC4", "#6CC79A", "#F0A860", "#E2735F", "#7CC8E8",
                Hex(Misturar(vidro, preto, 0.35), 0.92), Hex(Misturar(acento, branco, 0.25)), Hex(Misturar(vidro, branco, 0.14), 0.92),
                "#948BAE", "#5B5470",
            ];
        }
        else
        {
            var tinta = Color.Parse("#1E1B26");
            cores =
            [
                Hex(baseCor), Hex(Misturar(baseCor, branco, 0.75), 0.88), Hex(Misturar(baseCor, branco, 0.85), 0.9), Hex(Misturar(baseCor, branco, 0.95), 0.95),
                Hex(tinta, 0.14), "#1C1B1A", "#45423E", "#615D58",
                Hex(acento), Hex(Misturar(acento, preto, 0.12)), "#FFFFFF",
                RosaClaro, VerdeClaro, LaranjaClaro, VermelhoClaro, CianoClaro,
                Hex(Misturar(baseCor, tinta, 0.07), 0.94), Hex(Misturar(acento, preto, 0.15)), Hex(Misturar(acento, branco, 0.85), 0.92),
                "#5B5470", "#3A3548",
            ];
        }
        var tema = Novo(Papeis.TemaDe(papel), papel.Nome, papel.Escuro, papel.Escuro ? "DarkPlus" : "LightPlus", papel.Id, cores);
        tema.Escurecer = 0.2;
        return tema;
    }

    static Color Misturar(Color a, Color b, double k) =>
        Color.FromRgb((byte)Math.Round(a.R + (b.R - a.R) * k), (byte)Math.Round(a.G + (b.G - a.G) * k), (byte)Math.Round(a.B + (b.B - a.B) * k));

    static double Luminancia(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255;

    static string Hex(Color c, double alfa = 1) =>
        alfa >= 1 ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : $"#{(byte)Math.Round(alfa * 255):X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    public static IReadOnlyList<Tema> Todos() => [.. Embutidos, .. DoUsuario()];

    public static IReadOnlyList<Tema> DoUsuario()
    {
        if (!Directory.Exists(Pasta)) return [];
        var lista = new List<Tema>();
        foreach (var arquivo in Directory.EnumerateFiles(Pasta, "*.json"))
            if (Ler(arquivo) is { } tema) lista.Add(tema);
        return lista.OrderBy(t => t.Nome, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static Tema? Ler(string arquivo)
    {
        try
        {
            var tema = JsonSerializer.Deserialize<Tema>(File.ReadAllText(arquivo), Json);
            if (tema is null || string.IsNullOrWhiteSpace(tema.Nome)) return null;
            if (string.IsNullOrWhiteSpace(tema.Id)) tema.Id = Path.GetFileNameWithoutExtension(arquivo);
            tema.FundoAnimado = Papeis.Migrar(tema.FundoAnimado);
            return tema;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static string Gravar(Tema tema, string? destino = null)
    {
        destino ??= Path.Combine(Pasta, tema.Id + ".json");
        Core.Arquivos.ArquivoSeguro.Gravar(destino, JsonSerializer.Serialize(tema, Json));
        return destino;
    }

    public static void Apagar(Tema tema)
    {
        var arquivo = Path.Combine(Pasta, tema.Id + ".json");
        if (!tema.Embutido && File.Exists(arquivo)) File.Delete(arquivo);
    }

    public static string NovoId(string nome)
    {
        var baseId = Core.Git.Repositorio.Slug(nome);
        if (baseId.Length == 0) baseId = "tema";
        var id = baseId;
        for (var i = 2; Todos().Any(t => t.Id == id); i++) id = $"{baseId}-{i}";
        return id;
    }

    public const string IdSistema = "sistema";

    public static string Escolhido { get; private set; } = IdSistema;

    public static Func<bool?>? SistemaEscuroDeTeste { get; set; }

    public static bool SegueOSistema(string? id) => id is null or "" or IdSistema;

    public static bool SistemaEscuro()
    {
        if (SistemaEscuroDeTeste?.Invoke() is { } fixo) return fixo;
        return Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant != Avalonia.Platform.PlatformThemeVariant.Light;
    }

    public static Tema DoSistema() => SistemaEscuro() ? Embutidos[0] : Embutidos[1];

    public static Tema Encontrar(string? id) => id switch
    {
        "escuro" => Dracula,
        "claro" => Alucard,
        "suiciniv-papel" => Embutidos[1],
        "suiciniv" => Embutidos[0],
        null or "" or IdSistema => DoSistema(),
        _ => Todos().FirstOrDefault(t => t.Id == id)
            ?? Embutidos.FirstOrDefault(t => Papeis.TemaAntigo(id) is { } novo && t.Id == novo)
            ?? Embutidos[0],
    };

    public static void Escolher(string? id)
    {
        Escolhido = SegueOSistema(id) ? IdSistema : id!;
        Aplicar(Encontrar(id));
    }

    public static void Escolher(Tema tema)
    {
        Escolhido = tema.Id;
        Aplicar(tema);
    }

    static bool _acompanhando;

    public static void AcompanharSistema()
    {
        if (_acompanhando || Application.Current?.PlatformSettings is not { } plataforma) return;
        _acompanhando = true;
        plataforma.ColorValuesChanged += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (Escolhido == IdSistema && DoSistema() is var tema && tema.Id != Atual.Id) Aplicar(tema);
        });
    }

    public static string FonteDaInterface { get; set; } = Aparencia.FonteDoSistema;

    public static void Aplicar(Tema tema)
    {
        if (Application.Current is not { } app) return;
        var variante = tema.Escuro ? ThemeVariant.Dark : ThemeVariant.Light;
        app.RequestedThemeVariant = variante;
        var vidro = EmVidro(tema, Modo);
        foreach (var chave in Chaves) app.Resources[chave] = new SolidColorBrush(NoModo(tema, chave, vidro));
        var superficie = NoModo(tema, "Superficie", vidro);
        var fundoCheio = tema.Cor("Fundo");
        app.Resources["FundoSolido"] = new SolidColorBrush(Color.FromRgb(fundoCheio.R, fundoCheio.G, fundoCheio.B));
        SolidColorBrush Tinto(Color cor, double opacidade)
        {
            if (superficie.A == 255) return Transparente(cor, opacidade);
            var mistura = Misturar(superficie, cor, opacidade * 1.3);
            return new SolidColorBrush(Color.FromArgb(Math.Max(superficie.A, (byte)235), mistura.R, mistura.G, mistura.B));
        }
        app.Resources["Vidro"] = superficie.A == 255 ? Brushes.Transparent : new SolidColorBrush(superficie);
        app.Resources["PedidoFundo"] = Tinto(tema.Cor("Laranja"), 0.12);
        app.Resources["ErroFundo"] = Tinto(tema.Cor("Vermelho"), 0.12);
        app.Resources["AcentoFundo"] = Tinto(tema.Cor("Acento"), tema.Escuro ? 0.16 : 0.12);
        app.Resources["DiffMais"] = Transparente(tema.Cor("Verde"), tema.Escuro ? 0.16 : 0.14);
        app.Resources["DiffMenos"] = Transparente(tema.Cor("Vermelho"), tema.Escuro ? 0.18 : 0.14);
        var marca = tema.Cor("Marca");
        var marcaFim = tema.Cor("MarcaFim");
        app.Resources["SobreVermelho"] = new SolidColorBrush(MelhorTexto(tema.Cor("Vermelho")));
        app.Resources["SobreLaranja"] = new SolidColorBrush(MelhorTexto(tema.Cor("Laranja")));
        app.Resources["SobreMarca"] = new SolidColorBrush(MelhorTexto(Misturar(marca, marcaFim, 0.5)));
        app.Resources["VeuConteudo"] = tema.Escuro ? Transparente(Colors.Black, 0.19) : (IBrush)Brushes.Transparent;
        app.Resources["FundoDoConteudo"] = tema.Escuro ? Transparente(Colors.White, 0.07) : Transparente(Colors.White, 0.35);
        var animado = Modo == "animado" && Papeis.Encontrar(tema.FundoAnimado) is not null;
        app.Resources["BrilhoFundo"] = vidro || animado || tema.Imagem is not null
            ? (IBrush)Brushes.Transparent
            : new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb((byte)(tema.Escuro ? 34 : 22), marca.R, marca.G, marca.B), 0),
                    new GradientStop(Color.FromArgb(0, marca.R, marca.G, marca.B), 0.45),
                    new GradientStop(Color.FromArgb(0, marcaFim.R, marcaFim.G, marcaFim.B), 0.7),
                    new GradientStop(Color.FromArgb((byte)(tema.Escuro ? 22 : 14), marcaFim.R, marcaFim.G, marcaFim.B), 1),
                },
            };
        app.Resources["MarcaGradiente"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(marca, 0), new GradientStop(marcaFim, 1) },
        };
        app.Resources["MarcaFundo"] = Tinto(marca, tema.Escuro ? 0.18 : 0.12);
        app.Resources["SelecaoTexto"] = Transparente(marca, tema.Escuro ? 0.42 : 0.28);
        var contraste = tema.Escuro ? Colors.White : Colors.Black;
        app.Resources["SelecaoFundo"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb((byte)(tema.Escuro ? 72 : 46), marca.R, marca.G, marca.B), 0),
                new GradientStop(Color.FromArgb((byte)(tema.Escuro ? 22 : 14), marca.R, marca.G, marca.B), 0.55),
                new GradientStop(Color.FromArgb((byte)(tema.Escuro ? 14 : 10), contraste.R, contraste.G, contraste.B), 1),
            },
        };
        app.Resources["SelecaoBorda"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb((byte)(tema.Escuro ? 120 : 90), marca.R, marca.G, marca.B), 0),
                new GradientStop(Color.FromArgb((byte)(tema.Escuro ? 20 : 16), contraste.R, contraste.G, contraste.B), 1),
            },
        };
        app.Resources["SelecaoSombra"] = BoxShadows.Parse($"0 2 10 0 {Hex(Colors.Black, tema.Escuro ? 0.28 : 0.08)}");
        app.Resources["SelecaoBrilho"] = BoxShadows.Parse($"0 0 10 1 {Hex(marca, tema.Escuro ? 0.75 : 0.5)}");
        app.Resources["SombraAba"] = BoxShadows.Parse($"inset 0 2 0 0 {Hex(marca)}");
        app.Resources["BrilhoCaixa"] = BoxShadows.Parse($"0 0 0 1 {Hex(marca, 0.45)}, 0 0 18 0 {Hex(marcaFim, tema.Escuro ? 0.22 : 0.14)}");
        var claroOuEscuro = tema.Escuro ? Colors.White : Colors.Black;
        app.Resources["BordaVidro"] = Transparente(claroOuEscuro, tema.Escuro ? 0.09 : 0.10);
        app.Resources["SombraCartao"] = BoxShadows.Parse(tema.Escuro
            ? $"inset 0 1 0 0 {Hex(Colors.White, 0.06)}, 0 10 30 0 {Hex(Colors.Black, 0.28)}"
            : $"inset 0 1 0 0 {Hex(Colors.White, 0.8)}, 0 6 20 0 {Hex(Colors.Black, 0.07)}");
        var laranja = tema.Cor("Laranja");
        app.Resources["BrilhoPrecisa"] = BoxShadows.Parse($"0 0 0 1 {Hex(laranja, 0.28)}, 0 0 30 0 {Hex(laranja, tema.Escuro ? 0.22 : 0.16)}");
        app.Resources["FonteUi"] = Familia(FonteDaInterface);
        app.Resources["Mono"] = new FontFamily($"{tema.FonteCodigo}, Cascadia Mono, Consolas, SF Mono, Menlo, Courier New");
        if (app.Styles.OfType<FluentTheme>().FirstOrDefault() is { } fluent && fluent.Palettes.TryGetValue(variante, out var paleta))
        {
            paleta.Accent = tema.Cor("Acento");
            paleta.RegionColor = tema.Cor("Fundo");
        }
        Atual = tema;
        Mudou?.Invoke();
    }

    public static string EditorDe(Tema tema) => tema.Editor ?? (tema.Escuro ? "Dracula" : "QuietLight");

    public static FontFamily Familia(string nome) => nome switch
    {
        FonteEmbutida => new FontFamily("fonts:Inter#Inter"),
        Aparencia.FonteDoSistema or "" => new FontFamily(NomeDaFonteDoSistema() + ", fonts:Inter#Inter"),
        _ => new FontFamily($"{nome}, fonts:Inter#Inter"),
    };

    static bool _fonteRegistrada;

    static string NomeDaFonteDoSistema()
    {
        if (!FonteDoSistema.Disponivel) return OperatingSystem.IsWindows() ? "Segoe UI" : "$Default";
        if (!_fonteRegistrada)
        {
            FontManager.Current.AddFontCollection(new FonteDoSistema());
            _fonteRegistrada = true;
        }
        return FonteDoSistema.Familia + ", Segoe UI";
    }

    public static Color MelhorTexto(Color fundo)
    {
        var escuro = Color.Parse("#161514");
        return Contraste(Colors.White, fundo) >= Contraste(escuro, fundo) ? Colors.White : escuro;
    }

    public static double Contraste(Color a, Color b)
    {
        static double Canal(byte v)
        {
            var c = v / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        static double Luz(Color c) => 0.2126 * Canal(c.R) + 0.7152 * Canal(c.G) + 0.0722 * Canal(c.B);
        var (la, lb) = (Luz(a), Luz(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    static SolidColorBrush Transparente(Color cor, double opacidade) =>
        new(Color.FromArgb((byte)Math.Round(opacidade * 255), cor.R, cor.G, cor.B));

    static Tema Novo(string id, string nome, bool escuro, string editor, string fundo, params string[] cores)
    {
        var mapa = Chaves.Zip(cores).ToDictionary(p => p.First, p => p.Second);
        mapa.TryAdd("Marca", mapa["Acento"]);
        mapa.TryAdd("MarcaFim", mapa["Rosa"]);
        return new()
        {
            Id = id,
            Nome = nome,
            Escuro = escuro,
            Editor = editor,
            FundoAnimado = fundo,
            Embutido = true,
            Cores = mapa,
        };
    }
}
