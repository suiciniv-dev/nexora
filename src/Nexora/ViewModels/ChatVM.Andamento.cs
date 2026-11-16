using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public enum FaseTurno { Pensando, Escrevendo, Preparando, Ferramenta }

public sealed partial class ChatVM
{
    DispatcherTimer? _relogioTurno;
    ItemPensamento? _pensamentoAtual;
    string? _ferramentaDaFase;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AndamentoTexto))]
    public partial FaseTurno Fase { get; set; }

    public string AndamentoTexto
    {
        get
        {
            if (SoNoFundo) return TextoDoFundo + "…";
            var fase = Fase switch
            {
                FaseTurno.Escrevendo => Idioma.T("Escrevendo"),
                FaseTurno.Preparando when _ferramentaDaFase is { } nome => Idioma.F("Preparando {0}", nome),
                FaseTurno.Ferramenta when _ferramentaDaFase is { } nome => Idioma.F("Rodando {0}", nome),
                FaseTurno.Preparando or FaseTurno.Ferramenta => Idioma.T("Trabalhando"),
                _ => Idioma.T("Pensando"),
            };
            return TurnoDesde is { } desde
                ? $"{fase}…  ·  {Tempo.Duracao(Math.Max(1000, (long)(DateTime.Now - desde).TotalMilliseconds))}"
                : $"{fase}…";
        }
    }

    void MudarFase(FaseTurno fase, string? ferramenta = null)
    {
        _ferramentaDaFase = ferramenta;
        if (Fase == fase) OnPropertyChanged(nameof(AndamentoTexto));
        else Fase = fase;
    }

    void AcompanharTurno(bool trabalhando)
    {
        if (!trabalhando)
        {
            _relogioTurno?.Stop();
            if (Estado != EstadoChat.PrecisaDeVoce) FecharPensamento();
            return;
        }
        _relogioTurno ??= new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => OnPropertyChanged(nameof(AndamentoTexto)));
        if (!_relogioTurno.IsEnabled) _relogioTurno.Start();
        OnPropertyChanged(nameof(AndamentoTexto));
    }

    void AbrirPensamento()
    {
        DescarregarParcial();
        _textoAtual = null;
        FecharPensamento();
        _pensamentoAtual = new ItemPensamento();
        Adicionar(_pensamentoAtual);
        MudarFase(FaseTurno.Pensando);
    }

    void FecharPensamento()
    {
        _pensamentoAtual?.Encerrar();
        _pensamentoAtual = null;
    }

    bool AplicarAndamento(EventoClaude evento)
    {
        switch (evento)
        {
            case PensamentoComecou:
                AbrirPensamento();
                return true;
            case Pensando pensando:
                if (_pensamentoAtual is { } aberto && pensando.TokensEstimados > 0) aberto.Tokens = pensando.TokensEstimados;
                if (Fase != FaseTurno.Escrevendo) MudarFase(FaseTurno.Pensando);
                return true;
            case PensamentoParcial parcial:
                if (_pensamentoAtual is { } comTexto) comTexto.Texto += parcial.Texto;
                return true;
            case FerramentaComecou comecou:
                FecharPensamento();
                MudarFase(FaseTurno.Preparando, Resumos.NomeDaFerramenta(comecou.Nome));
                return true;
            case BlocoEncerrado:
                FecharPensamento();
                if (Fase is FaseTurno.Escrevendo) MudarFase(FaseTurno.Pensando);
                return true;
            default:
                return false;
        }
    }
}
