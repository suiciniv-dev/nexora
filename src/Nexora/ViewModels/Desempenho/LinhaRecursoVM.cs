using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class LinhaRecursoVM(string nome, string detalhe, long memoria, bool ativo, Func<Task>? parar) : ObservableObject
{
    public string Nome { get; } = nome;
    public string Detalhe { get; } = detalhe;
    public long Memoria { get; } = memoria;
    public bool Ativo { get; } = ativo;
    public string TextoMemoria => Ativo ? RecursosVM.Formatar(Memoria) : "parado";
    public bool PodeParar => parar is not null && Ativo;

    [RelayCommand]
    async Task Parar()
    {
        if (parar is not null) await parar();
    }
}
