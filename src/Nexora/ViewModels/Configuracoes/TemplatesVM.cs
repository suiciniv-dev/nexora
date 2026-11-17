using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

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
