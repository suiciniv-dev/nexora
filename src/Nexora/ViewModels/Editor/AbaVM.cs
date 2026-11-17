using System.Collections.ObjectModel;
using System.Text;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Nexora.Core.Revisao;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public abstract partial class AbaVM : ObservableObject
{
    protected AbaVM(string titulo, string dica, Action<AbaVM> fechar, Action<AbaVM> selecionar)
    {
        Titulo = titulo;
        Dica = dica;
        _fechar = fechar;
        _selecionar = selecionar;
    }

    readonly Action<AbaVM> _fechar;
    readonly Action<AbaVM> _selecionar;

    public string Titulo { get; }
    public string Dica { get; }

    [ObservableProperty]
    public partial bool Ativa { get; set; }

    public virtual bool Modificada => false;

    [RelayCommand]
    void Fechar() => _fechar(this);

    [RelayCommand]
    void Selecionar() => _selecionar(this);

    public virtual void Descartar()
    {
    }
}
