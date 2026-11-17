using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class FaixaVM(string nome) : ObservableObject
{
    public string Nome { get; } = nome;
    public string Iniciais { get; } = Siglas.De(nome);
    public ObservableCollection<CartaoVM> Precisa { get; } = [];
    public ObservableCollection<CartaoVM> Trabalhando { get; } = [];
    public ObservableCollection<CartaoVM> Prontas { get; } = [];
    public ObservableCollection<CartaoVM> Paradas { get; } = [];

    [ObservableProperty]
    public partial string Resumo { get; set; } = "";

    public IReadOnlyList<CartaoVM> Coluna(int indice) => indice switch
    {
        0 => Precisa,
        1 => Trabalhando,
        2 => Prontas,
        _ => Paradas,
    };

    public void Atualizar(IReadOnlyList<CartaoVM> cartoes)
    {
        VisaoGeralVM.Encher(Precisa, [.. cartoes.Where(c => c.Precisa)], false);
        VisaoGeralVM.Encher(Trabalhando, [.. cartoes.Where(c => c.Trabalhando && !c.Precisa)], false);
        VisaoGeralVM.Encher(Prontas, [.. cartoes.Where(c => c.Pronta && !c.Trabalhando && !c.Precisa)], false);
        VisaoGeralVM.Encher(Paradas, [.. cartoes.Where(c => !c.Pronta && !c.Trabalhando && !c.Precisa)], false);
        var partes = new List<string>();
        if (Precisa.Count > 0) partes.Add(Precisa.Count == 1 ? Idioma.T("1 precisa de você") : Idioma.F("{0} precisam de você", Precisa.Count));
        if (Trabalhando.Count > 0) partes.Add(Idioma.F("{0} trabalhando", Trabalhando.Count));
        partes.Add(cartoes.Count == 1 ? Idioma.T("1 sessão") : Idioma.F("{0} sessões", cartoes.Count));
        Resumo = string.Join("  ·  ", partes);
    }
}
