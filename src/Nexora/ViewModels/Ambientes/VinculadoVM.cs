using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Ambientes;
using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class VinculadoVM(string pasta, Action<VinculadoVM> remover) : ObservableObject
{
    [RelayCommand]
    void Remover() => remover(this);

    public string Pasta { get; } = pasta;
    public string Nome { get; } = Path.GetFileName(pasta.TrimEnd('\\', '/'));
    public bool Existe => Directory.Exists(Pasta);

    [ObservableProperty]
    public partial string? Branch { get; set; }

    public void Atualizar() => Branch = Existe ? Repositorio.BranchAtual(Pasta) ?? Idioma.T("sem git") : Idioma.T("pasta não existe");
}
