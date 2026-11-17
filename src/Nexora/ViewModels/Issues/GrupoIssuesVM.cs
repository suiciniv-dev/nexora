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

public sealed partial class GrupoIssuesVM(string nome, string? cor, IReadOnlyList<IssueLinhaVM> linhas, string? opcaoId = null) : ObservableObject
{
    public string Nome { get; } = nome;
    public string? OpcaoId { get; } = opcaoId;

    [ObservableProperty]
    public partial bool Alvo { get; set; }

    public IBrush Cor { get; } = ChipVM.De(nome, cor).Ponto;
    public IReadOnlyList<IssueLinhaVM> Linhas { get; } = linhas;
    public string Contagem => Linhas.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Seta))]
    public partial bool Aberto { get; set; } = true;

    public string Seta => Aberto ? "abaixo" : "direita";

    [RelayCommand]
    void Alternar() => Aberto = !Aberto;
}
