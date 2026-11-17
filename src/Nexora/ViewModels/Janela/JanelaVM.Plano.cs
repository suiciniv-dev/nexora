using CommunityToolkit.Mvvm.ComponentModel;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class JanelaVM
{
    public const double PlanoEmAlerta = 0.8;
    public const double PlanoCritico = 0.95;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemPlano), nameof(TextoPlanoSessao), nameof(SessaoEmAlerta), nameof(SessaoCritica), nameof(DicaPlano), nameof(ValorPlanoSessao), nameof(AvisoDoPlano), nameof(TemAvisoDoPlano))]
    public partial double? PlanoSessao { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemPlano), nameof(TextoPlanoSemana), nameof(SemanaEmAlerta), nameof(SemanaCritica), nameof(DicaPlano), nameof(ValorPlanoSemana), nameof(AvisoDoPlano), nameof(TemAvisoDoPlano))]
    public partial double? PlanoSemana { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DicaPlano), nameof(AvisoDoPlano))]
    public partial DateTimeOffset? PlanoRenova { get; set; }

    public DateTimeOffset? PlanoSemanaRenova { get; private set; }

    public bool TemPlano => PlanoSessao is not null || PlanoSemana is not null;
    public double ValorPlanoSessao => PlanoSessao ?? 0;
    public double ValorPlanoSemana => PlanoSemana ?? 0;
    public string TextoPlanoSessao => $"5h  {Porcento(PlanoSessao)}";
    public string TextoPlanoSemana => Idioma.F("Semana  {0}", Porcento(PlanoSemana));
    public bool SessaoEmAlerta => PlanoSessao is >= PlanoEmAlerta and < PlanoCritico;
    public bool SessaoCritica => PlanoSessao is >= PlanoCritico;
    public bool SemanaEmAlerta => PlanoSemana is >= PlanoEmAlerta and < PlanoCritico;
    public bool SemanaCritica => PlanoSemana is >= PlanoCritico;

    public string DicaPlano
    {
        get
        {
            var renova = PlanoRenova is { } quando ? Idioma.F(" (renova às {0:HH:mm})", quando.ToLocalTime()) : "";
            return Idioma.F("Uso do plano informado pelo Claude Code\nSessão de 5 horas: {0}{1}\nSemana: {2}\nClique para ver o uso por projeto.", Porcento(PlanoSessao), renova, Porcento(PlanoSemana));
        }
    }

    public bool TemAvisoDoPlano => AvisoDoPlano is not null;

    public string? AvisoDoPlano => AvisoDe(PlanoSessao, PlanoSemana, PlanoRenova);

    public static string? AvisoDe(double? sessao, double? semana, DateTimeOffset? renova)
    {
        var quando = renova is { } r ? Idioma.F(" (renova às {0:HH:mm})", r.ToLocalTime()) : "";
        if (sessao is >= PlanoEmAlerta && (semana is null || sessao >= semana))
            return Idioma.F("Você já usou {0} da sessão de 5 horas{1}. Vários agentes juntos podem bater o limite antes de terminar.", Porcento(sessao), quando);
        if (semana is >= PlanoEmAlerta)
            return Idioma.F("Você já usou {0} do plano da semana. Vários agentes juntos podem bater o limite antes de terminar.", Porcento(semana));
        return null;
    }

    static string Porcento(double? valor) => valor is { } x ? $"{Math.Round(x * 100)}%" : "–";

    public async Task LerPlanoAsync()
    {
        if (Demo || TemPlano) return;
        try
        {
            if (LocalizadorClaude.Procurar() is not { } claude) return;
            if (await Core.Uso.PlanoDoClaude.LerAsync(claude) is { } plano) AplicarPlano(plano);
        }
        catch (Exception)
        {
        }
    }

    public void AplicarPlano(Core.Uso.Plano plano)
    {
        PlanoSessao ??= plano.Sessao?.Percentual / 100.0;
        PlanoSemana ??= plano.Semana?.Percentual / 100.0;
        PlanoRenova ??= plano.Sessao?.Reinicia is { } r ? new DateTimeOffset(r) : null;
        PlanoSemanaRenova ??= plano.Semana?.Reinicia is { } w ? new DateTimeOffset(w) : null;
        VirarJanelas(DateTimeOffset.Now);
    }

    public void VirarJanelas(DateTimeOffset agora)
    {
        if (PlanoRenova is { } sessao && sessao <= agora)
        {
            PlanoRenova = null;
            if (PlanoSessao is not null) PlanoSessao = 0;
        }
        if (PlanoSemanaRenova is { } semana && semana <= agora)
        {
            PlanoSemanaRenova = null;
            if (PlanoSemana is not null) PlanoSemana = 0;
        }
        if (PlanoCodex is { } codex)
        {
            var virou = codex;
            if (codex.SessaoReinicia is { } s && s <= agora) virou = virou with { Sessao = virou.Sessao is null ? null : 0, SessaoReinicia = null };
            if (codex.SemanaReinicia is { } w && w <= agora) virou = virou with { Semana = virou.Semana is null ? null : 0, SemanaReinicia = null };
            if (virou != codex) PlanoCodex = virou;
        }
    }

    void AoUsar(UsoDoPlano uso)
    {
        PlanoSessao = uso.Sessao ?? PlanoSessao;
        PlanoSemana = uso.Semana ?? PlanoSemana;
        PlanoRenova = uso.SessaoReinicia ?? PlanoRenova;
        PlanoSemanaRenova = uso.SemanaReinicia ?? PlanoSemanaRenova;
        VirarJanelas(DateTimeOffset.Now);
    }
}
