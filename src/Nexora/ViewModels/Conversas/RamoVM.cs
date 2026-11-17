using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed class RamoVM(string? nome, bool worktree, bool cabecalho)
{
    public string? Nome { get; } = nome;
    public bool Worktree { get; } = worktree;
    public bool Branch => !Worktree;
    public bool Cabecalho { get; } = cabecalho;
    public ObservableCollection<ChatVM> Sessoes { get; } = [];
}
