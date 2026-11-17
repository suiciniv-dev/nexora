using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Codex;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class JanelaVM
{
    EscolhaDeAgenteVM? _escolhaDeAgente;
    bool _guiaDepoisDaEscolha;

    public EscolhaDeAgenteVM EscolhaDeAgente => _escolhaDeAgente ??= new EscolhaDeAgenteVM(ConcluirEscolhaDeAgente);

    [ObservableProperty]
    public partial bool MostraEscolhaDeAgente { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemPlanoCodex), nameof(TextoRestanteCodex), nameof(RestanteCodex), nameof(DetalheCodex), nameof(TextoLimitesCodex))]
    public partial UsoDoCodex? PlanoCodex { get; set; }

    public bool TemPlanoCodex => PlanoCodex is not null;
    public double RestanteCodex => 1 - Math.Clamp(PlanoCodex?.Sessao ?? 0, 0, 1);
    public string TextoRestanteCodex => PlanoCodex?.Sessao is { } s ? $"{Math.Round((1 - Math.Clamp(s, 0, 1)) * 100)}%" : "–";

    public string DetalheCodex
    {
        get
        {
            if (PlanoCodex is not { } p) return "";
            var semana = Idioma.F("semana {0}", Porcento(p.Semana));
            return p.SessaoReinicia is { } r ? Idioma.F("renova às {0:HH:mm} · {1}", r.ToLocalTime(), semana) : semana;
        }
    }

    [RelayCommand]
    public void AbrirEscolhaDeAgente()
    {
        EscolhaDeAgente.Procurar();
        MostraEscolhaDeAgente = true;
    }

    public void AbrirPrimeiroUso()
    {
        _guiaDepoisDaEscolha = true;
        AbrirEscolhaDeAgente();
    }

    void ConcluirEscolhaDeAgente(string agente)
    {
        App.Config.AgentePadrao = agente;
        App.Config.AgenteEscolhido = true;
        if (!Isolada) App.Config.Salvar();
        MostraEscolhaDeAgente = false;
        _paginaConfiguracoes?.Recarregar();
        if (!_guiaDepoisDaEscolha) return;
        _guiaDepoisDaEscolha = false;
        AbrirBoasVindas();
    }

    public void AcompanharCodex()
    {
        if (Demo)
        {
            PlanoCodex = new UsoDoCodex(0.25, 0.4, DateTimeOffset.Now.AddHours(3), null);
            return;
        }
        ChatVM.UsoDoCodexChegou += uso => PlanoCodex = uso;
    }
}
