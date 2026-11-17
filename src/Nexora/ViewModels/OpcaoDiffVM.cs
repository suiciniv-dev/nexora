using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Arquivos;
using Nexora.Core.Git;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed class OpcaoDiffVM(string rotulo, ItemTurno? turno)
{
    public string Rotulo { get; } = rotulo;
    public ItemTurno? Turno { get; } = turno;
}
