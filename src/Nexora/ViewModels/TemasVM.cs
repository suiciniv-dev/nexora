using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;
using Nexora.Fundos;
using Nexora.Temas;
using TextMateSharp.Grammars;

namespace Nexora.ViewModels;

public sealed partial class TemasVM : ObservableObject
{
    public static readonly string[] Modos = ["animado", "vidro", "solido"];

    readonly Action<string> _aoUsar;

    public TemasVM(Action<string> aoUsar)
    {
        _aoUsar = aoUsar;
        Recarregar();
    }

    public ObservableCollection<TemaCartaoVM> Cartoes { get; } = [];
    public ObservableCollection<GrupoTemasVM> Grupos { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Editando), nameof(NaGaleria))]
    public partial TemaEditorVM? Editor { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMensagem))]
    public partial string? Mensagem { get; set; }

    public bool Editando => Editor is not null;
    public bool NaGaleria => Editor is null;
    public bool TemMensagem => Mensagem is not null;

    public static IEnumerable<Tema> Principais => Catalogo.Embutidos.Where(t => Papeis.Encontrar(t.FundoAnimado) is null);

    public static IEnumerable<Tema> Animados => Catalogo.Embutidos.Where(t => Papeis.Encontrar(t.FundoAnimado) is not null);

    public void Recarregar()
    {
        Cartoes.Clear();
        Cartoes.Add(TemaCartaoVM.DoSistema(this));
        foreach (var tema in Catalogo.Todos()) Cartoes.Add(new TemaCartaoVM(tema, this));
        Grupos.Clear();
        Grupos.Add(new GrupoTemasVM("Nexora", Idioma.T("Cores cheias e calmas. Seguir o sistema troca entre claro e escuro junto com o computador."),
            Cartoes.Where(c => c.EhSistema || c.Tema.Embutido && Papeis.Encontrar(c.Tema.FundoAnimado) is null)));
        Grupos.Add(new GrupoTemasVM(Idioma.T("Temas animados"), Idioma.T("Céus vivos do Nexora: estrelas que cintilam e um grafo sutil. Se mexem com o fundo em \"Vidro, com animação\"; nos outros modos ficam parados."),
            Cartoes.Where(c => !c.EhSistema && c.Tema.Embutido && Papeis.Encontrar(c.Tema.FundoAnimado) is not null)));
        var meus = Cartoes.Where(c => !c.EhSistema && !c.Tema.Embutido).ToList();
        if (meus.Count > 0) Grupos.Add(new GrupoTemasVM(Idioma.T("Seus temas"), Idioma.T("Salvos em %APPDATA%\\Nexora\\temas."), meus));
    }

    public void Usar(TemaCartaoVM cartao)
    {
        if (cartao.EhSistema) Catalogo.Escolher(Catalogo.IdSistema);
        else Catalogo.Escolher(cartao.Tema);
        _aoUsar(cartao.Id);
        foreach (var c in Cartoes) c.AtualizarUso();
    }

    public void Duplicar(Tema tema) =>
        Editor = new TemaEditorVM(tema.Copiar(Catalogo.NovoId(tema.Nome + " meu"), Idioma.F("{0} (meu)", tema.NomeExibido)), this);

    public void Editar(Tema tema) => Editor = new TemaEditorVM(tema.Copiar(tema.Id, tema.Nome), this);

    public void Apagar(Tema tema)
    {
        if (Catalogo.Escolhido == tema.Id) Usar(Cartoes[0]);
        Catalogo.Apagar(tema);
        Recarregar();
        Mensagem = Idioma.F("Tema {0} apagado.", tema.NomeExibido);
    }

    public void Exportar(Tema tema, string destino)
    {
        Catalogo.Gravar(tema, destino);
        Mensagem = Idioma.F("Tema exportado para {0}.", destino);
    }

    public void Importar(string arquivo)
    {
        if (Catalogo.Ler(arquivo) is not { } tema)
        {
            Mensagem = Idioma.T("Esse arquivo não é um tema do Nexora.");
            return;
        }
        tema.Id = Catalogo.NovoId(tema.Nome);
        Catalogo.Gravar(tema);
        Recarregar();
        Mensagem = Idioma.F("Tema {0} importado.", tema.NomeExibido);
    }

    [RelayCommand]
    void NovoTema() => Duplicar(Catalogo.Atual);

    public void Salvar(Tema tema)
    {
        Catalogo.Gravar(tema);
        Editor = null;
        Recarregar();
        Catalogo.Escolher(tema);
        _aoUsar(tema.Id);
        foreach (var c in Cartoes) c.AtualizarUso();
        Mensagem = Idioma.F("Tema {0} salvo e em uso.", tema.NomeExibido);
    }

    public void Cancelar()
    {
        Editor = null;
        Catalogo.Escolher(App.Config.Tema);
    }
}

public sealed class GrupoTemasVM(string titulo, string descricao, IEnumerable<TemaCartaoVM> cartoes)
{
    public string Titulo { get; } = titulo;
    public string Descricao { get; } = descricao;
    public IReadOnlyList<TemaCartaoVM> Cartoes { get; } = cartoes.ToList();
}

public sealed partial class TemaCartaoVM : ObservableObject
{
    readonly TemasVM? _dono;

    public TemaCartaoVM(Tema tema, TemasVM? dono)
    {
        Tema = tema;
        _dono = dono;
        if (Papeis.Encontrar(tema.FundoAnimado) is { } papel) _ = CarregarMiniatura(papel.Id);
    }

    public static TemaCartaoVM DoSistema(TemasVM? dono) => new(Catalogo.Embutidos[0], dono)
    {
        EhSistema = true,
        Escuro = new TemaCartaoVM(Catalogo.Embutidos[0], null),
        Claro = new TemaCartaoVM(Catalogo.Embutidos[1], null),
    };

    async Task CarregarMiniatura(string papel)
    {
        var imagem = await Miniaturas.Obter(papel);
        Dispatcher.UIThread.Post(() => Miniatura = imagem);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMiniatura))]
    public partial Bitmap? Miniatura { get; set; }

    public bool EhSistema { get; private init; }
    public bool NaoEhSistema => !EhSistema;
    public TemaCartaoVM? Escuro { get; private init; }
    public TemaCartaoVM? Claro { get; private init; }
    public bool TemMiniatura => Miniatura is not null;
    public Tema Tema { get; }
    public string Id => EhSistema ? Catalogo.IdSistema : Tema.Id;
    public string? Apelido { get; init; }
    public string Nome => Apelido ?? (EhSistema ? Idioma.T("Seguir o sistema") : Tema.NomeExibido);
    public string Descricao => EhSistema
        ? Idioma.T(OperatingSystem.IsMacOS() ? "Claro ou escuro, como o macOS" : "Claro ou escuro, como o Windows")
        : Papeis.Encontrar(Tema.FundoAnimado) is { } papel
            ? Idioma.F("{0}  ·  {1}", Tema.Escuro ? Idioma.T("Escuro") : Idioma.T("Claro"), Idioma.T(Tema.Embutido ? papel.Ideia : papel.Nome))
            : Tema.Escuro ? Idioma.T("Escuro") : Idioma.T("Claro");
    public bool Embutido => Tema.Embutido;
    public bool Meu => !EhSistema && !Tema.Embutido;
    public bool PodeDuplicar => !EhSistema;
    public bool EmUso => Catalogo.Escolhido == Id;
    public bool Disponivel => !EmUso;
    public string NomeAcessivel => EmUso ? Idioma.F("{0}, em uso", Nome) : Nome;
    public IBrush Fundo => P("Fundo");
    public IBrush Lateral => P("Lateral");
    public IBrush Superficie => P("Superficie");
    public IBrush SuperficieAlta => P("SuperficieAlta");
    public IBrush Acento => P("Acento");
    public IBrush Texto => P("Texto");
    public IBrush TextoFraco => P("TextoFraco");
    public IBrush Verde => P("Verde");
    public IBrush Laranja => P("Laranja");
    public IBrush Rosa => P("Rosa");

    IBrush P(string chave)
    {
        var cor = Tema.Cor(chave);
        return new SolidColorBrush(Color.FromRgb(cor.R, cor.G, cor.B));
    }

    public void AtualizarUso()
    {
        OnPropertyChanged(nameof(EmUso));
        OnPropertyChanged(nameof(Disponivel));
        OnPropertyChanged(nameof(NomeAcessivel));
    }

    [RelayCommand]
    void Usar() => _dono?.Usar(this);

    [RelayCommand]
    void Duplicar() => _dono?.Duplicar(Tema);

    [RelayCommand]
    void Editar() => _dono?.Editar(Tema);

    [RelayCommand]
    void Apagar() => _dono?.Apagar(Tema);
}

public sealed partial class TemaEditorVM : ObservableObject
{
    static readonly Dictionary<string, string> Rotulos = new()
    {
        ["Fundo"] = Idioma.T("Fundo"),
        ["Lateral"] = Idioma.T("Lateral"),
        ["Superficie"] = Idioma.T("Cartões"),
        ["SuperficieAlta"] = Idioma.T("Cartões em destaque"),
        ["Borda"] = Idioma.T("Bordas"),
        ["Texto"] = Idioma.T("Texto"),
        ["TextoSuave"] = Idioma.T("Texto suave"),
        ["TextoFraco"] = Idioma.T("Texto fraco"),
        ["Acento"] = Idioma.T("Destaque"),
        ["AcentoHover"] = Idioma.T("Destaque ao passar o mouse"),
        ["SobreAcento"] = Idioma.T("Texto sobre o destaque"),
        ["Rosa"] = Idioma.T("Rosa"),
        ["Verde"] = Idioma.T("Verde (pronta)"),
        ["Laranja"] = Idioma.T("Laranja (precisa de você)"),
        ["Vermelho"] = Idioma.T("Vermelho (erro)"),
        ["Ciano"] = Idioma.T("Ciano"),
        ["CodigoFundo"] = Idioma.T("Fundo do código"),
        ["CodigoTexto"] = Idioma.T("Código dentro do texto"),
        ["BolhaUsuario"] = Idioma.T("Sua mensagem"),
        ["Marca"] = Idioma.T("Cor do Nexora (início)"),
        ["MarcaFim"] = Idioma.T("Cor do Nexora (fim)"),
    };

    readonly TemasVM _dono;
    bool _montando = true;

    public TemaEditorVM(Tema rascunho, TemasVM dono)
    {
        Rascunho = rascunho;
        _dono = dono;
        Nome = rascunho.Nome;
        Escuro = rascunho.Escuro;
        Fonte = rascunho.Fonte;
        FonteCodigo = rascunho.FonteCodigo;
        FundoAnimado = NomeDoFundo(rascunho.FundoAnimado);
        Imagem = rascunho.Imagem;
        Escurecer = rascunho.Escurecer;
        TemaEditor = Catalogo.EditorDe(rascunho);
        foreach (var chave in Catalogo.Chaves) Cores.Add(new CorVM(chave, Rotulos[chave], rascunho.Cor(chave), Mudou));
        _montando = false;
        Mudou();
    }

    public Tema Rascunho { get; }
    public ObservableCollection<CorVM> Cores { get; } = [];
    public IReadOnlyList<string> Fontes { get; } = [Catalogo.FonteEmbutida, .. FontManager.Current.SystemFonts.Select(f => f.Name).Distinct().Order()];
    public IReadOnlyList<string> FundosAnimados { get; } = [NomeDoFundo(Papeis.Nenhum), .. Papeis.Todos.Select(p => NomeDoFundo(p.Id))];
    public IReadOnlyList<string> TemasDeEditor { get; } = Enum.GetNames<ThemeName>();

    [ObservableProperty]
    public partial string Nome { get; set; }

    [ObservableProperty]
    public partial bool Escuro { get; set; }

    [ObservableProperty]
    public partial string Fonte { get; set; }

    [ObservableProperty]
    public partial string FonteCodigo { get; set; }

    [ObservableProperty]
    public partial string FundoAnimado { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemImagem))]
    public partial string? Imagem { get; set; }

    [ObservableProperty]
    public partial double Escurecer { get; set; }

    [ObservableProperty]
    public partial string TemaEditor { get; set; }

    public bool TemImagem => Imagem is not null;

    public static string NomeDoFundo(string id) =>
        Papeis.Encontrar(id) is { } papel ? Idioma.T(papel.Nome) : Idioma.T("Sem fundo animado");

    static string IdDoFundo(string nome) => Papeis.Todos.FirstOrDefault(p => NomeDoFundo(p.Id) == nome)?.Id ?? Papeis.Nenhum;

    partial void OnNomeChanged(string value) => Mudou();
    partial void OnEscuroChanged(bool value) => Mudou();
    partial void OnFonteChanged(string value) => Mudou();
    partial void OnFonteCodigoChanged(string value) => Mudou();
    partial void OnFundoAnimadoChanged(string value) => Mudou();
    partial void OnImagemChanged(string? value) => Mudou();
    partial void OnEscurecerChanged(double value) => Mudou();
    partial void OnTemaEditorChanged(string value) => Mudou();

    void Mudou()
    {
        if (_montando) return;
        Rascunho.Nome = Nome.Trim().Length > 0 ? Nome.Trim() : Idioma.T("Meu tema");
        Rascunho.Escuro = Escuro;
        Rascunho.Fonte = Fonte;
        Rascunho.FonteCodigo = FonteCodigo;
        Rascunho.FundoAnimado = IdDoFundo(FundoAnimado);
        Rascunho.Imagem = Imagem;
        Rascunho.Escurecer = Escurecer;
        Rascunho.Editor = TemaEditor;
        foreach (var cor in Cores) Rascunho.Cores[cor.Chave] = cor.Hex;
        Catalogo.Aplicar(Rascunho);
    }

    [RelayCommand]
    void Salvar()
    {
        Mudou();
        _dono.Salvar(Rascunho);
    }

    [RelayCommand]
    void Cancelar() => _dono.Cancelar();

    [RelayCommand]
    void TirarImagem() => Imagem = null;
}

public sealed partial class CorVM : ObservableObject
{
    readonly Action _mudou;
    bool _sincronizando;

    public CorVM(string chave, string rotulo, Color cor, Action mudou)
    {
        Chave = chave;
        Rotulo = rotulo;
        _mudou = mudou;
        Cor = cor;
        Hex = Formatar(cor);
    }

    public string Chave { get; }
    public string Rotulo { get; }
    public IBrush Pincel => new SolidColorBrush(Cor);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Pincel))]
    public partial Color Cor { get; set; }

    [ObservableProperty]
    public partial string Hex { get; set; }

    partial void OnCorChanged(Color value)
    {
        if (_sincronizando) return;
        _sincronizando = true;
        Hex = Formatar(value);
        _sincronizando = false;
        _mudou();
    }

    partial void OnHexChanged(string value)
    {
        if (_sincronizando || !Color.TryParse(value.Trim().StartsWith('#') ? value.Trim() : "#" + value.Trim(), out var cor)) return;
        _sincronizando = true;
        Cor = cor;
        _sincronizando = false;
        _mudou();
    }

    static string Formatar(Color cor) =>
        cor.A == 255 ? $"#{cor.R:X2}{cor.G:X2}{cor.B:X2}" : $"#{cor.A:X2}{cor.R:X2}{cor.G:X2}{cor.B:X2}";
}
