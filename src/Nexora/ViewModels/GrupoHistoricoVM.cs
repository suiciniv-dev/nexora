using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class GrupoHistoricoVM(string nome, IReadOnlyList<ConversaVM> todas) : ObservableObject
{
    const int Curto = 5;

    public string Nome { get; } = nome;
    public IReadOnlyList<ConversaVM> Todas { get; } = todas;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Visiveis), nameof(TemMais), nameof(TextoMais))]
    public partial bool Completo { get; set; } = App.Config.ConversasCompletas.Contains(nome);

    [ObservableProperty]
    public partial bool Expandido { get; set; } = !App.Config.ConversasFechadas.Contains(nome);

    partial void OnCompletoChanged(bool value) => Lembrar(App.Config.ConversasCompletas, value);

    partial void OnExpandidoChanged(bool value) => Lembrar(App.Config.ConversasFechadas, !value);

    void Lembrar(List<string> lista, bool presente)
    {
        var mudou = presente ? !lista.Contains(Nome) : lista.Remove(Nome);
        if (presente && mudou) lista.Add(Nome);
        if (mudou && !JanelaVM.Isolada) App.Config.Salvar();
    }

    public IReadOnlyList<ConversaVM> Visiveis => Completo ? Todas : [.. Todas.Take(Curto)];
    public bool TemMais => Todas.Count > Curto;
    public string TextoMais => Completo ? Idioma.T("Mostrar menos") : Idioma.F("Mostrar mais {0}", Todas.Count - Curto);

    [RelayCommand]
    void AlternarMais() => Completo = !Completo;

    [RelayCommand]
    void Alternar() => Expandido = !Expandido;
}
