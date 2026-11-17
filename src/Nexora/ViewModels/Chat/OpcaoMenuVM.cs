using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class OpcaoMenuVM(string nome, string? descricao, bool selecionada, Action escolher) : ObservableObject
{
    public string Nome { get; } = nome;
    public string? Descricao { get; } = descricao;
    public bool TemDescricao => !string.IsNullOrEmpty(Descricao);
    public bool Selecionada { get; } = selecionada;

    [RelayCommand]
    void Escolher() => escolher();
}
