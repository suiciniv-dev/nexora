using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Nexora.Core.Revisao;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ComentarioPendenteVM : ObservableObject
{
    readonly RevisaoPendenteVM _dono;

    public ComentarioPendenteVM(ComentarioDeLinha dados, string origem, int indiceFim, RevisaoPendenteVM dono)
    {
        Dados = dados;
        Origem = origem;
        IndiceFim = indiceFim;
        _dono = dono;
        Texto = dados.Texto;
    }

    public ComentarioDeLinha Dados { get; private set; }
    public string Origem { get; }
    public int IndiceFim { get; }
    public string Arquivo => Dados.Arquivo;
    public string Nome => Path.GetFileName(Dados.Arquivo);
    public string Faixa => ComentariosDeRevisao.Faixa(Dados.Inicio, Dados.Fim, Dados.Antiga);
    public string Onde => $"{Nome} · {Faixa}";
    public Action? AoRemover { get; set; }

    [ObservableProperty]
    public partial string Texto { get; set; }

    partial void OnTextoChanged(string value) => Dados = Dados with { Texto = value.Trim() };

    [ObservableProperty]
    public partial bool Editando { get; set; }

    [ObservableProperty]
    public partial string Edicao { get; set; } = "";

    [RelayCommand]
    void Editar()
    {
        Edicao = Texto;
        Editando = true;
    }

    [RelayCommand]
    void SalvarEdicao()
    {
        if (string.IsNullOrWhiteSpace(Edicao)) return;
        Texto = Edicao.Trim();
        Editando = false;
    }

    [RelayCommand]
    void CancelarEdicao() => Editando = false;

    [RelayCommand]
    void Apagar() => _dono.Remover(this);
}
