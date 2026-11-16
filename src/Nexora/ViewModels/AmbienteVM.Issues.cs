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

public sealed partial class AmbienteVM
{
    DateTime _issuesEm;

    public ObservableCollection<IssueVM> Issues { get; } = [];

    [ObservableProperty]
    public partial bool MostraIssues { get; set; }

    [ObservableProperty]
    public partial string? TextoIssues { get; set; }

    [RelayCommand]
    async Task AlternarIssues()
    {
        MostraIssues = !MostraIssues;
        if (MostraIssues && (Issues.Count == 0 || DateTime.UtcNow - _issuesEm > TimeSpan.FromMinutes(2))) await CarregarIssuesAsync();
    }

    [RelayCommand]
    Task RecarregarIssues() => CarregarIssuesAsync();

    async Task CarregarIssuesAsync()
    {
        TextoIssues = Idioma.T("Buscando as issues abertas no GitHub…");
        var lista = await GitHub.IssuesAsync(Principal);
        _issuesEm = DateTime.UtcNow;
        Issues.Clear();
        if (lista is null)
        {
            TextoIssues = Idioma.T("Não deu para listar as issues. O projeto precisa estar no GitHub e o gh, instalado e logado (gh auth login).");
            return;
        }
        foreach (var issue in lista) Issues.Add(new IssueVM(issue, UsarIssueAsync));
        TextoIssues = lista.Count == 0 ? Idioma.T("Nenhuma issue aberta neste repositório.") : null;
    }

    async Task UsarIssueAsync(IssueVM issue)
    {
        TextoIssues = Idioma.F("Abrindo a {0}…", issue.Numero);
        var detalhe = await GitHub.IssueAsync(Principal, issue.Dados.Numero);
        var titulo = detalhe?.Titulo ?? issue.Titulo;
        var corpo = detalhe?.Corpo is { Length: > 0 } c ? $"\n\n{c.Trim()}" : "";
        var url = detalhe?.Url ?? issue.Dados.Url;
        Rascunho = Idioma.F("Resolva a issue {0}: {1}{2}\n\n{3}", issue.Numero, titulo, corpo, url);
        NomeTarefa = $"issue {issue.Dados.Numero} {titulo}";
        TextoIssues = null;
        MostraIssues = false;
    }
}
