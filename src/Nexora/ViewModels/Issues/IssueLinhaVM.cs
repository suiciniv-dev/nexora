using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using System.Text.RegularExpressions;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class IssueLinhaVM : ObservableObject
{
    readonly Action<IssueLinhaVM> _abrir;
    readonly Action<PrLigado> _abrirPr;

    public IssueLinhaVM(ItemDoQuadro item, AmbienteVM? projeto, ColunasIssuesVM colunas, Action<IssueLinhaVM> abrir, Action<PrLigado>? abrirPr = null)
    {
        Projeto = projeto;
        Colunas = colunas;
        _abrir = abrir;
        _abrirPr = abrirPr ?? (pr => PainelPrVM.AbrirLink(pr.Url));
        Item = item;
        Montar();
    }

    public ItemDoQuadro Item { get; private set; }
    public AmbienteVM? Projeto { get; }
    public ColunasIssuesVM Colunas { get; }
    public string Numero => Item.Numero > 0 ? $"#{Item.Numero}" : Idioma.T("rascunho");
    public string Titulo => Item.Titulo;
    public string Repositorio => Item.Repositorio.Split('/').LastOrDefault() ?? "";
    public string Icone => Item.EhPr ? "branch" : "issue";
    public bool Fechada => !Item.Aberto;
    public string Responsavel => Item.Responsaveis.FirstOrDefault() ?? "";
    public bool TemResponsavel => Responsavel.Length > 0;
    public string Inicial => Responsavel.Length > 0 ? Responsavel[..1].ToUpperInvariant() : "";
    public string MaisResponsaveis => Item.Responsaveis.Count > 1 ? $"+{Item.Responsaveis.Count - 1}" : "";
    public string Iteracao => Item.Iteracao ?? "";
    public string Quando => Item.Atualizada is { } q ? Core.Texto.Tempo.Relativo(q.LocalDateTime) : "";
    public ChipVM? Status { get; private set; }
    public ChipVM? Prioridade { get; private set; }
    public ChipVM? Tamanho { get; private set; }
    public IReadOnlyList<ChipVM> Etiquetas { get; private set; } = [];
    public IReadOnlyList<PilulaPrVM> Pilulas { get; private set; } = [];
    public bool TemPilulas => Pilulas.Count > 0;

    [ObservableProperty]
    public partial bool Escolhida { get; set; }

    void Montar()
    {
        static ChipVM? Chip(ValorDeCampo? v) => v is null ? null : ChipVM.De(v.Nome, v.Cor);
        Status = Chip(Item.Valor("Status"));
        Prioridade = Chip(Item.Valor("Priority", "Prioridade"));
        Tamanho = Chip(Item.Valor("Size", "Tamanho"));
        Etiquetas = [.. Item.Etiquetas.Select(e => ChipVM.De(e.Nome, e.Cor))];
        Pilulas = [.. Item.PrsLigados.OrderByDescending(p => p.Aberto).ThenByDescending(p => p.Numero).Select(p => new PilulaPrVM(p, _abrirPr))];
    }

    public void Trocar(ItemDoQuadro novo)
    {
        Item = novo;
        Montar();
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    void Abrir() => _abrir(this);
}
