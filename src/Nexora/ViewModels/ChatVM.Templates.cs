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

public sealed partial class ChatVM
{
    public ObservableCollection<OpcaoTemplateVM> OpcoesTemplates { get; } = [];

    [ObservableProperty]
    public partial bool MenuTemplatesAberto { get; set; }

    public event Action<int>? PedirCursor;

    string? _corpoDaIssue;
    string? _ultimoTemplate;

    [RelayCommand]
    void AbrirTemplates()
    {
        OpcoesTemplates.Clear();
        foreach (var t in TemplatesDoApp.Todos) OpcoesTemplates.Add(new OpcaoTemplateVM(t, this));
        MenuTemplatesAberto = true;
    }

    [RelayCommand]
    void EditarTemplates()
    {
        MenuTemplatesAberto = false;
        TemplatesDoApp.AbrirEditor();
    }

    public void ComecarComTemplate(string pedido, string? corpoDaIssue)
    {
        _corpoDaIssue = corpoDaIssue;
        Rascunho = _ultimoTemplate = pedido;
    }

    public async Task AplicarTemplateAsync(TemplateDeTarefa modelo)
    {
        MenuTemplatesAberto = false;
        var corpo = _corpoDaIssue;
        if (corpo is null && Issue is { Numero: > 0 } issue && !JanelaVM.Demo && modelo.Texto.Contains("{corpo}", StringComparison.Ordinal))
        {
            var detalhe = await Core.Git.GitHub.IssueAsync(Pasta, issue.Numero, issue.Repositorio, issue.EhPr);
            corpo = _corpoDaIssue = detalhe?.Corpo;
        }
        var digitado = Rascunho?.Trim();
        var valores = ValoresDoTemplate(corpo is not null ? IssuesVM.TopicosDoCorpo(corpo) : null);
        if (string.IsNullOrWhiteSpace(valores["corpo"]) && digitado is { Length: > 0 } && digitado != _ultimoTemplate?.Trim())
            valores["corpo"] = digitado;
        var (texto, cursor) = Templates.Preencher(modelo.Texto, valores);
        Rascunho = _ultimoTemplate = texto;
        PedirFoco?.Invoke();
        PedirCursor?.Invoke(cursor);
    }

    Dictionary<string, string?> ValoresDoTemplate(string? corpo) => new()
    {
        ["saudacao"] = Templates.Saudacao(DateTime.Now),
        ["tipo"] = Issue is { EhPr: true } ? "PR" : "issue",
        ["url"] = Issue?.Url,
        ["numero"] = Issue is { Numero: > 0 } i ? i.Numero.ToString(Idioma.Cultura) : null,
        ["titulo"] = Issue?.Titulo,
        ["corpo"] = corpo,
        ["branch"] = Branch,
        ["handoff"] = Templates.Handoff(Branch),
        ["projeto"] = NomeProjeto,
    };
}
