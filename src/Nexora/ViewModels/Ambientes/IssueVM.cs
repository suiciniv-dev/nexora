using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class IssueVM(IssueGh dados, Func<IssueVM, Task> usar)
{
    public IssueGh Dados { get; } = dados;
    public string Numero => $"#{Dados.Numero}";
    public string Titulo => Dados.Titulo;
    public string Etiquetas => string.Join("  ·  ", Dados.Etiquetas);
    public bool TemEtiquetas => Dados.Etiquetas.Count > 0;
    public string Quando => Dados.Atualizada is { } q ? Core.Texto.Tempo.Relativo(q.LocalDateTime) : "";

    [RelayCommand]
    Task Usar() => usar(this);
}
