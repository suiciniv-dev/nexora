using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    IReadOnlyList<string> _branches = [];

    public ObservableCollection<OpcaoMenuVM> OpcoesBranch { get; } = [];

    [ObservableProperty]
    public partial bool MenuBranchAberto { get; set; }

    [ObservableProperty]
    public partial string FiltroBranch { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAvisoBranch))]
    public partial string? AvisoBranch { get; set; }

    [ObservableProperty]
    public partial bool TrocandoBranch { get; set; }

    public bool TemAvisoBranch => !string.IsNullOrEmpty(AvisoBranch);

    [RelayCommand]
    async Task AbrirMenuBranch()
    {
        AvisoBranch = null;
        FiltroBranch = "";
        MenuBranchAberto = true;
        _branches = await Repositorio.BranchesAsync(Pasta);
        MontarOpcoesBranch();
    }

    partial void OnFiltroBranchChanged(string value) => MontarOpcoesBranch();

    void MontarOpcoesBranch()
    {
        OpcoesBranch.Clear();
        var filtro = FiltroBranch.Trim();
        foreach (var branch in _branches.Where(b => filtro.Length == 0 || b.Contains(filtro, StringComparison.OrdinalIgnoreCase)).Take(60))
            OpcoesBranch.Add(new OpcaoMenuVM(branch, branch == Branch ? Idioma.T("a branch atual") : null, branch == Branch, () => _ = TrocarBranchAsync(branch)));
        if (OpcoesBranch.Count == 0 && _branches.Count > 0) AvisoBranch = Idioma.T("Nenhuma branch com esse nome.");
        else if (AvisoBranch == Idioma.T("Nenhuma branch com esse nome.")) AvisoBranch = null;
    }

    public async Task TrocarBranchAsync(string branch)
    {
        if (branch == Branch)
        {
            MenuBranchAberto = false;
            return;
        }
        if (Trabalhando || Precisa || _anfitriao.Ocupada(Pasta))
        {
            AvisoBranch = Idioma.T("Tem uma sessão trabalhando nesta pasta. Espere ela terminar para trocar de branch.");
            return;
        }
        TrocandoBranch = true;
        AvisoBranch = null;
        var resultado = await Repositorio.TrocarAsync(Pasta, branch);
        TrocandoBranch = false;
        if (!resultado.Ok)
        {
            AvisoBranch = resultado.Mensagem;
            return;
        }
        MenuBranchAberto = false;
        AtualizarBranch();
        if (Itens.Count > 0) Adicionar(ItemAviso.Troca(Idioma.F("Agora na branch {0}", branch)));
    }
}
