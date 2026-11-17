using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class PainelPrVM : ObservableObject
{
    readonly ChatVM _chat;
    DateTime _lidoEm;
    string? _lidoDe;
    PropostaDePr? _proposta;
    BranchParaPr? _branch;
    int _geracao;

    public PainelPrVM(ChatVM chat) => _chat = chat;

    public ObservableCollection<CheckPrVM> Checks { get; } = [];
    public ObservableCollection<RevisorPrVM> Revisores { get; } = [];
    public ObservableCollection<GrupoConversasVM> Grupos { get; } = [];
    public ObservableCollection<ComentarioPrVM> Comentarios { get; } = [];
    public ObservableCollection<string> Bases { get; } = [];

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemErro))]
    public partial string? Erro { get; set; }

    public bool TemErro => Erro is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAviso))]
    public partial string? Aviso { get; set; }

    public bool TemAviso => Aviso is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemConfirmacao), nameof(ConfirmacaoPerigosa))]
    public partial ConfirmacaoPrVM? Confirmacao { get; set; }

    public bool TemConfirmacao => Confirmacao is not null;
    public bool ConfirmacaoPerigosa => Confirmacao is { Perigo: true };

    [ObservableProperty]
    public partial bool Trabalhando { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemPr), nameof(Titulo), nameof(Numero), nameof(EstadoTexto), nameof(Ramos), nameof(Aberto), nameof(Rascunho), nameof(Mergeado),
        nameof(Fechado), nameof(TemConflito), nameof(TextoConflito), nameof(PodeMarcarPronto), nameof(MostraMerge), nameof(PodeMergear), nameof(TextoBloqueio),
        nameof(TemBloqueio), nameof(AutoMergeLigado), nameof(PodeLigarAutoMerge), nameof(ResumoChecks), nameof(TemChecks), nameof(TextoConversas), nameof(TemConversas),
        nameof(TemRevisores), nameof(TemComentarios), nameof(TextoComentarios), nameof(Atrasado), nameof(InfoAutor), nameof(TemConversasAbertas))]
    public partial DetalhePr? Pr { get; set; }

    [ObservableProperty]
    public partial bool SemPr { get; set; }

    [ObservableProperty]
    public partial bool NaPrincipal { get; set; }

    public bool TemPr => Pr is not null;
    public string Titulo => Pr?.Titulo ?? "";
    public string Numero => Pr is { } p ? $"#{p.Numero}" : "";
    public bool Aberto => Pr is { Aberto: true, Rascunho: false };
    public bool Rascunho => Pr is { Aberto: true, Rascunho: true };
    public bool Mergeado => Pr is { Mergeado: true };
    public bool Fechado => Pr is { Aberto: false, Mergeado: false };
    public string InfoAutor => Pr is { } p
        ? string.Join("  ·  ", new[] { p.Autor.Length > 0 ? Idioma.F("aberto por {0}", p.Autor) : null, p.Atualizado is { } a ? Idioma.F("atualizado {0}", Core.Texto.Tempo.Relativo(a.LocalDateTime)) : null }.OfType<string>())
        : "";

    public string EstadoTexto => Pr switch
    {
        null => "",
        { Mergeado: true } => Idioma.T("Mergeado"),
        { Aberto: false } => Idioma.T("Fechado"),
        { Rascunho: true } => Idioma.T("Rascunho"),
        _ => Idioma.T("Aberto"),
    };

    public string Ramos => Pr is { } p ? $"{p.HeadBranch}  →  {p.BaseBranch}" : "";
    public bool TemConflito => Pr is { Aberto: true, TemConflito: true };
    public bool Atrasado => Pr is { Aberto: true, Atrasado: true, TemConflito: false };
    public string TextoConflito => Pr is { } p ? Idioma.F("Esta branch tem conflito com {0}. O GitHub não deixa mergear até resolver.", p.BaseBranch) : "";
    public bool PodeMarcarPronto => Pr is { Aberto: true, Rascunho: true };
    public bool MostraMerge => Pr is { Aberto: true, Rascunho: false };
    public bool PodeMergear => Pr is { PodeMergear: true } && !Trabalhando;
    public bool AutoMergeLigado => Pr is { Aberto: true, AutoMerge: true };
    public bool PodeLigarAutoMerge => Pr is { Aberto: true, Rascunho: false, AutoMerge: false, PodeMergear: false, TemConflito: false };

    public string TextoBloqueio => Pr switch
    {
        null or { PodeMergear: true } or { Aberto: false } or { Rascunho: true } => "",
        { TemConflito: true } => Idioma.T("Resolva o conflito antes de mergear."),
        { Atrasado: true } => Idioma.F("A branch está atrás de {0}. Atualize antes de mergear.", Pr.BaseBranch),
        { ChecksFalhos: > 0 } => Idioma.T("Há checks falhando. O GitHub bloqueia o merge até passarem."),
        { ChecksPendentes: > 0 } => Idioma.T("Os checks ainda estão rodando. Ligue o auto-merge para mergear quando passarem."),
        { Revisao: "CHANGES_REQUESTED" } => Idioma.T("Pediram mudanças. O merge espera uma nova aprovação."),
        { Revisao: "REVIEW_REQUIRED" } => Idioma.T("Falta a aprovação de alguém. Ligue o auto-merge para mergear quando aprovarem."),
        _ => Idioma.T("O GitHub ainda não liberou o merge."),
    };

    public bool TemBloqueio => TextoBloqueio.Length > 0;

    public string ResumoChecks => Pr is not { Checks.Count: > 0 } p ? Idioma.T("Nenhum check neste PR.")
        : string.Join("  ·  ", new[]
        {
            p.ChecksFalhos switch { 0 => null, 1 => Idioma.T("1 falhou"), var n => Idioma.F("{0} falharam", n) },
            p.ChecksPendentes switch { 0 => null, 1 => Idioma.T("1 rodando"), var n => Idioma.F("{0} rodando", n) },
            p.ChecksOk switch { 0 => null, 1 => Idioma.T("1 ok"), var n => Idioma.F("{0} ok", n) },
        }.OfType<string>());

    public bool TemChecks => Pr is { Checks.Count: > 0 };
    public bool TemRevisores => Revisores.Count > 0;
    public bool TemConversas => Grupos.Count > 0;
    public bool TemComentarios => Comentarios.Count > 0;
    public string TextoComentarios => Comentarios.Count == 1 ? Idioma.T("1 comentário") : Idioma.F("{0} comentários", Comentarios.Count);

    public string TextoConversas => Pr is not { } p ? ""
        : !p.ConversasLidas && p.Conversas.Count == 0 ? Idioma.T("Não deu para ler os comentários de revisão.")
        : (p.ConversasAbertas.Count, p.Conversas.Count - p.ConversasAbertas.Count) switch
        {
            (0, 0) => Idioma.T("Nenhum comentário de revisão."),
            (0, var r) => r == 1 ? Idioma.T("Nenhuma conversa aberta (1 resolvida).") : Idioma.F("Nenhuma conversa aberta ({0} resolvidas).", r),
            (var a, var r) => string.Join("  ·  ", new[]
            {
                a == 1 ? Idioma.T("1 conversa aberta") : Idioma.F("{0} conversas abertas", a),
                r switch { 0 => null, 1 => Idioma.T("1 resolvida"), _ => Idioma.F("{0} resolvidas", r) },
            }.OfType<string>()),
        };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModoSquash), nameof(ModoMergeCommit), nameof(ModoRebase))]
    public partial ModoMerge Modo { get; set; } = ModoMerge.Squash;

    public bool ModoSquash => Modo == ModoMerge.Squash;
    public bool ModoMergeCommit => Modo == ModoMerge.Merge;
    public bool ModoRebase => Modo == ModoMerge.Rebase;

    [RelayCommand]
    void EscolherModo(string modo) => Modo = modo switch
    {
        "merge" => ModoMerge.Merge,
        "rebase" => ModoMerge.Rebase,
        _ => ModoMerge.Squash,
    };

    static string NomeDoModo(ModoMerge modo) => modo switch
    {
        ModoMerge.Merge => Idioma.T("merge commit"),
        ModoMerge.Rebase => "rebase",
        _ => "squash",
    };

    public void Mostrar()
    {
        var alvo = $"{_chat.Pasta}|{_chat.Branch}";
        if (Carregando || alvo == _lidoDe && DateTime.UtcNow - _lidoEm < TimeSpan.FromSeconds(30)) return;
        Aviso = null;
        _ = CarregarAsync();
    }

    [RelayCommand]
    Task Atualizar()
    {
        Aviso = null;
        return CarregarAsync();
    }

    public async Task CarregarAsync()
    {
        if (Carregando) return;
        Carregando = true;
        Erro = null;
        Confirmacao = null;
        var geracao = ++_geracao;
        try
        {
            if (!JanelaVM.Demo) _chat.AtualizarBranch();
            var branch = _chat.Branch;
            _lidoDe = $"{_chat.Pasta}|{branch}";
            _lidoEm = DateTime.UtcNow;
            if (!GitHub.TemPr(branch))
            {
                Aplicar(null);
                SemPr = false;
                NaPrincipal = true;
                return;
            }
            NaPrincipal = false;
            ConsultaPr consulta;
            if (JanelaVM.Demo) consulta = new ConsultaPr(PrsDemo.Detalhe(_chat.Pr?.Numero, branch!), null);
            else consulta = await GitHub.DetalhePrAsync(_chat.Pasta, branch!);
            if (geracao != _geracao) return;
            if (consulta.Erro is { } erro)
            {
                Erro = erro;
                return;
            }
            Aplicar(consulta.Pr);
            SemPr = consulta.SemPr;
            if (consulta.SemPr) await PrepararNovoAsync(false);
        }
        finally
        {
            if (geracao == _geracao) Carregando = false;
        }
    }

    void Aplicar(DetalhePr? pr)
    {
        Checks.Clear();
        Revisores.Clear();
        Grupos.Clear();
        Comentarios.Clear();
        if (pr is not null)
        {
            foreach (var c in pr.Checks.OrderBy(c => c.Estado switch { EstadoCheck.Falhou => 0, EstadoCheck.Pendente => 1, EstadoCheck.Ok => 2, _ => 3 }).ThenBy(c => c.Chave, StringComparer.OrdinalIgnoreCase))
                Checks.Add(new CheckPrVM(c, PedirCheck));
            foreach (var r in pr.Revisores.OrderBy(r => r.Estado switch { "CHANGES_REQUESTED" => 0, "REQUESTED" => 1, "APPROVED" => 2, _ => 3 }))
                Revisores.Add(new RevisorPrVM(r));
            foreach (var g in pr.Conversas.GroupBy(c => c.Arquivo, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                Grupos.Add(new GrupoConversasVM(g.Key,
                    [.. g.Where(c => !c.Resolvida).OrderBy(c => c.Linha ?? 0).Select(c => new ConversaPrVM(c, AbrirArquivo, PedirConversa, AtualizarEnvio))],
                    [.. g.Where(c => c.Resolvida).OrderBy(c => c.Linha ?? 0).Select(c => new ConversaNaLinhaVM(c))],
                    a => _ = VerDiffAsync(a)));
            foreach (var c in pr.Comentarios.OrderByDescending(c => c.Quando ?? DateTimeOffset.MinValue).Take(5))
                Comentarios.Add(new ComentarioPrVM(c, EhMeu(pr, c.Autor), AtualizarEnvio));
            _chat.Pr = pr.ComoEstado();
        }
        Pr = pr;
        OnPropertyChanged(nameof(TemRevisores));
        OnPropertyChanged(nameof(TemConversas));
        OnPropertyChanged(nameof(TemComentarios));
        OnPropertyChanged(nameof(TextoComentarios));
        AtualizarEnvio();
        if (pr is not null) _ = BuscarPassagemAsync(pr);
    }

    public bool TemConversasAbertas => Pr is { ConversasAbertas.Count: > 0 } || Pr is { Revisao: "CHANGES_REQUESTED" };

    public static string Resumir(string texto, int maximo)
    {
        var limpo = System.Text.RegularExpressions.Regex.Replace(texto, @"<!--.*?-->", "", System.Text.RegularExpressions.RegexOptions.Singleline).Trim();
        return limpo.Length <= maximo ? limpo : limpo[..maximo].TrimEnd() + "…";
    }

    static string UmaLinha(string texto, int maximo) =>
        Resumir(System.Text.RegularExpressions.Regex.Replace(texto, @"\s+", " "), maximo);

    public static void AbrirLink(string url)
    {
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            Sistema.Abrir(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    [RelayCommand]
    void AbrirNoGitHub()
    {
        if (Pr?.Url is { Length: > 0 } url) AbrirLink(url);
    }

    void AbrirArquivo(ConversaDoPr conversa)
    {
        var caminho = Path.Combine(_chat.Pasta, conversa.Arquivo.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(caminho)) _chat.Editor.Abrir(caminho, conversa.Linha);
        else if (conversa.Primeiro?.Url is { Length: > 0 } url) AbrirLink(url);
    }

    public static string PedidoDoCheck(DetalhePr pr, CheckDoPr check)
    {
        var onde = check.Execucao is { } id ? Idioma.F(" com `gh run view {0} --log-failed`", id)
            : check.Link.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? Idioma.F(" em {0}", check.Link) : "";
        return Idioma.F("O check \"{0}\" do PR #{1} falhou. Leia o log da falha{2}, descubra a causa e corrija. Rode os testes, faça o commit e o push. Não comente no PR.", check.Chave, pr.Numero, onde);
    }

    public static string PedidoDasConversas(DetalhePr pr, IEnumerable<ConversaDoPr> conversas)
    {
        var linhas = conversas.Select(c => $"- `{c.Arquivo}{(c.Linha is { } l ? $":{l}" : "")}` ({c.Primeiro?.Autor}): {UmaLinha(c.Primeiro?.Corpo ?? "", 500)}").ToList();
        linhas.AddRange(pr.Revisoes.Where(r => r.Estado == "CHANGES_REQUESTED" && r.Corpo.Trim().Length > 0)
            .Select(r => Idioma.F("- Revisão de {0}: {1}", r.Autor, UmaLinha(r.Corpo, 500))));
        return Idioma.F("Resolva estes comentários de revisão do PR #{0}. Mude o código, rode os testes, faça o commit e o push. Não responda nem comente no PR e não marque as conversas como resolvidas: só o código muda.", pr.Numero)
            + "\n\n" + string.Join("\n", linhas);
    }

    public static string PedidoDoConflito(DetalhePr pr) =>
        Idioma.F("O PR #{0} tem conflito com {1}. Rode `git fetch origin`, faça o merge de origin/{1} nesta branch, resolva os conflitos mantendo a intenção das duas versões, rode os testes, faça o commit e o push. Não use force push.", pr.Numero, pr.BaseBranch);

    void Pedir(string texto)
    {
        _chat.InserirNoRascunho(texto);
        Aviso = Idioma.T("O pedido está no campo da conversa. Revise e mande quando quiser.");
    }

    void PedirCheck(CheckDoPr check)
    {
        if (Pr is { } pr) Pedir(PedidoDoCheck(pr, check));
    }

    void PedirConversa(ConversaDoPr conversa)
    {
        if (Pr is { } pr) Pedir(PedidoDasConversas(pr with { Revisoes = [] }, [conversa]));
    }

    [RelayCommand]
    void PedirConversas()
    {
        if (Pr is { } pr) Pedir(PedidoDasConversas(pr, pr.ConversasAbertas));
    }

    [RelayCommand]
    void PedirConflito()
    {
        if (Pr is { } pr) Pedir(PedidoDoConflito(pr));
    }

    [RelayCommand]
    void CancelarConfirmacao() => Confirmacao = null;

    [RelayCommand]
    async Task Confirmar()
    {
        if (Confirmacao is not { } c || Trabalhando) return;
        Confirmacao = null;
        Trabalhando = true;
        OnPropertyChanged(nameof(PodeMergear));
        try
        {
            await c.Acao();
        }
        finally
        {
            Trabalhando = false;
            OnPropertyChanged(nameof(PodeMergear));
        }
    }

    async Task Executar(Func<Task<ResultadoGit>> real, Func<DetalhePr, DetalhePr> demo, string sucesso)
    {
        if (Pr is not { } pr) return;
        if (JanelaVM.Demo)
        {
            Aplicar(demo(pr));
            Aviso = sucesso;
            return;
        }
        var r = await real();
        if (!r.Ok)
        {
            Aviso = Idioma.F("O GitHub recusou: {0}", r.Mensagem);
            return;
        }
        Aviso = sucesso;
        _lidoEm = DateTime.MinValue;
        await CarregarAsync();
    }

    [RelayCommand]
    void MarcarPronto()
    {
        if (Pr is not { } pr) return;
        Confirmacao = new ConfirmacaoPrVM(Idioma.F("Tirar o PR #{0} do rascunho? Os revisores pedidos passam a ser avisados.", pr.Numero), Idioma.T("Marcar como pronto"),
            () => Executar(() => GitHub.MarcarProntoAsync(_chat.Pasta, pr.Numero), p => p with { Rascunho = false, EstadoDoMerge = p.TemConflito ? "DIRTY" : "BLOCKED" },
                Idioma.T("O PR saiu do rascunho.")), false);
    }

    [RelayCommand]
    void Mergear()
    {
        if (Pr is not { PodeMergear: true } pr) return;
        var modo = Modo;
        Confirmacao = new ConfirmacaoPrVM(Idioma.F("Mergear o PR #{0} em {1} com {2}? Isso muda a branch {1} no GitHub.", pr.Numero, pr.BaseBranch, NomeDoModo(modo)), Idioma.T("Mergear"),
            () => Executar(() => GitHub.MergearAsync(_chat.Pasta, pr.Numero, modo, false, pr.ShaDoHead), p => p with { Estado = "MERGED" },
                Idioma.F("PR #{0} mergeado em {1}.", pr.Numero, pr.BaseBranch)), true);
    }

    [RelayCommand]
    void LigarAutoMerge()
    {
        if (Pr is not { Aberto: true } pr) return;
        var modo = Modo;
        Confirmacao = new ConfirmacaoPrVM(Idioma.F("Ligar o auto-merge do PR #{0}? O GitHub faz o merge em {1} com {2} assim que os checks e as revisões liberarem.", pr.Numero, pr.BaseBranch, NomeDoModo(modo)),
            Idioma.T("Ligar o auto-merge"),
            () => Executar(() => GitHub.MergearAsync(_chat.Pasta, pr.Numero, modo, true, pr.ShaDoHead), p => p with { AutoMerge = true },
                Idioma.T("Auto-merge ligado.")), true);
    }

    [RelayCommand]
    async Task DesligarAutoMerge()
    {
        if (Pr is not { AutoMerge: true } pr || Trabalhando) return;
        Trabalhando = true;
        try
        {
            await Executar(() => GitHub.DesligarAutoMergeAsync(_chat.Pasta, pr.Numero), p => p with { AutoMerge = false }, Idioma.T("Auto-merge desligado."));
        }
        finally
        {
            Trabalhando = false;
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CriarPrCommand), nameof(PedirPrAoAgenteCommand))]
    public partial string NovoTitulo { get; set; } = "";

    [ObservableProperty]
    public partial string NovoCorpo { get; set; } = "";

    [ObservableProperty]
    public partial bool NovoRascunho { get; set; } = true;

    [ObservableProperty]
    public partial string? BaseEscolhida { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemResumoBranch))]
    public partial string ResumoBranch { get; set; } = "";

    public bool TemResumoBranch => ResumoBranch.Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemAvisoNovo))]
    [NotifyCanExecuteChangedFor(nameof(CriarPrCommand))]
    public partial string? AvisoNovo { get; set; }

    public bool TemAvisoNovo => AvisoNovo is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CriarPrCommand))]
    public partial bool SemCommits { get; set; }

    bool _preparando;

    partial void OnBaseEscolhidaChanged(string? value)
    {
        if (!_preparando && value is { Length: > 0 } && SemPr) _ = LerBranchAsync(false);
    }

    public async Task PrepararNovoAsync(bool forcar)
    {
        _preparando = true;
        try
        {
            if (Bases.Count == 0 || forcar)
            {
                IReadOnlyList<string> branches;
                string? padrao;
                if (JanelaVM.Demo) (branches, padrao) = (["main", "develop", "cupom-checkout"], "main");
                else
                {
                    branches = await Repositorio.BranchesAsync(_chat.Pasta);
                    padrao = await Repositorio.BranchPadraoAsync(_chat.Pasta);
                }
                Bases.Clear();
                foreach (var b in branches.Where(b => b != _chat.Branch)) Bases.Add(b);
                if (padrao is { Length: > 0 } && !Bases.Contains(padrao)) Bases.Insert(0, padrao);
                BaseEscolhida = BaseEscolhida is { } atual && Bases.Contains(atual) ? atual : padrao ?? Bases.FirstOrDefault();
            }
        }
        finally
        {
            _preparando = false;
        }
        await LerBranchAsync(forcar);
    }

    async Task LerBranchAsync(bool forcar)
    {
        if (BaseEscolhida is not { Length: > 0 } baseBranch || _chat.Branch is not { Length: > 0 } branch) return;
        _branch = JanelaVM.Demo ? PrsDemo.Branch(baseBranch) : await GeradorDePr.LerBranchAsync(_chat.Pasta, baseBranch);
        SemCommits = _branch.Commits.Count == 0;
        ResumoBranch = string.Join("  ·  ", new[]
        {
            _branch.Commits.Count switch { 0 => Idioma.F("nenhum commit além de {0}", baseBranch), 1 => Idioma.F("1 commit além de {0}", baseBranch), var n => Idioma.F("{0} commits além de {1}", n, baseBranch) },
            _branch.SemCommit switch { 0 => null, 1 => Idioma.T("1 arquivo sem commit"), var n => Idioma.F("{0} arquivos sem commit", n) },
        }.OfType<string>());
        AvisoNovo = SemCommits ? Idioma.T("Ainda não há commits para abrir o PR. Peça ao agente o commit e o PR, com este título e esta descrição.")
            : _branch.SemCommit > 0 ? Idioma.T("Há alterações sem commit. Criar o PR agora manda só o que já tem commit.")
            : null;
        var proposta = GeradorDePr.Propor(branch, _branch.Commits, _branch.Arquivos, _chat.Issue);
        var editado = _proposta is not null && (NovoTitulo != _proposta.Titulo || NovoCorpo != _proposta.Corpo);
        if (forcar || !editado || NovoTitulo.Length == 0)
        {
            NovoTitulo = proposta.Titulo;
            NovoCorpo = proposta.Corpo;
        }
        _proposta = proposta;
    }

    [RelayCommand]
    Task GerarDeNovo() => PrepararNovoAsync(true);

    bool PodeCriar() => !string.IsNullOrWhiteSpace(NovoTitulo) && !SemCommits && BaseEscolhida is { Length: > 0 };

    [RelayCommand(CanExecute = nameof(PodeCriar))]
    void CriarPr()
    {
        if (BaseEscolhida is not { } baseBranch || _chat.Branch is not { } branch) return;
        var (titulo, corpo, rascunho) = (NovoTitulo.Trim(), NovoCorpo.Trim(), NovoRascunho);
        Confirmacao = new ConfirmacaoPrVM(rascunho
                ? Idioma.F("Enviar a branch {0} e abrir um PR em rascunho em {1}?", branch, baseBranch)
                : Idioma.F("Enviar a branch {0} e abrir um PR pronto para revisão em {1}?", branch, baseBranch),
            Idioma.T("Criar o PR"), () => CriarAsync(branch, baseBranch, titulo, corpo, rascunho), false);
    }

    async Task CriarAsync(string branch, string baseBranch, string titulo, string corpo, bool rascunho)
    {
        if (JanelaVM.Demo)
        {
            SemPr = false;
            Aplicar(PrsDemo.Novo(branch, baseBranch, titulo, corpo, rascunho));
            Aviso = Idioma.T("PR criado.");
            return;
        }
        var r = await GitHub.CriarPrAsync(_chat.Pasta, branch, baseBranch, titulo, corpo, rascunho);
        if (!r.Ok)
        {
            AvisoNovo = Idioma.F("Não deu para criar o PR: {0}", r.Mensagem);
            return;
        }
        Aviso = Idioma.T("PR criado.");
        _lidoEm = DateTime.MinValue;
        await CarregarAsync();
    }

    bool PodePedirAoAgente() => !string.IsNullOrWhiteSpace(NovoTitulo);

    [RelayCommand(CanExecute = nameof(PodePedirAoAgente))]
    void PedirPrAoAgente()
    {
        var baseBranch = BaseEscolhida ?? "main";
        _chat.EsperarPr();
        Pedir(PedidoDoNovoPr(baseBranch, NovoTitulo.Trim(), NovoCorpo.Trim(), NovoRascunho));
    }

    public static string PedidoDoNovoPr(string baseBranch, string titulo, string corpo, bool rascunho) =>
        (rascunho
            ? Idioma.F("Faça o commit das alterações desta sessão, envie a branch e abra um PR em rascunho em {0}, sem revisores.", baseBranch)
            : Idioma.F("Faça o commit das alterações desta sessão, envie a branch e abra um PR pronto para revisão em {0}, sem revisores.", baseBranch))
        + " " + Idioma.T("Use exatamente o título e a descrição abaixo (passe a descrição por arquivo com --body-file). No fim, me passe o link do PR.")
        + "\n\n" + Idioma.F("Título: {0}", titulo) + "\n\n" + Idioma.T("Descrição:") + "\n" + corpo;
}
