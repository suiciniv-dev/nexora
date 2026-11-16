using System.Collections.ObjectModel;
using System.Text;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Nexora.Core.Revisao;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public abstract partial class AbaVM : ObservableObject
{
    protected AbaVM(string titulo, string dica, Action<AbaVM> fechar, Action<AbaVM> selecionar)
    {
        Titulo = titulo;
        Dica = dica;
        _fechar = fechar;
        _selecionar = selecionar;
    }

    readonly Action<AbaVM> _fechar;
    readonly Action<AbaVM> _selecionar;

    public string Titulo { get; }
    public string Dica { get; }

    [ObservableProperty]
    public partial bool Ativa { get; set; }

    public virtual bool Modificada => false;

    [RelayCommand]
    void Fechar() => _fechar(this);

    [RelayCommand]
    void Selecionar() => _selecionar(this);

    public virtual void Descartar()
    {
    }
}

public sealed partial class ArquivoVM : AbaVM
{
    const int Maior = 8 * 1024 * 1024;
    static readonly HashSet<string> Imagens = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico" };

    static ArquivoVM() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    readonly Action<string> _citar;
    readonly string? _raizRelativa;
    FileSystemWatcher? _vigia;
    Encoding _codificacao = new UTF8Encoding(false);
    DateTime _gravadoEm;

    public ArquivoVM(string caminho, string? raizRelativa, Action<AbaVM> fechar, Action<AbaVM> selecionar, Action<string> citar)
        : base(Path.GetFileName(caminho), caminho, fechar, selecionar)
    {
        Caminho = caminho;
        _raizRelativa = raizRelativa;
        _citar = citar;
        Documento = new TextDocument();
        Documento.UndoStack.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UndoStack.IsOriginalFile)) OnPropertyChanged(nameof(Modificada));
        };
        Carregar();
        Vigiar();
    }

    public string Caminho { get; }
    public TextDocument Documento { get; }
    public string Extensao => Path.GetExtension(Caminho);
    public string Relativo => _raizRelativa is { } raiz ? Path.GetRelativePath(raiz, Caminho).Replace('\\', '/') : Caminho;
    public override bool Modificada => !Documento.UndoStack.IsOriginalFile;
    public int? LinhaInicial { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Legivel), nameof(MostraTexto))]
    public partial string? Problema { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EhImagem), nameof(MostraTexto))]
    public partial Avalonia.Media.Imaging.Bitmap? Imagem { get; set; }

    [ObservableProperty]
    public partial string? InfoImagem { get; set; }

    public bool EhImagem => Imagem is not null;
    public bool MostraTexto => Problema is null && Imagem is null;

    [ObservableProperty]
    public partial bool MudouNoDisco { get; set; }

    public bool Legivel => Problema is null;

    public event Action? Recarregado;

    [RelayCommand]
    public void Salvar()
    {
        if (!MostraTexto) return;
        try
        {
            _vigia?.Dispose();
            File.WriteAllText(Caminho, Documento.Text, _codificacao);
            _gravadoEm = File.GetLastWriteTimeUtc(Caminho);
            Documento.UndoStack.MarkAsOriginalFile();
            MudouNoDisco = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Problema = Idioma.F("Não consegui salvar: {0}", ex.Message);
        }
        finally
        {
            Vigiar();
        }
    }

    [RelayCommand]
    void Recarregar()
    {
        Carregar();
        MudouNoDisco = false;
        Recarregado?.Invoke();
    }

    [RelayCommand]
    void ManterMeu() => MudouNoDisco = false;

    public void Citar(int primeira, int ultima, string texto)
    {
        var faixa = primeira == ultima ? Idioma.F("linha {0}", primeira) : Idioma.F("linhas {0}-{1}", primeira, ultima);
        var linguagem = Extensao.TrimStart('.');
        _citar($"`{Relativo}` ({faixa}):\n```{linguagem}\n{texto.TrimEnd()}\n```\n");
    }

    public void CitarImagem() => _citar($"@{Relativo} ");

    public override void Descartar()
    {
        _vigia?.Dispose();
        Imagem?.Dispose();
    }

    static string Tamanho(long bytes) => bytes >= 1024 * 1024 ? $"{(bytes / (1024.0 * 1024)).ToString("0.#", Idioma.Cultura)} MB" : $"{Math.Max(1, bytes / 1024)} KB";

    bool CarregarImagem(byte[] bytes, FileInfo info)
    {
        var anterior = Imagem;
        try
        {
            using var fluxo = new MemoryStream(bytes);
            Imagem = new Avalonia.Media.Imaging.Bitmap(fluxo);
            InfoImagem = $"{Imagem.PixelSize.Width} × {Imagem.PixelSize.Height} · {Tamanho(info.Length)}";
            Problema = null;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or IOException)
        {
            Imagem = null;
            Problema = Idioma.T("Não consegui mostrar esta imagem.");
        }
        anterior?.Dispose();
        _gravadoEm = info.LastWriteTimeUtc;
        return true;
    }

    void Carregar()
    {
        try
        {
            var info = new FileInfo(Caminho);
            if (!info.Exists)
            {
                Problema = Idioma.T("O arquivo não existe mais.");
                return;
            }
            if (info.Length > Maior)
            {
                Problema = Idioma.T("Arquivo grande demais para abrir aqui.");
                return;
            }
            var bytes = File.ReadAllBytes(Caminho);
            if (Imagens.Contains(Extensao) && CarregarImagem(bytes, info)) return;
            if (bytes.AsSpan(0, Math.Min(bytes.Length, 8000)).Contains((byte)0))
            {
                Problema = Idioma.T("Arquivo binário.");
                return;
            }
            var bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            string texto;
            try
            {
                texto = new UTF8Encoding(false, true).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
                _codificacao = new UTF8Encoding(bom);
            }
            catch (DecoderFallbackException)
            {
                _codificacao = Encoding.GetEncoding(1252);
                texto = _codificacao.GetString(bytes);
            }
            Documento.Text = texto;
            Documento.UndoStack.ClearAll();
            Documento.UndoStack.MarkAsOriginalFile();
            _gravadoEm = info.LastWriteTimeUtc;
            Problema = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Problema = Idioma.F("Não consegui abrir: {0}", ex.Message);
        }
    }

    void Vigiar()
    {
        try
        {
            _vigia = new FileSystemWatcher(Path.GetDirectoryName(Caminho)!, Path.GetFileName(Caminho))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };
            _vigia.Changed += (_, _) => Dispatcher.UIThread.Post(AoMudarNoDisco);
            _vigia.Created += (_, _) => Dispatcher.UIThread.Post(AoMudarNoDisco);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
        }
    }

    void AoMudarNoDisco()
    {
        DateTime agora;
        try
        {
            agora = File.GetLastWriteTimeUtc(Caminho);
        }
        catch (IOException)
        {
            return;
        }
        if (agora == _gravadoEm) return;
        if (Modificada)
        {
            MudouNoDisco = true;
            return;
        }
        Carregar();
        Recarregado?.Invoke();
    }
}

public sealed partial class DiffVM : AbaVM
{
    readonly Action<string>? _abrir;
    readonly IReadOnlyList<LinhaDiff> _brutas;
    LinhaDiffVM? _escrevendo;
    int _inicio = -1;

    public DiffVM(string titulo, string dica, IReadOnlyList<LinhaDiff> linhas, string? arquivo, Action<AbaVM> fechar, Action<AbaVM> selecionar, Action<string>? abrir,
        RevisaoPendenteVM? revisao = null, string? relativo = null, IReadOnlyList<ConversaDoPr>? conversas = null)
        : base(titulo, dica, fechar, selecionar)
    {
        _brutas = linhas;
        Revisao = revisao;
        Linhas = linhas.Select((l, i) => new LinhaDiffVM(l, i, this)).ToList();
        Arquivo = arquivo;
        _abrir = abrir;
        Relativo = relativo ?? (arquivo is not null ? Path.GetFileName(arquivo) : Dica.Split('#')[0]);
        Adicionadas = linhas.Count(l => l.Tipo == TipoLinha.Adicionada);
        Removidas = linhas.Count(l => l.Tipo == TipoLinha.Removida);
        if (revisao is not null)
            foreach (var pendente in revisao.De(dica))
                if (pendente.IndiceFim >= 0 && pendente.IndiceFim < Linhas.Count) Prender(Linhas[pendente.IndiceFim], pendente);
        if (conversas is not null) Pendurar(conversas);
    }

    public List<LinhaDiffVM> Linhas { get; }
    public string? Arquivo { get; }
    public string Relativo { get; }
    public RevisaoPendenteVM? Revisao { get; }
    public bool PodeComentar => Revisao is not null;
    public string Caminho => Arquivo ?? Dica.Split('#')[0];
    public int Adicionadas { get; }
    public int Removidas { get; }
    public string Resumo => $"+{Adicionadas}  −{Removidas}";
    public bool PodeAbrir => Arquivo is not null && File.Exists(Arquivo);
    public bool Vazio => Linhas.Count == 0;
    public int ConversasNoDiff { get; private set; }

    void Pendurar(IReadOnlyList<ConversaDoPr> conversas)
    {
        foreach (var conversa in conversas.Where(c => !c.Desatualizada && c.Linha is not null))
        {
            var linha = Linhas.LastOrDefault(l => l.NumeroNovo == conversa.Linha) ?? Linhas.LastOrDefault(l => l.NumeroNovo is null && l.NumeroAntigo == conversa.Linha);
            if (linha is null) continue;
            linha.Pendurar(new ConversaNaLinhaVM(conversa));
            ConversasNoDiff++;
        }
    }

    static void Prender(LinhaDiffVM linha, ComentarioPendenteVM comentario)
    {
        linha.Comentarios.Add(comentario);
        comentario.AoRemover = () => linha.Comentarios.Remove(comentario);
    }

    public void Comentar(LinhaDiffVM linha, bool estender)
    {
        if (Revisao is null || linha.Cabecalho) return;
        if (!estender && _escrevendo is { } aberto && aberto != linha && !string.IsNullOrWhiteSpace(aberto.Rascunho)) Confirmar(aberto);
        var rascunho = _escrevendo?.Rascunho ?? "";
        if (!estender || _escrevendo is null || _inicio < 0) _inicio = linha.Indice;
        var (de, ate) = (Math.Min(_inicio, linha.Indice), Math.Max(_inicio, linha.Indice));
        if (_escrevendo is not null && _escrevendo != Linhas[ate]) _escrevendo.Escrevendo = false;
        _escrevendo = Linhas[ate];
        _escrevendo.Rascunho = estender ? rascunho : "";
        _escrevendo.FaixaDoRascunho = ComentariosDeRevisao.Comentario(Relativo, _brutas, de, ate, "") is { } previa
            ? ComentariosDeRevisao.Faixa(previa.Inicio, previa.Fim, previa.Antiga) : "";
        _escrevendo.Escrevendo = true;
        Marcar(de, ate);
    }

    void Marcar(int de, int ate)
    {
        foreach (var l in Linhas) l.NaFaixa = l.Indice >= de && l.Indice <= ate && ate > de;
    }

    public void Confirmar(LinhaDiffVM linha)
    {
        if (Revisao is null || linha != _escrevendo || string.IsNullOrWhiteSpace(linha.Rascunho)) return;
        var de = Math.Min(_inicio < 0 ? linha.Indice : _inicio, linha.Indice);
        if (ComentariosDeRevisao.Comentario(Relativo, _brutas, de, linha.Indice, linha.Rascunho) is not { } dados) return;
        var pendente = new ComentarioPendenteVM(dados, Dica, linha.Indice, Revisao);
        Revisao.Adicionar(pendente);
        Prender(linha, pendente);
        Cancelar();
    }

    public void Cancelar()
    {
        if (_escrevendo is not null)
        {
            _escrevendo.Escrevendo = false;
            _escrevendo.Rascunho = "";
        }
        _escrevendo = null;
        _inicio = -1;
        Marcar(-1, -1);
    }

    [RelayCommand]
    void AbrirArquivo()
    {
        if (Arquivo is not null) _abrir?.Invoke(Arquivo);
    }
}

public sealed partial class LinhaDiffVM : ObservableObject
{
    readonly DiffVM? _dono;
    readonly List<ConversaNaLinhaVM> _resolvidas = [];

    public LinhaDiffVM(LinhaDiff linha, int indice = 0, DiffVM? dono = null)
    {
        _dono = dono;
        Indice = indice;
        NumeroAntigo = linha.Antiga;
        NumeroNovo = linha.Nova;
        Texto = linha.Tipo == TipoLinha.Cabecalho ? linha.Texto : linha.Texto.Replace("\t", "    ");
        Antiga = linha.Antiga?.ToString() ?? "";
        Nova = linha.Nova?.ToString() ?? "";
        Marca = linha.Tipo switch { TipoLinha.Adicionada => "+", TipoLinha.Removida => "−", _ => "" };
        Adicionada = linha.Tipo == TipoLinha.Adicionada;
        Removida = linha.Tipo == TipoLinha.Removida;
        Cabecalho = linha.Tipo == TipoLinha.Cabecalho;
        Comentarios.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TemComentarios));
    }

    public int Indice { get; }
    public int? NumeroAntigo { get; }
    public int? NumeroNovo { get; }
    public string Texto { get; }
    public string Antiga { get; }
    public string Nova { get; }
    public string Marca { get; }
    public bool Adicionada { get; }
    public bool Removida { get; }
    public bool Cabecalho { get; }
    public bool Comentavel => _dono is { PodeComentar: true } && !Cabecalho;

    public ObservableCollection<ComentarioPendenteVM> Comentarios { get; } = [];
    public bool TemComentarios => Comentarios.Count > 0;

    public ObservableCollection<ConversaNaLinhaVM> Conversas { get; } = [];
    public IReadOnlyList<ConversaNaLinhaVM> Resolvidas => _resolvidas;
    public IReadOnlyList<ConversaNaLinhaVM> ResolvidasVisiveis => [.. _resolvidas.Take(3)];
    public bool TemResolvidas => _resolvidas.Count > 0;
    public bool TemMaisResolvidas => _resolvidas.Count > 3;
    public string MaisResolvidas => $"+{_resolvidas.Count - 3}";

    public string DicaResolvidas =>
        (_resolvidas.Count == 1 ? Idioma.T("1 conversa resolvida") : Idioma.F("{0} conversas resolvidas", _resolvidas.Count))
        + ": " + string.Join(", ", _resolvidas.Select(r => r.Autor).Distinct()) + ". "
        + (ResolvidasAbertas ? Idioma.T("Clique para dobrar.") : Idioma.T("Clique para abrir."));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DicaResolvidas))]
    public partial bool ResolvidasAbertas { get; set; }

    [ObservableProperty]
    public partial bool Escrevendo { get; set; }

    [ObservableProperty]
    public partial string Rascunho { get; set; } = "";

    [ObservableProperty]
    public partial string FaixaDoRascunho { get; set; } = "";

    [ObservableProperty]
    public partial bool NaFaixa { get; set; }

    public void Pendurar(ConversaNaLinhaVM conversa)
    {
        if (!conversa.Resolvida)
        {
            Conversas.Add(conversa);
            return;
        }
        _resolvidas.Add(conversa);
        OnPropertyChanged(nameof(TemResolvidas));
        OnPropertyChanged(nameof(ResolvidasVisiveis));
        OnPropertyChanged(nameof(TemMaisResolvidas));
        OnPropertyChanged(nameof(MaisResolvidas));
        OnPropertyChanged(nameof(DicaResolvidas));
    }

    partial void OnResolvidasAbertasChanged(bool value)
    {
        foreach (var r in _resolvidas)
            if (value) Conversas.Add(r);
            else Conversas.Remove(r);
    }

    public void Comentar(bool estender) => _dono?.Comentar(this, estender);

    [RelayCommand]
    void AlternarResolvidas() => ResolvidasAbertas = !ResolvidasAbertas;

    [RelayCommand]
    void Confirmar() => _dono?.Confirmar(this);

    [RelayCommand]
    void Cancelar() => _dono?.Cancelar();
}

public sealed partial class EditorVM : ObservableObject
{
    readonly Func<string?> _raiz;
    readonly Action<string> _citar;

    public EditorVM(Func<string?> raiz, Action<string> citar)
    {
        _raiz = raiz;
        _citar = citar;
        Abas.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TemAbas));
    }

    public ObservableCollection<AbaVM> Abas { get; } = [];
    public bool TemAbas => Abas.Count > 0;

    [ObservableProperty]
    public partial AbaVM? Atual { get; set; }

    partial void OnAtualChanged(AbaVM? oldValue, AbaVM? newValue)
    {
        if (oldValue is not null) oldValue.Ativa = false;
        if (newValue is not null) newValue.Ativa = true;
    }

    public void Abrir(string caminho, int? linha = null)
    {
        var existente = Abas.OfType<ArquivoVM>().FirstOrDefault(a => string.Equals(a.Caminho, caminho, StringComparison.OrdinalIgnoreCase));
        if (existente is null)
        {
            existente = new ArquivoVM(caminho, RaizDe(caminho), Fechar, a => Atual = a, _citar);
            Abas.Add(existente);
        }
        existente.LinhaInicial = linha;
        Atual = existente;
    }

    public RevisaoPendenteVM? Revisao { get; init; }
    public Func<string, IReadOnlyList<ConversaDoPr>>? ConversasDoArquivo { get; init; }

    public string RelativoDe(string arquivo)
    {
        var raiz = _raiz();
        var relativo = raiz is { Length: > 0 } && arquivo.StartsWith(raiz, StringComparison.OrdinalIgnoreCase) ? Path.GetRelativePath(raiz, arquivo) : arquivo;
        return relativo.Replace('\\', '/');
    }

    public DiffVM AbrirDiff(string titulo, string dica, IReadOnlyList<LinhaDiff> linhas, string? arquivo, IReadOnlyList<ConversaDoPr>? conversas = null)
    {
        foreach (var antigo in Abas.OfType<DiffVM>().Where(d => d.Dica == dica).ToList()) Abas.Remove(antigo);
        var relativo = arquivo is not null ? RelativoDe(arquivo) : null;
        conversas ??= relativo is not null ? ConversasDoArquivo?.Invoke(relativo) : null;
        var diff = new DiffVM(titulo, dica, linhas, arquivo, Fechar, a => Atual = a, c => Abrir(c), Revisao, relativo, conversas);
        Abas.Add(diff);
        Atual = diff;
        return diff;
    }

    [RelayCommand]
    void FecharTodas()
    {
        foreach (var aba in Abas.ToList()) Fechar(aba);
    }

    static bool Dentro(string caminho, string alvo) =>
        string.Equals(caminho, alvo, StringComparison.OrdinalIgnoreCase)
        || caminho.StartsWith(alvo.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);

    public bool TemModificadaEm(string alvo) => Abas.OfType<ArquivoVM>().Any(a => a.Modificada && Dentro(a.Caminho, alvo));

    public bool FecharDe(string alvo)
    {
        var abas = Abas.Where(a => a is ArquivoVM arquivo && Dentro(arquivo.Caminho, alvo)).ToList();
        foreach (var aba in abas) Fechar(aba);
        return abas.Count > 0;
    }

    public void Fechar(AbaVM aba)
    {
        var indice = Abas.IndexOf(aba);
        if (indice < 0) return;
        aba.Descartar();
        Abas.RemoveAt(indice);
        if (Atual == aba) Atual = Abas.Count == 0 ? null : Abas[Math.Min(indice, Abas.Count - 1)];
    }

    public void Descartar()
    {
        foreach (var aba in Abas) aba.Descartar();
    }

    string? RaizDe(string caminho) =>
        _raiz() is { } raiz && caminho.StartsWith(raiz, StringComparison.OrdinalIgnoreCase) ? raiz : Path.GetDirectoryName(caminho);
}
