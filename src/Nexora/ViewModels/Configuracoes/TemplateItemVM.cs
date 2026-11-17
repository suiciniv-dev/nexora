using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class TemplateItemVM(TemplateDeTarefa modelo) : ObservableObject
{
    public string Id { get; } = modelo.Id;

    [ObservableProperty]
    public partial string Nome { get; set; } = modelo.Nome;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Previa))]
    public partial string Texto { get; set; } = modelo.Texto;

    [ObservableProperty]
    public partial bool DaIssue { get; set; }

    public string Previa => Resumo(Texto);

    public static string Resumo(string texto) => texto.Replace("\r", "").Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? "";

    public TemplateDeTarefa ParaSalvar() => new() { Id = Id, Nome = Nome.Trim().Length > 0 ? Nome.Trim() : Idioma.T("Sem nome"), Texto = Texto };
}
