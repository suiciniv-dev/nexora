using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Nexora.Core.Painel;
using Nexora.Core.Texto;
using Nexora.Core.Uso;
using Nexora.Views;

namespace Nexora.ViewModels;

public sealed partial class LinkDoPainelVM(string titulo, string detalhe, string? link, string? selo = null, string? estado = null) : ObservableObject
{
    public string Titulo { get; } = titulo;
    public string Detalhe { get; } = detalhe;
    public string? Link { get; } = link;
    public string? Selo { get; } = selo;
    public string? Estado { get; } = estado;
    public bool TemSelo => !string.IsNullOrEmpty(Selo);
    public bool Mergeado => Estado == "merged";
    public bool Aberto => Estado == "open";
    public bool Fechado => Estado == "closed";

    [RelayCommand]
    void Abrir()
    {
        if (string.IsNullOrWhiteSpace(Link)) return;
        try
        {
            Sistema.Abrir(new ProcessStartInfo(Link) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }
}

public sealed record DiaDaSemanaVM(string Nome, bool Hoje);

public sealed record LegendaVM(string Texto, IBrush Cor);

public sealed record CartaoDePlanoVM(string Nome, string Icone, string Numero, string Detalhe, string Renova, IReadOnlyList<double> Linha, IBrush Cor, string RotuloDaLinha);

public sealed partial class DashboardVM : ObservableObject
{
    const int DiasDeUso = 30;

    readonly JanelaVM _janela;
    readonly DispatcherTimer _relogio;
    IReadOnlyList<Parcela> _porDia = [];
    IReadOnlyList<string>? _instalados;
    DateTime _usoLidoEm = DateTime.MinValue;
    DateTime _noticiasLidasEm = DateTime.MinValue;
    DateTime _gitLidoEm = DateTime.MinValue;
    DateTime _gitHubLidoEm = DateTime.MinValue;
    string? _gitDe;
    bool _lendoUso, _lendoGit, _lendoNoticias, _lendoGitHub;

    public DashboardVM(JanelaVM janela)
    {
        _janela = janela;
        _relogio = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, (_, _) => Atualizar(false));
        _janela.RegistroMudou += () => Dispatcher.UIThread.Post(MontarTempo);
        _janela.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(JanelaVM.PlanoSessao) or nameof(JanelaVM.PlanoSemana) or nameof(JanelaVM.PlanoCodex) or nameof(JanelaVM.PlanoRenova))
                MontarPlanos();
        };
        _janela.Abertas.CollectionChanged += (_, _) => MontarSessoes();
        _janela.Ambientes.CollectionChanged += (_, _) => EscolherProjeto();
    }

    public JanelaVM Janela => _janela;
    public ObservableCollection<AmbienteVM> Projetos => _janela.Ambientes;
    public ObservableCollection<PedidoDaFilaVM> Fila => _janela.VisaoGeral.Fila;
    public ObservableCollection<ChatVM> EmAndamento { get; } = [];
    public ObservableCollection<ConversaVM> Recentes { get; } = [];
    public ObservableCollection<LinkDoPainelVM> MeusPrs { get; } = [];
    public ObservableCollection<LinkDoPainelVM> ParaRevisar { get; } = [];
    public ObservableCollection<LinkDoPainelVM> Commits { get; } = [];
    public ObservableCollection<LinkDoPainelVM> Noticias { get; } = [];
    public ObservableCollection<DiaDaSemanaVM> Semana { get; } = [];
    public ObservableCollection<CartaoDePlanoVM> Planos { get; } = [];
    public ObservableCollection<LegendaVM> LegendasDeUso { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ComecarCommand))]
    public partial string Pedido { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NomeDoProjeto), nameof(BranchDoProjeto), nameof(TemProjeto), nameof(PastaDoProjeto), nameof(ProjetoFixo), nameof(DicaDoFixo))]
    public partial AmbienteVM? Projeto { get; set; }

    public bool TemProjeto => Projeto is not null;
    public string NomeDoProjeto => Projeto?.Nome ?? Idioma.T("um projeto novo");
    public string BranchDoProjeto => Projeto?.BranchCurta ?? "";
    public string PastaDoProjeto => Projeto?.Principal ?? "";
    public bool ProjetoFixo => Projeto is not null && App.Config.ProjetoDoDashboard is { } fixo && Core.Ambientes.Workspaces.MesmaPasta(fixo, Projeto.Principal);
    public string DicaDoFixo => ProjetoFixo
        ? Idioma.T("O Dashboard sempre abre neste projeto. Clique para voltar a abrir no último usado.")
        : Idioma.T("Sempre abrir o Dashboard neste projeto");

    [ObservableProperty] public partial string TextoMudancas { get; set; } = "";
    [ObservableProperty] public partial string Mais { get; set; } = "";
    [ObservableProperty] public partial string Menos { get; set; } = "";

    [ObservableProperty] public partial int ColunasDosPlanos { get; set; } = 1;

    [ObservableProperty] public partial IReadOnlyList<int> Contribuicoes { get; set; } = [];
    [ObservableProperty] public partial DateTime InicioDasContribuicoes { get; set; } = DateTime.Today;
    [ObservableProperty] public partial string TextoAtividade { get; set; } = "";
    [ObservableProperty] public partial bool SemGitHub { get; set; }
    [ObservableProperty] public partial string LoginDoGitHub { get; set; } = "";

    [ObservableProperty] public partial IReadOnlyList<SerieDoGrafico> SeriesDeUso { get; set; } = [];
    [ObservableProperty] public partial string TextoUso { get; set; } = "";
    [ObservableProperty] public partial string InicioDoUso { get; set; } = "";
    [ObservableProperty] public partial string MeioDoUso { get; set; } = "";
    [ObservableProperty] public partial string FimDoUso { get; set; } = "";

    [ObservableProperty] public partial string TempoAtivo { get; set; } = "–";
    [ObservableProperty] public partial string TempoAgentes { get; set; } = "–";
    [ObservableProperty] public partial string TextoTurnos { get; set; } = "";
    [ObservableProperty] public partial IReadOnlyList<double> SemanaAgentes { get; set; } = [];
    [ObservableProperty] public partial IReadOnlyList<double> SemanaResto { get; set; } = [];

    [ObservableProperty] public partial bool SemMeusPrs { get; set; }
    [ObservableProperty] public partial bool TemParaRevisar { get; set; }
    [ObservableProperty] public partial bool SemCommits { get; set; }
    [ObservableProperty] public partial bool SemNoticias { get; set; }
    [ObservableProperty] public partial string TextoNoticias { get; set; } = "";

    public bool TemFila => Fila.Count > 0;
    public bool TemAndamento => EmAndamento.Count > 0;
    public bool NadaPendente => !TemFila && !TemAndamento;
    public bool TemRecentes => Recentes.Count > 0;

    public string Saudacao => DateTime.Now.Hour switch
    {
        < 5 => Idioma.T("Boa noite"),
        < 12 => Idioma.T("Bom dia"),
        < 18 => Idioma.T("Boa tarde"),
        _ => Idioma.T("Boa noite"),
    };

    public void Ativar(bool ligada)
    {
        if (!ligada)
        {
            _relogio.Stop();
            return;
        }
        Fila.CollectionChanged -= AoMudarFila;
        Fila.CollectionChanged += AoMudarFila;
        EscolherProjeto();
        Atualizar(false);
        if (!JanelaVM.Demo) _relogio.Start();
    }

    void AoMudarFila(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => AvisarPendentes();

    void AvisarPendentes()
    {
        OnPropertyChanged(nameof(TemFila));
        OnPropertyChanged(nameof(TemAndamento));
        OnPropertyChanged(nameof(NadaPendente));
    }

    [RelayCommand]
    void Recarregar() => Atualizar(true);

    void Atualizar(bool forcar)
    {
        OnPropertyChanged(nameof(Saudacao));
        MontarPlanos();
        MontarTempo();
        MontarSessoes();
        MontarRecentes();
        _ = CarregarUsoAsync(forcar);
        _ = CarregarGitAsync(forcar);
        _ = CarregarGitHubAsync(forcar);
        _ = CarregarNoticiasAsync(forcar);
    }

    void EscolherProjeto()
    {
        if (Projeto is not null && Projetos.Contains(Projeto)) return;
        AmbienteVM? Achar(string? pasta) => pasta is null ? null : Projetos.FirstOrDefault(p => Core.Ambientes.Workspaces.MesmaPasta(p.Principal, pasta));
        Projeto = Achar(App.Config.ProjetoDoDashboard) ?? Achar(App.Config.UltimaPasta) ?? Projetos.FirstOrDefault();
    }

    partial void OnProjetoChanged(AmbienteVM? value)
    {
        _gitLidoEm = DateTime.MinValue;
        _ = CarregarGitAsync(true);
    }

    [RelayCommand]
    void AlternarFixo()
    {
        if (Projeto is null) return;
        App.Config.ProjetoDoDashboard = ProjetoFixo ? null : Projeto.Principal;
        if (!JanelaVM.Isolada) App.Config.Salvar();
        OnPropertyChanged(nameof(ProjetoFixo));
        OnPropertyChanged(nameof(DicaDoFixo));
    }

    bool PodeComecar() => !string.IsNullOrWhiteSpace(Pedido);

    [RelayCommand(CanExecute = nameof(PodeComecar))]
    void Comecar()
    {
        var texto = Pedido.Trim();
        if (texto.Length == 0) return;
        var local = Projeto?.Local ?? _janela.LocalDe(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Pedido = "";
        _janela.AbrirSessao(local, texto);
    }

    [RelayCommand]
    void AbrirChat(ChatVM chat) => _janela.Mostrar(chat);

    [RelayCommand]
    void AbrirProjeto()
    {
        if (Projeto is { } projeto) _janela.Navegar(projeto);
    }

    [RelayCommand]
    void NovaSessaoNoProjeto()
    {
        if (Projeto is { } projeto) _janela.AbrirSessao(projeto.Local);
    }

    [RelayCommand]
    void AbrirAoVivo() => _janela.MostrarVisaoGeralCommand.Execute(null);

    [RelayCommand]
    void AbrirUso() => _janela.MostrarUsoCommand.Execute(null);

    static IBrush Pincel(string chave) =>
        Application.Current?.TryGetResource(chave, Application.Current.ActualThemeVariant, out var valor) == true && valor is IBrush pincel ? pincel : Brushes.White;

    static IBrush CorDoAgente(string agente) => agente switch
    {
        UsoLocal.Claude => Pincel("Laranja"),
        UsoLocal.Codex => Pincel("Texto"),
        UsoLocal.Gemini => Pincel("Ciano"),
        _ => Pincel("Rosa"),
    };

    IReadOnlyList<string> Instalados()
    {
        if (_instalados is not null) return _instalados;
        if (JanelaVM.Demo) return _instalados = [UsoLocal.Claude, UsoLocal.Codex];
        try
        {
            _instalados = [.. ChatVM.AgentesInstalados.Select(a => a.Nome)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _instalados = [UsoLocal.Claude];
        }
        return _instalados;
    }

    bool Instalado(string agente) => Instalados().Any(n => n.Contains(agente, StringComparison.OrdinalIgnoreCase));

    void MontarPlanos()
    {
        var registro = _janela.Registro;
        var desde = DateTimeOffset.Now.AddHours(-24);
        var pontos = registro.Plano.Where(p => p.Hora >= desde).ToList();
        var cartoes = new List<CartaoDePlanoVM>();
        if (Instalado(UsoLocal.Claude) || _janela.PlanoSessao is not null)
            cartoes.Add(new CartaoDePlanoVM("Claude", "faisca",
                _janela.PlanoSessao is { } s ? Idioma.F("{0}% usado", Math.Round(s * 100)) : "–",
                _janela.PlanoSemana is { } w ? Idioma.F("{0}% da semana", Math.Round(w * 100)) : Idioma.T("Aparece depois da primeira resposta"),
                _janela.PlanoRenova is { } r ? Idioma.F("Renova às {0:HH:mm}", r.ToLocalTime()) : "",
                [.. pontos.Where(p => p.Sessao is not null).Select(p => p.Sessao!.Value)], CorDoAgente(UsoLocal.Claude), Idioma.T("SESSÃO DE 5 H")));
        var codex = _janela.PlanoCodex;
        if (Instalado(UsoLocal.Codex) || codex is not null)
            cartoes.Add(new CartaoDePlanoVM("Codex", "terminal",
                codex?.Sessao is { } cs ? Idioma.F("{0}% restante", Math.Round((1 - Math.Clamp(cs, 0, 1)) * 100)) : "–",
                codex?.Semana is { } cw ? Idioma.F("{0}% da semana usado", Math.Round(cw * 100)) : Idioma.T("Aparece depois da primeira resposta"),
                codex?.SessaoReinicia is { } cr ? Idioma.F("Renova às {0:HH:mm}", cr.ToLocalTime()) : "",
                [.. pontos.Where(p => p.CodexSessao is not null).Select(p => 1 - p.CodexSessao!.Value)], CorDoAgente(UsoLocal.Codex), Idioma.T("RESTANTE")));
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        foreach (var nome in Instalados().Where(n => !n.Contains(UsoLocal.Claude, StringComparison.OrdinalIgnoreCase) && !n.Contains(UsoLocal.Codex, StringComparison.OrdinalIgnoreCase)))
        {
            var chave = nome.Contains(UsoLocal.Gemini, StringComparison.OrdinalIgnoreCase) ? UsoLocal.Gemini : nome;
            var dias = Enumerable.Range(0, DiasDeUso).Select(i => RegistroDoPainel.Chave(hoje.AddDays(i - DiasDeUso + 1))).ToList();
            var porDia = dias.Select(d => _porDia.Where(p => p.Agente == chave && p.Dia == d).Sum(p => (double)p.Tokens)).ToList();
            var hojeTokens = porDia[^1];
            cartoes.Add(new CartaoDePlanoVM(nome, "faisca", Idioma.F("{0} tokens hoje", Grande(hojeTokens)),
                Idioma.F("{0} em 30 dias", Grande(porDia.Sum())), "", porDia, CorDoAgente(chave), Idioma.T("30 DIAS")));
        }
        Planos.Clear();
        foreach (var c in cartoes) Planos.Add(c);
        ColunasDosPlanos = Math.Max(1, Math.Min(3, cartoes.Count));
    }

    void MontarSessoes()
    {
        var lista = _janela.Abertas
            .OrderBy(c => c.Precisa ? 0 : c.Trabalhando ? 1 : c.Pronta ? 2 : 3)
            .ThenByDescending(c => c.AtividadeEm)
            .Take(5)
            .ToList();
        Sincronizar(EmAndamento, lista);
        AvisarPendentes();
    }

    void MontarRecentes()
    {
        Sincronizar(Recentes, _janela.ConversasRecentes.Take(5).ToList());
        OnPropertyChanged(nameof(TemRecentes));
    }

    static void Sincronizar<T>(ObservableCollection<T> destino, IReadOnlyList<T> novos)
    {
        if (destino.SequenceEqual(novos)) return;
        destino.Clear();
        foreach (var item in novos) destino.Add(item);
    }

    void MontarTempo()
    {
        var registro = _janela.Registro;
        var hoje = DateOnly.FromDateTime(DateTime.Now);
        var (ativo, agentes, turnos) = registro.Semana(hoje);
        TempoAtivo = Horas(ativo);
        TempoAgentes = Horas(agentes);
        TextoTurnos = turnos == 1 ? Idioma.T("1 turno de agente") : Idioma.F("{0} turnos de agente", turnos.ToString("N0", CultureInfo.CurrentCulture));
        var segunda = hoje.AddDays(-(((int)hoje.DayOfWeek + 6) % 7));
        var dias = Enumerable.Range(0, 7).Select(i => segunda.AddDays(i)).ToList();
        DiaDeUso Uso(DateOnly d) => registro.Dias.TryGetValue(RegistroDoPainel.Chave(d), out var u) ? u : new DiaDeUso();
        SemanaAgentes = [.. dias.Select(d => Uso(d).Agentes / 3600)];
        SemanaResto = [.. dias.Select(d => Math.Max(0, Uso(d).Ativo - Uso(d).Agentes) / 3600)];
        Semana.Clear();
        foreach (var d in dias)
            Semana.Add(new DiaDaSemanaVM(d.ToDateTime(TimeOnly.MinValue).ToString("ddd", Idioma.Cultura)[..1].ToUpperInvariant(), d == hoje));
    }

    public static string Horas(TimeSpan tempo) => tempo.TotalMinutes < 1 ? "0 min"
        : tempo.TotalHours < 1 ? $"{(int)tempo.TotalMinutes} min"
        : $"{(int)tempo.TotalHours} h {tempo.Minutes:00} min";

    async Task CarregarUsoAsync(bool forcar)
    {
        if (_lendoUso || !forcar && DateTime.Now - _usoLidoEm < TimeSpan.FromMinutes(5)) return;
        _lendoUso = true;
        try
        {
            var desde = DateTime.Today.AddDays(-DiasDeUso);
            _porDia = JanelaVM.Demo ? UsoDemo(DateTime.Today)
                : await Task.Run(() => UsoLocal.Ler(UsoLocal.FontesPadrao, CacheDoUso, desde, porDia: true));
            _usoLidoEm = DateTime.Now;
            MontarUso();
            MontarPlanos();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
        }
        finally
        {
            _lendoUso = false;
        }
    }

    static string CacheDoUso => Path.Combine(JanelaVM.Isolada ? Path.GetTempPath() : Core.DadosDoNexora.Pasta, "uso-diario.json");

    void MontarUso()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var primeiro = hoje.AddDays(-(DiasDeUso - 1));
        var datas = Enumerable.Range(0, DiasDeUso).Select(i => primeiro.AddDays(i)).ToList();
        var series = new List<SerieDoGrafico>();
        LegendasDeUso.Clear();
        double total = 0;
        foreach (var agente in _porDia.Select(p => p.Agente).Distinct().OrderBy(a => a == UsoLocal.Claude ? 0 : a == UsoLocal.Codex ? 1 : 2))
        {
            var valores = datas.Select(d => _porDia.Where(p => p.Agente == agente && p.Dia == RegistroDoPainel.Chave(d)).Sum(p => (double)p.Tokens)).ToList();
            var soma = valores.Sum();
            if (soma <= 0) continue;
            total += soma;
            var cor = CorDoAgente(agente);
            series.Add(new SerieDoGrafico(agente, valores, cor));
            LegendasDeUso.Add(new LegendaVM($"{agente} {Grande(soma)}", cor));
        }
        GraficoDeUso.Formatar = Grande;
        SeriesDeUso = series;
        TextoUso = Idioma.F("{0} tokens em 30 dias", Grande(total));
        InicioDoUso = Curta(datas[0]);
        MeioDoUso = Curta(datas[DiasDeUso / 2]);
        FimDoUso = Curta(datas[^1]);
    }

    static string Curta(DateOnly d) => d.ToDateTime(TimeOnly.MinValue).ToString(Idioma.T("d MMM"), Idioma.Cultura).TrimEnd('.');

    public static string Grande(double valor) => valor switch
    {
        >= 1e9 => (valor / 1e9).ToString("0.#", Idioma.Cultura) + (Idioma.EmIngles ? "B" : " bi"),
        >= 1e6 => (valor / 1e6).ToString("0.#", Idioma.Cultura) + (Idioma.EmIngles ? "M" : " mi"),
        >= 1e3 => (valor / 1e3).ToString("0.#", Idioma.Cultura) + (Idioma.EmIngles ? "K" : " mil"),
        _ => valor.ToString("0", Idioma.Cultura),
    };

    static LinkDoPainelVM LinkDoPr(PrRecente pr, bool comRepositorio) => new(pr.Titulo,
        (comRepositorio && pr.Repositorio.Length > 0 ? $"{pr.Repositorio} #{pr.Numero}" : $"#{pr.Numero}") + $" · {Tempo.Relativo(pr.Atualizado.LocalDateTime)}",
        pr.Url,
        pr.Mergeado ? Idioma.T("MERGEADO") : pr.Rascunho ? Idioma.T("RASCUNHO") : pr.Aberto ? Idioma.T("ABERTO") : Idioma.T("FECHADO"),
        pr.Mergeado ? "merged" : pr.Rascunho ? "draft" : pr.Aberto ? "open" : "closed");

    async Task CarregarGitHubAsync(bool forcar)
    {
        if (_lendoGitHub || !forcar && DateTime.Now - _gitHubLidoEm < TimeSpan.FromMinutes(10)) return;
        _lendoGitHub = true;
        try
        {
            var painel = JanelaVM.Demo ? GitHubDemo(DateTimeOffset.Now) : await GitHubDoUsuario.LerAsync();
            _gitHubLidoEm = DateTime.Now;
            SemGitHub = painel is null;
            if (painel is null) return;
            LoginDoGitHub = painel.Login.Length > 0 ? "@" + painel.Login : "";
            if (painel.Dias.Count > 0)
            {
                InicioDasContribuicoes = painel.Dias[0].Dia.ToDateTime(TimeOnly.MinValue);
                Contribuicoes = [.. painel.Dias.Select(d => d.Nivel)];
            }
            TextoAtividade = Idioma.F("{0} contribuições no último ano", painel.Total.ToString("N0", Idioma.Cultura));
            MeusPrs.Clear();
            foreach (var pr in painel.Meus) MeusPrs.Add(LinkDoPr(pr, true));
            ParaRevisar.Clear();
            foreach (var pr in painel.ParaRevisar) ParaRevisar.Add(LinkDoPr(pr, true));
            SemMeusPrs = MeusPrs.Count == 0;
            TemParaRevisar = ParaRevisar.Count > 0;
        }
        finally
        {
            _lendoGitHub = false;
        }
    }

    async Task CarregarGitAsync(bool forcar)
    {
        var projeto = Projeto;
        if (projeto is null)
        {
            Commits.Clear();
            SemCommits = true;
            return;
        }
        if (_lendoGit || !forcar && _gitDe == projeto.Principal && DateTime.Now - _gitLidoEm < TimeSpan.FromMinutes(3)) return;
        _lendoGit = true;
        try
        {
            IReadOnlyList<CommitRecente> commits;
            IReadOnlyList<LinhasDoArquivo> mudancas;
            string? url = null;
            if (JanelaVM.Demo)
                (commits, mudancas, url) = GitDemo(DateTimeOffset.Now);
            else
            {
                var pasta = projeto.Principal;
                var tarefaCommits = Core.Git.Recentes.CommitsAsync(pasta, 6);
                var tarefaMudancas = Alteracoes.ContarAsync(pasta);
                var tarefaRepositorio = GitHub.RepositorioDaPastaAsync(pasta);
                await Task.WhenAll(tarefaCommits, tarefaMudancas, tarefaRepositorio);
                (commits, mudancas) = (tarefaCommits.Result, tarefaMudancas.Result);
                if (tarefaRepositorio.Result is { } repositorio) url = $"https://github.com/{repositorio}";
            }
            if (Projeto != projeto) return;
            _gitDe = projeto.Principal;
            _gitLidoEm = DateTime.Now;
            Commits.Clear();
            foreach (var c in commits)
                Commits.Add(new LinkDoPainelVM(c.Assunto, $"{c.Autor} · {Tempo.Relativo(c.Quando.LocalDateTime)}", url is null ? null : $"{url}/commit/{c.Sha}", c.Sha));
            SemCommits = Commits.Count == 0;
            var mais = mudancas.Sum(m => m.Mais);
            var menos = mudancas.Sum(m => m.Menos);
            TextoMudancas = mudancas.Count == 0 ? Idioma.T("Sem mudanças") : mudancas.Count == 1 ? Idioma.T("1 arquivo") : Idioma.F("{0} arquivos", mudancas.Count);
            Mais = mudancas.Count == 0 ? "" : $"+{mais:N0}";
            Menos = mudancas.Count == 0 ? "" : $"−{menos:N0}";
        }
        finally
        {
            _lendoGit = false;
        }
    }

    async Task CarregarNoticiasAsync(bool forcar)
    {
        if (_lendoNoticias || !forcar && DateTime.Now - _noticiasLidasEm < TimeSpan.FromMinutes(15)) return;
        _lendoNoticias = true;
        try
        {
            var feeds = App.Config.FeedsDoDashboard is { Count: > 0 } proprios ? proprios : Core.Painel.Noticias.Padrao;
            var lidas = JanelaVM.Demo ? NoticiasDemo(DateTimeOffset.Now) : await Core.Painel.Noticias.BuscarAsync(feeds, 6);
            _noticiasLidasEm = DateTime.Now;
            Noticias.Clear();
            foreach (var n in lidas)
                Noticias.Add(new LinkDoPainelVM(n.Titulo, n.Quando is { } q ? $"{n.Fonte} · {Tempo.Relativo(q.LocalDateTime)}" : n.Fonte, n.Link));
            SemNoticias = Noticias.Count == 0;
            TextoNoticias = feeds.Count == 1 ? Idioma.T("1 fonte") : Idioma.F("{0} fontes", feeds.Count);
        }
        finally
        {
            _lendoNoticias = false;
        }
    }

    static IReadOnlyList<Parcela> UsoDemo(DateTime hoje)
    {
        var sorte = new Random(7);
        var lista = new List<Parcela>();
        var fim = DateOnly.FromDateTime(hoje);
        for (var d = fim.AddDays(-DiasDeUso); d <= fim; d = d.AddDays(1))
        {
            var fimDeSemana = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            if (sorte.NextDouble() < (fimDeSemana ? 0.7 : 0.12)) continue;
            var ritmo = 0.4 + 0.6 * Math.Sin((d.DayNumber % 60) / 60.0 * Math.PI);
            var chave = RegistroDoPainel.Chave(d);
            var semana = UsoLocal.Chave(UsoLocal.SemanaDe(d.ToDateTime(TimeOnly.MinValue)));
            lista.Add(new Parcela { Agente = UsoLocal.Claude, Dia = chave, Semana = semana, Respostas = (int)(40 + 260 * ritmo * sorte.NextDouble()), Lida = (long)(4e7 + 1.6e8 * ritmo * sorte.NextDouble()) });
            lista.Add(new Parcela { Agente = UsoLocal.Codex, Dia = chave, Semana = semana, Respostas = (int)(10 + 60 * sorte.NextDouble()), Lida = (long)(5e6 + 4e7 * sorte.NextDouble()) });
        }
        return lista;
    }

    static PainelDoGitHub GitHubDemo(DateTimeOffset agora)
    {
        var sorte = new Random(3);
        var hoje = DateOnly.FromDateTime(agora.LocalDateTime);
        var inicio = hoje.AddDays(-364 - (int)hoje.AddDays(-364).DayOfWeek);
        var dias = new List<DiaDeContribuicao>();
        var total = 0;
        for (var d = inicio; d <= hoje; d = d.AddDays(1))
        {
            var fimDeSemana = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var ritmo = 0.35 + 0.65 * Math.Abs(Math.Sin(d.DayNumber / 45.0));
            var quantos = sorte.NextDouble() < (fimDeSemana ? 0.75 : 0.18) ? 0 : (int)(ritmo * sorte.Next(1, 14));
            total += quantos;
            dias.Add(new DiaDeContribuicao(d, quantos, quantos switch { 0 => 0, < 3 => 1, < 6 => 2, < 9 => 3, _ => 4 }));
        }
        return new PainelDoGitHub("ana", total, dias,
        [
            new(55, "Sync offline notes and resolve conflicts", "OPEN", false, agora.AddMinutes(-12), "https://github.com/acme/notes/pull/55", "ana", "notes"),
            new(52, "Coupon discount in checkout", "OPEN", false, agora.AddMinutes(-40), "https://github.com/acme/shop/pull/52", "ana", "shop"),
            new(50, "Orders page shows only today", "OPEN", true, agora.AddHours(-3), "https://github.com/acme/shop/pull/50", "ana", "shop"),
            new(48, "Expired coupon no longer applies", "MERGED", false, agora.AddHours(-20), "https://github.com/acme/shop/pull/48", "ana", "shop"),
            new(46, "Pix with dynamic QR code", "MERGED", false, agora.AddDays(-1), "https://github.com/acme/shop/pull/46", "ana", "shop"),
        ],
        [
            new(61, "Rate limiting on the public API", "OPEN", false, agora.AddMinutes(-25), "https://github.com/acme/shop-api/pull/61", "bruno", "shop-api"),
            new(19, "Dark mode for the admin panel", "OPEN", false, agora.AddHours(-6), "https://github.com/acme/admin/pull/19", "carla", "admin"),
        ]);
    }

    static (IReadOnlyList<CommitRecente>, IReadOnlyList<LinhasDoArquivo>, string) GitDemo(DateTimeOffset agora) =>
    (
        [
            new("a1c9e40", "ana", agora.AddMinutes(-18), "Apply coupon before shipping in checkout"),
            new("7f2b113", "bruno", agora.AddHours(-2), "Add concurrency test for coupon redemption"),
            new("c44d0e2", "ana", agora.AddHours(-5), "Fix order list filter on the orders page"),
            new("19be7aa", "carla", agora.AddHours(-26), "Sync notes when the connection comes back"),
            new("e0d5f31", "bruno", agora.AddHours(-30), "Bump EF Core to 10.0.2"),
        ],
        [new("src/Loja.Application/Cupons/AplicarCupom.cs", 86, 12, false, false), new("tests/Loja.Application.Tests/CupomTests.cs", 64, 0, false, true), new("src/Loja.Api/Controllers/CheckoutController.cs", 9, 7, false, false)],
        "https://github.com/acme/shop"
    );

    static IReadOnlyList<Noticia> NoticiasDemo(DateTimeOffset agora) =>
    [
        new("Claude Code 2.1: background agents and a faster /usage", "https://github.com/anthropics/claude-code/releases", agora.AddHours(-3), "claude-code"),
        new("Codex CLI 0.60 adds session resume across machines", "https://github.com/openai/codex/releases", agora.AddHours(-9), "codex"),
        new("GitHub Actions: larger arm64 runners are generally available", "https://github.blog/changelog/", agora.AddHours(-20), "github.blog"),
        new("Show HN: a terminal UI for reviewing diffs side by side", "https://news.ycombinator.com/", agora.AddDays(-1), "hnrss.org"),
    ];
}
