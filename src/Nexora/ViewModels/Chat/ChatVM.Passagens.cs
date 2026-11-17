using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Senna;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    public ObservableCollection<PassagemVM> Passagens { get; } = [];
    public ObservableCollection<PassagemVM> TodasPassagens { get; } = [];
    IReadOnlyList<Passagem> _passagensOrdenadas = [];
    public bool TemPassagens => Passagens.Count > 0;
    public bool SemPassagens => _passagensOrdenadas.Count == 0;
    public bool SemResultadoPassagens => _passagensOrdenadas.Count > 0 && TodasPassagens.Count == 0;
    public bool TemMaisPassagens => _passagensOrdenadas.Count > Passagens.Count;
    public string VerMaisPassagens => Idioma.F("Ver mais ({0})", _passagensOrdenadas.Count);
    public static bool SennaDisponivel => PassagensDoSenna.Disponivel && !JanelaVM.Demo;

    [ObservableProperty]
    public partial bool MenuPassagensAberto { get; set; }

    [ObservableProperty]
    public partial string BuscaPassagens { get; set; } = "";

    partial void OnBuscaPassagensChanged(string value) => FiltrarPassagens();

    void FiltrarPassagens()
    {
        TodasPassagens.Clear();
        foreach (var passagem in PassagensDoSenna.Filtrar(_passagensOrdenadas, BuscaPassagens).Take(300))
            TodasPassagens.Add(new PassagemVM(passagem, this));
        OnPropertyChanged(nameof(SemResultadoPassagens));
    }

    void IniciarPassagens()
    {
        if (!JanelaVM.Isolada && !JanelaVM.Demo && SennaDisponivel && Vazia) _ = CarregarPassagensAsync(false);
    }

    async Task CarregarPassagensAsync(bool forcar)
    {
        var todas = await PassagensDoSenna.ListarAsync(forcar);
        var doProjeto = PassagensDoSenna.DoProjeto(todas, Pasta);
        Passagens.Clear();
        foreach (var passagem in doProjeto.Take(5)) Passagens.Add(new PassagemVM(passagem, this));
        _passagensOrdenadas = [.. doProjeto.Concat(todas.Except(doProjeto))];
        FiltrarPassagens();
        OnPropertyChanged(nameof(TemPassagens));
        OnPropertyChanged(nameof(SemPassagens));
        OnPropertyChanged(nameof(TemMaisPassagens));
        OnPropertyChanged(nameof(VerMaisPassagens));
    }

    [RelayCommand]
    void AbrirPassagens()
    {
        BuscaPassagens = "";
        MenuPassagensAberto = true;
        _ = CarregarPassagensAsync(true);
    }

    internal void RetomarPassagem(PassagemVM passagem)
    {
        var pedido = Idioma.F("Retome o handoff \"{0}\".", passagem.Titulo);
        Rascunho = string.IsNullOrWhiteSpace(Rascunho) ? pedido : $"{Rascunho.TrimEnd()}\n{pedido}";
        MenuPassagensAberto = false;
        PedirFoco?.Invoke();
    }
}
