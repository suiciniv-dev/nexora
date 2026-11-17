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
