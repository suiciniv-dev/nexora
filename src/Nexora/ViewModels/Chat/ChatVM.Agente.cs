using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Codex;
using Nexora.Core.Provedores;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    static IReadOnlyList<DescricaoAgente>? _instalados;

    public static IReadOnlyList<DescricaoAgente> AgentesInstalados => _instalados ??= JanelaVM.Demo
        ? [.. CatalogoAgentes.Todos.Where(a => a.Id is Agentes.Claude or Agentes.Codex or "gemini" or "antigravity")]
        : [.. CatalogoAgentes.Todos.Where(a => CatalogoAgentes.Procurar(a) is not null)];

    public static bool CodexInstalado => AgentesInstalados.Any(a => a.Id == Agentes.Codex);

    public static void EsquecerInstalacoes() => _instalados = null;

    public static event Action<UsoDoCodex>? UsoDoCodexChegou;
    public static event Action<TimeSpan>? TurnoTerminou;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EhCodex), nameof(EhClaude), nameof(DescricaoAgente), nameof(NomeAgente), nameof(InicialAgente), nameof(DicaDoCampo),
        nameof(ModeloRotulo), nameof(MostraTrocaDeAgente), nameof(TextoDoInicio), nameof(TemEsforco), nameof(SuportaRapido), nameof(SuportaUltracode), nameof(AgenteDoCartao), nameof(NomeDoAgente))]
    public partial string Agente { get; set; } = Agentes.Normalizar(App.Config.AgentePadrao);

    public DescricaoAgente DescricaoAgente => CatalogoAgentes.De(Agente);
    public override string AgenteDoCartao => EhChefe ? CatalogoDeAvatares.IdOrquestrador : Agente;
    public bool EhCodex => Agente == Agentes.Codex;
    public bool EhClaude => Agente == Agentes.Claude;
    public string NomeAgente => DescricaoAgente.Nome;
    public string InicialAgente => DescricaoAgente.Inicial;
    public bool PodeTrocarAgente => _sessao is null && SessaoId is null && Itens.Count == 0 && !EhLateral;
    public bool MostraTrocaDeAgente => !EhClaude || AgentesInstalados.Count > 1;

    public string DicaDoCampo => EhClaude
        ? Idioma.T("Mensagem para o Claude  ·  @ cita um arquivo  ·  / lista comandos  ·  Ctrl+V cola print")
        : _comandos.Count > 0
            ? Idioma.F("Mensagem para o {0}  ·  @ cita um arquivo  ·  / lista comandos  ·  Ctrl+V cola print", NomeAgente)
            : Idioma.F("Mensagem para o {0}  ·  @ cita um arquivo  ·  Ctrl+V cola print", NomeAgente);

    public string TextoDoInicio => EhClaude
        ? Idioma.T("Escolha onde o Claude vai trabalhar e escreva o que precisa.")
        : Idioma.F("Escolha onde o {0} vai trabalhar e escreva o que precisa.", NomeAgente);

    public string DicaDoAgente => PodeTrocarAgente
        ? Idioma.T("Escolher o agente desta conversa")
        : Idioma.F("Esta conversa é do {0}", NomeAgente);

    [ObservableProperty]
    public partial bool MenuAgenteAberto { get; set; }

    public ObservableCollection<OpcaoMenuVM> OpcoesAgente { get; } = [];

    [RelayCommand]
    void TrocarAgente()
    {
        if (!PodeTrocarAgente) return;
        OpcoesAgente.Clear();
        foreach (var agente in AgentesInstalados)
        {
            var id = agente.Id;
            OpcoesAgente.Add(new OpcaoMenuVM(agente.Nome, $"{agente.Empresa}  ·  {agente.Descricao}", id == Agente, () => EscolherAgente(id)));
        }
        if (!AgentesInstalados.Any(a => a.Id == Agente))
            OpcoesAgente.Insert(0, new OpcaoMenuVM(NomeAgente, Idioma.T("não encontrado neste computador"), true, () => EscolherAgente(Agente)));
        MenuAgenteAberto = true;
    }

    public void EscolherAgente(string id)
    {
        MenuAgenteAberto = false;
        if (!PodeTrocarAgente || id == Agente) return;
        Agente = Agentes.Normalizar(id);
        _comandos = [];
        ModeloDoAgente = EhClaude || JanelaVM.Isolada ? null : App.Config.ModeloPorAgente.GetValueOrDefault(Agente);
        AvisarRotulos();
    }

    public static Func<OpcoesSessao, string, ISessaoAgente>? SessaoDeTeste { get; set; }

    ISessaoAgente CriarSessao(OpcoesSessao opcoes) =>
        SessaoDeTeste is { } deTeste ? deTeste(opcoes, Agente) : Aplicativo.FabricaDeSessoes.CriarNoNucleo(Agente, opcoes, Titulo);

    static void AvisarUsoDoCodex(UsoDoCodex uso) => UsoDoCodexChegou?.Invoke(uso);

    static void AvisarFimDoTurno(TimeSpan duracao) => TurnoTerminou?.Invoke(duracao);

    void AvisarTrocaDeAgente()
    {
        OnPropertyChanged(nameof(PodeTrocarAgente));
        OnPropertyChanged(nameof(PodeTrocarOndeRoda));
        OnPropertyChanged(nameof(DicaOndeRoda));
        OnPropertyChanged(nameof(DicaDoAgente));
    }
}
