using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public static class TemplatesDoApp
{
    public static IReadOnlyList<TemplateDeTarefa> Todos
    {
        get
        {
            if (App.Config.Templates is not { Count: > 0 } salvos) return Templates.Padroes();
            var vistos = App.Config.TemplatesPadraoVistos;
            var novos = Templates.Padroes().Where(p => salvos.All(s => s.Id != p.Id)
                && (vistos is null ? p.Id.StartsWith("fluxo-", StringComparison.Ordinal) : !vistos.Contains(p.Id)));
            return [.. salvos, .. novos];
        }
    }

    public static TemplateDeTarefa? DaIssue => Todos.FirstOrDefault(t => t.Id == App.Config.TemplateDaIssue) ?? Todos.FirstOrDefault();

    public static event Action? PedirEditor;

    public static void AbrirEditor() => PedirEditor?.Invoke();

    public static void Salvar(IEnumerable<TemplateDeTarefa> lista, string? daIssue)
    {
        App.Config.Templates = [.. lista];
        App.Config.TemplatesPadraoVistos = [.. Templates.Padroes().Select(p => p.Id)];
        if (daIssue is not null) App.Config.TemplateDaIssue = daIssue;
        if (!JanelaVM.Isolada) App.Config.Salvar();
    }
}

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

public sealed partial class TemplatesVM : ObservableObject
{
    public ObservableCollection<TemplateItemVM> Itens { get; } = [];

    [ObservableProperty]
    public partial bool Aberto { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemSelecionado))]
    [NotifyCanExecuteChangedFor(nameof(DuplicarCommand), nameof(ExcluirCommand), nameof(UsarNasIssuesCommand))]
    public partial TemplateItemVM? Selecionado { get; set; }

    public bool TemSelecionado => Selecionado is not null;

    public string Variaveis => string.Join("  ", Templates.Variaveis.Select(v => "{" + v + "}"));

    public void Abrir()
    {
        Carregar(TemplatesDoApp.Todos, App.Config.TemplateDaIssue);
        Aberto = true;
    }

    void Carregar(IEnumerable<TemplateDeTarefa> lista, string daIssue)
    {
        Itens.Clear();
        foreach (var t in lista) Itens.Add(new TemplateItemVM(t.Copia()) { DaIssue = t.Id == daIssue });
        if (Itens.Count > 0 && Itens.All(i => !i.DaIssue)) Itens[0].DaIssue = true;
        Selecionado = Itens.FirstOrDefault();
    }

    [RelayCommand]
    void Novo()
    {
        var item = new TemplateItemVM(new TemplateDeTarefa { Id = Guid.NewGuid().ToString("N")[..10], Nome = Idioma.T("Novo template"), Texto = "{saudacao} claude, " });
        Itens.Add(item);
        Selecionado = item;
    }

    [RelayCommand(CanExecute = nameof(TemSelecionado))]
    void Duplicar()
    {
        if (Selecionado is not { } atual) return;
        var copia = new TemplateItemVM(new TemplateDeTarefa { Id = Guid.NewGuid().ToString("N")[..10], Nome = Idioma.F("{0} (cópia)", atual.Nome), Texto = atual.Texto });
        Itens.Insert(Itens.IndexOf(atual) + 1, copia);
        Selecionado = copia;
    }

    [RelayCommand(CanExecute = nameof(TemSelecionado))]
    void Excluir()
    {
        if (Selecionado is not { } atual) return;
        var i = Itens.IndexOf(atual);
        Itens.Remove(atual);
        if (atual.DaIssue && Itens.Count > 0) Itens[0].DaIssue = true;
        Selecionado = Itens.Count == 0 ? null : Itens[Math.Min(i, Itens.Count - 1)];
    }

    [RelayCommand(CanExecute = nameof(TemSelecionado))]
    void UsarNasIssues()
    {
        foreach (var item in Itens) item.DaIssue = item == Selecionado;
    }

    [RelayCommand]
    void RestaurarPadroes() => Carregar(Templates.Padroes(), Templates.IdIssue);

    [RelayCommand]
    void Salvar()
    {
        TemplatesDoApp.Salvar(Itens.Select(i => i.ParaSalvar()), Itens.FirstOrDefault(i => i.DaIssue)?.Id ?? Templates.IdIssue);
        Aberto = false;
    }

    [RelayCommand]
    void Cancelar() => Aberto = false;
}
