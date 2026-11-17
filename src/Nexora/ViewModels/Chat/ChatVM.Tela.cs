using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Historico;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    public const int MensagensNaTela = 40;
    public const int TurnosPorVez = 12;
    public const int LoteInicial = 12;
    public const int FolgaDaTela = 20;

    int _alvo = MensagensNaTela;
    bool _telaSuspensa;
    bool _inserindoAntigos;
    string? _arquivoHistorico;
    long _inicioNoArquivo;

    public ObservableCollection<ItemChat> ItensNaTela { get; } = [];

    public bool NoFim { get; set; } = true;

    public event Action? MontagemPendente;

    public int Escondidas => Math.Max(0, Itens.Count - Math.Max(_alvo, ItensNaTela.Count));
    public bool TemAnterioresNoDisco => _inicioNoArquivo > 0;
    public bool TemEscondidas => Escondidas > 0 || TemAnterioresNoDisco;
    public int Pendentes => Math.Max(0, Math.Min(_alvo, Itens.Count) - ItensNaTela.Count);
    public bool Montando => Pendentes > 0;

    public bool CarregandoAnteriores
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
            AvisarEscondidas();
        }
    }

    public string TextoEscondidas => CarregandoAnteriores ? Idioma.T("Carregando turnos anteriores…")
        : Escondidas == 0 ? Idioma.T("Carregar turnos anteriores")
        : Escondidas == 1 ? Idioma.T("Mostrar 1 mensagem anterior")
        : Idioma.F("Mostrar {0} mensagens anteriores", Math.Min(MensagensNaTela, Escondidas));

    void SincronizarTela(NotifyCollectionChangedEventArgs e)
    {
        if (_telaSuspensa) return;
        switch (e)
        {
            case { Action: NotifyCollectionChangedAction.Add, NewItems: { } novos }:
                var antes = Itens.Count - novos.Count;
                var inicio = antes - ItensNaTela.Count;
                if (!_inserindoAntigos && e.NewStartingIndex >= inicio)
                {
                    var lugar = e.NewStartingIndex - inicio;
                    foreach (var novo in novos.OfType<ItemChat>()) ItensNaTela.Insert(lugar++, novo);
                    _alvo += novos.Count;
                }
                break;
            case { Action: NotifyCollectionChangedAction.Remove, OldItems: { } velhos }:
                var inicioAntes = Itens.Count + velhos.Count - ItensNaTela.Count;
                if (e.OldStartingIndex >= inicioAntes)
                    for (var i = 0; i < velhos.Count; i++) ItensNaTela.RemoveAt(e.OldStartingIndex - inicioAntes);
                _alvo = Math.Max(Math.Min(_alvo, Itens.Count), ItensNaTela.Count);
                break;
            case { Action: NotifyCollectionChangedAction.Replace, NewItems: [ItemChat trocado] }:
                var posicao = e.NewStartingIndex - (Itens.Count - ItensNaTela.Count);
                if (posicao >= 0 && posicao < ItensNaTela.Count) ItensNaTela[posicao] = trocado;
                break;
            default:
                RefazerTela();
                return;
        }
        if (NoFim && ItensNaTela.Count > MensagensNaTela + FolgaDaTela) DeixarSoAsUltimas();
        AvisarEscondidas();
    }

    void RefazerTela()
    {
        ItensNaTela.Clear();
        _alvo = MensagensNaTela;
        foreach (var item in Itens.Skip(Math.Max(0, Itens.Count - LoteInicial))) ItensNaTela.Add(item);
        AvisarEscondidas();
        PedirMontagem();
    }

    public bool MontarMais(int quantos)
    {
        var fim = Itens.Count - ItensNaTela.Count;
        var parar = Math.Max(0, Itens.Count - Math.Min(_alvo, Itens.Count));
        for (var i = fim - 1; i >= parar && quantos > 0; i--, quantos--) ItensNaTela.Insert(0, Itens[i]);
        return Montando;
    }

    void PedirMontagem()
    {
        if (Montando) MontagemPendente?.Invoke();
    }

    void DeixarSoAsUltimas()
    {
        while (ItensNaTela.Count > MensagensNaTela) ItensNaTela.RemoveAt(0);
        _alvo = MensagensNaTela;
    }

    public void ChegouAoFim()
    {
        NoFim = true;
        if (ItensNaTela.Count <= MensagensNaTela + FolgaDaTela) return;
        DeixarSoAsUltimas();
        AvisarEscondidas();
    }

    [RelayCommand]
    async Task MostrarAnterioresAsync()
    {
        if (Montando || CarregandoAnteriores) return;
        NoFim = false;
        if (Escondidas == 0 && TemAnterioresNoDisco) await CarregarAnterioresAsync();
        if (Escondidas == 0) return;
        _alvo = Math.Max(_alvo, ItensNaTela.Count) + MensagensNaTela;
        AvisarEscondidas();
        PedirMontagem();
    }

    void ComecarHistorico(string arquivo, TrechoDaTranscricao trecho)
    {
        _arquivoHistorico = arquivo;
        _inicioNoArquivo = trecho.Inicio;
    }

    async Task CarregarAnterioresAsync()
    {
        if (_arquivoHistorico is not { } arquivo || _inicioNoArquivo <= 0) return;
        CarregandoAnteriores = true;
        try
        {
            var ate = _inicioNoArquivo;
            var (trecho, internos) = await Task.Run(() =>
            {
                var lido = Transcricao.LerTurnos(arquivo, TurnosPorVez, ate);
                return (lido, InternosDoHistorico(arquivo, lido.Eventos));
            });
            _inicioNoArquivo = trecho.Inicio;
            var antigos = MontarItens(trecho.Eventos, internos, antigos: true);
            _inserindoAntigos = true;
            try
            {
                for (var i = 0; i < antigos.Count; i++) Itens.Insert(i, antigos[i]);
            }
            finally
            {
                _inserindoAntigos = false;
            }
            EncaixarTurnos();
        }
        finally
        {
            CarregandoAnteriores = false;
        }
    }

    void AvisarEscondidas()
    {
        OnPropertyChanged(nameof(Escondidas));
        OnPropertyChanged(nameof(TemEscondidas));
        OnPropertyChanged(nameof(TextoEscondidas));
    }
}
