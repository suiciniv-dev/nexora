using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class ItemSubagentes : ItemChat
{
    public ObservableCollection<ItemSubagente> Agentes { get; } = [];

    public void Adicionar(ItemSubagente agente)
    {
        Agentes.Add(agente);
        agente.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ItemSubagente.Estado)) Avisar();
        };
        Avisar();
    }

    public bool Varios => Agentes.Count > 1;
    public bool AlgumRodando => Agentes.Any(a => a.Rodando);
    public string Titulo => Idioma.F("{0} subagents em paralelo", Agentes.Count);

    public string Resumo
    {
        get
        {
            var rodando = Agentes.Count(a => a.Rodando);
            var prontos = Agentes.Count(a => a.Ok);
            var erros = Agentes.Count(a => a.Falhou);
            var parados = Agentes.Count(a => a.Parou);
            return string.Join("  ·  ", new[]
            {
                rodando > 0 ? Idioma.F("{0} rodando", rodando) : null,
                prontos switch { 0 => null, 1 => Idioma.T("1 terminou"), _ => Idioma.F("{0} terminaram", prontos) },
                erros switch { 0 => null, 1 => Idioma.T("1 deu erro"), _ => Idioma.F("{0} deram erro", erros) },
                parados switch { 0 => null, 1 => Idioma.T("1 parou"), _ => Idioma.F("{0} pararam", parados) },
            }.OfType<string>());
        }
    }

    void Avisar()
    {
        OnPropertyChanged(nameof(Varios));
        OnPropertyChanged(nameof(AlgumRodando));
        OnPropertyChanged(nameof(Titulo));
        OnPropertyChanged(nameof(Resumo));
    }
}
