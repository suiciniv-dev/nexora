using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Ambientes;
using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class WorktreeVM(Worktree dados, bool atual, Action<WorktreeVM> abrir, Func<WorktreeVM, Task> remover) : ObservableObject
{
    public string Caminho => dados.Caminho;
    public string Nome => Path.GetFileName(dados.Caminho.TrimEnd('\\', '/'));
    public string Branch => dados.Branch ?? Idioma.T("(sem branch)");
    public string? BranchReal => dados.Branch;
    public bool Principal => dados.Principal;
    public bool Removivel => !dados.Principal;
    public bool Atual => atual;

    [ObservableProperty]
    public partial bool Confirmando { get; set; }

    [ObservableProperty]
    public partial bool ApagarBranch { get; set; } = true;

    [ObservableProperty]
    public partial bool Removendo { get; set; }

    public string TextoConfirmar => Idioma.F("Remover a pasta {0}? O trabalho com commit continua na branch.", Nome);
    public string TextoApagarBranch => Idioma.F("Apagar também a branch {0} (só se já estiver mergeada)", Branch);

    [RelayCommand]
    void NovaSessao() => abrir(this);

    [RelayCommand]
    void PedirRemocao() => Confirmando = true;

    [RelayCommand]
    void CancelarRemocao() => Confirmando = false;

    [RelayCommand]
    async Task Remover()
    {
        Removendo = true;
        await remover(this);
        Removendo = false;
        Confirmando = false;
    }
}
