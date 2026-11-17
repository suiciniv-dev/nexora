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
