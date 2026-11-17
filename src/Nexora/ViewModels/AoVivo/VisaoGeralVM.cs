using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Historico;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class VisaoGeralVM : ObservableObject
{
    readonly JanelaVM _janela;
    readonly DispatcherTimer _relogio;
    readonly Dictionary<string, SessaoForaVM> _fora = [];
    readonly Dictionary<string, (string Arquivo, DateTime Escrita, Andamento Dados)> _lidos = [];
    bool _lendo;
    int _precisam, _trabalhando, _paradas, _prontas;

    public VisaoGeralVM(JanelaVM janela)
    {
        _janela = janela;
        _relogio = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Background, async (_, _) =>
        {
            foreach (var cartao in _janela.Abertas.Concat<CartaoVM>(_fora.Values)) cartao.AtualizarTempo();
            await AtualizarForaAsync();
            await AtualizarPrsAsync();
        });
    }

    readonly Dictionary<string, (Core.Git.EstadoPr? Pr, DateTime Em)> _prs = new(StringComparer.OrdinalIgnoreCase);
    bool _buscandoPr;

    static (string Pasta, string Branch)? AlvoDoPr(CartaoVM cartao) => cartao switch
    {
        ChatVM { Pasta: { } p, Branch: var b } when Core.Git.GitHub.TemPr(b) => (p, b!),
        SessaoForaVM { Pasta: { } p, Branch: var b } when Core.Git.GitHub.TemPr(b) => (p, b!),
        _ => null,
    };

    static TimeSpan Validade(Core.Git.EstadoPr? pr) => pr switch
    {
        null => TimeSpan.FromMinutes(10),
        { Aberto: true } => TimeSpan.FromMinutes(2),
        _ => TimeSpan.FromMinutes(30),
    };

    async Task AtualizarPrsAsync()
    {
        if (_buscandoPr || JanelaVM.Demo) return;
        var agora = DateTime.UtcNow;
        var alvos = Todas.Select(c => (Cartao: c, Alvo: AlvoDoPr(c))).Where(x => x.Alvo is not null).ToList();
        foreach (var (cartao, alvo) in alvos)
            if (_prs.TryGetValue($"{alvo!.Value.Pasta}|{alvo.Value.Branch}", out var guardado)) cartao.Pr = guardado.Pr;
        if (!Pulso.Instancia.Rodando) return;
        var vencido = alvos.Select(x => x.Alvo!.Value)
            .FirstOrDefault(a => !_prs.TryGetValue($"{a.Pasta}|{a.Branch}", out var g) || agora - g.Em > Validade(g.Pr));
        if (vencido == default) return;
        _buscandoPr = true;
        try
        {
            var pr = await Core.Git.GitHub.PrDoBranchAsync(vencido.Pasta, vencido.Branch);
            _prs[$"{vencido.Pasta}|{vencido.Branch}"] = (pr, DateTime.UtcNow);
            foreach (var (cartao, alvo) in alvos.Where(x => x.Alvo == vencido)) cartao.Pr = pr;
        }
        finally
        {
            _buscandoPr = false;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Espiando), nameof(EspiadoChat), nameof(EspiadoFora))]
    public partial CartaoVM? Espiado { get; set; }

    public bool Espiando => Espiado is not null;

    [RelayCommand]
    void AbrirAtivasEmSplit() => _janela.AbrirAtivasEmSplit();
    public ChatVM? EspiadoChat => Espiado as ChatVM;
    public SessaoForaVM? EspiadoFora => Espiado as SessaoForaVM;

    [RelayCommand]
    void Espiar(CartaoVM cartao)
    {
        Soltar();
        Espiado = cartao;
        if (cartao is not SessaoForaVM fora) return;
        fora.TamanhoTrecho = 60;
        fora.Expandido = true;
        fora.Continuou += AoContinuar;
    }

    [RelayCommand]
    void FecharEspiada()
    {
        Soltar();
        Espiado = null;
    }

    void Soltar()
    {
        if (Espiado is not SessaoForaVM fora) return;
        fora.Continuou -= AoContinuar;
        fora.Expandido = false;
        fora.TamanhoTrecho = SessaoForaVM.TrechoDoCartao;
    }

    void AoContinuar(ChatVM chat)
    {
        Soltar();
        chat.Expandido = false;
        Espiado = chat;
    }

    [RelayCommand]
    void MaximizarEspiada()
    {
        var cartao = Espiado;
        FecharEspiada();
        switch (cartao)
        {
            case ChatVM chat:
                chat.MostrarCommand.Execute(null);
                break;
            case SessaoForaVM fora:
                fora.VerCommand.Execute(null);
                break;
        }
    }

    public ObservableCollection<CartaoVM> Todas { get; } = [];
    public ObservableCollection<CartaoVM> ColPrecisa { get; } = [];
    public ObservableCollection<CartaoVM> ColTrabalhando { get; } = [];
    public ObservableCollection<CartaoVM> ColProntas { get; } = [];
    public ObservableCollection<CartaoVM> ColParadas { get; } = [];
    public string ResumoTopo => Todas.Count switch
    {
        0 => Idioma.T("nenhuma sessão aberta"),
        1 => Idioma.T("1 sessão"),
        var n => Idioma.F("{0} sessões", n),
    } + (_janela.ResumoMcp is { Length: > 0 } mcp ? $"  ·  {mcp}" : "");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModoLista), nameof(QuadroColunas), nameof(VerColunas), nameof(VerFaixas))]
    public partial bool ModoQuadro { get; set; } = App.Config.Quadro || App.Config.Mesa;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QuadroColunas), nameof(VerColunas), nameof(VerFaixas))]
    public partial bool ModoMesa { get; set; } = App.Config.Mesa;

    public bool ModoLista => !ModoQuadro;
    public bool QuadroColunas => ModoQuadro && !ModoMesa;
    public bool VerColunas => !ModoMesa && !PorProjeto;
    public bool VerFaixas => !ModoMesa && PorProjeto;

    partial void OnModoQuadroChanged(bool value) => SalvarModo();

    partial void OnModoMesaChanged(bool value) => SalvarModo();

    void SalvarModo()
    {
        App.Config.Quadro = ModoQuadro;
        App.Config.Mesa = ModoMesa;
        if (!JanelaVM.Isolada) App.Config.Salvar();
    }

    [RelayCommand]
    void MostrarMesa()
    {
        ModoMesa = true;
        ModoQuadro = true;
    }

    [RelayCommand]
    void MostrarQuadro()
    {
        ModoMesa = false;
        ModoQuadro = true;
    }

    [RelayCommand]
    void MostrarLista()
    {
        ModoMesa = false;
        ModoQuadro = false;
    }

    public ObservableCollection<CartaoVM> Mesa { get; } = [];

    static int Urgencia(CartaoVM c) => c.Precisa ? 0 : c.Trabalhando ? 1 : c.Pronta ? 2 : 3;

    public string Saudacao => IssuesVM.Saudacao(DateTime.Now);

    public string ResumoMesa => Todas.Count == 0
        ? Idioma.T("Nenhuma sessão aberta. Comece uma pelo botão ao lado ou por um template.")
        : string.Join("  ·  ", new[]
        {
            _precisam == 0 ? null : _precisam == 1 ? Idioma.T("1 sessão precisa de você") : Idioma.F("{0} sessões precisam de você", _precisam),
            _trabalhando == 0 ? null : Idioma.F("{0} trabalhando", _trabalhando),
            _prontas == 0 ? null : _prontas == 1 ? Idioma.T("1 pronta para revisar") : Idioma.F("{0} prontas para revisar", _prontas),
            _precisam + _trabalhando + _prontas == 0 ? Idioma.T("tudo parado") : null,
        }.OfType<string>());

    [RelayCommand]
    void AbrirFoco() => _janela.AbrirFocoCommand.Execute(null);

    [RelayCommand]
    void EditarTemplates() => TemplatesDoApp.AbrirEditor();

    public static readonly string TodosOsProjetos = Idioma.T("Todos os projetos");
    readonly Dictionary<string, FaixaVM> _faixas = [];

    [ObservableProperty]
    public partial bool PorProjeto { get; set; } = App.Config.QuadroPorProjeto;

    partial void OnPorProjetoChanged(bool value)
    {
        OnPropertyChanged(nameof(VerColunas));
        OnPropertyChanged(nameof(VerFaixas));
        App.Config.QuadroPorProjeto = value;
        if (!JanelaVM.Isolada) App.Config.Salvar();
        Reagrupar();
    }

    [RelayCommand]
    void AlternarPorProjeto() => PorProjeto = !PorProjeto;

    [ObservableProperty]
    public partial string? FiltroProjeto { get; set; } = TodosOsProjetos;

    partial void OnFiltroProjetoChanged(string? value) => Reagrupar();

    public IReadOnlyList<string> Estados { get; } =
        [Idioma.T("Todos os estados"), Idioma.T("Precisa de você"), Idioma.T("Trabalhando"), Idioma.T("Prontas"), Idioma.T("Paradas")];

    [ObservableProperty]
    public partial int FiltroEstado { get; set; }

    partial void OnFiltroEstadoChanged(int value) => Reagrupar();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemBusca))]
    public partial string BuscaSessoes { get; set; } = "";

    public bool TemBusca => BuscaSessoes.Trim().Length > 0;

    partial void OnBuscaSessoesChanged(string value) => Reagrupar();

    [RelayCommand]
    void LimparBusca() => BuscaSessoes = "";

    public static bool Combina(CartaoVM cartao, string busca)
    {
        if (busca.Length == 0) return true;
        var texto = cartao switch
        {
            ChatVM c => $"{c.Titulo} {c.Onde} {c.UltimaAtividade} {c.PedidoInicial} {c.TextoIssue}",
            SessaoForaVM f => $"{f.Titulo} {f.Onde} {f.UltimaAtividade}",
            _ => "",
        } + " " + cartao.NomeProjeto;
        return busca.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(termo =>
            System.Globalization.CultureInfo.InvariantCulture.CompareInfo.IndexOf(texto, termo,
                System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0);
    }

    static bool NoEstado(CartaoVM c, int estado) => estado switch
    {
        1 => c.Precisa,
        2 => c.Trabalhando && !c.Precisa,
        3 => c.Pronta && !c.Trabalhando && !c.Precisa,
        4 => !c.Pronta && !c.Trabalhando && !c.Precisa,
        _ => true,
    };

    public ObservableCollection<string> Projetos { get; } = [TodosOsProjetos];
    public ObservableCollection<FaixaVM> Faixas { get; } = [];

    public string ProjetoDe(CartaoVM cartao)
    {
        var pasta = cartao switch
        {
            ChatVM chat => chat.Pasta,
            SessaoForaVM fora => fora.Pasta,
            _ => null,
        };
        return _janela.DonoDaPasta(pasta)?.Nome ?? cartao.NomeProjeto;
    }

    IReadOnlyList<CartaoVM> Coluna(int indice) => PorProjeto
        ? [.. Faixas.SelectMany(f => f.Coluna(indice))]
        : indice switch
        {
            0 => ColPrecisa,
            1 => ColTrabalhando,
            2 => ColProntas,
            _ => ColParadas,
        };

    public IReadOnlyList<CartaoVM> Ordem() => ModoMesa ? [.. Mesa] : [.. Enumerable.Range(0, 4).SelectMany(Coluna)];

    public CartaoVM? Vizinho(CartaoVM? atual, int dx, int dy) => ModoMesa
        ? NavegacaoQuadro.Passo(Mesa, atual, dx + dy)
        : NavegacaoQuadro.Vizinho([.. Enumerable.Range(0, 4).Select(Coluna)], atual, dx, dy);

    public CartaoVM? EspiarVizinho(int passo)
    {
        if (Espiado is null) return null;
        var alvo = NavegacaoQuadro.Passo(Ordem(), Espiado, passo);
        if (alvo is not null && alvo != Espiado) Espiar(alvo);
        return alvo;
    }

    [RelayCommand]
    void PedirAoOrquestrador() => _janela.MostrarOrquestraCommand.Execute(null);
    public string TextoPrecisam => _precisam == 1 ? Idioma.T("1 precisa de você") : Idioma.F("{0} precisam de você", _precisam);
    public string TextoTrabalhando => Idioma.F("{0} trabalhando", _trabalhando);
    public string TextoParadas => _paradas == 1 ? Idioma.T("1 parada") : Idioma.F("{0} paradas", _paradas);

    public bool TemPrecisam => _precisam > 0;
    public bool TemTrabalhando => _trabalhando > 0;
    public bool TemParadas => _paradas > 0;
    public bool Vazia => Todas.Count == 0;

    public void Ativar(bool ativa)
    {
        if (!ativa)
        {
            _relogio.Stop();
            return;
        }
        Reagrupar();
        _ = AtualizarForaAsync();
        _relogio.Start();
    }

    public void Reagrupar()
    {
        foreach (var tarefa in _janela.Abertas.GroupBy(c => c.Issue is { } i ? $"{i.Repositorio}#{i.Numero}" : ""))
            foreach (var chat in tarefa)
                chat.TarefaTexto = tarefa.Key.Length == 0 ? null : ChatVM.TextoDaTarefa(tarefa.Count(), tarefa.Count(c => c.Pronta && !c.Trabalhando && !c.Precisa));
        var todos = _janela.Abertas.Concat<CartaoVM>(_fora.Values.Where(f => !Escondida(f)))
            .OrderByDescending(c => c.Precisa)
            .ThenByDescending(c => c.Recencia ?? DateTime.MinValue)
            .ToList();
        Sessoes = todos;
        var projetoDe = todos.ToDictionary(c => c, ProjetoDe);
        var nomes = projetoDe.Values.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
        if (FiltroProjeto is { } escolhido && escolhido != TodosOsProjetos && !nomes.Contains(escolhido, StringComparer.OrdinalIgnoreCase)) nomes.Add(escolhido);
        Encher(Projetos, [TodosOsProjetos, .. nomes], false);
        var filtro = FiltroProjeto is { } f && f != TodosOsProjetos ? f : null;
        var busca = BuscaSessoes.Trim();
        var cartoes = todos.Where(c => (filtro is null || string.Equals(projetoDe[c], filtro, StringComparison.OrdinalIgnoreCase))
            && Combina(c, busca) && NoEstado(c, FiltroEstado)).ToList();
        MontarFaixas(cartoes, projetoDe);
        _precisam = cartoes.Count(c => c.Precisa);
        _trabalhando = cartoes.Count(c => c.Trabalhando);
        _paradas = cartoes.Count - _precisam - _trabalhando;
        _prontas = cartoes.Count(c => c.Pronta && !c.Trabalhando && !c.Precisa);
        Encher(Todas, cartoes, manterOrdem: Todas.Any(c => c.Expandido));
        Encher(Mesa, [.. cartoes.OrderBy(Urgencia).ThenByDescending(c => c.Recencia ?? DateTime.MinValue)], manterOrdem: cartoes.Any(c => c.Expandido));
        Encher(ColPrecisa, [.. cartoes.Where(c => c.Precisa)], false);
        Encher(ColTrabalhando, [.. cartoes.Where(c => c.Trabalhando && !c.Precisa)], false);
        Encher(ColProntas, [.. cartoes.Where(c => c.Pronta && !c.Trabalhando && !c.Precisa)], false);
        Encher(ColParadas, [.. cartoes.Where(c => !c.Pronta && !c.Trabalhando && !c.Precisa)], false);
        OnPropertyChanged(nameof(ResumoTopo));
        OnPropertyChanged(nameof(ResumoMesa));
        OnPropertyChanged(nameof(Saudacao));
        OnPropertyChanged(nameof(TextoPrecisam));
        OnPropertyChanged(nameof(TextoTrabalhando));
        OnPropertyChanged(nameof(TextoParadas));
        OnPropertyChanged(nameof(TemPrecisam));
        OnPropertyChanged(nameof(TemTrabalhando));
        OnPropertyChanged(nameof(TemParadas));
        OnPropertyChanged(nameof(Vazia));
        if (Espiado is not null && !cartoes.Contains(Espiado)) FecharEspiada();
        Reagrupou?.Invoke();
    }

    public IReadOnlyList<CartaoVM> Sessoes { get; private set; } = [];
    public event Action? Reagrupou;

    void MontarFaixas(List<CartaoVM> cartoes, Dictionary<CartaoVM, string> projetoDe)
    {
        if (!PorProjeto)
        {
            Faixas.Clear();
            return;
        }
        var grupos = cartoes.GroupBy(c => projetoDe[c], StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count(c => c.Precisa))
            .ThenByDescending(g => g.Max(c => c.Recencia ?? DateTime.MinValue))
            .ToList();
        var faixas = new List<FaixaVM>();
        foreach (var g in grupos)
        {
            if (!_faixas.TryGetValue(g.Key, out var faixa)) _faixas[g.Key] = faixa = new FaixaVM(g.Key);
            faixa.Atualizar([.. g]);
            faixas.Add(faixa);
        }
        foreach (var sobra in _faixas.Keys.Except(grupos.Select(g => g.Key), StringComparer.OrdinalIgnoreCase).ToList()) _faixas.Remove(sobra);
        Encher(Faixas, faixas, false);
    }

    [RelayCommand]
    void NovaSessao() => _janela.NovaSessaoCommand.Execute(null);

    public static bool Escondida(SessaoForaVM sessao) =>
        App.Config.SessoesEscondidas.TryGetValue(sessao.SessaoId, out var quando) && sessao.PodeTirar && (sessao.Recencia ?? DateTime.MinValue) <= quando;

    [RelayCommand]
    void Tirar(CartaoVM? cartao)
    {
        if (cartao is not { PodeTirar: true }) return;
        if (Espiado == cartao) FecharEspiada();
        if (cartao is ChatVM chat)
        {
            chat.FecharCommand.Execute(null);
            return;
        }
        if (cartao is not SessaoForaVM fora || fora.SessaoId.Length == 0) return;
        App.Config.SessoesEscondidas[fora.SessaoId] = DateTime.Now;
        if (!JanelaVM.Isolada) App.Config.Salvar();
        Reagrupar();
    }

    static readonly TimeSpan JanelaDasEncerradas = TimeSpan.FromMinutes(65);
    DateTime _recentesEm;
    IReadOnlyList<SessaoAtiva> _recentes = [];

    public static void LembrarDoNexora(string sessaoId)
    {
        var lista = App.Config.SessoesDoNexora;
        if (JanelaVM.Demo || sessaoId.Length == 0 || lista.Contains(sessaoId)) return;
        lista.Add(sessaoId);
        if (lista.Count > 300) lista.RemoveRange(0, lista.Count - 300);
        if (!JanelaVM.Isolada) App.Config.Salvar();
    }

    public static bool EstaCopiadaNoNexora(SessaoAtiva sessao, IReadOnlySet<string> copiadas) =>
        copiadas.Contains(sessao.SessaoId) && !sessao.Ocupada && !sessao.Esperando;

    async Task AtualizarForaAsync()
    {
        if (_lendo || JanelaVM.Demo) return;
        _lendo = true;
        try
        {
            var nossos = _janela.Abertas.Select(c => c.Pid).OfType<int>().Concat(SessaoClaude.PidsDoNexora).ToHashSet();
            var copiadas = _janela.Abertas.Select(c => c.SessaoOrigem).OfType<string>().Concat(_janela.VersoesNovas.Keys).ToHashSet();
            var doNexora = _janela.Abertas.Select(c => c.SessaoId).OfType<string>().Concat(App.Config.SessoesDoNexora).Concat(copiadas)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var procurarRecentes = DateTime.UtcNow - _recentesEm > TimeSpan.FromSeconds(30);
            if (procurarRecentes) _recentesEm = DateTime.UtcNow;
            var lidas = await Task.Run(() =>
            {
                var todas = SessoesAtivas.Listar();
                var ativas = todas.Where(s => !nossos.Contains(s.Pid) && !EstaCopiadaNoNexora(s, copiadas)).ToList();
                if (procurarRecentes) _recentes = SessoesAtivas.Recentes(JanelaDasEncerradas, doNexora.Contains);
                var vivas = todas.Select(s => s.SessaoId).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var encerradas = _recentes.Where(s => !vivas.Contains(s.SessaoId) && !doNexora.Contains(s.SessaoId));
                return ativas.Concat(encerradas).Select(s => (Sessao: s, Lido: LerAndamento(s))).ToList();
            });
            foreach (var id in _fora.Keys.Except(lidas.Select(l => l.Sessao.SessaoId)).ToList()) _fora.Remove(id);
            foreach (var (sessao, (andamento, arquivo)) in lidas)
            {
                if (!_fora.TryGetValue(sessao.SessaoId, out var cartao)) _fora[sessao.SessaoId] = cartao = new SessaoForaVM(_janela.AbrirPorSessao, _janela.ContinuarAsync);
                cartao.Atualizar(sessao, andamento, _janela.TituloDe(sessao.SessaoId), arquivo);
                if (sessao.Encerrada && !cartao.CacheValendo) _fora.Remove(sessao.SessaoId);
            }
            var sumiram = App.Config.SessoesEscondidas.Keys.Where(id => !_fora.ContainsKey(id)).ToList();
            foreach (var id in sumiram) App.Config.SessoesEscondidas.Remove(id);
            if (sumiram.Count > 0 && !JanelaVM.Isolada) App.Config.Salvar();
            Reagrupar();
        }
        finally
        {
            _lendo = false;
        }
    }

    (Andamento? Dados, string? Arquivo) LerAndamento(SessaoAtiva sessao)
    {
        var lido = _lidos.GetValueOrDefault(sessao.SessaoId);
        if ((lido.Arquivo ?? ConversasSalvas.ArquivoDe(sessao.SessaoId, sessao.Pasta)) is not { } arquivo) return (null, null);
        var escrita = File.GetLastWriteTimeUtc(arquivo);
        if (lido.Arquivo == arquivo && lido.Escrita == escrita) return (lido.Dados, arquivo);
        var dados = Andamento.Ler(arquivo, sessao.Pasta);
        _lidos[sessao.SessaoId] = (arquivo, escrita, dados);
        return (dados, arquivo);
    }

    internal static void Encher<T>(ObservableCollection<T> colecao, IReadOnlyList<T> itens, bool manterOrdem)
    {
        foreach (var sobra in colecao.Except(itens).ToList()) colecao.Remove(sobra);
        if (manterOrdem)
        {
            foreach (var novo in itens.Except(colecao).ToList()) colecao.Insert(Math.Min(itens.ToList().IndexOf(novo), colecao.Count), novo);
            return;
        }
        for (var i = 0; i < itens.Count; i++)
        {
            var atual = colecao.IndexOf(itens[i]);
            if (atual < 0) colecao.Insert(i, itens[i]);
            else if (atual != i) colecao.Move(atual, i);
        }
    }
}
