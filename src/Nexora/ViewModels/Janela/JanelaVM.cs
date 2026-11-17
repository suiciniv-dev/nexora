using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Ambientes;
using Nexora.Core.Claude;
using Nexora.Core.Git;
using Nexora.Core.Historico;
using Nexora.Core.Texto;
using Nexora.Temas;

namespace Nexora.ViewModels;

public sealed partial class JanelaVM : ObservableObject, IAnfitriao
{
    List<ConversaVM> _todas = [];

    public JanelaVM()
    {
        Busca = "";
        ModoFundo = App.Config.ModoFundo;
        LateralVisivel = App.Config.LateralVisivel;
        VisaoGeral = new VisaoGeralVM(this);
        Foco = new FocoVM(this);
        TelaGrande = new TelaGrandeVM(this);
        TemplatesDoApp.PedirEditor += Templates.Abrir;
        Orquestra = new OrquestraVM(this);
        Principal = new AreaVM(this, principal: true);
        Areas.Add(Principal);
        _ultimaArea = Principal;
        Catalogo.Mudou += AoMudarTema;
        AoMudarTema();
        Abertas.CollectionChanged += AoMudarAbertas;
        if (!Demo) _hibernacao = new Avalonia.Threading.DispatcherTimer(TimeSpan.FromSeconds(30), Avalonia.Threading.DispatcherPriority.Background, (_, _) =>
        {
            foreach (var chat in Abertas) chat.AtualizarTempo();
            foreach (var grupo in EmAndamento) grupo.Arrumar();
            AtualizarRodape();
            Hibernar();
            AnotarNoRegistro();
        });
        LigarVigia();
    }

    readonly Avalonia.Threading.DispatcherTimer? _hibernacao;

    public int Hibernar() => Abertas.ToList().Count(chat => chat.HibernarSePuder());

    public VisaoGeralVM VisaoGeral { get; }
    public FocoVM Foco { get; }
    public TelaGrandeVM TelaGrande { get; }
    public TemplatesVM Templates { get; } = new();
    public OrquestraVM Orquestra { get; }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    void AbrirFoco()
    {
        if (Foco.Aberto) Foco.FecharCommand.Execute(null);
        else Foco.Abrir();
    }
    public AreaVM Principal { get; }
    public ObservableCollection<AreaVM> Areas { get; } = [];
    public IntPtr Janela { get; set; }
    public event Action<AreaVM, Avalonia.PixelPoint?>? NovaArea;
    AreaVM _ultimaArea;
    bool _sincronizando;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemPrecisa))]
    public partial int TotalPrecisa { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemTrabalhando))]
    public partial int TotalTrabalhando { get; set; }

    public bool TemPrecisa => TotalPrecisa > 0;
    public bool TemTrabalhando => TotalTrabalhando > 0;
    public bool NaVisaoGeral => Conteudo == VisaoGeral;
    public bool NaOrquestra => Conteudo == Orquestra;
    public object? Conteudo => Principal.Conteudo;

    public AreaVM UltimaArea
    {
        get => Areas.Contains(_ultimaArea) ? _ultimaArea : Principal;
        set => _ultimaArea = value;
    }

    [ObservableProperty]
    public partial bool LateralVisivel { get; set; }

    [ObservableProperty]
    public partial bool Vertical { get; set; }

    [ObservableProperty]
    public partial bool LateralAberta { get; set; }

    public bool LateralFixa => LateralVisivel && !Vertical;
    public bool LateralSobreposta => !LateralFixa && LateralAberta;
    public bool LateralNaTela => LateralFixa || LateralSobreposta;
    public bool VeuDaLateral => !LateralFixa && LateralAberta;
    public bool MostraTrilho => !TelaCheia;
    public bool MostraTrilhoSolto => MostraTrilho && !LateralNaTela;
    public string DicaBotaoLateral => LateralFixa ? Idioma.T("Soltar a lateral: ela volta a fechar ao clicar fora") : Idioma.T("Fixar a lateral aberta ao lado do conteúdo");


    partial void OnLateralVisivelChanged(bool value)
    {
        App.Config.LateralVisivel = value;
        SalvarConfig();
        AvisarLateral();
    }

    partial void OnVerticalChanged(bool value) => AvisarLateral();

    partial void OnLateralAbertaChanged(bool value) => AvisarLateral();

    void AvisarLateral()
    {
        OnPropertyChanged(nameof(LateralFixa));
        OnPropertyChanged(nameof(LateralSobreposta));
        OnPropertyChanged(nameof(LateralNaTela));
        OnPropertyChanged(nameof(VeuDaLateral));
        OnPropertyChanged(nameof(MostraTrilho));
        OnPropertyChanged(nameof(MostraTrilhoSolto));
        OnPropertyChanged(nameof(DicaBotaoLateral));
        if (LateralNaTela) TelaGrande?.Fechar();
    }

    public void MostrarLateral()
    {
        if (Vertical) LateralAberta = true;
        else LateralVisivel = true;
    }

    [RelayCommand]
    void AlternarLateral()
    {
        if (LateralFixa)
        {
            LateralVisivel = false;
            return;
        }
        LateralAberta = !LateralAberta;
    }

    [RelayCommand]
    void FixarLateral()
    {
        if (Vertical)
        {
            LateralAberta = !LateralAberta;
            return;
        }
        var fixar = !LateralVisivel;
        LateralAberta = false;
        LateralVisivel = fixar;
    }

    [RelayCommand]
    void FecharLateralSobreposta() => LateralAberta = false;

    [RelayCommand]
    void MostrarVisaoGeral() => Navegar(VisaoGeral);

    [RelayCommand]
    void MostrarOrquestra() => Navegar(Orquestra);

    public void Mostrar(ChatVM chat) => Navegar(chat);

    readonly Dictionary<string, ExecutarVM> _execucoes = new(StringComparer.OrdinalIgnoreCase);

    public ExecutarVM ExecutarDe(Local local)
    {
        if (!_execucoes.TryGetValue(local.Pasta, out var painel)) _execucoes[local.Pasta] = painel = new ExecutarVM(local, url => AbrirNoNavegadorDoNexora(local, url));
        return painel;
    }

    public void MostrarExecutar(Local local)
    {
        var painel = ExecutarDe(local);
        painel.CarregarOndeRoda();
        Navegar(painel);
    }

    void AbrirNoNavegadorDoNexora(Local local, string url)
    {
        var chat = Abertas.FirstOrDefault(c => Workspaces.MesmaPasta(c.Pasta, local.Pasta)) ?? Atual ?? Abertas.FirstOrDefault();
        if (chat is null)
        {
            NavegadorVM.AbrirFora?.Invoke(url);
            return;
        }
        Mostrar(chat);
        chat.AbrirNavegador(url);
    }

    public IReadOnlyList<ServidorNoAr> ServidoresNoAr(Local local)
    {
        ExecutarDe(local);
        var lista = new List<ServidorNoAr>();
        foreach (var (pasta, painel) in _execucoes.OrderByDescending(e => Workspaces.MesmaPasta(e.Key, local.Pasta)))
        {
            var doProjeto = Workspaces.MesmaPasta(pasta, local.Pasta);
            foreach (var alvo in painel.Alvos)
            {
                if (alvo.Endereco is { } url) lista.Add(new ServidorNoAr(alvo.Nome, url, painel.Local.Nome, true));
                else if (doProjeto && alvo.TemPorta && int.TryParse(alvo.Porta, out var porta) && porta is > 0 and < 65536)
                    lista.Add(new ServidorNoAr(alvo.Nome, $"http://localhost:{porta}/", painel.Local.Nome, false));
            }
        }
        return [.. lista.DistinctBy(s => s.Url).OrderByDescending(s => s.NoAr)];
    }

    public IReadOnlyList<string> UsosDe(string pasta)
    {
        var usos = Abertas.Where(c => Workspaces.MesmaPasta(c.Pasta, pasta)).Select(c => Idioma.F("a sessão \"{0}\"", c.Titulo)).ToList();
        if (_execucoes.TryGetValue(pasta, out var painel) && painel.Rodando > 0) usos.Add(Idioma.T("o que está rodando no painel Executar"));
        return usos;
    }

    public void Esquecer(string pasta)
    {
        if (AmbienteDe(pasta) is { } ambiente)
        {
            Ambientes.Remove(ambiente);
            FecharGuias(ambiente);
        }
        if (_execucoes.Remove(pasta, out var painel)) FecharGuias(painel);
        foreach (var chave in App.Config.Portas.Keys.Where(k => k.StartsWith(pasta, StringComparison.OrdinalIgnoreCase)).ToList()) App.Config.Portas.Remove(chave);
        App.Config.Workspaces.RemoveAll(w => w.StartsWith(pasta, StringComparison.OrdinalIgnoreCase));
        SalvarConfig();
    }

    public void Navegar(object conteudo)
    {
        LateralAberta = false;
        TelaGrande.Fechar();
        if (AreaVM.EhPagina(conteudo))
        {
            foreach (var area in Areas.Where(a => !a.Principal))
                if (area.Achar(conteudo) is { } velha) FecharGuia(velha);
            Principal.MostrarPagina(conteudo);
            Principal.Focar();
            return;
        }
        if (Areas.Select(a => a.Achar(conteudo)).FirstOrDefault(g => g is not null) is { } guia)
        {
            guia.Area.Pagina = null;
            guia.Area.Selecionada = guia;
        }
        else
        {
            UltimaArea.Pagina = null;
            guia = UltimaArea.Abrir(conteudo);
        }
        guia.Area.Focar();
        if (guia.Area != Principal) Avalonia.Threading.Dispatcher.UIThread.Post(() => AoSelecionar(Principal));
    }

    public void AoSelecionar(AreaVM area)
    {
        if (area == Principal)
        {
            _sincronizando = true;
            Atual = Principal.Conteudo as ChatVM;
            AmbienteAtual = Principal.Conteudo as AmbienteVM;
            _sincronizando = false;
            OnPropertyChanged(nameof(Conteudo));
            OnPropertyChanged(nameof(NaVisaoGeral));
            OnPropertyChanged(nameof(NaOrquestra));
            AoTrocarConteudo();
        }
        VisaoGeral.Ativar(TelaGrande is { Aberta: true } || Areas.Any(a => a.Conteudo == VisaoGeral));
        switch (area.Conteudo)
        {
            case AmbienteVM ambiente:
                _ = ambiente.Atualizar();
                break;
            case OrquestraVM orquestra:
                orquestra.Preparar();
                break;
        }
    }

    public AreaVM NovaAreaVazia()
    {
        var area = new AreaVM(this, principal: false);
        Areas.Add(area);
        return area;
    }

    public void RemoverArea(AreaVM area)
    {
        if (area == Principal || !Areas.Remove(area)) return;
        foreach (var guia in area.Guias) guia.Desligar();
        area.Guias.Clear();
        VisaoGeral.Ativar(Areas.Any(a => a.Conteudo == VisaoGeral));
    }

    public void FecharGuia(GuiaVM guia)
    {
        var area = guia.Area;
        area.Soltar(guia);
        guia.Desligar();
        if (!area.Principal && area.Guias.Count == 0) area.Fechar();
    }

    void FecharGuias(object conteudo)
    {
        foreach (var guia in Areas.Select(a => a.Achar(conteudo)).OfType<GuiaVM>().ToList()) FecharGuia(guia);
    }

    public void Substituir(object antigo, object novo)
    {
        if (Areas.Select(a => a.Achar(antigo)).FirstOrDefault(g => g is not null) is not { } guia)
        {
            Navegar(novo);
            return;
        }
        FecharGuias(novo);
        guia.Conteudo = novo;
        guia.Area.Selecionada = guia;
        AoSelecionar(guia.Area);
    }

    public void Destacar(GuiaVM guia, Avalonia.PixelPoint? onde)
    {
        if (!guia.Area.Principal && guia.Area.Guias.Count == 1) return;
        var area = NovaAreaVazia();
        NovaArea?.Invoke(area, onde);
        Levar(guia, area, null);
    }

    public void Levar(GuiaVM guia, AreaVM destino, int? posicao)
    {
        var origem = guia.Area;
        if (origem == destino)
        {
            if (posicao is { } p) destino.Mover(guia, p);
            return;
        }
        origem.Soltar(guia);
        destino.Receber(guia, posicao);
        destino.Selecionada = guia;
        destino.Focar();
        if (!origem.Principal && origem.Guias.Count == 0) origem.Fechar();
    }

    public object? PorChave(string chave) => chave switch
    {
        "visao" => VisaoGeral,
        "orquestra" => Orquestra,
        "temas" => PaginaConfiguracoes,
        "projetos" => PaginaProjetos,
        "recursos" => Recursos,
        "uso" => PaginaUso,
        "dashboard" => PaginaDashboard,
        "issues" => PaginaIssues,
        "agendados" => PaginaAgendados,
        "configuracoes" => PaginaConfiguracoes,
        _ when chave.StartsWith("executar|", StringComparison.Ordinal) && chave["executar|".Length..] is var pasta && Directory.Exists(pasta) => ExecutarDe(LocalDe(pasta)),
        _ => null,
    };

    public string? EmOutroLugar(string sessaoId)
    {
        var nossos = Abertas.Select(c => c.Pid).OfType<int>().Concat(SessaoClaude.PidsDoNexora).ToHashSet();
        return SessoesAtivas.Listar().FirstOrDefault(s => s.SessaoId == sessaoId && !nossos.Contains(s.Pid))?.OrigemLegivel;
    }

    public IReadOnlyList<ConversaVM> ConversasDe(string pasta) =>
        _todas.Where(c => c.Dados.Pasta is { } p && Workspaces.MesmaPasta(p, pasta)).Take(6).ToList();

    public string? TituloDe(string sessaoId) => _todas.FirstOrDefault(c => c.Dados.SessaoId == sessaoId)?.Titulo;

    public IReadOnlyDictionary<string, string> VersoesNovas { get; private set; } = new Dictionary<string, string>();

    string VersaoAtual(string sessaoId)
    {
        for (var i = 0; i < 10 && VersoesNovas.TryGetValue(sessaoId, out var nova); i++) sessaoId = nova;
        return sessaoId;
    }

    public void AbrirPorSessao(string sessaoId, string? pasta)
    {
        sessaoId = VersaoAtual(sessaoId);
        if (AbertaPorSessao(sessaoId) is { } aberta)
        {
            Navegar(aberta);
            return;
        }
        if (ConversaDe(sessaoId, pasta) is { } dados) Navegar(ChatDe(dados));
    }

    public async Task<ChatVM?> ContinuarAsync(string sessaoId, string? pasta, string texto)
    {
        sessaoId = VersaoAtual(sessaoId);
        var chat = AbertaPorSessao(sessaoId) ?? (ConversaDe(sessaoId, pasta) is { } dados ? ChatDe(dados) : null);
        if (chat is null) return null;
        await chat.Carregamento;
        chat.EnviarTexto(texto);
        return chat;
    }

    ChatVM? AbertaPorSessao(string sessaoId) =>
        Abertas.FirstOrDefault(a => a.SessaoId == sessaoId) ?? Abertas.FirstOrDefault(a => a.SessaoOrigem == sessaoId);

    ConversaSalva? ConversaDe(string sessaoId, string? pasta) =>
        _todas.FirstOrDefault(c => c.Dados.SessaoId == sessaoId)?.Dados
        ?? (ConversasSalvas.ArquivoDe(sessaoId, pasta) is { } arquivo ? ConversasSalvas.Resumir(new FileInfo(arquivo)) : null)
        ?? (Demo || Isolada ? null : ConversasDosAgentes.Achar(sessaoId)?.ParaSalva());

    void AoMudarAbertas(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var chat in e.OldItems?.OfType<ChatVM>() ?? []) chat.PropertyChanged -= AoMudarChat;
        foreach (var chat in e.NewItems?.OfType<ChatVM>() ?? []) chat.PropertyChanged += AoMudarChat;
        Contar();
    }

    void AoMudarChat(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatVM.PermissaoPendente))
        {
            VisaoGeral.AtualizarFila();
            return;
        }
        if (e.PropertyName != nameof(ChatVM.Estado) || sender is not ChatVM chat) return;
        Contar();
        if (Isolada || Areas.Any(a => a.Ativa && a.Conteudo == chat)) return;
        var detalhe = chat.UltimaAtividade ?? chat.Onde;
        switch (chat.Estado)
        {
            case EstadoChat.PrecisaDeVoce when chat.PermissaoPendente is { MostraBotoes: true } pedido:
                Avisos.Notificar(Idioma.F("{0} pede permissão", chat.Titulo), pedido.Detalhe is { Length: > 0 } d ? $"{pedido.Titulo}\n{Resumos.UmaLinha(d, 140)}" : pedido.Titulo,
                    chat.Codigo, AoClicarNoAviso,
                    [(Idioma.T("Permitir"), $"permitir|{chat.Codigo}|{pedido.Pedido.PedidoId}"), (Idioma.T("Negar"), $"negar|{chat.Codigo}|{pedido.Pedido.PedidoId}")]);
                Avisos.Piscar(Janela);
                break;
            case EstadoChat.PrecisaDeVoce:
                Avisar(Idioma.F("{0} precisa de você", chat.Titulo), detalhe, chat);
                break;
            case EstadoChat.Pronta when chat.EstadoAnterior is EstadoChat.Trabalhando or EstadoChat.PrecisaDeVoce:
                Avisar(Idioma.F("{0} terminou", chat.Titulo), detalhe, chat);
                break;
            case EstadoChat.Erro:
                Avisar(Idioma.F("{0} parou com erro", chat.Titulo), detalhe, chat);
                break;
        }
    }

    void Contar()
    {
        TotalPrecisa = Abertas.Count(c => c.Precisa);
        TotalTrabalhando = Abertas.Count(c => c.Trabalhando);
        OnPropertyChanged(nameof(TextoAtividade));
        VisaoGeral.Reagrupar();
        VisaoGeral.AtualizarFila();
        Reagrupar();
    }

    void Avisar(string titulo, string texto, ChatVM chat)
    {
        Avisos.Notificar(titulo, texto, chat.Codigo, AoClicarNoAviso);
        Avisos.Piscar(Janela);
    }

    void AoClicarNoAviso(string argumento) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
    {
        if (argumento.Split('|') is [var acao and ("permitir" or "negar"), var codigo, var pedidoId])
        {
            var item = Abertas.FirstOrDefault(c => c.Codigo == codigo)?.Itens.OfType<ItemPermissao>()
                .FirstOrDefault(p => p.Pedido.PedidoId == pedidoId && p.MostraBotoes);
            if (item is null) return;
            if (acao == "permitir") item.PermitirCommand.Execute(null);
            else item.NegarCommand.Execute(null);
            return;
        }
        AbrirPeloAviso(argumento);
    });

    readonly HashSet<string> _liberadas = [];

    public bool TemLiberadas => _liberadas.Count > 0;
    public string TextoLiberadas => _liberadas.Count == 1
        ? Idioma.T("1 permissão vale para todas as sessões até fechar o Nexora.")
        : Idioma.F("{0} permissões valem para todas as sessões até fechar o Nexora.", _liberadas.Count);

    public bool LiberadaEmTodas(PermissaoPedida pedido) => _liberadas.Contains(ChaveDePermissao.De(pedido.Ferramenta, pedido.Entrada));

    public void LiberarEmTodas(string chave)
    {
        if (!_liberadas.Add(chave)) return;
        foreach (var chat in Abertas.ToList()) chat.AplicarLiberacao(chave);
        OnPropertyChanged(nameof(TemLiberadas));
        OnPropertyChanged(nameof(TextoLiberadas));
        VisaoGeral.AtualizarFila();
    }

    [RelayCommand]
    void EsquecerLiberadas()
    {
        _liberadas.Clear();
        OnPropertyChanged(nameof(TemLiberadas));
        OnPropertyChanged(nameof(TextoLiberadas));
    }

    public void AbrirSessaoDaIssue(Local local, string pedido, Core.Git.IssueDaSessao? issue, string? corpo = null)
    {
        var chat = CriarSessao(local);
        chat.ComecarComTemplate(pedido, corpo);
        if (issue is not null) chat.VincularIssue(issue);
        Navegar(chat);
        if (!Isolada)
        {
            App.Config.UltimaPasta = local.Pasta;
            App.Config.Salvar();
        }
    }

    public ChatVM? SessaoDoPr(Core.Git.PrLigado pr) =>
        Abertas.FirstOrDefault(c => c.Pr?.Url is { Length: > 0 } url && url.Equals(pr.Url, StringComparison.OrdinalIgnoreCase))
        ?? Abertas.FirstOrDefault(c => pr.Branch.Length > 0 && pr.Branch.Equals(c.Branch, StringComparison.Ordinal) && (c.Pr is null || c.Pr.Numero == pr.Numero));

    public bool AbrirPrDaSessao(Core.Git.PrLigado pr)
    {
        if (SessaoDoPr(pr) is not { } chat) return false;
        Navegar(chat);
        chat.MostrarAbaCommand.Execute("pr");
        return true;
    }

    public void AbrirPeloAviso(string codigo)
    {
        if (Abertas.FirstOrDefault(c => c.Codigo == codigo) is { } chat) Navegar(chat);
        else Principal.Focar();
    }

    public static bool Isolada { get; set; }
    public static bool Demo { get; set; }

    public ObservableCollection<AmbienteVM> Ambientes { get; } = [];
    public ObservableCollection<ChatVM> Abertas { get; } = [];
    public ObservableCollection<ConversaVM> ConversasVisiveis { get; } = [];

    [ObservableProperty]
    public partial ChatVM? Atual { get; set; }

    [ObservableProperty]
    public partial AmbienteVM? AmbienteAtual { get; set; }

    [ObservableProperty]
    public partial string Busca { get; set; }

    [ObservableProperty]
    public partial bool CarregandoConversas { get; set; }

    [ObservableProperty]
    public partial bool CarregandoAmbientes { get; set; }

    partial void OnAtualChanged(ChatVM? oldValue, ChatVM? newValue)
    {
        if (oldValue is not null) oldValue.EmFoco = false;
        if (newValue is not null) newValue.EmFoco = true;
        if (!_sincronizando && newValue is not null) Navegar(newValue);
    }

    partial void OnAmbienteAtualChanged(AmbienteVM? value)
    {
        if (!_sincronizando && value is not null) Navegar(value);
    }

    partial void OnBuscaChanged(string value)
    {
        Filtrar();
        if (BuscaAberta) MontarResultados();
        else if (value.Length > 0) MostrarLateral();
    }

    public Task CarregarAsync() => Task.WhenAll(CarregarAmbientesAsync(), CarregarConversasAsync(), LerPlanoAsync());

    public async Task CarregarAmbientesAsync()
    {
        CarregandoAmbientes = true;
        var lista = await Task.Run(() =>
        {
            var achados = Workspaces.ProcurarProjetos(App.Config.Raizes).ToList();
            foreach (var arquivo in App.Config.Workspaces)
                if (Workspaces.Ler(arquivo) is { } extra && !achados.Any(a => a.Arquivo is not null && Workspaces.MesmaPasta(a.Principal, extra.Principal)))
                {
                    achados.RemoveAll(a => a.Arquivo is null && new[] { extra.Principal }.Concat(extra.Vinculados).Any(p => Workspaces.MesmaPasta(p, a.Principal)));
                    achados.Add(extra);
                }
            return achados
                .Where(a => !App.Config.Ocultos.Any(o => Workspaces.MesmaPasta(o, a.Principal)))
                .OrderBy(a => a.Nome, StringComparer.OrdinalIgnoreCase)
                .ToList();
        });
        Ambientes.Clear();
        foreach (var a in lista) Ambientes.Add(new AmbienteVM(a, this));
        OnPropertyChanged(nameof(Ocultos));
        CarregandoAmbientes = false;
        Orquestra.Preparar();
        Reagrupar();
        MontarHistorico();
        _paginaProjetos?.Atualizar();
    }

    public async Task CarregarConversasAsync()
    {
        CarregandoConversas = true;
        var desfeitos = new Dictionary<string, string>(App.Config.TurnosDesfeitos);
        var doClaude = await App.Nucleo.ListarConversasAsync();
        var (lista, antigas) = await Task.Run(() =>
        {
            var dosAgentes = Demo || Isolada ? [] : ConversasDosAgentes.Listar().Select(c => c.ParaSalva());
            var todas = doClaude.Concat(dosAgentes).OrderByDescending(c => c.Atualizada).ToList();
            var existentes = todas.Select(c => c.SessaoId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return (todas, VersoesDeConversa.ComDesfeitos(VersoesDeConversa.Antigas(todas), desfeitos, existentes));
        });
        VersoesNovas = antigas;
        _todas = lista.Where(c => !antigas.ContainsKey(c.SessaoId) && !ChatVM.EhIdLateral(c.SessaoId)).Select(NovaConversa).ToList();
        Filtrar();
        CarregandoConversas = false;
        if (NaVisaoGeral) VisaoGeral.Ativar(true);
        foreach (var chat in Abertas.Where(c => c.Vazia)) chat.AtualizarRecentes();
    }

    public void EncerrarTudo()
    {
        GravarRegistro();
        foreach (var chat in Abertas) chat.Encerrar(esperar: true);
        ChatVM.EsperarParadasNaSaida(TimeSpan.FromSeconds(4));
        foreach (var painel in _execucoes.Values) painel.PararTudo();
        App.Hub?.Dispose();
        Orquestra.Dispose();
    }

    [RelayCommand]
    void NovaSessao() => NovaSessaoEm(Principal);

    public void NovaSessaoEm(AreaVM area)
    {
        UltimaArea = area;
        var local = area.Conteudo switch
        {
            AmbienteVM a => a.Local,
            ChatVM c => c.LocalAtual,
            _ => App.Config.UltimaPasta is { } ultima && Directory.Exists(ultima)
                ? LocalDe(ultima)
                : Ambientes.FirstOrDefault()?.Local ?? LocalDe(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
        };
        AbrirSessao(local);
    }

    TemasVM? _temas;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemVeu))]
    public partial string FundoAnimado { get; set; } = Nexora.Fundos.Papeis.Nenhum;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemImagem), nameof(TemVeu))]
    public partial Avalonia.Media.Imaging.Bitmap? ImagemFundo { get; set; }

    [ObservableProperty]
    public partial double Escurecer { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemVeu))]
    public partial string ModoFundo { get; set; }

    public bool TemImagem => ImagemFundo is not null;
    public bool TemVeu => TemImagem || Nexora.Fundos.Papeis.Encontrar(FundoAnimado) is not null && ModoFundo == "animado";

    public void MudarModoFundo(string modo)
    {
        ModoFundo = modo;
        App.Config.ModoFundo = modo;
        SalvarConfig();
        Catalogo.Modo = modo;
        Catalogo.Aplicar(Catalogo.Atual);
        PedirReinicio(modo);
    }
    string? _imagemCarregada;

    [RelayCommand]
    void MostrarTemas()
    {
        MostrarConfiguracoesCommand.Execute(null);
        PaginaConfiguracoes.IrPara(ConfiguracoesVM.SecaoAparencia);
    }

    public TemasVM GaleriaDeTemas => _temas ??= new TemasVM(UsarTema);

    public void EditarTemaAtual()
    {
        MostrarTemas();
        GaleriaDeTemas.NovoTemaCommand.Execute(null);
    }

    void UsarTema(string id)
    {
        App.Config.Tema = id;
        SalvarConfig();
    }

    void AoMudarTema()
    {
        var tema = Catalogo.Atual;
        FundoAnimado = tema.FundoAnimado;
        Escurecer = tema.Escurecer;
        if (tema.Imagem == _imagemCarregada) return;
        _imagemCarregada = tema.Imagem;
        try
        {
            ImagemFundo = tema.Imagem is { } caminho && File.Exists(caminho) ? new Avalonia.Media.Imaging.Bitmap(caminho) : null;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        {
            ImagemFundo = null;
        }
    }

    [RelayCommand]
    Task Atualizar() => CarregarAsync();

    public ChatVM CriarSessao(Local local)
    {
        var chat = ChatVM.Nova(local, this, AoUsar, Fechar);
        Abertas.Insert(0, chat);
        return chat;
    }

    public void AbrirSessao(Local local, string? primeiraMensagem = null)
    {
        var chat = CriarSessao(local);
        Navegar(chat);
        if (!Isolada)
        {
            App.Config.UltimaPasta = local.Pasta;
            App.Config.Salvar();
        }
        if (primeiraMensagem is null) return;
        chat.Rascunho = primeiraMensagem;
        chat.EnviarCommand.Execute(null);
    }

    public bool Ocupada(string pasta) => Abertas.Any(c => c.Trabalhando && Workspaces.MesmaPasta(c.Pasta, pasta));

    public void BranchMudou(string pasta)
    {
        foreach (var chat in Abertas.Where(c => Workspaces.MesmaPasta(c.Pasta, pasta))) chat.AtualizarBranch();
        if (AmbienteDe(pasta) is { } ambiente) ambiente.Branch = Repositorio.BranchAtual(ambiente.Principal);
    }

    public AmbienteVM Registrar(Ambiente ambiente)
    {
        if (AmbienteDe(ambiente.Principal) is { } existente) return existente;
        var novo = new AmbienteVM(ambiente, this);
        Ambientes.Add(novo);
        Reagrupar();
        return novo;
    }

    public IReadOnlyList<string> Ocultos => App.Config.Ocultos;

    public void Esconder(AmbienteVM ambiente)
    {
        if (!App.Config.Ocultos.Any(o => Workspaces.MesmaPasta(o, ambiente.Principal))) App.Config.Ocultos.Add(ambiente.Principal);
        SalvarConfig();
        Ambientes.Remove(ambiente);
        FecharGuias(ambiente);
        OnPropertyChanged(nameof(Ocultos));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemExclusao))]
    public partial ExclusaoVM? Exclusao { get; set; }

    public bool TemExclusao => Exclusao is not null;

    public void PedirExclusao(AmbienteVM ambiente) => Exclusao = new ExclusaoVM(ambiente, this);

    public void ConcluirExclusao(AmbienteVM ambiente, bool conversasApagadas)
    {
        Exclusao = null;
        var principal = ambiente.Principal;
        var arquivoFica = ambiente.Arquivo is { } arquivo && File.Exists(arquivo);
        if (arquivoFica && Directory.Exists(principal)) Esconder(ambiente);
        else
        {
            App.Config.Ocultos.RemoveAll(o => Workspaces.MesmaPasta(o, principal));
            if (arquivoFica || Directory.Exists(principal)) App.Config.Ocultos.Add(principal);
            if (!arquivoFica && ambiente.Arquivo is { } a) App.Config.Workspaces.RemoveAll(w => string.Equals(w, a, StringComparison.OrdinalIgnoreCase));
            if (!Directory.Exists(principal)) App.Config.BasesDasTarefas.Remove(Config.ChaveDePasta(principal));
            Esquecer(principal);
            OnPropertyChanged(nameof(Ocultos));
            Reagrupar();
        }
        if (conversasApagadas) _ = CarregarConversasAsync();
    }

    public void Mostrar(string principal)
    {
        App.Config.Ocultos.RemoveAll(o => Workspaces.MesmaPasta(o, principal));
        SalvarConfig();
        _ = CarregarAmbientesAsync();
    }

    public void Lembrar(Ambiente ambiente)
    {
        if (ambiente.Arquivo is not { } arquivo) return;
        var achavel = App.Config.Raizes.Any(r => Workspaces.MesmaPasta(Path.GetDirectoryName(arquivo) ?? "", r)
            || Workspaces.MesmaPasta(Path.GetDirectoryName(Path.GetDirectoryName(arquivo) ?? "") ?? "", r));
        if (achavel || App.Config.Workspaces.Any(w => string.Equals(w, arquivo, StringComparison.OrdinalIgnoreCase))) return;
        App.Config.Workspaces.Add(arquivo);
        SalvarConfig();
    }

    void SalvarConfig()
    {
        if (!Isolada) App.Config.Salvar();
    }

    [RelayCommand]
    void NovoAmbiente() =>
        Navegar(Areas.SelectMany(a => a.Guias).Select(g => g.Conteudo).OfType<NovoAmbienteVM>().FirstOrDefault() ?? new NovoAmbienteVM(this));

    public AmbienteVM? AmbienteDe(string pasta) => Ambientes.FirstOrDefault(a => Workspaces.MesmaPasta(a.Principal, pasta));

    public IReadOnlyList<Local> Locais() => Ambientes.Select(a => a.Local).ToList();

    public Local LocalDe(string pasta) =>
        AmbienteDe(pasta)?.Local ?? new Local(Path.GetFileName(pasta.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : pasta, pasta, []);

    void Filtrar()
    {
        var busca = Busca.Trim();
        ConversasVisiveis.Clear();
        foreach (var c in _todas.Where(c => c.Combina(busca)).Take(150)) ConversasVisiveis.Add(c);
        MontarHistorico();
    }

    void Abrir(ConversaVM conversa)
    {
        if (OrquestraVM.EhOrquestrador(conversa.Dados.SessaoId) && Orquestra.Retomar(conversa.Dados.SessaoId)) return;
        Navegar(ChatDe(conversa.Dados));
    }

    public ChatVM? ChatDaSessao(string sessaoId, string? pasta) =>
        Abertas.FirstOrDefault(a => a.SessaoId == sessaoId) ?? (ConversaDe(sessaoId, pasta) is { } dados ? ChatDe(dados) : null);

    ChatVM ChatDe(ConversaSalva dados)
    {
        if (Abertas.FirstOrDefault(a => a.SessaoId == dados.SessaoId) is { } aberta) return aberta;
        var pasta = dados.Pasta ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var chat = ChatVM.DeConversa(dados, LocalDe(pasta), this, AoUsar, Fechar);
        Abertas.Insert(0, chat);
        return chat;
    }

    void Fechar(ChatVM chat)
    {
        Abertas.Remove(chat);
        chat.Encerrar();
        chat.ApagarRastro();
        FecharGuias(chat);
        if (!Isolada && !Demo && !Abertas.Any(c => c.Trabalhando)) _ = Task.Run(() => Core.Recursos.ServidoresDeBuild.DesligarAsync());
    }


    public async Task AbrirAoCarregarAsync(int indice)
    {
        while (CarregandoConversas || _todas.Count == 0) await Task.Delay(50);
        if (indice < ConversasVisiveis.Count) ConversasVisiveis[indice].AbrirCommand.Execute(null);
    }

    public async Task AbrirAmbienteAoCarregarAsync(string nome)
    {
        while (CarregandoAmbientes || Ambientes.Count == 0) await Task.Delay(50);
        AmbienteAtual = Ambientes.FirstOrDefault(a => a.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase)) ?? Ambientes[0];
    }

    public void PreencherDemo()
    {
        static JsonElement J(string json) => JsonDocument.Parse(json).RootElement.Clone();
        var raiz = @"D:\Projetos";
        var loja = new Ambiente("Loja", $@"{raiz}\Loja", [$@"{raiz}\Loja.Api", $@"{raiz}\Loja.Componentes"], null);
        Registrar(loja);
        Registrar(new Ambiente("Loja-pedidos", $@"{raiz}\Loja-pedidos", [$@"{raiz}\Loja.Api"], null));
        Registrar(new Ambiente("App.Notas", $@"{raiz}\App.Notas", [$@"{raiz}\Loja.Componentes"], null));

        var agora = DateTime.Now;
        _todas =
        [
            new(new ConversaSalva("a", "", Idioma.T("Cupom de desconto no checkout"), loja.Principal, "cupom-checkout", agora.AddMinutes(-4)), Abrir),
            new(new ConversaSalva("b", "", Idioma.T("Erro na tela de pedidos"), $@"{raiz}\Loja-pedidos", "erro-tela-pedidos", agora.AddHours(-2)), Abrir),
            new(new ConversaSalva("c", "", Idioma.T("Notas offline"), $@"{raiz}\App.Notas", "notas-offline", agora.AddDays(-1)), Abrir),
        ];
        Filtrar();
        (PlanoSessao, PlanoSemana) = (0.04, 0.35);
        Novidade = new VersaoNova(new Version(0, 3, 1), "v0.3.1", "", new Dictionary<string, string>());

        var chat = ChatVM.Nova(LocalDe(loja.Principal), this, AoUsar, Fechar);
        chat.Titulo = Idioma.T("Cupom de desconto no checkout");
        chat.Itens.Add(new ItemUsuario(Idioma.T("Revise o cupom de desconto do checkout e me diga o que falta para fechar a issue 48.")));
        var pensou = new ItemPensamento(DateTime.Now.AddSeconds(-7)) { Tokens = 2140 };
        pensou.Encerrar();
        chat.Itens.Add(pensou);
        SubagentesDemo(chat, loja.Principal);
        chat.Branch = "cupom-checkout";
        chat.Itens.Add(new ItemTexto("""
            Olhei a issue e o código do `CupomService`. A base está pronta e o desconto já aparece no carrinho, mas faltam **três coisas** antes de fechar a 48.

            **O que falta**

            - **Cupom vencido:** hoje o serviço lança exceção quando o cupom passou da validade; o certo é devolver uma falha que a tela consiga mostrar.
            - **Limite por cliente:** o mesmo cupom pode ser usado duas vezes no mesmo pedido se a pessoa clicar rápido.
            - **Teste de concorrência:** não há teste cobrindo dois pedidos ao mesmo tempo, que é exatamente o caso do item anterior.

            ```csharp
            if (cupom.Vencido(agora))
                return Resultado.Falha("Este cupom venceu");
            ```
            """));
        var lida = new ItemFerramenta("Read", @"src\Loja.Application\Cupons\CupomService.cs");
        lida.Concluir("namespace Loja.Application.Cupons;", false);
        var exploracao = new ItemExploracao(lida);
        var busca = new ItemFerramenta("Grep", @"Vencido  em src\Loja.Domain");
        busca.Concluir(@"src\Loja.Domain\Cupons\Cupom.cs:18:    public bool Vencido(DateTime agora) => agora > Validade;", false);
        exploracao.Juntar(busca);
        var dominio = new ItemFerramenta("Read", @"src\Loja.Domain\Cupons\Cupom.cs");
        dominio.Concluir("namespace Loja.Domain.Cupons;", false);
        exploracao.Juntar(dominio);
        chat.Itens.Add(exploracao);
        var testes = new ItemFerramenta("Bash", "dotnet test tests/Loja.Application.Tests --filter Cupom");
        testes.Concluir("Com falha: 1, Aprovado: 41\n  CupomServiceTests.CupomVencido_DevolveFalha [FAIL]", true);
        chat.Itens.Add(testes);
        chat.Itens.Add(new ItemPermissao(
            new PermissaoPedida("p1", "Bash", J("""{"command":"dotnet ef database update --project src/Loja.Infra"}"""),
                Idioma.T("Aplica a migration"), Idioma.T("O comando altera o banco de dados local."), J("""[{"type":"addRules"}]""")),
            loja.Principal, (_, _) => { }));
        var testador = chat.Subagentes.First(a => a.Id == "demo-teste");
        chat.Itens.Add(new ItemPermissao(
            new PermissaoPedida("p1b", "Bash", J("""{"command":"dotnet test tests/Loja.Application.Tests --filter Concorrencia"}"""),
                Idioma.T("Roda os testes de concorrência"), null, J("""[{"type":"addRules"}]"""), "demo-teste", "t3"),
            loja.Principal, (_, _) => { }) { Subagente = Idioma.F("Subagent {0}: {1}", testador.Tipo, testador.Descricao) });
        chat.Estado = EstadoChat.PrecisaDeVoce;

        var outra = ChatVM.Nova(LocalDe($@"{raiz}\Loja-pedidos"), this, AoUsar, Fechar);
        outra.Titulo = Idioma.T("Erro na tela de pedidos");
        outra.Branch = "erro-tela-pedidos";
        outra.Itens.Add(new ItemPermissao(
            new PermissaoPedida("p2", "AskUserQuestion", J($$"""
                {"questions":[{"question":"{{Idioma.T("A lista de pedidos deve abrir mostrando só os de hoje ou todos?")}}","header":"{{Idioma.T("Filtro")}}","multiSelect":false,
                  "options":[{"label":"{{Idioma.T("Só os de hoje")}}","description":"{{Idioma.T("Mais rápido para quem atende no balcão")}}"},{"label":"{{Idioma.T("Todos")}}","description":"{{Idioma.T("Como é hoje")}}"}]}]}
                """), null, null, null),
            $@"{raiz}\Loja-pedidos", (_, _) => { }));
        outra.PermissaoPendente = outra.Itens.OfType<ItemPermissao>().LastOrDefault();
        outra.Estado = EstadoChat.PrecisaDeVoce;
        var nova = ChatVM.Nova(LocalDe($@"{raiz}\App.Notas"), this, AoUsar, Fechar);
        nova.Titulo = Idioma.T("Notas offline");
        nova.Branch = "notas-offline";

        chat.PermissaoPendente = chat.Itens.OfType<ItemPermissao>().LastOrDefault();
        (chat.Modelo, chat.ContextoTokens, chat.JanelaContexto, chat.CacheLonga, chat.UltimaResposta) = ("Opus 5.5", 84_000L, 200_000L, true, DateTimeOffset.Now.AddMinutes(-4));
        (outra.Modelo, outra.ContextoTokens, outra.JanelaContexto, outra.CacheLonga, outra.UltimaResposta) = ("Sonnet 5.5", 171_000L, 200_000L, true, DateTimeOffset.Now.AddMinutes(-53));
        (nova.Modelo, nova.ContextoTokens, nova.JanelaContexto, nova.CacheLonga, nova.UltimaResposta) = ("Opus 5.5", 41_000L, 1_000_000L, true, DateTimeOffset.Now.AddMinutes(-75));
        chat.UltimaAtividade = Idioma.T("Permitir Bash: dotnet ef database update --project src/Loja.Infra");
        chat.AtividadeEm = DateTime.Now.AddMinutes(-3);
        outra.UltimaAtividade = Idioma.T("O agente tem uma pergunta");
        outra.AtividadeEm = DateTime.Now.AddSeconds(-20);
        outra.TurnoDesde = DateTime.Now.AddMinutes(-7);
        nova.UltimaAtividade = Idioma.T("Pronto: as notas agora sincronizam offline e o teste novo passou.");
        nova.AtividadeEm = DateTime.Now.AddMinutes(-12);
        nova.Estado = EstadoChat.Pronta;
        TurnosDemo(nova);
        chat.Pr = PrsDemo.Detalhe(52, chat.Branch)?.ComoEstado();
        outra.Pr = PrsDemo.Detalhe(50, outra.Branch)?.ComoEstado();

        Abertas.Add(chat);
        Abertas.Add(outra);
        Abertas.Add(nova);

        var chefe = ChatVM.Nova(LocalDe(loja.Principal), this, AoUsar, Fechar);
        chefe.Titulo = Idioma.T("Orquestrador");
        chefe.Branch = "dev";
        chefe.Itens.Add(new ItemUsuario(Idioma.T("Feche o cupom do checkout, corrija a tela de pedidos e termine as notas offline do app.")));
        chefe.Itens.Add(new ItemTexto(Idioma.T("Dividi em três frentes, cada agente no seu worktree. Vou esperar os três e aviso quando der para revisar.")));
        chefe.Estado = EstadoChat.Trabalhando;
        chefe.TurnoDesde = DateTime.Now.AddMinutes(-9);
        chefe.UltimaAtividade = "esperar  s1, s2, s3";
        chefe.AtividadeEm = DateTime.Now.AddSeconds(-40);
        (chefe.Modelo, chefe.ContextoTokens, chefe.JanelaContexto, chefe.CacheLonga, chefe.UltimaResposta) = ("Opus 5.5", 23_000L, 200_000L, true, DateTimeOffset.Now.AddMinutes(-1));
        Abertas.Add(chefe);
        Orquestra.Demo(chefe, [(chat, "Loja"), (outra, "Loja"), (nova, null)]);
        chat.Issue = new Core.Git.IssueDaSessao(48, "voce/loja", "https://github.com/voce/loja/issues/48", Idioma.T("Cupom vencido ainda aplica desconto"));
        nova.Issue = new Core.Git.IssueDaSessao(55, "voce/app-notas", "https://github.com/voce/app-notas/issues/55", Idioma.T("Notas offline"));
        nova.PreencherContagem([
            new("src/Notas/Sincronizacao.cs", 86, 12, false, false),
            new("src/Notas/FilaOffline.cs", 64, 0, false, true),
            new("tests/Notas.Testes/SincronizacaoTestes.cs", 41, 0, false, true),
            new("src/Notas/Armazenamento.cs", 9, 7, false, false),
            new("README.md", 4, 1, false, false),
        ]);
        Atual = chat;
    }

    public void SegredoDemo()
    {
        if (Abertas.FirstOrDefault(c => c.Titulo == Idioma.T("Notas offline")) is not { } chat) return;
        App.Segredos.Cofre.GuardarVariavel("STRIPE_SECRET_KEY", @"D:\Projetos\Loja", "demo");
        App.Segredos.Cofre.RegistrarArquivo("DATABASE_URL", @"D:\Projetos\Loja-pedidos", ".env.local");
        chat.MostrarPedidoDeSegredo(new Core.Segredos.PedidoDeSegredo("demo", "OPENAI_API_KEY",
            Idioma.T("Para o resumo automático das notas chamar a API da OpenAI nos testes de integração."), Core.Segredos.DestinoDoSegredo.ArquivoEnv, chat.Pasta, ".env"));
        chat.UltimaAtividade = Idioma.F("O agente pede um segredo: {0}", "OPENAI_API_KEY");
        Atual = chat;
    }

    public ConversaSalva? ConversaLonga { get; private set; }

    public void AdicionarConversaLonga(int turnos)
    {
        var arquivo = Path.Combine(Path.GetTempPath(), "nexora-demo", $"longa-{turnos}.jsonl");
        TranscricaoDeExemplo.Escrever(arquivo, turnos);
        ConversaLonga = new ConversaSalva("longa", arquivo, Idioma.T("Conversa longa"), @"D:\Projetos\Loja", "conversa-longa", DateTime.Now.AddMinutes(-1));
        _todas.Insert(0, new(ConversaLonga, Abrir));
        Filtrar();
    }

    public ChatVM AbrirConversaLonga(bool mostrar)
    {
        var chat = ChatDe(ConversaLonga! with { SessaoId = $"longa-{Abertas.Count}-{Environment.TickCount64}" });
        if (mostrar) Navegar(chat);
        return chat;
    }

    static void TurnosDemo(ChatVM chat)
    {
        var pedido = new ItemUsuario(Idioma.T("Faça as notas funcionarem sem internet e sincronizarem quando a conexão voltar."));
        chat.Itens.Add(pedido);
        chat.Itens.Add(new ItemTexto(Idioma.T("Criei uma fila local para as notas feitas sem internet e a sincronização envia a fila quando a conexão volta. O teste novo cobre a nota editada nos dois lados.")));
        chat.Itens.Add(ItemAviso.Rodape(new TurnoEncerrado(false, null, null, 134_000), "Opus 5.5"));
        chat.AdicionarTurnoDemo(pedido, [
            new("src/Notas/Sincronizacao.cs", "M", 86, 12, false),
            new("src/Notas/FilaOffline.cs", "A", 64, 0, false),
            new("tests/Notas.Testes/SincronizacaoTestes.cs", "A", 41, 0, false),
            new("src/Notas/Armazenamento.cs", "M", 9, 7, false),
        ]);
        chat.Itens.Add(ItemAviso.Troca(Idioma.F("Esforço: {0}", Idioma.T("Alto")), "esforco"));
        var ajuste = new ItemUsuario(Core.Texto.Citacoes.Montar(Idioma.T("O teste novo cobre a nota editada nos dois lados."), Idioma.T("Mostre no README como testar sem internet.")));
        chat.Itens.Add(ajuste);
        chat.Itens.Add(new ItemTexto(Idioma.T("Pronto: as notas agora sincronizam offline e o teste novo passou.")));
        chat.Itens.Add(ItemAviso.Rodape(new TurnoEncerrado(false, null, null, 21_000), "Opus 5.5"));
        chat.AdicionarTurnoDemo(ajuste, [new("README.md", "M", 4, 1, false)]);
    }

    static void SubagentesDemo(ChatVM chat, string pasta)
    {
        static JsonElement J(string json) => JsonDocument.Parse(json).RootElement.Clone();
        string C(string relativo) => JsonEncodedText.Encode(Path.Combine(pasta, relativo)).ToString();
        chat.Aplicar(new FerramentaChamada("demo-explore", "Agent", J("""
            {"description":"Mapear onde o cupom é validado","subagent_type":"Explore",
             "prompt":"Ache todos os pontos do checkout que validam ou aplicam cupom de desconto. Para cada um, diga arquivo, método e o que acontece quando o cupom venceu."}
            """)));
        chat.Aplicar(new FerramentaChamada("demo-teste", "Agent", J("""
            {"description":"Escrever o teste de concorrência do cupom","subagent_type":"general-purpose","model":"sonnet","run_in_background":true,
             "prompt":"Escreva um teste que aplique o mesmo cupom em dois pedidos ao mesmo tempo e confira que só um recebe o desconto. Rode os testes no fim."}
            """)));
        (string Pai, string Id, string Nome, string Entrada, string? Saida)[] passos =
        [
            ("demo-explore", "e1", "Grep", """{"pattern":"Cupom","path":"src"}""", "src/Loja.Application/Cupons/CupomService.cs\nsrc/Loja.Api/Controllers/CheckoutController.cs\nsrc/Loja.Domain/Pedidos/Pedido.cs"),
            ("demo-explore", "e2", "Read", $$"""{"file_path":"{{C(@"src\Loja.Application\Cupons\CupomService.cs")}}"}""", "namespace Loja.Application.Cupons;"),
            ("demo-explore", "e3", "Read", $$"""{"file_path":"{{C(@"src\Loja.Api\Controllers\CheckoutController.cs")}}"}""", "namespace Loja.Api.Controllers;"),
            ("demo-explore", "e4", "Grep", """{"pattern":"AplicarDesconto","path":"src"}""", "src/Loja.Domain/Pedidos/Pedido.cs:88"),
            ("demo-explore", "e5", "Read", $$"""{"file_path":"{{C(@"src\Loja.Domain\Pedidos\Pedido.cs")}}"}""", "namespace Loja.Domain.Pedidos;"),
            ("demo-explore", "e6", "Glob", """{"pattern":"tests/**/*Cupom*.cs"}""", "tests/Loja.Application.Tests/CupomServiceTests.cs"),
            ("demo-explore", "e7", "Read", $$"""{"file_path":"{{C(@"tests\Loja.Application.Tests\CupomServiceTests.cs")}}"}""", "namespace Loja.Application.Tests;"),
            ("demo-teste", "t1", "Read", $$"""{"file_path":"{{C(@"tests\Loja.Application.Tests\CupomServiceTests.cs")}}"}""", "namespace Loja.Application.Tests;"),
            ("demo-teste", "t2", "Write", $$"""{"file_path":"{{C(@"tests\Loja.Application.Tests\CupomConcorrenciaTests.cs")}}","content":"public class CupomConcorrenciaTests\n{\n}\n"}""", "Arquivo criado."),
            ("demo-teste", "t3", "Bash", """{"command":"dotnet test tests/Loja.Application.Tests --filter Concorrencia"}""", null),
        ];
        foreach (var (pai, id, nome, entrada, saida) in passos)
        {
            chat.Aplicar(new EventoDeSubagente(pai, new FerramentaChamada(id, nome, J(entrada))));
            if (saida is not null) chat.Aplicar(new EventoDeSubagente(pai, new FerramentaRespondeu(id, saida, false)));
        }
        chat.Aplicar(new FerramentaRespondeu("demo-explore", """
            O cupom passa por **três pontos**:

            1. `CheckoutController.Aplicar` recebe o código e chama o serviço.
            2. `CupomService.Validar` confere validade e limite, mas **lança exceção** quando o cupom venceu.
            3. `Pedido.AplicarDesconto` grava o desconto sem checar se o cupom já entrou no mesmo pedido.

            Os testes ficam em `tests/Loja.Application.Tests/CupomServiceTests.cs` e não cobrem dois pedidos ao mesmo tempo.
            """, false, new ResumoDeSubagente("completed", null, "Explore", 7, 41_000, 18_400)));
        chat.Aplicar(new FerramentaRespondeu("demo-teste", "Async agent launched successfully.", false, new ResumoDeSubagente("async_launched", null, null, null, null, null)));
        if (chat.Subagentes.FirstOrDefault(a => a.Id == "demo-teste") is { } segundoPlano) segundoPlano.Inicio = DateTime.Now.AddSeconds(-134);
    }
}
