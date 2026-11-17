using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class CheckPrVM(CheckDoPr dados, Action<CheckDoPr> pedir)
{
    public CheckDoPr Dados { get; } = dados;
    public string Nome => dados.Nome;
    public string Fluxo => dados.Fluxo;
    public bool TemFluxo => dados.Fluxo.Length > 0;
    public bool Falhou => dados.Estado == EstadoCheck.Falhou;
    public bool Ok => dados.Estado == EstadoCheck.Ok;
    public bool Pendente => dados.Estado == EstadoCheck.Pendente;
    public bool Pulado => dados.Estado == EstadoCheck.Pulado;
    public bool TemLink => dados.Link.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    public string Icone => dados.Estado switch
    {
        EstadoCheck.Falhou => "fechar",
        EstadoCheck.Pendente => "relogio",
        EstadoCheck.Pulado => "pulado",
        _ => "ok",
    };

    public string Dica => dados.Estado switch
    {
        EstadoCheck.Falhou => Idioma.T("Falhou. Abre o log no GitHub."),
        EstadoCheck.Pendente => Idioma.T("Rodando ou na fila"),
        EstadoCheck.Pulado => Idioma.T("Pulado"),
        _ => Idioma.T("Passou"),
    };

    [RelayCommand]
    void Abrir()
    {
        if (TemLink) PainelPrVM.AbrirLink(dados.Link);
    }

    [RelayCommand]
    void PedirCorrecao() => pedir(dados);
}
