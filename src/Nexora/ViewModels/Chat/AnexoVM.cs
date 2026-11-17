using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class AnexoVM(Anexo dados, Avalonia.Media.Imaging.Bitmap miniatura, Action<AnexoVM> remover) : ObservableObject
{
    public Anexo Dados { get; } = dados;
    public Avalonia.Media.Imaging.Bitmap Miniatura { get; } = miniatura;
    public string Nome => Dados.Nome;

    [RelayCommand]
    void Remover() => remover(this);
}
