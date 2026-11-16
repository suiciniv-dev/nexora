using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    static readonly string[] CoresContexto = ["#8AB4F8", "#F28B82", "#81C995", "#FDD663", "#C58AF9", "#78D9EC", "#FCAD70", "#E59AC4"];

    readonly Dictionary<string, ItemFerramenta> _ultimaEdicao = new(StringComparer.OrdinalIgnoreCase);
    bool _detalhando;

    [ObservableProperty]
    public partial bool DetalhesVisivel { get; set; } = App.Config.PainelDireitoAberto;

    [ObservableProperty]
    public partial bool DetalhesSobrepostos { get; set; }

    [ObservableProperty]
    public partial bool DetalhesCabem { get; set; } = true;

    [ObservableProperty]
    public partial bool DetalhesAbertos { get; set; }

    public bool DetalhesAoLado => DetalhesVisivel && !DetalhesSobrepostos;
    public bool DetalhesLateral => DetalhesAoLado && DetalhesCabem;
    public bool DetalhesPorCima => DetalhesAbertos && DetalhesSobrepostos;
    public bool DetalhesNaTela => DetalhesLateral || DetalhesPorCima;
    public bool DetalhesLigados => DetalhesSobrepostos ? DetalhesAbertos : DetalhesVisivel;
    public bool DetalhesDisponiveis => DetalhesSobrepostos || DetalhesCabem;

    void AvisarDetalhes()
    {
        OnPropertyChanged(nameof(DetalhesAoLado));
        OnPropertyChanged(nameof(DetalhesLateral));
        OnPropertyChanged(nameof(DetalhesPorCima));
        OnPropertyChanged(nameof(DetalhesNaTela));
        OnPropertyChanged(nameof(DetalhesLigados));
        OnPropertyChanged(nameof(DetalhesDisponiveis));
    }

    partial void OnDetalhesSobrepostosChanged(bool value)
    {
        if (!value) DetalhesAbertos = false;
        AvisarDetalhes();
    }

    partial void OnDetalhesCabemChanged(bool value) => AvisarDetalhes();

    partial void OnDetalhesAbertosChanged(bool value)
    {
        AvisarDetalhes();
        if (value) _ = DetalharContextoAsync();
    }

    public ObservableCollection<SaidaVM> Saidas { get; } = [];
    public bool TemSaidas => Saidas.Count > 0;
    public string TituloSaidas => Saidas.Count == 1 ? Idioma.T("1 arquivo alterado") : Idioma.F("{0} arquivos alterados", Saidas.Count);
    public ObservableCollection<CategoriaContextoVM> CategoriasContexto { get; } = [];
    public bool TemCategorias => CategoriasContexto.Count > 0;
    public bool MostraAcoesRapidas => Estado == EstadoChat.Pronta && TemSaidas;
    public override bool Pronta => Estado == EstadoChat.Pronta;
    public override string NomeProjeto => LocalAtual.Nome;
    public override string? PastaDoProjeto => Pasta;
    public bool TemMcpDoHub => McpCompartilhados.Count > 0;
    public string TextoMcpDoHub => string.Join(", ", McpCompartilhados);

    [ObservableProperty]
    public partial string? ResumoContexto { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AbaSessao), nameof(AbaArquivos), nameof(AbaGit), nameof(AbaBusca), nameof(AbaPr), nameof(AbaDoPainel), nameof(AbaNavegador), nameof(LarguraDetalhes))]
    public partial string AbaDetalhe { get; set; } = "sessao";

    public bool AbaSessao => AbaDetalhe == "sessao";
    public bool AbaArquivos => AbaDetalhe == "arquivos";
    public bool AbaGit => AbaDetalhe == "git";
    public bool AbaBusca => AbaDetalhe == "busca";
    public bool AbaPr => AbaDetalhe == "pr";
    public bool AbaDoPainel => AbaArquivos || AbaGit || AbaBusca;

    PainelPrVM? _painelPr;
    public PainelPrVM PainelPr => _painelPr ??= new PainelPrVM(this);

    [RelayCommand]
    void MostrarAba(string aba)
    {
        var mesma = AbaDetalhe == aba;
        AbaDetalhe = aba;
        if (DetalhesSobrepostos) DetalhesAbertos = true;
        else DetalhesVisivel = true;
        if (mesma && aba == "pr") PainelPr.Mostrar();
    }

    partial void OnAbaDetalheChanged(string value)
    {
        if (App.Config.AbaDetalhe != value && !JanelaVM.Isolada)
        {
            App.Config.AbaDetalhe = value;
            App.Config.Salvar();
        }
        switch (value)
        {
            case "arquivos":
                Painel.VerArquivosCommand.Execute(null);
                Painel.Visivel = true;
                break;
            case "git":
                Painel.VerAlteracoesCommand.Execute(null);
                Painel.Visivel = true;
                break;
            case "busca":
                Painel.AbrirBuscaCommand.Execute(null);
                break;
            case "pr":
                PainelPr.Mostrar();
                break;
            case "navegador":
                UsarNavegador();
                break;
        }
    }

    void IniciarDetalhes()
    {
        if (!JanelaVM.Isolada && App.Config.AbaDetalhe is "arquivos" or "git" or "busca") AbaDetalhe = App.Config.AbaDetalhe;
        Itens.CollectionChanged += ObservarSaidas;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(Estado)) return;
            OnPropertyChanged(nameof(MostraAcoesRapidas));
            AvisarPronta();
            if (Estado == EstadoChat.Pronta && DetalhesNaTela) _ = DetalharContextoAsync();
        };
    }

    partial void OnDetalhesVisivelChanged(bool value)
    {
        App.Config.PainelDireitoAberto = value;
        if (!JanelaVM.Isolada) App.Config.Salvar();
        AvisarDetalhes();
        if (value) _ = DetalharContextoAsync();
    }

    void ObservarSaidas(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            Saidas.Clear();
            _ultimaEdicao.Clear();
            AvisarSaidas();
            return;
        }
        var antiga = e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { } novos && e.NewStartingIndex < Itens.Count - novos.Count;
        foreach (var f in e.NewItems?.OfType<ItemFerramenta>() ?? []) RegistrarSaida(f, antiga);
    }

    void RegistrarSaida(ItemFerramenta f, bool antiga = false)
    {
        if (f is not { TemDiff: true, Caminho: { } caminho }) return;
        if (!antiga) _ultimaEdicao[caminho] = f;
        else _ultimaEdicao.TryAdd(caminho, f);
        if (Saidas.FirstOrDefault(s => string.Equals(s.Caminho, caminho, StringComparison.OrdinalIgnoreCase)) is { } existente)
        {
            existente.Edicoes++;
            if (!antiga) Saidas.Move(Saidas.IndexOf(existente), 0);
        }
        else if (antiga) Saidas.Add(new SaidaVM(caminho, AbrirSaida));
        else Saidas.Insert(0, new SaidaVM(caminho, AbrirSaida));
        AvisarSaidas();
    }

    void AvisarSaidas()
    {
        OnPropertyChanged(nameof(TemSaidas));
        OnPropertyChanged(nameof(TituloSaidas));
        OnPropertyChanged(nameof(MostraAcoesRapidas));
        AvisarPronta();
    }

    void AbrirSaida(string caminho, bool diff)
    {
        if (diff && _ultimaEdicao.TryGetValue(caminho, out var edicao)) AcaoDaFerramenta(edicao, AcaoFerramenta.Diff);
        else Editor.Abrir(caminho);
    }

    [RelayCommand]
    async Task DetalharContextoAsync()
    {
        if (_detalhando || _sessao is not { Ativa: true } sessao) return;
        _detalhando = true;
        try
        {
            var r = await sessao.PedirAsync(id => Protocolo.Controle(id, new JsonObject { ["subtype"] = "get_context_usage" }), TimeSpan.FromSeconds(20));
            if (r is not { Sucesso: true, Dados: { } dados }) return;
            LerContexto(dados);
        }
        finally
        {
            _detalhando = false;
        }
    }

    void LerContexto(JsonElement dados)
    {
        var maximo = dados.TryGetProperty("maxTokens", out var m) && m.TryGetInt64(out var mx) && mx > 0 ? mx : (JanelaContexto ?? 0);
        var total = dados.TryGetProperty("totalTokens", out var t) && t.TryGetInt64(out var tt) ? tt : 0;
        var categorias = Protocolo.Itens(Protocolo.Objeto(dados, "categories"))
            .Select(c => (
                Nome: Protocolo.Texto(c, "name") ?? "?",
                Tokens: c.TryGetProperty("tokens", out var k) && k.TryGetInt64(out var n) ? n : 0,
                Tipo: Protocolo.Texto(c, "kind") ?? "used"))
            .Where(c => c.Tokens > 0 && c.Tipo != "deferred")
            .ToList();
        CategoriasContexto.Clear();
        var i = 0;
        foreach (var c in categorias.Where(c => c.Tipo == "used").OrderByDescending(c => c.Tokens))
            CategoriasContexto.Add(new CategoriaContextoVM(Traduzir(c.Nome), c.Tokens, maximo > 0 ? (double)c.Tokens / maximo : 0, CoresContexto[i++ % CoresContexto.Length]));
        foreach (var c in categorias.Where(c => c.Tipo != "used"))
            CategoriasContexto.Add(new CategoriaContextoVM(Traduzir(c.Nome), c.Tokens, maximo > 0 ? (double)c.Tokens / maximo : 0, "#5F5B55"));
        if (total > 0) ContextoTokens = total;
        if (maximo > 0) JanelaContexto = maximo;
        var pct = dados.TryGetProperty("percentage", out var p) && p.TryGetDouble(out var pp) ? pp : maximo > 0 ? 100.0 * total / maximo : 0;
        ResumoContexto = Idioma.F("{0:0}% usado", pct);
        OnPropertyChanged(nameof(TemCategorias));
    }

    static string Traduzir(string nome) => nome switch
    {
        "System prompt" => Idioma.T("Prompt do sistema"),
        "System tools" => Idioma.T("Ferramentas do sistema"),
        "MCP tools" => Idioma.T("Ferramentas MCP"),
        "Custom agents" => Idioma.T("Agentes"),
        "Memory files" => Idioma.T("Arquivos de memória"),
        "Skills" => "Skills",
        "Messages" => Idioma.T("Mensagens"),
        "Autocompact buffer" => Idioma.T("Reserva da compactação"),
        "Free space" => Idioma.T("Espaço livre"),
        _ => nome,
    };

    [RelayCommand]
    void Comitar() => EnviarTexto(Idioma.T("Faça o commit das alterações desta sessão com uma mensagem clara, sem abrir PR."));

    [RelayCommand]
    void ComitarEPr()
    {
        _anfitriao.Mostrar(this);
        MostrarAba("pr");
    }

    [RelayCommand]
    void AlternarDetalhesAqui()
    {
        if (DetalhesSobrepostos) DetalhesAbertos = !DetalhesAbertos;
        else DetalhesVisivel = !DetalhesVisivel;
    }

    [RelayCommand]
    void FecharDetalhesSobrepostos() => DetalhesAbertos = false;
}
