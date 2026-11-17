using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class AtalhoVM(string icone, string nome, Action abrir, Func<object?, bool>? pagina = null) : ObservableObject
{
    public string Icone { get; } = icone;
    public string Nome { get; } = nome;
    public void Abrir() => abrir();
    public bool DaPagina(object? conteudo) => pagina?.Invoke(conteudo) ?? false;

    [ObservableProperty]
    public partial bool Ativo { get; set; }
}
