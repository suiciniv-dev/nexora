using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class OpcaoTemplateVM(TemplateDeTarefa modelo, ChatVM chat)
{
    public TemplateDeTarefa Modelo { get; } = modelo;
    public string Nome => Modelo.Nome;
    public string Previa => TemplateItemVM.Resumo(Modelo.Texto);

    [RelayCommand]
    void Escolher() => _ = chat.AplicarTemplateAsync(Modelo);
}
