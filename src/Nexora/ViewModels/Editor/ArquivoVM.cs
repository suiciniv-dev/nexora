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
