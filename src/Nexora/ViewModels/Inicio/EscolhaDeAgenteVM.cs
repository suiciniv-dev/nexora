using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Provedores;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class EscolhaDeAgenteVM : ObservableObject
{
    readonly Action<string> _concluir;
    readonly List<CartaoAgenteVM> _todos;

    public EscolhaDeAgenteVM(Action<string> concluir)
    {
        _concluir = concluir;
        _todos = [.. CatalogoAgentes.Todos.Select(a => new CartaoAgenteVM(a, this))];
        Procurar();
    }

    public ObservableCollection<CartaoAgenteVM> Cartoes { get; } = [];

    [ObservableProperty]
    public partial string Outros { get; set; } = "";

    public bool TemOutros => Outros.Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAviso), nameof(Aviso))]
    public partial CartaoAgenteVM? Escolhido { get; set; }

    public bool TemAviso => Escolhido is { Detectado: false };
    public string Aviso => Escolhido is { Detectado: false } c ? Idioma.F("O {0} não está neste computador. Instale pelo terminal com: {1}", c.Nome, c.Instalar) : "";

    public void Escolher(CartaoAgenteVM cartao)
    {
        foreach (var c in Cartoes) c.Escolhido = c == cartao;
        Escolhido = cartao;
    }

    [RelayCommand]
    public void Procurar()
    {
        ChatVM.EsquecerInstalacoes();
        var instalados = ChatVM.AgentesInstalados.Select(a => a.Id).ToHashSet();
        foreach (var c in _todos) c.Detectado = instalados.Contains(c.Id);
        Cartoes.Clear();
        foreach (var c in _todos.Where(c => c.EhClaude || c.EhCodex || c.Detectado)) Cartoes.Add(c);
        var faltam = _todos.Where(c => !Cartoes.Contains(c)).Select(c => c.Nome).ToList();
        Outros = faltam.Count == 0 ? "" : Idioma.F("Também funcionam: {0}. Instale um deles e clique em Procurar de novo.", string.Join(", ", faltam));
        OnPropertyChanged(nameof(TemOutros));
        var atual = (Escolhido is { } e && Cartoes.Contains(e) ? e : null)
            ?? Cartoes.FirstOrDefault(c => c.Id == Agentes.Normalizar(App.Config.AgentePadrao) && c.Detectado)
            ?? Cartoes.FirstOrDefault(c => c.Detectado) ?? Cartoes[0];
        Escolher(atual);
        OnPropertyChanged(nameof(TemAviso));
        OnPropertyChanged(nameof(Aviso));
    }

    [RelayCommand]
    void Continuar() => _concluir(Escolhido?.Id ?? Agentes.Claude);
}
