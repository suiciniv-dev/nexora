using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class GrupoProjetoVM : ObservableObject
{
    readonly JanelaVM _janela;

    public GrupoProjetoVM(JanelaVM janela, AmbienteVM? ambiente, string nome)
    {
        _janela = janela;
        Ambiente = ambiente;
        Nome = nome;
        Expandido = !App.Config.ProjetosFechados.Contains(nome);
    }

    public AmbienteVM? Ambiente { get; }
    public string Nome { get; }
    public string? Branch => Ambiente?.Branch;
    public ObservableCollection<ChatVM> Sessoes { get; } = [];
    public bool TemSessoes => Sessoes.Count > 0;
    public string Contagem => Sessoes.Count == 0 ? "" : Sessoes.Count.ToString();

    [ObservableProperty]
    public partial bool Expandido { get; set; }

    partial void OnExpandidoChanged(bool value)
    {
        App.Config.ProjetosFechados.Remove(Nome);
        if (!value) App.Config.ProjetosFechados.Add(Nome);
        if (!JanelaVM.Isolada) App.Config.Salvar();
    }

    public ObservableCollection<RamoVM> Ramos { get; } = [];
    public ChatVM? Primeira => Sessoes.Count > 0 ? Sessoes[0] : null;
    public string Iniciais => Siglas.De(Nome);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemParadas), nameof(TextoParadas))]
    public partial int Paradas { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoParadas))]
    public partial bool MostrarParadas { get; set; }

    public bool TemParadas => Paradas > 0;

    public string TextoParadas => MostrarParadas ? Idioma.T("Esconder as paradas")
        : Paradas == 1 ? Idioma.T("1 parada há mais de 1 h") : Idioma.F("{0} paradas há mais de 1 h", Paradas);

    string _assinatura = "";

    partial void OnMostrarParadasChanged(bool value) => Arrumar();

    [RelayCommand]
    void AlternarParadas() => MostrarParadas = !MostrarParadas;

    public void Avisar()
    {
        OnPropertyChanged(nameof(TemSessoes));
        OnPropertyChanged(nameof(Contagem));
        OnPropertyChanged(nameof(Branch));
        OnPropertyChanged(nameof(Primeira));
        Arrumar();
    }

    public void Arrumar()
    {
        var chaves = Sessoes.Select(c => c.Branch ?? "").Distinct().ToList();
        var principal = Ambiente?.Principal.TrimEnd('\\', '/');
        var cabecalhos = chaves.Count > 1 || chaves.Count == 1 && chaves[0].Length > 0 && Ambiente is not null && chaves[0] != Ambiente.Branch;
        var assinatura = string.Join("|", Sessoes.Select(c => $"{c.Branch}:{c.Codigo}")) + cabecalhos;
        if (assinatura != _assinatura)
        {
            _assinatura = assinatura;
            Ramos.Clear();
            foreach (var chave in chaves)
            {
                var dele = Sessoes.Where(c => (c.Branch ?? "") == chave).ToList();
                var worktree = principal is not null && dele.Any(c => !Core.Ambientes.Workspaces.MesmaPasta(c.Pasta.TrimEnd('\\', '/'), principal));
                var ramo = new RamoVM(chave.Length > 0 ? chave : null, worktree, cabecalhos && chave.Length > 0);
                foreach (var c in dele) ramo.Sessoes.Add(c);
                Ramos.Add(ramo);
            }
        }
        var paradas = 0;
        foreach (var c in Sessoes)
        {
            var parada = c.ParadaHaTempo;
            if (parada) paradas++;
            c.RecolhidaNaLateral = parada && !MostrarParadas;
        }
        Paradas = paradas;
        if (paradas == 0) MostrarParadas = false;
    }

    [RelayCommand]
    void Alternar() => Expandido = !Expandido;

    [RelayCommand]
    void Abrir()
    {
        if (Ambiente is not null) _janela.Navegar(Ambiente);
    }

    [RelayCommand]
    void NovaSessao()
    {
        if (Ambiente is not null) _janela.AbrirSessao(Ambiente.Local);
    }
}
