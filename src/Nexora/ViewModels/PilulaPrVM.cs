using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class PilulaPrVM(PrLigado pr, Action<PrLigado> abrir)
{
    public PrLigado Dados { get; } = pr;
    public string Curto => $"#{pr.Numero}";
    public bool Falhou => pr.Aberto && (pr.ChecksFalharam || pr.Revisao == "CHANGES_REQUESTED");
    public bool Aprovado => pr.Aberto && !pr.ChecksFalharam && !pr.ChecksRodando && pr.Revisao == "APPROVED";
    public bool Mergeado => pr.Mergeado;
    public bool Fechado => !pr.Aberto;

    public string Situacao => pr switch
    {
        { Mergeado: true } => Idioma.T("mergeado"),
        { Aberto: false } => Idioma.T("fechado"),
        { ChecksFalharam: true } => Idioma.T("checks falharam"),
        { Revisao: "CHANGES_REQUESTED" } => Idioma.T("pediram mudanças"),
        { Rascunho: true } => Idioma.T("rascunho"),
        { ChecksRodando: true } => Idioma.T("checks rodando"),
        { Revisao: "APPROVED" } => Idioma.T("aprovado"),
        { Revisao: "REVIEW_REQUIRED" } => Idioma.T("esperando revisão"),
        { ChecksOk: true } => Idioma.T("checks ok"),
        _ => Idioma.T("aberto"),
    };

    public string Texto => $"#{pr.Numero} · {Situacao}";
    public string Dica => string.Join(" · ", new[] { $"PR #{pr.Numero}", pr.Branch.Length > 0 ? pr.Branch : null, Situacao }.OfType<string>());

    [RelayCommand]
    void Abrir() => abrir(pr);
}
