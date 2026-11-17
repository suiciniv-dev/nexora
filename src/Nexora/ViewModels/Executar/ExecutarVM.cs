using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Executar;
using Nexora.Core.Git;
using Nexora.Core.Nuvem;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ExecutarVM : ObservableObject
{
    public ExecutarVM(Local local, Action<string>? abrirNoNexora = null)
    {
        Local = local;
        AbrirNoNexora = abrirNoNexora;
        App.Nuvem.Mudou += () => Dispatcher.UIThread.Post(CarregarOndeRoda);
        _ = Recarregar();
    }

    public Action<string>? AbrirNoNexora { get; }
    public ObservableCollection<OndeExecutaVM> OpcoesOndeRoda { get; } = [];
    public bool MostraOndeRoda => OpcoesOndeRoda.Count > 1;
    public string ProjetoDaNuvem => Repositorio.PrincipalDoWorktree(Local.Pasta) ?? Local.Pasta;

    [ObservableProperty]
    public partial OndeExecutaVM? OndeRoda { get; set; }

    public MaquinaNaNuvem? MaquinaEscolhida => OndeRoda?.MaquinaId is { } id ? App.Nuvem.Maquina(id) : null;

    public void CarregarOndeRoda()
    {
        var antes = OndeRoda?.MaquinaId;
        OpcoesOndeRoda.Clear();
        OpcoesOndeRoda.Add(new OndeExecutaVM(Idioma.T("Neste PC"), null));
        if (ChatVM.PodeIrParaNuvem(ProjetoDaNuvem))
            foreach (var maquina in ChatVM.MaquinasProntas)
                OpcoesOndeRoda.Add(new OndeExecutaVM(Idioma.F("Nuvem · {0}", maquina.Nome), maquina.Id));
        OndeRoda = OpcoesOndeRoda.FirstOrDefault(o => o.MaquinaId == antes) ?? OpcoesOndeRoda[0];
        OnPropertyChanged(nameof(MostraOndeRoda));
    }

    public Local Local { get; }
    public string Titulo => Idioma.F("Executar · {0}", Local.Nome);
    public ObservableCollection<AlvoVM> Alvos { get; } = [];
    public bool Vazio => !Carregando && Alvos.Count == 0;
    public int Rodando => Alvos.Count(a => a.Ativo);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Vazio))]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemSelecionado))]
    public partial AlvoVM? Selecionado { get; set; }

    public bool TemSelecionado => Selecionado is not null;

    [RelayCommand]
    async Task Recarregar()
    {
        Carregando = true;
        CarregarOndeRoda();
        var raizes = new[] { Local.Pasta }.Concat(Local.Vinculados).ToList();
        var achados = await Task.Run(() => Core.Executar.Alvos.Descobrir(raizes));
        var antigos = Alvos.ToDictionary(a => a.Dados.Chave);
        Alvos.Clear();
        foreach (var dados in achados)
        {
            if (antigos.Remove(dados.Chave, out var existente)) existente.Dados = dados;
            else existente = new AlvoVM(dados, NomeDaRaiz(dados.Pasta, raizes), () => OnPropertyChanged(nameof(Rodando)), this);
            Alvos.Add(existente);
        }
        foreach (var ativo in antigos.Values.Where(a => a.Ativo)) Alvos.Add(ativo);
        Selecionado = Selecionado is { } s && Alvos.Contains(s) ? s : Alvos.FirstOrDefault();
        Carregando = false;
    }

    static string NomeDaRaiz(string pasta, IReadOnlyList<string> raizes) =>
        Path.GetFileName((raizes.FirstOrDefault(r => pasta.StartsWith(r, StringComparison.OrdinalIgnoreCase)) ?? pasta).TrimEnd('\\', '/'));

    public void PararTudo()
    {
        foreach (var alvo in Alvos) alvo.PararCommand.Execute(null);
    }
}
