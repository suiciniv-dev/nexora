using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Arquivos;
using Nexora.Core.Git;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class NoArquivoVM : ObservableObject
{
    static readonly NoArquivoVM Carregando = new("…", "", false, null, null);
    readonly PainelVM? _painel;
    bool _carregado;

    public NoArquivoVM(string nome, string caminho, bool ehPasta, PainelVM? painel, NoArquivoVM? pai, bool novo = false)
    {
        Nome = nome;
        Caminho = caminho;
        EhPasta = ehPasta;
        Pai = pai;
        Novo = novo;
        _painel = painel;
        NomeEditado = nome;
        if (ehPasta && !novo) Filhos.Add(Carregando);
        if (ehPasta && !novo && pai is not null && PastasAbertas.Contains(caminho)) Expandido = true;
    }

    static readonly HashSet<string> PastasAbertas = new(App.Config.PastasAbertas, StringComparer.OrdinalIgnoreCase);

    static void LembrarPasta(string caminho, bool aberta)
    {
        if (caminho.Length == 0 || (aberta ? !PastasAbertas.Add(caminho) : !PastasAbertas.Remove(caminho))) return;
        App.Config.PastasAbertas = [.. PastasAbertas.Where(Directory.Exists).Take(400)];
        if (!JanelaVM.Isolada) App.Config.Salvar();
    }

    public string Nome { get; }
    public string Caminho { get; }
    public bool EhPasta { get; }
    public NoArquivoVM? Pai { get; }
    public bool Novo { get; }
    public bool Real => Caminho.Length > 0 && !Novo;
    public bool Mexivel => Real && Pai is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostraNome))]
    public partial bool Editando { get; set; }

    [ObservableProperty]
    public partial string NomeEditado { get; set; }

    [ObservableProperty]
    public partial bool Confirmando { get; set; }

    public bool MostraNome => !Editando;
    public string Dica => EhPasta ? Idioma.T("nome da pasta") : Idioma.T("nome do arquivo");
    public string TextoApagar => Idioma.F("Mandar {0} para a Lixeira?", Nome);

    [RelayCommand]
    void NovoArquivo() => _painel?.Novo(this, pasta: false);

    [RelayCommand]
    void NovaPasta() => _painel?.Novo(this, pasta: true);

    [RelayCommand]
    void Renomear()
    {
        NomeEditado = Nome;
        Editando = true;
    }

    [RelayCommand]
    void Confirmar() => _painel?.ConfirmarEdicao(this);

    [RelayCommand]
    void Cancelar() => _painel?.CancelarEdicao(this);

    [RelayCommand]
    void Apagar() => Confirmando = true;

    [RelayCommand]
    void NaoApagar() => Confirmando = false;

    [RelayCommand]
    void ConfirmarApagar() => _painel?.Apagar(this);

    [RelayCommand]
    void MostrarNoExplorer() => Sistema.Revelar(Caminho, EhPasta);
    public string Icone => EhPasta ? "pasta" : "arquivo";
    public ObservableCollection<NoArquivoVM> Filhos { get; } = [];

    [ObservableProperty]
    public partial bool Expandido { get; set; }

    partial void OnExpandidoChanged(bool value)
    {
        if (value) Carregar();
        if (EhPasta && Real && Pai is not null) LembrarPasta(Caminho, value);
    }

    public void Recarregar()
    {
        _carregado = false;
        if (Expandido) Carregar();
    }

    void Carregar()
    {
        if (_carregado || !EhPasta) return;
        _carregado = true;
        Filhos.Clear();
        foreach (var e in Pastas.Filhos(Caminho)) Filhos.Add(new NoArquivoVM(e.Nome, e.Caminho, e.EhPasta, _painel, this));
    }
}
