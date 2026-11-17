using CommunityToolkit.Mvvm.ComponentModel;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class JanelaVM
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemMemoria))]
    public partial string? TextoMemoria { get; set; }

    public bool TemMemoria => TextoMemoria is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DicaTelaCheia), nameof(MostraTrilho), nameof(MostraTrilhoSolto))]
    public partial bool TelaCheia { get; set; }

    public string DicaTelaCheia => TelaCheia ? Idioma.T("Sair da tela grande (F11)") : Idioma.T("Tela grande: tela cheia, tudo maior e as sessões pelo teclado (F11)");

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    void AlternarTelaCheia() => TelaCheia = !TelaCheia;

    bool _lateralAntesDaTelaCheia;

    partial void OnTelaCheiaChanged(bool value)
    {
        if (value)
        {
            _lateralAntesDaTelaCheia = LateralVisivel;
            LateralVisivel = false;
            LateralAberta = false;
            TelaGrande.Ligada = true;
            TelaGrande.Abrir();
        }
        else
        {
            TelaGrande.Ligada = false;
            LateralVisivel = _lateralAntesDaTelaCheia;
        }
    }

    public bool TemSobreposicao => BuscaAberta || Foco.Aberto || Templates.Aberto || Atalhos.Aberto ||MostraBoasVindas || MostraEscolhaDeAgente || TemExclusao || PedindoReinicio;
    public double RestanteSessao => 1 - Math.Clamp(PlanoSessao ?? 0, 0, 1);
    public string TextoRestanteSessao => PlanoSessao is { } s ? $"{Math.Round((1 - Math.Clamp(s, 0, 1)) * 100)}%" : "–";

    public string DetalheRestante
    {
        get
        {
            var semana = Idioma.F("semana {0}", Porcento(PlanoSemana));
            return PlanoRenova is { } r ? Idioma.F("renova às {0:HH:mm} · {1}", r.ToLocalTime(), semana) : semana;
        }
    }

    public string TextoLimites
    {
        get
        {
            var sessao = Porcento(PlanoSessao);
            if (PlanoRenova is { } r && r - DateTimeOffset.Now is { TotalMinutes: > 0 } falta)
                sessao += falta.TotalHours >= 1 ? $" {(int)falta.TotalHours}h {falta.Minutes}m" : $" {falta.Minutes}m";
            return Idioma.F("{0}  ·  {1} na semana", sessao, Porcento(PlanoSemana));
        }
    }

    public string TextoLimitesCodex => PlanoCodex is { } p ? Idioma.F("Codex {0}  ·  {1} na semana", Porcento(p.Sessao), Porcento(p.Semana)) : "";

    public void AtualizarRodape()
    {
        VirarJanelas(DateTimeOffset.Now);
        OnPropertyChanged(nameof(TextoLimites));
    }

    partial void OnPlanoSessaoChanged(double? value)
    {
        OnPropertyChanged(nameof(RestanteSessao));
        OnPropertyChanged(nameof(TextoRestanteSessao));
        OnPropertyChanged(nameof(TextoLimites));
    }

    partial void OnPlanoSemanaChanged(double? value)
    {
        OnPropertyChanged(nameof(DetalheRestante));
        OnPropertyChanged(nameof(TextoLimites));
    }

    partial void OnPlanoRenovaChanged(DateTimeOffset? value)
    {
        OnPropertyChanged(nameof(DetalheRestante));
        OnPropertyChanged(nameof(TextoLimites));
    }

    public void AcompanharMemoria()
    {
        if (Demo)
        {
            TextoMemoria = RecursosVM.Formatar(2_576_980_378);
            return;
        }
        if (MonitorDeDesempenho.Instancia is not { } monitor) return;
        monitor.Mediu += () =>
        {
            if (monitor.Ultima is not { } a) return;
            TextoMemoria = RecursosVM.Formatar(a.NexoraRam + a.Sessoes.Ram + a.Mcps.Ram);
        };
    }
}
