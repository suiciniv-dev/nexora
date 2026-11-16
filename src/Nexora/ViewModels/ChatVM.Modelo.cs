using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    static readonly (string Valor, string Rotulo)[] NiveisEsforco =
        [("low", Idioma.T("Baixo")), ("medium", Idioma.T("Médio")), ("high", Idioma.T("Alto")), ("xhigh", Idioma.T("Extra alto")), ("max", Idioma.T("Máximo"))];

    static readonly string[] ModelosReserva = ["opus", "sonnet", "haiku", "fable"];

    static readonly string[] DescricoesModo =
    [
        "",
        Idioma.T("Pede permissão antes de editar e de rodar comandos"),
        Idioma.T("Edita arquivos sem perguntar; comandos ainda pedem"),
        Idioma.T("O Claude decide o que é seguro e só pergunta no resto"),
        Idioma.T("Só lê e planeja, sem mexer em nada"),
        Idioma.T("Faz tudo sem pedir permissão"),
    ];

    static readonly Dictionary<string, string> Traducoes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Best for everyday, complex tasks"] = "O melhor para o dia a dia e tarefas complexas",
        ["For complex work and everyday tasks"] = "Para trabalho complexo e o dia a dia",
        ["For your toughest challenges"] = "Para os desafios mais difíceis",
        ["Fastest for quick answers"] = "O mais rápido, para respostas curtas",
        ["Efficient for routine tasks"] = "Eficiente para tarefas de rotina",
        ["Most efficient for simpler tasks"] = "O mais eficiente para tarefas simples",
        ["Most capable for your hardest and longest-running tasks"] = "O mais capaz, para as tarefas mais difíceis e longas",
    };

    PadroesDoClaude _padroes = new(null, null, null);
    static event Action<ChatVM>? EscolhasLembradas;
    bool _escolheuAqui;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeloRotulo), nameof(EsforcoRotulo), nameof(TemEsforcoRotulo), nameof(TemEsforco))]
    public partial string? ModeloValor { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeloRotulo), nameof(EsforcoRotulo), nameof(TemEsforcoRotulo), nameof(TemEsforco))]
    public partial string? ModeloDoAgente { get; set; }

    static readonly Dictionary<string, IReadOnlyList<ModeloDisponivel>> ModelosPorAgente = [];

    static event Action<string>? ModelosDoAgenteMudaram;

    IReadOnlyList<ModeloDisponivel> ModelosDesteAgente => ModelosPorAgente.GetValueOrDefault(Agente) ?? [];

    public static void GuardarModelosDoAgente(string agente, IReadOnlyList<ModeloDisponivel>? modelos)
    {
        if (modelos is not { Count: > 0 }) return;
        ModelosPorAgente[agente] = modelos;
        ModelosDoAgenteMudaram?.Invoke(agente);
    }

    public string? ModeloDaSessao => EhClaude ? ModeloValor : ModeloDoAgente;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsforcoRotulo), nameof(TemEsforcoRotulo), nameof(EsforcoOrigem), nameof(PodeVoltarEsforco))]
    public partial string? EsforcoValor { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostraRapido))]
    public partial bool ModoRapido { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostraUltracode))]
    public partial bool Ultracode { get; set; }

    public bool SuportaRapido => EhClaude && ModeloEfetivo?.Rapido == true;
    public bool SuportaUltracode => EhClaude && TemEsforco;
    public bool MostraRapido => ModoRapido && SuportaRapido;
    public bool MostraUltracode => Ultracode && SuportaUltracode;

    public string? ConfiguracoesDaSessao =>
        (ModoRapido && SuportaRapido) || (Ultracode && SuportaUltracode)
            ? new System.Text.Json.Nodes.JsonObject { ["fastMode"] = ModoRapido && SuportaRapido, ["ultracode"] = Ultracode && SuportaUltracode }.ToJsonString()
            : null;

    partial void OnModoRapidoChanged(bool value) => AplicarNaSessao("fastMode", value);

    partial void OnUltracodeChanged(bool value) => AplicarNaSessao("ultracode", value);

    void AplicarNaSessao(string chave, bool valor)
    {
        if (_sessao is { Ativa: true } sessao) sessao.AplicarConfiguracoes(new System.Text.Json.Nodes.JsonObject { [chave] = valor });
    }

    [RelayCommand]
    void AlternarRapido()
    {
        ModoRapido = !ModoRapido;
        Lembrar();
    }

    [RelayCommand]
    void AlternarUltracode()
    {
        Ultracode = !Ultracode;
        Lembrar();
    }

    [ObservableProperty]
    public partial bool MenuModeloAberto { get; set; }

    [ObservableProperty]
    public partial bool MenuModoAberto { get; set; }

    [ObservableProperty]
    public partial bool BuscandoModelos { get; set; }

    public ObservableCollection<OpcaoMenuVM> OpcoesModelo { get; } = [];
    public ObservableCollection<OpcaoEsforcoVM> OpcoesEsforco { get; } = [];
    public ObservableCollection<OpcaoMenuVM> OpcoesModo { get; } = [];

    public string ModeloRotulo => !EhClaude
        ? ModeloDoAgente is { } escolhido ? NomeDoModeloDoAgente(escolhido) : Modelo ?? Idioma.T("Modelo padrão")
        : ModeloValor is { } valor ? CatalogoModelos.Nome(valor) : NomeDoPadrao();

    string NomeDoModeloDoAgente(string valor) => ModelosDesteAgente.FirstOrDefault(m => Igual(m.Valor, valor))?.Nome ?? valor;

    string NomeDoPadrao() =>
        _padroes.Modelo is { } doSettings ? CatalogoModelos.Nome(doSettings)
        : CatalogoModelos.Achar(CatalogoModelos.Padrao) is { } recomendado ? CatalogoModelos.Nome(recomendado.Valor)
        : Modelo ?? Idioma.T("Modelo padrão");

    ModeloDisponivel? ModeloEfetivo => EhClaude
        ? CatalogoModelos.Achar(ModeloValor ?? _padroes.Modelo ?? CatalogoModelos.Padrao)
        : ModelosDesteAgente.FirstOrDefault(m => Igual(m.Valor, ModeloDoAgente)) ?? ModelosDesteAgente.FirstOrDefault();

    IReadOnlyList<string> NiveisDoModelo => ModeloEfetivo is { } modelo ? modelo.Esforcos
        : EhClaude ? [.. NiveisEsforco.Select(n => n.Valor)] : [];

    public bool TemEsforco => NiveisDoModelo.Count > 0;
    public string? EsforcoEfetivo => EsforcoValor ?? (EhClaude ? _padroes.Esforco : null);
    public string EsforcoRotulo => TemEsforco && EsforcoEfetivo is { } esforco ? RotuloDoEsforco(esforco) : "";
    public bool TemEsforcoRotulo => EsforcoRotulo.Length > 0;
    public string EsforcoOrigem => EhClaude && EsforcoValor is null && _padroes.Esforco is not null ? Idioma.T("padrão do Claude") : "";
    public bool PodeVoltarEsforco => EsforcoValor is not null && EsforcoValor != _padroes.Esforco;

    public string ModoRotulo => ModoIndice > 0 ? Modos[ModoIndice] : RotuloDoModoPadrao();

    string RotuloDoModoPadrao() => _padroes.Modo switch
    {
        "acceptEdits" => Modos[2],
        "auto" => Modos[3],
        "plan" => Modos[4],
        "bypassPermissions" => Modos[5],
        _ => Modos[1],
    };

    public static string RotuloDoEsforco(string valor) =>
        NiveisEsforco.FirstOrDefault(n => n.Valor == valor).Rotulo ?? valor;

    static string? DescricaoDoModelo(string? descricao) =>
        descricao is null ? null : !Idioma.EmIngles && Traducoes.TryGetValue(descricao.Trim(), out var traduzida) ? traduzida : descricao;

    void IniciarModelo()
    {
        if (!JanelaVM.Isolada)
        {
            ModeloValor = App.Config.UltimoModelo;
            EsforcoValor = App.Config.UltimoEsforco;
            ModoIndice = App.Config.UltimoModo is > 0 and var modo && modo < Modos.Length ? modo : 0;
            ModoRapido = App.Config.UltimoRapido;
            Ultracode = App.Config.UltimoUltracode;
        }
        LerPadroes();
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Modelo) && ModeloValor is null && _padroes.Modelo is null) OnPropertyChanged(nameof(ModeloRotulo));
        };
        CatalogoModelos.Mudou += AoMudarCatalogo;
        ModelosDoAgenteMudaram += AoMudarModelosDoAgente;
        EscolhasLembradas += AdotarEscolhas;
        if (!EhClaude && !JanelaVM.Isolada) ModeloDoAgente = App.Config.ModeloPorAgente.GetValueOrDefault(Agente);
    }

    void AoMudarModelosDoAgente(string agente)
    {
        if (agente == Agente) AoMudarCatalogo();
    }

    void AdotarEscolhas(ChatVM origem)
    {
        if (origem == this || _escolheuAqui || _sessao is not null) return;
        ModeloValor = App.Config.UltimoModelo;
        EsforcoValor = App.Config.UltimoEsforco;
        ModoIndice = App.Config.UltimoModo is > 0 and var modo && modo < Modos.Length ? modo : 0;
        ModoRapido = App.Config.UltimoRapido;
        Ultracode = App.Config.UltimoUltracode;
    }

    void LerPadroes()
    {
        _padroes = PadroesDoClaude.Ler(Pasta, JanelaVM.Isolada);
        AvisarRotulos();
    }

    void AvisarRotulos()
    {
        OnPropertyChanged(nameof(ModeloRotulo));
        OnPropertyChanged(nameof(EsforcoRotulo));
        OnPropertyChanged(nameof(TemEsforcoRotulo));
        OnPropertyChanged(nameof(TemEsforco));
        OnPropertyChanged(nameof(EsforcoOrigem));
        OnPropertyChanged(nameof(PodeVoltarEsforco));
        OnPropertyChanged(nameof(ModoRotulo));
    }

    void AoMudarCatalogo() => Dispatcher.UIThread.Post(() =>
    {
        AvisarRotulos();
        if (MenuModeloAberto) MontarOpcoesModelo();
    });

    void EsquecerCatalogo()
    {
        CatalogoModelos.Mudou -= AoMudarCatalogo;
        ModelosDoAgenteMudaram -= AoMudarModelosDoAgente;
        EscolhasLembradas -= AdotarEscolhas;
    }

    public static int IndiceDoModoDoClaude(string? modo) => modo switch
    {
        "default" or "manual" => 1,
        "acceptEdits" => 2,
        "auto" => 3,
        "plan" => 4,
        "bypassPermissions" => 5,
        _ => -1,
    };

    void UsarDoHistorico(string? idDoModelo, string? modo)
    {
        if (_escolheuAqui) return;
        if (IndiceDoModoDoClaude(modo) is > 0 and var indice)
        {
            ModoIndice = indice;
            _escolheuAqui = true;
        }
        if (string.IsNullOrWhiteSpace(idDoModelo) || !idDoModelo.StartsWith("claude-", StringComparison.OrdinalIgnoreCase)) return;
        ModeloValor = CatalogoModelos.ValorPara(idDoModelo);
        _escolheuAqui = true;
    }

    [RelayCommand]
    void AbrirMenuModelo()
    {
        LerPadroes();
        OnPropertyChanged(nameof(TemAvisoCustoTroca));
        OnPropertyChanged(nameof(AvisoCustoTroca));
        MontarOpcoesModelo();
        MenuModeloAberto = true;
        if (!EhClaude)
        {
            if (ModelosDesteAgente.Count == 0 && !BuscandoModelos) _ = BuscarModelosDoAgenteAsync();
            return;
        }
        if (CatalogoModelos.Atual.Count == 0 && !BuscandoModelos) _ = BuscarModelosAsync();
    }

    [RelayCommand]
    void AbrirMenuModo()
    {
        LerPadroes();
        MontarOpcoesModo();
        MenuModoAberto = true;
    }

    [RelayCommand]
    void VoltarEsforcoPadrao() => EscolherEsforco(null);

    void EscolherEsforco(string? valor)
    {
        if (Igual(valor, EsforcoValor)) return;
        var antes = EsforcoEfetivo;
        EsforcoValor = valor;
        Lembrar();
        if (Itens.Count > 0 && TemEsforco && !Igual(antes, EsforcoEfetivo))
            MarcarTroca("esforco", EsforcoEfetivo is { } efetivo
                ? Idioma.F("Esforço: {0}", RotuloDoEsforco(efetivo))
                : Idioma.T("Esforço: padrão"));
    }

    void MarcarTroca(string chave, string texto)
    {
        if (Itens.LastOrDefault() is ItemAviso { EhTroca: true } anterior && anterior.Chave == chave) Itens.Remove(anterior);
        Adicionar(ItemAviso.Troca(texto, chave));
    }

    public bool TemAvisoCustoTroca => Itens.Count > 0 && ContextoTokens is >= LimiteAvisoTroca && !CacheExpirado;

    public string AvisoCustoTroca => ContextoTokens is { } tokens
        ? Idioma.F("Trocar de modelo agora relê ~{0} tokens sem cache: o cache do prompt é de cada modelo, e a conversa inteira entra de novo como nova.", TokensPorExtenso(tokens))
        : "";

    const long LimiteAvisoTroca = 30_000;

    void Lembrar()
    {
        _escolheuAqui = true;
        if (JanelaVM.Isolada) return;
        App.Config.UltimoModelo = ModeloValor;
        App.Config.UltimoEsforco = EsforcoValor;
        App.Config.UltimoModo = ModoIndice;
        App.Config.UltimoRapido = ModoRapido;
        App.Config.UltimoUltracode = Ultracode;
        App.Config.Salvar();
        EscolhasLembradas?.Invoke(this);
    }

    async Task BuscarModelosAsync()
    {
        BuscandoModelos = true;
        try
        {
            var inicio = await CatalogoComandos.ObterAsync(Pasta, JanelaVM.Isolada);
            CatalogoModelos.Guardar(inicio.Modelos);
        }
        finally
        {
            BuscandoModelos = false;
        }
        if (MenuModeloAberto) MontarOpcoesModelo();
    }

    async Task BuscarModelosDoAgenteAsync()
    {
        if (JanelaVM.Demo)
        {
            GuardarModelosDoAgente(Agente, ModelosDeDemonstracao(Agente));
            return;
        }
        if (_sessao is { Ativa: true }) return;
        BuscandoModelos = true;
        try
        {
            GarantirSessao();
            var limite = DateTime.UtcNow.AddSeconds(25);
            while (ModelosDesteAgente.Count == 0 && DateTime.UtcNow < limite && _sessao is { Ativa: true }) await Task.Delay(250);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Adicionar(ItemAviso.Erro(ex.Message));
        }
        finally
        {
            BuscandoModelos = false;
        }
        if (MenuModeloAberto) MontarOpcoesModelo();
    }

    static IReadOnlyList<ModeloDisponivel> ModelosDeDemonstracao(string agente) => agente switch
    {
        Agentes.Codex =>
        [
            new("gpt-5.5-codex", "GPT-5.5 Codex", Idioma.T("O mais capaz para código"), null, ["low", "medium", "high", "xhigh"]),
            new("gpt-5.5-mini", "GPT-5.5 Mini", Idioma.T("Mais rápido e barato"), null, ["low", "medium", "high"]),
        ],
        "gemini" or "antigravity" =>
        [
            new("gemini-3-pro", "Gemini 3 Pro", Idioma.T("O mais capaz para código"), null, []),
            new("gemini-3-flash", "Gemini 3 Flash", Idioma.T("Mais rápido e barato"), null, []),
        ],
        _ => [],
    };

    void MontarOpcoesModeloDoAgente()
    {
        OpcoesModelo.Add(new OpcaoMenuVM(Idioma.T("Padrão"), Idioma.F("o modelo configurado no {0}", NomeAgente), ModeloDoAgente is null, () => EscolherModeloDoAgente(null)));
        var lista = ModelosDesteAgente;
        if (ModeloDoAgente is { } atual && !lista.Any(m => Igual(m.Valor, atual)))
            OpcoesModelo.Add(new OpcaoMenuVM(atual, Idioma.T("o modelo desta conversa"), true, () => EscolherModeloDoAgente(atual)));
        foreach (var modelo in lista)
            OpcoesModelo.Add(new OpcaoMenuVM(modelo.Nome, modelo.Descricao, Igual(modelo.Valor, ModeloDoAgente), () => EscolherModeloDoAgente(modelo.Valor)));
        MontarOpcoesEsforco();
    }

    void EscolherModeloDoAgente(string? valor)
    {
        MenuModeloAberto = false;
        if (Igual(valor, ModeloDoAgente)) return;
        ModeloDoAgente = valor;
        if (!JanelaVM.Isolada)
        {
            if (valor is null) App.Config.ModeloPorAgente.Remove(Agente);
            else App.Config.ModeloPorAgente[Agente] = valor;
            App.Config.Salvar();
        }
        if (_sessao is { Ativa: true } sessao) sessao.MudarModelo(valor);
        MontarOpcoesEsforco();
        if (Itens.Count > 0)
            MarcarTroca("modelo", valor is null ? Idioma.F("Trocou para o modelo padrão do {0}", NomeAgente) : Idioma.F("Modelo trocado para {0}", ModeloRotulo));
    }

    void MontarOpcoesModelo()
    {
        OpcoesModelo.Clear();
        if (!EhClaude)
        {
            MontarOpcoesModeloDoAgente();
            return;
        }
        var origem = _padroes.Modelo is not null ? Idioma.T("definido no settings do Claude") : Idioma.T("recomendado pelo Claude");
        OpcoesModelo.Add(new OpcaoMenuVM(Idioma.T("Padrão"), $"{NomeDoPadrao()}  ·  {origem}", ModeloValor is null, () => EscolherModelo(null)));
        var lista = CatalogoModelos.Atual.Where(m => m.Valor != CatalogoModelos.Padrao).ToList();
        if (lista.Count == 0) lista = [.. ModelosReserva.Select(v => new ModeloDisponivel(v, CatalogoModelos.NomeDoId(v), null, null, []))];
        var selecionado = ModeloValor is { } atual ? CatalogoModelos.Achar(atual)?.Valor ?? atual : null;
        if (selecionado is not null && !lista.Any(m => Igual(m.Valor, selecionado)))
            OpcoesModelo.Add(new OpcaoMenuVM(CatalogoModelos.Nome(selecionado), Idioma.T("o modelo desta conversa"), true, () => EscolherModelo(selecionado)));
        foreach (var modelo in lista)
            OpcoesModelo.Add(new OpcaoMenuVM(modelo.Nome, DescricaoDoModelo(modelo.Descricao), Igual(modelo.Valor, selecionado), () => EscolherModelo(modelo.Valor)));
        MontarOpcoesEsforco();
    }

    void MontarOpcoesEsforco()
    {
        OpcoesEsforco.Clear();
        var niveis = NiveisDoModelo;
        var efetivo = EsforcoEfetivo;
        foreach (var (valor, rotulo) in NiveisEsforco.Where(n => niveis.Contains(n.Valor)))
            OpcoesEsforco.Add(new OpcaoEsforcoVM(rotulo, valor == efetivo, valor == _padroes.Esforco, () => EscolherEsforco(valor)));
        OnPropertyChanged(nameof(TemEsforco));
        OnPropertyChanged(nameof(SuportaRapido));
        OnPropertyChanged(nameof(SuportaUltracode));
        OnPropertyChanged(nameof(MostraRapido));
        OnPropertyChanged(nameof(MostraUltracode));
    }

    void MontarOpcoesModo()
    {
        OpcoesModo.Clear();
        OpcoesModo.Add(new OpcaoMenuVM(Idioma.T("Padrão"), $"{RotuloDoModoPadrao()}  ·  {(_padroes.Modo is null ? Idioma.T("o normal do Claude") : Idioma.T("definido no settings do Claude"))}",
            ModoIndice == 0, () => EscolherModo(0)));
        for (var i = 1; i < Modos.Length; i++)
        {
            var indice = i;
            OpcoesModo.Add(new OpcaoMenuVM(Modos[i], DescricoesModo[i], ModoIndice == i, () => EscolherModo(indice)));
        }
    }

    void EscolherModelo(string? valor)
    {
        MenuModeloAberto = false;
        if (Igual(valor, ModeloValor)) return;
        ModeloValor = valor;
        Lembrar();
        if (Itens.Count > 0)
            MarcarTroca("modelo", valor is null ? Idioma.F("Modelo trocado para o padrão ({0})", NomeDoPadrao()) : Idioma.F("Modelo trocado para {0}", ModeloRotulo));
    }

    void EscolherModo(int indice)
    {
        ModoIndice = indice;
        MenuModoAberto = false;
        Lembrar();
    }

    static bool Igual(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    partial void OnModeloValorChanged(string? value)
    {
        if (EhClaude && _sessao is { Ativa: true } s) s.MudarModelo(value);
        MontarOpcoesEsforco();
    }

    partial void OnEsforcoValorChanged(string? value)
    {
        MontarOpcoesEsforco();
        if (_sessao is not { Ativa: true }) return;
        if (Trabalhando || Precisa) _reiniciar = true;
        else Reiniciar();
    }
}
