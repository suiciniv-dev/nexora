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
