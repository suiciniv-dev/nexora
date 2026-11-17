using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Ambientes;
using Nexora.Core.Git;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class JanelaVM
{
    ChatVM? _ultimoChat;
    RecursosVM? _recursos;
    ProjetosVM? _paginaProjetos;
    bool _hubLigado;

    BoasVindasVM? _boasVindas;

    public RecursosVM Recursos => _recursos ??= new RecursosVM(this);
    UsoVM? _paginaUso;
    public UsoVM PaginaUso => _paginaUso ??= new UsoVM(this);
    public bool NaUso => Ativo is UsoVM;

    [RelayCommand]
    void MostrarUso() => Navegar(PaginaUso);

    IssuesVM? _paginaIssues;
    public IssuesVM PaginaIssues => _paginaIssues ??= new IssuesVM(this);
    public bool NaIssues => Ativo is IssuesVM;

    [RelayCommand]
    void MostrarIssues() => Navegar(PaginaIssues);
    public BoasVindasVM BoasVindas => _boasVindas ??= new BoasVindasVM(this);

    [ObservableProperty]
    public partial bool MostraBoasVindas { get; set; }

    public event Action? PedirApresentacao;

    [RelayCommand]
    public void AbrirBoasVindas()
    {
        BoasVindas.Reiniciar();
        if (PedirApresentacao is { } pedir) pedir();
        else MostraBoasVindas = true;
    }

    public AmbienteVM? ImportarWorkspace(string arquivo)
    {
        if (Workspaces.Ler(arquivo) is not { } ambiente) return null;
        var vm = Registrar(ambiente);
        Lembrar(ambiente);
        return vm;
    }

    public static string PastaDeProjetos => Path.Combine(
        Isolada ? Path.GetTempPath() : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nexora", "Projetos");

    public AmbienteVM? AdicionarPastaComoProjeto(string pasta)
    {
        if (!Directory.Exists(pasta)) return null;
        var limpa = Path.GetFullPath(pasta).TrimEnd('\\', '/');
        App.Config.Ocultos.RemoveAll(o => Workspaces.MesmaPasta(o, limpa));
        if (AmbienteDe(limpa) is { } existente)
        {
            SalvarConfig();
            return existente;
        }
        var arquivo = WorkspaceDentro(limpa) ?? CriarWorkspace(limpa);
        var vm = arquivo is null ? null : ImportarWorkspace(arquivo);
        SalvarConfig();
        return vm;
    }

    static string? WorkspaceDentro(string pasta)
    {
        try
        {
            return Directory.EnumerateFiles(pasta, "*.code-workspace")
                .Order(StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(a => Workspaces.Ler(a) is { } lido && Workspaces.MesmaPasta(lido.Principal, pasta));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    static string? CriarWorkspace(string pasta)
    {
        var nome = Path.GetFileName(pasta) is { Length: > 0 } n ? n : "projeto";
        var arquivo = Path.Combine(PastaDeProjetos, nome + ".code-workspace");
        for (var i = 2; File.Exists(arquivo) && Workspaces.Ler(arquivo) is { } existente && !Workspaces.MesmaPasta(existente.Principal, pasta); i++)
            arquivo = Path.Combine(PastaDeProjetos, $"{nome} ({i}).code-workspace");
        try
        {
            return Workspaces.Salvar(new Ambiente(nome, pasta, [], arquivo)).Arquivo;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public Task AdicionarRaiz(string pasta)
    {
        if (!App.Config.Raizes.Any(r => Workspaces.MesmaPasta(r, pasta))) App.Config.Raizes.Add(pasta);
        SalvarConfig();
        return CarregarAmbientesAsync();
    }

    public void RemoverRaiz(string pasta)
    {
        App.Config.Raizes.RemoveAll(r => Workspaces.MesmaPasta(r, pasta));
        SalvarConfig();
        _ = CarregarAmbientesAsync();
    }
    public ProjetosVM PaginaProjetos => _paginaProjetos ??= new ProjetosVM(this);
    public ObservableCollection<GrupoProjetoVM> EmAndamento { get; } = [];
    public ObservableCollection<GrupoHistoricoVM> Historico { get; } = [];
    public ObservableCollection<ConversaVM> Fixadas { get; } = [];
    public bool TemFixadas => Fixadas.Count > 0;
    public bool TemEmAndamento => EmAndamento.Count > 0;
    public event Action? PedirBusca;

    object? Ativo => Areas.Count == 0 ? null : Principal.Conteudo;
    public bool NaProjetos => Ativo is ProjetosVM;
    public bool NaRecursos => Ativo is RecursosVM;

    [ObservableProperty]
    public partial string ResumoMcp { get; set; } = "";

    public bool HubLigado => App.Hub is not null;

    public string TextoAtividade => (TotalTrabalhando, TotalPrecisa) switch
    {
        (0, 0) => "",
        (var t, 0) => t == 1 ? Idioma.T("1 trabalhando") : Idioma.F("{0} trabalhando", t),
        (0, var p) => p == 1 ? Idioma.T("1 precisa de você") : Idioma.F("{0} precisam de você", p),
        var (t, p) => p == 1 ? Idioma.F("{0} trabalhando · 1 precisa de você", t) : Idioma.F("{0} trabalhando · {1} precisam de você", t, p),
    };

    [RelayCommand]
    void MostrarChat(ChatVM chat) => Navegar(chat);

    [RelayCommand]
    void MostrarProjetos()
    {
        PaginaProjetos.Atualizar();
        Navegar(PaginaProjetos);
    }

    [RelayCommand]
    void MostrarRecursos() => Navegar(Recursos);

    [RelayCommand]
    void Procurar()
    {
        Busca = "";
        BuscaAberta = true;
        MontarResultados();
        PedirBusca?.Invoke();
    }

    [RelayCommand]
    void AbrirPainelDaSessao(string aba)
    {
        if ((Ativo as ChatVM ?? _ultimoChat) is not { } chat) return;
        if (Ativo != chat) Navegar(chat);
        chat.MostrarAbaCommand.Execute(aba);
    }

    public void AbrirEmSplit(object conteudo)
    {
        LateralAberta = false;
        if (Areas.Select(a => a.Achar(conteudo)).OfType<GuiaVM>().FirstOrDefault() is { } existente && existente.Area != UltimaArea)
        {
            Navegar(conteudo);
            return;
        }
        var area = UltimaArea;
        var guia = area.Achar(conteudo) ?? area.Receber(new GuiaVM(conteudo, area));
        if (area.Selecionada is null) area.Selecionada = guia;
        else area.Dividir(guia);
        area.Focar();
    }

    [RelayCommand]
    public void AbrirAtivasEmSplit()
    {
        var ativas = Abertas.Where(c => !c.EhChefe)
            .OrderByDescending(c => c.Precisa).ThenByDescending(c => c.Trabalhando)
            .Take(AreaVM.MaxPaineis).ToList();
        if (ativas.Count == 0) return;
        if (ativas.Count == 1)
        {
            Navegar(ativas[0]);
            return;
        }
        LateralAberta = false;
        var area = UltimaArea;
        var guias = ativas.Select(c => area.Achar(c) ?? area.Receber(new GuiaVM(c, area))).ToList();
        area.DefinirDivisao(guias);
        area.Focar();
    }

    [RelayCommand]
    void PerguntaLateral()
    {
        if ((Ativo as ChatVM ?? _ultimoChat) is { } chat) AbrirLateral(chat, null);
    }

    public void AbrirLateral(ChatVM origem, string? pergunta)
    {
        var chat = ChatVM.Lateral(origem, this, AoUsar, Fechar);
        Abertas.Insert(0, chat);
        var guia = Areas.Select(a => a.Achar(origem)).OfType<GuiaVM>().FirstOrDefault();
        var area = guia?.Area ?? UltimaArea;
        if (guia is not null && area.Selecionada != guia && !area.Divisao.Contains(guia)) area.Selecionada = guia;
        UltimaArea = area;
        area.AbrirAoLado(() => Navegar(chat));
        if (pergunta is null)
        {
            chat.FocarCampo();
            return;
        }
        chat.Rascunho = pergunta;
        chat.EnviarCommand.Execute(null);
    }

    [RelayCommand]
    void AlternarDetalhes()
    {
        if (Ativo is ChatVM chat) chat.AlternarDetalhesAquiCommand.Execute(null);
    }

    [RelayCommand]
    void ExecutarAtivo()
    {
        var local = Ativo switch
        {
            ChatVM c => c.LocalAtual,
            AmbienteVM a => a.Local,
            _ => _ultimoChat?.LocalAtual ?? Ambientes.FirstOrDefault()?.Local,
        };
        if (local is not null) MostrarExecutar(local);
    }

    void AoTrocarConteudo()
    {
        if (Ativo is ChatVM chat) _ultimoChat = chat;
        else if (_ultimoChat is not null && !Abertas.Contains(_ultimoChat)) _ultimoChat = null;
        OnPropertyChanged(nameof(NaProjetos));
        OnPropertyChanged(nameof(NaRecursos));
        OnPropertyChanged(nameof(NaUso));
        OnPropertyChanged(nameof(NaDashboard));
        OnPropertyChanged(nameof(NaIssues));
        OnPropertyChanged(nameof(NaAgendados));
        OnPropertyChanged(nameof(NaConfiguracoes));
        _recursos?.Ativar(Areas.Any(a => a.Conteudo == _recursos));
        if (Ativo is UsoVM uso) uso.Ativar(true);
        _paginaDashboard?.Ativar(Ativo is DashboardVM);
        if (Ativo is AgendadosVM agendados) agendados.Recarregar();
        if (Ativo is IssuesVM issues) issues.Ativar(true);
        if (Ativo is ProjetosVM projetos) projetos.Atualizar();
    }

    void LigarHub()
    {
        if (_hubLigado || App.Hub is not { } hub) return;
        _hubLigado = true;
        hub.Mudou += () => Dispatcher.UIThread.Post(ResumirHub);
        ResumirHub();
    }

    void ResumirHub()
    {
        if (App.Hub is not { } hub)
        {
            ResumoMcp = "";
            return;
        }
        var estado = hub.Estado();
        var ativos = estado.Count(e => e.Ativo);
        ResumoMcp = ativos switch
        {
            0 => Idioma.T("MCPs parados"),
            1 => Idioma.T("1 MCP ativo"),
            _ => Idioma.F("{0} MCPs ativos", ativos),
        };
    }

    public AmbienteVM? DonoDaPasta(string? pasta)
    {
        if (string.IsNullOrEmpty(pasta)) return null;
        AmbienteVM? melhor = null;
        foreach (var a in Ambientes)
        {
            var principal = a.Principal.TrimEnd('\\', '/');
            var dentro = Workspaces.MesmaPasta(principal, pasta)
                || pasta.StartsWith(principal + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || pasta.StartsWith(principal + '/', StringComparison.OrdinalIgnoreCase);
            if (dentro && (melhor is null || principal.Length > melhor.Principal.TrimEnd('\\', '/').Length)) melhor = a;
        }
        return melhor;
    }

    public int SessoesDe(AmbienteVM ambiente) => Abertas.Count(c => DonoDaPasta(c.Pasta) == ambiente);

    readonly Dictionary<string, string?> _principaisDosWorktrees = new(StringComparer.OrdinalIgnoreCase);

    AmbienteVM? ProjetoDaLateral(string? pasta)
    {
        var dono = DonoDaPasta(pasta);
        if (dono is null) return null;
        if (!_principaisDosWorktrees.TryGetValue(dono.Principal, out var principal))
            _principaisDosWorktrees[dono.Principal] = principal = Repositorio.PrincipalDoWorktree(dono.Principal);
        if (principal is null) return dono;
        return Ambientes.FirstOrDefault(a => Workspaces.MesmaPasta(a.Principal.TrimEnd('\\', '/'), principal.TrimEnd('\\', '/'))) ?? dono;
    }

    void Reagrupar()
    {
        LigarHub();
        var grupos = new List<GrupoProjetoVM>();
        foreach (var chat in Abertas)
        {
            var dono = ProjetoDaLateral(chat.Pasta);
            var nome = dono?.Nome ?? Idioma.T("Outras pastas");
            var grupo = grupos.FirstOrDefault(g => g.Ambiente == dono && g.Nome == nome);
            if (grupo is null)
            {
                grupo = EmAndamento.FirstOrDefault(g => g.Ambiente == dono && g.Nome == nome) ?? new GrupoProjetoVM(this, dono, nome);
                grupo.Sessoes.Clear();
                grupos.Add(grupo);
            }
            grupo.Sessoes.Add(chat);
        }
        foreach (var g in grupos) g.Avisar();
        EmAndamento.Clear();
        foreach (var g in grupos) EmAndamento.Add(g);
        OnPropertyChanged(nameof(TemEmAndamento));
        AvisarListaVazia();
        _paginaProjetos?.Atualizar();
    }

    public bool TemHistorico => Historico.Count > 0;
    public bool SemConversas => !CarregandoConversas && !TemFixadas && !TemEmAndamento && !TemHistorico;
    public bool SemConversasNenhuma => SemConversas && Busca.Trim().Length == 0;
    public bool SemConversasNaBusca => SemConversas && Busca.Trim().Length > 0;

    void AvisarListaVazia()
    {
        OnPropertyChanged(nameof(TemHistorico));
        OnPropertyChanged(nameof(SemConversas));
        OnPropertyChanged(nameof(SemConversasNenhuma));
        OnPropertyChanged(nameof(SemConversasNaBusca));
    }

    partial void OnCarregandoConversasChanged(bool value) => AvisarListaVazia();

    void MontarHistorico()
    {
        Historico.Clear();
        var busca = Busca.Trim();
        foreach (var grupo in _todas.Where(c => c.Combina(busca)).Take(400)
                     .GroupBy(c => DonoDaPasta(c.Dados.Pasta)?.Nome ?? c.Pasta)
                     .OrderByDescending(g => g.Max(c => c.Dados.Atualizada)))
            Historico.Add(new GrupoHistoricoVM(grupo.Key, [.. grupo]));
        Fixadas.Clear();
        foreach (var c in _todas.Where(c => c.Fixada)) Fixadas.Add(c);
        OnPropertyChanged(nameof(TemFixadas));
        AvisarListaVazia();
    }

    void AlternarFixada(ConversaVM conversa)
    {
        conversa.Fixada = !conversa.Fixada;
        App.Config.Fixadas.Remove(conversa.Dados.SessaoId);
        if (conversa.Fixada) App.Config.Fixadas.Add(conversa.Dados.SessaoId);
        SalvarConfig();
        MontarHistorico();
    }

    ConversaVM NovaConversa(Core.Historico.ConversaSalva dados) =>
        new(dados, Abrir, AlternarFixada, c => AbrirEmSplit(ChatDe(c.Dados))) { Fixada = App.Config.Fixadas.Contains(dados.SessaoId) };
}

public sealed partial class ProjetosVM(JanelaVM janela) : ObservableObject
{
    public ObservableCollection<CartaoProjetoVM> Cartoes { get; } = [];
    public bool Vazio => Cartoes.Count == 0 && !janela.CarregandoAmbientes;
    public bool TemRaizes => App.Config.Raizes.Count > 0;
    public string TextoRaizes => Idioma.F("O Nexora procurou em: {0}", string.Join(", ", App.Config.Raizes));

    public void Atualizar()
    {
        Cartoes.Clear();
        foreach (var a in janela.Ambientes) Cartoes.Add(new CartaoProjetoVM(janela, a));
        OnPropertyChanged(nameof(Vazio));
        OnPropertyChanged(nameof(TemRaizes));
        OnPropertyChanged(nameof(TextoRaizes));
    }

    public void AdicionarPasta(string pasta)
    {
        if (janela.AdicionarPastaComoProjeto(pasta) is null) return;
        Atualizar();
    }

    public Task AdicionarRaiz(string pasta) => janela.AdicionarRaiz(pasta);

    public void Importar(string arquivo)
    {
        if (janela.ImportarWorkspace(arquivo) is null) return;
        Atualizar();
    }

    [RelayCommand]
    void NovoProjeto() => janela.NovoAmbienteCommand.Execute(null);
}

public sealed partial class CartaoProjetoVM(JanelaVM janela, AmbienteVM ambiente)
{
    public AmbienteVM Ambiente { get; } = ambiente;
    public string Nome => Ambiente.Nome;
    public string Iniciais => Ambiente.Iniciais;
    public bool Destacado => janela.Destacado(Ambiente);
    public string Branch => Ambiente.BranchCurta;
    public string Vinculados => Ambiente.TemVinculados ? Ambiente.Resumo : Idioma.T("sem projetos vinculados");
    public string Sessoes => janela.SessoesDe(Ambiente) switch
    {
        0 => Idioma.T("nenhuma sessão aberta"),
        1 => Idioma.T("1 sessão aberta"),
        var n => Idioma.F("{0} sessões abertas", n),
    };
    public bool TemSessoes => janela.SessoesDe(Ambiente) > 0;
    public string Ultima => janela.ConversasDe(Ambiente.Principal).FirstOrDefault() is { } c ? $"{c.Titulo} · {c.Quando}" : Idioma.T("sem conversas ainda");

    [RelayCommand]
    void Abrir() => janela.Navegar(Ambiente);

    [RelayCommand]
    void NovaSessao() => janela.AbrirSessao(Ambiente.Local);

    [RelayCommand]
    void Excluir() => janela.PedirExclusao(Ambiente);
}
