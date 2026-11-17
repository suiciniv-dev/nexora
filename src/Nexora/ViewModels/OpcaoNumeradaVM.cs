using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed record OpcaoNumeradaVM(int Numero, OpcaoVM Opcao)
{
    public string Tecla => Numero.ToString(Idioma.Cultura);
}
