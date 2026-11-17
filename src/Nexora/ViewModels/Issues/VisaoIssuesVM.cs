using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using System.Text.RegularExpressions;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class VisaoIssuesVM(VisaoDoQuadro dados, Action<VisaoIssuesVM> escolher) : ObservableObject
{
    public VisaoDoQuadro Dados { get; } = dados;
    public string Nome => Dados.Nome;
    public string Dica => Dados.Filtro.Length > 0 ? Dados.Filtro : Idioma.T("sem filtro");

    [ObservableProperty]
    public partial bool Escolhida { get; set; }

    [RelayCommand]
    void Escolher() => escolher(this);
}
