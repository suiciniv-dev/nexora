using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Ambientes;
using Nexora.Core.Git;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class CartaoProjetoVM(JanelaVM janela, AmbienteVM ambiente)
{
    public AmbienteVM Ambiente { get; } = ambiente;
    public string Nome => Ambiente.Nome;
    public string Iniciais => Ambiente.Iniciais;
    public bool Destacado => janela.Destacado(Ambiente);
    public string Branch => Ambiente.BranchCurta;
    public string Vinculados => Ambiente.TemVinculados ? Ambiente.Resumo : Idioma.T("sem projetos vinculados");
    public string Sessoes => janela.SessoesDe(Ambiente) switch
    {
        0 => Idioma.T("nenhuma sessão aberta"),
        1 => Idioma.T("1 sessão aberta"),
        var n => Idioma.F("{0} sessões abertas", n),
    };
    public bool TemSessoes => janela.SessoesDe(Ambiente) > 0;
    public string Ultima => janela.ConversasDe(Ambiente.Principal).FirstOrDefault() is { } c ? $"{c.Titulo} · {c.Quando}" : Idioma.T("sem conversas ainda");

    [RelayCommand]
    void Abrir() => janela.Navegar(Ambiente);

    [RelayCommand]
    void NovaSessao() => janela.AbrirSessao(Ambiente.Local);

    [RelayCommand]
    void Excluir() => janela.PedirExclusao(Ambiente);
}
