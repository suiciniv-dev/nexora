using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class FileiraVM(string titulo, string estado, Func<CartaoVM, bool> pertence) : ObservableObject
{
    public string Titulo { get; } = titulo;
    public string Estado { get; } = estado;
    public bool Precisa => Estado == "precisa";
    public bool Trabalhando => Estado == "trabalhando";
    public bool Pronta => Estado == "pronta";
    public ObservableCollection<CartaoVM> Cartoes { get; } = [];
    public CartaoVM? Focado { get; private set; }

    [ObservableProperty]
    public partial int Foco { get; set; }

    [ObservableProperty]
    public partial bool Ativa { get; set; }

    public bool Atualizar(IReadOnlyList<CartaoVM> sessoes)
    {
        VisaoGeralVM.Encher(Cartoes, [.. sessoes.Where(pertence)], false);
        var onde = Focado is null ? -1 : Cartoes.IndexOf(Focado);
        Ir(onde >= 0 ? onde : Foco);
        return Cartoes.Count > 0;
    }

    public bool Ir(int indice)
    {
        var antes = Focado;
        Foco = Cartoes.Count == 0 ? 0 : Math.Clamp(indice, 0, Cartoes.Count - 1);
        Focado = Cartoes.ElementAtOrDefault(Foco);
        return !ReferenceEquals(antes, Focado);
    }
}
