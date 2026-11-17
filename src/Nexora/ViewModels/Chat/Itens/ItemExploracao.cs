using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class ItemExploracao : ItemChat
{
    static readonly HashSet<string> DeLeitura = ["Read", "Grep", "Glob", "LS"];

    public ItemExploracao(ItemFerramenta primeira) => Juntar(primeira);

    public static bool Explora(string nome) => DeLeitura.Contains(nome);

    public static IEnumerable<ItemFerramenta> Ferramentas(IEnumerable<ItemChat> itens)
    {
        foreach (var item in itens)
            if (item is ItemFerramenta f) yield return f;
            else if (item is ItemExploracao e)
                foreach (var parte in e.Partes) yield return parte;
    }

    public ObservableCollection<ItemFerramenta> Partes { get; } = [];
    public ItemFerramenta Primeira => Partes[0];
    public bool Unica => Partes.Count == 1;
    public bool Varias => Partes.Count > 1;
    public bool Rodando => Partes.Any(p => p.Rodando);
    public bool Erro => Partes.Any(p => p.Erro);
    public bool Ok => !Rodando && !Erro;
    public bool Concluida => !Rodando;
    public string Rotulo => Unica ? Primeira.Rotulo : Idioma.T("Explorou");

    public string Resumo
    {
        get
        {
            if (Unica) return Primeira.Resumo;
            var lidos = Partes.Count(p => p.Nome == "Read");
            var buscas = Partes.Count - lidos;
            var partes = new List<string>();
            if (lidos > 0) partes.Add(lidos == 1 ? Idioma.T("1 arquivo lido") : Idioma.F("{0} arquivos lidos", lidos));
            if (buscas > 0) partes.Add(buscas == 1 ? Idioma.T("1 busca") : Idioma.F("{0} buscas", buscas));
            return string.Join(" · ", partes);
        }
    }

    [ObservableProperty]
    public partial bool Aberta { get; set; }

    [RelayCommand]
    void Alternar() => Aberta = !Aberta;

    public void Juntar(ItemFerramenta parte)
    {
        Partes.Add(parte);
        parte.PropertyChanged += AoMudarParte;
        Avisar();
        OnPropertyChanged(nameof(Unica));
        OnPropertyChanged(nameof(Varias));
        OnPropertyChanged(nameof(Resumo));
        OnPropertyChanged(nameof(Rotulo));
    }

    void AoMudarParte(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ItemFerramenta.Concluida) or nameof(ItemFerramenta.Erro)) Avisar();
    }

    void Avisar()
    {
        OnPropertyChanged(nameof(Rodando));
        OnPropertyChanged(nameof(Erro));
        OnPropertyChanged(nameof(Ok));
        OnPropertyChanged(nameof(Concluida));
    }
}
