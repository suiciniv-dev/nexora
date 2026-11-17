using System.Windows.Input;
using Nexora.Core.Texto;
using Nexora.ViewModels;

namespace Nexora.Aplicativo;

public sealed class ComandoAdiado(Func<ICommand?> alvo) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parametro) => alvo()?.CanExecute(parametro) == true;

    public void Execute(object? parametro)
    {
        if (alvo() is { } comando && comando.CanExecute(parametro)) comando.Execute(parametro);
    }
}
