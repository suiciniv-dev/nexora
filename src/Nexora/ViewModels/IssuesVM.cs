using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using System.Text.RegularExpressions;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed record ChipVM(string Nome, IBrush Fundo, IBrush Borda, IBrush Texto, IBrush Ponto)
{
    static readonly Dictionary<string, Color> Nomeadas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GRAY"] = Color.Parse("#9198A1"),
        ["BLUE"] = Color.Parse("#4493F8"),
        ["GREEN"] = Color.Parse("#3FB950"),
        ["YELLOW"] = Color.Parse("#D29922"),
        ["ORANGE"] = Color.Parse("#DB6D28"),
        ["RED"] = Color.Parse("#F85149"),
        ["PINK"] = Color.Parse("#DB61A2"),
        ["PURPLE"] = Color.Parse("#AB7DF8"),
    };

    public static Color Cor(string? cor) =>
        cor is null ? Nomeadas["GRAY"]
        : Nomeadas.TryGetValue(cor, out var nomeada) ? nomeada
        : Color.TryParse("#" + cor.TrimStart('#'), out var hex) ? hex
        : Nomeadas["GRAY"];

    static Color Misturar(Color a, Color b, double t) =>
        Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    public static ChipVM De(string nome, string? cor)
    {
        var c = Cor(cor);
        var escuro = Application.Current?.ActualThemeVariant != ThemeVariant.Light;
        var texto = escuro ? Misturar(c, Colors.White, .45) : Misturar(c, Colors.Black, .4);
        return new ChipVM(nome, new SolidColorBrush(c, escuro ? .16 : .14), new SolidColorBrush(c, .5), new SolidColorBrush(texto), new SolidColorBrush(c));
    }
}

public sealed partial class ColunasIssuesVM : ObservableObject
{
    [ObservableProperty]
    public partial bool Quadro { get; set; }

    [ObservableProperty]
    public partial bool Prioridade { get; set; }

    [ObservableProperty]
    public partial bool Tamanho { get; set; }

    [ObservableProperty]
    public partial bool Iteracao { get; set; }

    [ObservableProperty]
    public partial bool Prs { get; set; }

    [ObservableProperty]
    public partial bool Atualizada { get; set; }

    double _largura = 2000;

    [ObservableProperty]
    public partial bool VerEtiquetas { get; set; } = true;

    [ObservableProperty]
    public partial bool VerPrs { get; set; }

    [ObservableProperty]
    public partial bool VerPrioridade { get; set; }

    [ObservableProperty]
    public partial bool VerTamanho { get; set; }

    [ObservableProperty]
    public partial bool VerIteracao { get; set; }

    public void Ajustar(double largura)
    {
        _largura = largura;
        Recalcular();
    }

    void Recalcular()
    {
        var sobra = _largura - 420 - 140 - (Quadro ? 120 : 0) - (Atualizada ? 90 : 0);
        bool Cabe(bool tem, double largura)
        {
            if (!tem || sobra < largura) return false;
            sobra -= largura;
            return true;
        }
        VerEtiquetas = Cabe(true, 170);
        VerPrs = Cabe(Prs, 90);
        VerPrioridade = Cabe(Prioridade, 90);
        VerTamanho = Cabe(Tamanho, 85);
        VerIteracao = Cabe(Iteracao, 100);
    }

    partial void OnQuadroChanged(bool value) => Recalcular();
    partial void OnPrioridadeChanged(bool value) => Recalcular();
    partial void OnTamanhoChanged(bool value) => Recalcular();
    partial void OnIteracaoChanged(bool value) => Recalcular();
    partial void OnPrsChanged(bool value) => Recalcular();
    partial void OnAtualizadaChanged(bool value) => Recalcular();
}

public sealed partial class IssueLinhaVM : ObservableObject
{
    readonly Action<IssueLinhaVM> _abrir;
    readonly Action<PrLigado> _abrirPr;

    public IssueLinhaVM(ItemDoQuadro item, AmbienteVM? projeto, ColunasIssuesVM colunas, Action<IssueLinhaVM> abrir, Action<PrLigado>? abrirPr = null)
    {
        Projeto = projeto;
        Colunas = colunas;
        _abrir = abrir;
        _abrirPr = abrirPr ?? (pr => PainelPrVM.AbrirLink(pr.Url));
        Item = item;
        Montar();
    }

    public ItemDoQuadro Item { get; private set; }
    public AmbienteVM? Projeto { get; }
    public ColunasIssuesVM Colunas { get; }
    public string Numero => Item.Numero > 0 ? $"#{Item.Numero}" : Idioma.T("rascunho");
    public string Titulo => Item.Titulo;
    public string Repositorio => Item.Repositorio.Split('/').LastOrDefault() ?? "";
    public string Icone => Item.EhPr ? "branch" : "issue";
    public bool Fechada => !Item.Aberto;
    public string Responsavel => Item.Responsaveis.FirstOrDefault() ?? "";
    public bool TemResponsavel => Responsavel.Length > 0;
    public string Inicial => Responsavel.Length > 0 ? Responsavel[..1].ToUpperInvariant() : "";
    public string MaisResponsaveis => Item.Responsaveis.Count > 1 ? $"+{Item.Responsaveis.Count - 1}" : "";
    public string Iteracao => Item.Iteracao ?? "";
    public string Quando => Item.Atualizada is { } q ? Core.Texto.Tempo.Relativo(q.LocalDateTime) : "";
    public ChipVM? Status { get; private set; }
    public ChipVM? Prioridade { get; private set; }
    public ChipVM? Tamanho { get; private set; }
    public IReadOnlyList<ChipVM> Etiquetas { get; private set; } = [];
    public IReadOnlyList<PilulaPrVM> Pilulas { get; private set; } = [];
    public bool TemPilulas => Pilulas.Count > 0;

    [ObservableProperty]
    public partial bool Escolhida { get; set; }

    void Montar()
    {
        static ChipVM? Chip(ValorDeCampo? v) => v is null ? null : ChipVM.De(v.Nome, v.Cor);
        Status = Chip(Item.Valor("Status"));
        Prioridade = Chip(Item.Valor("Priority", "Prioridade"));
        Tamanho = Chip(Item.Valor("Size", "Tamanho"));
        Etiquetas = [.. Item.Etiquetas.Select(e => ChipVM.De(e.Nome, e.Cor))];
        Pilulas = [.. Item.PrsLigados.OrderByDescending(p => p.Aberto).ThenByDescending(p => p.Numero).Select(p => new PilulaPrVM(p, _abrirPr))];
    }

    public void Trocar(ItemDoQuadro novo)
    {
        Item = novo;
        Montar();
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    void Abrir() => _abrir(this);
}

public sealed partial class GrupoIssuesVM(string nome, string? cor, IReadOnlyList<IssueLinhaVM> linhas, string? opcaoId = null) : ObservableObject
{
    public string Nome { get; } = nome;
    public string? OpcaoId { get; } = opcaoId;

    [ObservableProperty]
    public partial bool Alvo { get; set; }

    public IBrush Cor { get; } = ChipVM.De(nome, cor).Ponto;
    public IReadOnlyList<IssueLinhaVM> Linhas { get; } = linhas;
    public string Contagem => Linhas.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Seta))]
    public partial bool Aberto { get; set; } = true;

    public string Seta => Aberto ? "abaixo" : "direita";

    [RelayCommand]
    void Alternar() => Aberto = !Aberto;
}

public sealed record FonteIssuesVM(string Nome, string Detalhe, QuadroGh? Quadro);

public sealed record ComentarioVM(string Autor, string Corpo, string Quando);

public sealed partial class VisaoIssuesVM(VisaoDoQuadro dados, Action<VisaoIssuesVM> escolher) : ObservableObject
{
    public VisaoDoQuadro Dados { get; } = dados;
    public string Nome => Dados.Nome;
    public string Dica => Dados.Filtro.Length > 0 ? Dados.Filtro : Idioma.T("sem filtro");

    [ObservableProperty]
    public partial bool Escolhida { get; set; }

    [RelayCommand]
    void Escolher() => escolher(this);
}

public sealed partial class StatusQuadroVM : ObservableObject
{
    readonly Func<StatusQuadroVM, OpcaoStatus, Task> _mudar;
    bool _voltando;

    public StatusQuadroVM(StatusNoQuadro dados, Func<StatusQuadroVM, OpcaoStatus, Task> mudar)
    {
        Dados = dados;
        _mudar = mudar;
        _voltando = true;
        Escolhida = dados.Opcoes.FirstOrDefault(o => o.Id == dados.AtualId);
        _voltando = false;
    }

    public StatusNoQuadro Dados { get; private set; }
    public string Rotulo => Idioma.F("Status em {0}", Dados.Quadro);
    public IReadOnlyList<OpcaoStatus> Opcoes => Dados.Opcoes;

    [ObservableProperty]
    public partial OpcaoStatus? Escolhida { get; set; }

    [ObservableProperty]
    public partial bool Salvando { get; set; }

    partial void OnEscolhidaChanged(OpcaoStatus? value)
    {
        if (_voltando || value is null || value.Id == Dados.AtualId) return;
        _ = _mudar(this, value);
    }

    public void Confirmar(string opcaoId) => Dados = Dados with { AtualId = opcaoId };

    public void Voltar()
    {
        _voltando = true;
        Escolhida = Dados.Opcoes.FirstOrDefault(o => o.Id == Dados.AtualId);
        _voltando = false;
    }
}

public sealed partial class IssuesVM(JanelaVM janela) : ObservableObject
{
    const string Lista = "lista";

    DateTime _lidoEm;
    bool _lendo;
    bool _trocandoFonte;
    int _versao;
    const int Simultaneas = 12;
    bool? _listaSoMinhas;
    List<(AmbienteVM Projeto, string? Repo, IReadOnlyList<IssueGh>? Issues)> _lista = [];
    Dictionary<string, AmbienteVM> _porRepositorio = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _contasSemPermissao = new(StringComparer.OrdinalIgnoreCase);
    QuadroGh? _quadro;
    string? _visoesDe;
    readonly Dictionary<string, QuadroGh> _detalhados = [];
    VisaoIssuesVM? _visao;
    CampoDoQuadro? _campoGrupo;
    List<IssueLinhaVM> _linhas = [];

    public ColunasIssuesVM Colunas { get; } = new();
    public ObservableCollection<FonteIssuesVM> Fontes { get; } = [];
    public ObservableCollection<VisaoIssuesVM> Visoes { get; } = [];
    public ObservableCollection<GrupoIssuesVM> Grupos { get; } = [];
    public ObservableCollection<StatusQuadroVM> Quadros { get; } = [];

    [ObservableProperty]
    public partial FonteIssuesVM? Fonte { get; set; }

    partial void OnFonteChanged(FonteIssuesVM? value)
    {
        if (_trocandoFonte || value is null || JanelaVM.Demo) return;
        App.Config.QuadroGitHub = value.Quadro?.Id ?? Lista;
        Salvar();
        _ = CarregarFonteAsync();
    }

    [ObservableProperty]
    public partial bool SoMinhas { get; set; } = true;

    partial void OnSoMinhasChanged(bool value)
    {
        if (JanelaVM.Demo)
        {
            if (_itensDemo.Count > 0) FiltrarDemo();
            return;
        }
        if (Fontes.Count > 0) _ = CarregarFonteAsync();
    }

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial string Resumo { get; set; } = "";

    [ObservableProperty]
    public partial bool PrecisaPermissao { get; set; }

    [ObservableProperty]
    public partial string TextoFaltaPermissao { get; set; } = Idioma.T("Sem ela, o Nexora só mostra a lista simples, sem as colunas de status.");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarKanban), nameof(MostrarLista))]
    public partial bool EmLista { get; set; } = App.Config.IssuesEmLista;

    partial void OnEmListaChanged(bool value)
    {
        App.Config.IssuesEmLista = value;
        Salvar();
    }

    public bool MostrarKanban => !EmLista && Colunas.Quadro && _campoGrupo is not null;
    public bool MostrarLista => !MostrarKanban;

    void AvisarModo()
    {
        OnPropertyChanged(nameof(MostrarKanban));
        OnPropertyChanged(nameof(MostrarLista));
    }

    [RelayCommand]
    void VerKanban() => EmLista = false;

    [RelayCommand]
    void VerLista() => EmLista = true;

    bool _preferirQuadro;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoPermissao))]
    public partial bool DandoPermissao { get; set; }

    public string TextoPermissao => DandoPermissao ? Idioma.T("Esperando a janela do gh…") : Idioma.T("Dar permissão");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Espiando), nameof(CabecalhoEspiada), nameof(TemProjeto), nameof(SemProjeto), nameof(MostraComentar))]
    [NotifyCanExecuteChangedFor(nameof(ComentarCommand))]
    public partial IssueLinhaVM? Espiada { get; set; }

    public bool Espiando => Espiada is not null;
    public bool MostraComentar => Espiada is { Item.Numero: > 0 };
    public bool TemProjeto => Espiada?.Projeto is not null;
    public bool SemProjeto => Espiada is not null && Espiada.Projeto is null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoCopiarLink))]
    public partial bool LinkCopiado { get; set; }

    public string TextoCopiarLink => LinkCopiado ? Idioma.T("Link copiado") : Idioma.T("Copiar link");

    public string? LinkDaEspiada => Detalhe?.Url is { Length: > 0 } u ? u : Espiada?.Item.Url is { Length: > 0 } l ? l : null;

    public async void MarcarLinkCopiado()
    {
        LinkCopiado = true;
        await Task.Delay(1600);
        LinkCopiado = false;
    }

    public static string Saudacao(DateTime agora) => Core.Texto.Templates.Saudacao(agora);

    public static string Rascunho(string? url, bool ehPr, string titulo, string? corpo, DateTime agora, string? modelo = null)
    {
        if (modelo is not null && url is { Length: > 0 })
            return Core.Texto.Templates.Preencher(modelo, new Dictionary<string, string?>
            {
                ["saudacao"] = Saudacao(agora),
                ["tipo"] = ehPr ? "PR" : "issue",
                ["url"] = url,
                ["numero"] = NumeroDaUrl().Match(url) is { Success: true } m ? m.Groups[1].Value : null,
                ["titulo"] = titulo,
                ["corpo"] = TopicosDoCorpo(corpo),
            }).Texto;
        var cabeca = url is { Length: > 0 } ? Idioma.F("{0} claude, {1}: {2}", Saudacao(agora), ehPr ? "PR" : "issue", url) : Idioma.F("{0} claude, {1}", Saudacao(agora), titulo);
        return TopicosDoCorpo(corpo) is { Length: > 0 } topicos ? $"{cabeca}\n\n{topicos}" : cabeca;
    }

    public static string TopicosDoCorpo(string? corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo)) return "";
        var texto = ComentarioHtml().Replace(corpo.Replace("\r", ""), "");
        texto = ImagemMarkdown().Replace(texto, "");
        texto = ImagemHtml().Replace(texto, "");
        if (texto.Contains("```")) return LinhasEmBranco().Replace(texto.Trim(), "\n\n");
        var linhas = texto.Split('\n')
            .Select(l => (Recuo: Recuo(l), Texto: Marcador().Replace(l.Trim(), "").Trim()))
            .Where(l => l.Texto.Length > 0);
        return string.Join("\n", linhas.Select(l => new string(' ', l.Recuo) + "- " + l.Texto));
    }

    static int Recuo(string linha)
    {
        var espacos = linha.Replace("\t", "  ").TakeWhile(c => c == ' ').Count();
        return espacos < 2 ? 0 : Math.Min(8, espacos / 2 * 2);
    }

    [GeneratedRegex(@"/(?:issues|pull)/(\d+)")]
    private static partial Regex NumeroDaUrl();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex ComentarioHtml();

    [GeneratedRegex(@"!\[[^\]]*\]\([^)]*\)")]
    private static partial Regex ImagemMarkdown();

    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ImagemHtml();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex LinhasEmBranco();

    [GeneratedRegex(@"^(#{1,6}\s+|[-*+]\s+|\d+[.)]\s+|>\s*)")]
    private static partial Regex Marcador();

    public string CabecalhoEspiada => Espiada is { } e
        ? string.Join("  ·  ", new[] { e.Item.Repositorio, e.Numero, e.Item.EhPr ? "pull request" : "issue", e.Fechada ? Idioma.T("fechada") : null }.Where(t => t is { Length: > 0 }))
        : "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InfoDetalhe), nameof(Comentarios), nameof(TemComentarios))]
    public partial DetalheIssue? Detalhe { get; set; }

    [ObservableProperty]
    public partial string? TextoDetalhe { get; set; }

    [ObservableProperty]
    public partial string? AvisoStatus { get; set; }

    [ObservableProperty]
    public partial string? AvisoSessao { get; set; }

    [ObservableProperty]
    public partial bool CriandoSessao { get; set; }

    public string InfoDetalhe => Detalhe is { } d
        ? string.Join("  ·  ", new[]
        {
            d.Autor.Length > 0 ? Idioma.F("aberta por {0}", d.Autor) : null,
            d.Criada is { } c ? Core.Texto.Tempo.Relativo(c.LocalDateTime) : null,
            d.Comentarios switch { 0 => null, 1 => Idioma.T("1 comentário"), var n => Idioma.F("{0} comentários", n) },
            Espiada is { Item.Responsaveis.Count: > 0 } e ? Idioma.F("com {0}", string.Join(", ", e.Item.Responsaveis)) : null,
        }.OfType<string>())
        : "";

    static void Salvar()
    {
        if (!JanelaVM.Isolada) App.Config.Salvar();
    }

    public void Ativar(bool ligada)
    {
        if (ligada) _ = CarregarAsync(false);
    }

    [RelayCommand]
    Task Atualizar() => CarregarAsync(true);

    async Task CarregarAsync(bool forcar)
    {
        if (_lendo || !forcar && Fontes.Count > 0 && DateTime.UtcNow - _lidoEm < TimeSpan.FromMinutes(5)) return;
        if (JanelaVM.Demo)
        {
            PreencherDemo();
            return;
        }
        _lendo = true;
        Carregando = true;
        Resumo = Idioma.T("Procurando os repositórios e os quadros no GitHub…");
        try
        {
            var espera = DateTime.UtcNow.AddSeconds(15);
            while ((janela.CarregandoAmbientes || janela.Ambientes.Count == 0) && DateTime.UtcNow < espera) await Task.Delay(200);
            var projetos = janela.Ambientes.GroupBy(a => a.Principal, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
            if (!forcar && Fontes.Count == 0) MostrarGuardadas(projetos);
            var soMinhas = SoMinhas;
            var comRepo = await Task.WhenAll(projetos.Select(async p => (Projeto: p, Repo: await GitHub.RepositorioDaPastaAsync(p.Principal))));
            var candidatos = comRepo.Where(x => x.Repo is not null)
                .GroupBy(x => x.Repo!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => MelhorProjeto(g.Key, g.Select(x => x.Projeto)), StringComparer.OrdinalIgnoreCase);
            _contasSemPermissao.Clear();
            _detalhados.Clear();
            _visoesDe = null;
            using var limite = new SemaphoreSlim(Simultaneas);
            var porRepo = (await Task.WhenAll(candidatos.Select(async par =>
            {
                await limite.WaitAsync();
                try
                {
                    var r = await GitHub.IssuesEQuadrosAsync(par.Value.Principal, par.Key, soMinhas);
                    if (r.Quadros.SemPermissao && await GitHub.ContaUsadaAsync(par.Value.Principal) is { } conta)
                        lock (_contasSemPermissao) _contasSemPermissao.Add(conta);
                    return (Repo: par.Key, Lido: r);
                }
                finally
                {
                    limite.Release();
                }
            }))).ToDictionary(x => x.Repo, x => x.Lido, StringComparer.OrdinalIgnoreCase);
            _lista = [.. comRepo.Select(x => (x.Projeto, x.Repo, Issues: x.Repo is null ? null : porRepo[x.Repo].Issues))];
            _listaSoMinhas = soMinhas;
            _porRepositorio = candidatos.Where(par => porRepo[par.Key].Issues is not null).ToDictionary(StringComparer.OrdinalIgnoreCase);
            var quadros = porRepo.Values.Where(r => r.Issues is not null)
                .Select(r => r.Quadros.SemPermissao ? (Falta: true, Quadros: (IReadOnlyList<QuadroGh>)[]) : (Falta: false, Quadros: r.Quadros.Valor ?? []))
                .ToList();
            PrecisaPermissao = quadros.Any(q => q.Falta);
            if (PrecisaPermissao) await ExplicarFaltaAsync();

            var anterior = Fonte?.Quadro?.Id ?? App.Config.QuadroGitHub;
            _trocandoFonte = true;
            Fontes.Clear();
            var abertas = _lista.Where(x => x.Repo is not null).GroupBy(x => x.Repo!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Max(x => x.Issues?.Count ?? 0), StringComparer.OrdinalIgnoreCase);
            foreach (var q in quadros.SelectMany(q => q.Quadros).GroupBy(q => q.Id).Select(g => g.OrderByDescending(x => abertas.GetValueOrDefault(x.Repositorio)).First())
                .OrderByDescending(q => abertas.GetValueOrDefault(q.Repositorio)).ThenBy(q => q.Titulo, StringComparer.CurrentCultureIgnoreCase))
                Fontes.Add(new FonteIssuesVM(q.Titulo, Idioma.F("quadro · {0}", q.Repositorio), q));
            Fontes.Add(new FonteIssuesVM(Idioma.T("Lista simples"), Idioma.T("issues abertas por repositório, sem status"), null));
            var comQuadro = Fontes.Count > 1;
            Fonte = (_preferirQuadro && comQuadro ? Fontes[0] : null)
                ?? Fontes.FirstOrDefault(f => anterior is not null && f.Quadro?.Id == anterior)
                ?? (anterior == Lista ? Fontes[^1] : Fontes[0]);
            if (_preferirQuadro && comQuadro) App.Config.QuadroGitHub = Fontes[0].Quadro?.Id;
            _preferirQuadro = false;
            _trocandoFonte = false;
            _lidoEm = DateTime.UtcNow;
        }
        finally
        {
            _trocandoFonte = false;
            _lendo = false;
        }
        await CarregarFonteAsync();
    }

    static AmbienteVM MelhorProjeto(string repo, IEnumerable<AmbienteVM> candidatos)
    {
        var nome = repo.Split('/').LastOrDefault() ?? repo;
        return candidatos.OrderByDescending(p => string.Equals(p.Nome, nome, StringComparison.OrdinalIgnoreCase))
            .ThenBy(p => p.Nome.Length)
            .First();
    }

    AmbienteVM? ProjetoDe(string repositorio) =>
        repositorio.Length > 0 && _porRepositorio.TryGetValue(repositorio, out var p) ? p : null;

    async Task CarregarFonteAsync()
    {
        var versao = ++_versao;
        Carregando = true;
        try
        {
            var lido = Fonte?.Quadro is { } quadro ? await CarregarQuadroAsync(quadro, versao) : await CarregarListaAsync(versao);
            if (lido && versao == _versao) Guardar();
        }
        finally
        {
            if (versao == _versao) Carregando = false;
        }
    }

    async Task<bool> CarregarListaAsync(int versao)
    {
        if (_listaSoMinhas != SoMinhas)
        {
            Resumo = Idioma.T("Buscando as issues abertas…");
            var soMinhas = SoMinhas;
            using var limite = new SemaphoreSlim(Simultaneas);
            var porRepo = (await Task.WhenAll(_lista.Where(x => x.Repo is not null).GroupBy(x => x.Repo!, StringComparer.OrdinalIgnoreCase).Select(async g =>
            {
                await limite.WaitAsync();
                try
                {
                    var pasta = (ProjetoDe(g.Key) ?? g.First().Projeto).Principal;
                    return (Repo: g.Key, Issues: (await GitHub.IssuesEQuadrosAsync(pasta, g.Key, soMinhas, quadros: false)).Issues);
                }
                finally
                {
                    limite.Release();
                }
            }))).ToDictionary(x => x.Repo, x => x.Issues, StringComparer.OrdinalIgnoreCase);
            if (versao != _versao) return false;
            _lista = [.. _lista.Select(x => (x.Projeto, x.Repo, Issues: x.Repo is null ? null : porRepo[x.Repo]))];
            _listaSoMinhas = soMinhas;
        }
        MostrarListaSimples();
        return true;
    }

    void MostrarListaSimples()
    {
        _quadro = null;
        _visoesDe = null;
        Visoes.Clear();
        Colunas.Quadro = false;
        Colunas.Prioridade = Colunas.Tamanho = Colunas.Iteracao = Colunas.Prs = false;
        Colunas.Atualizada = true;
        _campoGrupo = null;
        AvisarModo();
        _linhas = [];
        foreach (var (repo, issues) in _lista
            .Where(x => x.Repo is not null && x.Issues is { Count: > 0 })
            .GroupBy(x => x.Repo!, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Repo: g.Key, Issues: g.First().Issues!)))
        {
            var projeto = ProjetoDe(repo);
            _linhas.AddRange(issues.Select(i => new IssueLinhaVM(ItemDoQuadro.DaIssue(i, repo), projeto, Colunas, AbrirLinha, AbrirPr)));
        }
        AgruparLista();
        var total = _linhas.Count;
        var repositorios = _linhas.Select(l => l.Item.Repositorio).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var semGithub = _lista.Count(x => x.Issues is null);
        Resumo = total == 0
            ? SoMinhas ? Idioma.T("Nenhuma issue aberta sua ou sem responsável.") : Idioma.T("Nenhuma issue aberta nos projetos.")
            : Idioma.F("{0} em {1}", total == 1 ? Idioma.T("1 issue aberta") : Idioma.F("{0} issues abertas", total), repositorios == 1 ? Idioma.T("1 repositório") : Idioma.F("{0} repositórios", repositorios));
        if (semGithub > 0) Resumo += "  ·  " + Idioma.F("{0} fora do GitHub", semGithub == 1 ? Idioma.T("1 projeto") : Idioma.F("{0} projetos", semGithub));
    }

    async Task<bool> CarregarQuadroAsync(QuadroGh quadro, int versao)
    {
        if (_detalhados.TryGetValue(quadro.Id, out var guardado)) quadro = guardado;
        else if (!quadro.Detalhado)
        {
            Resumo = Idioma.F("Abrindo o quadro {0}…", quadro.Titulo);
            var d = await GitHub.DetalharQuadroAsync(quadro);
            if (versao != _versao) return false;
            if (d.SemPermissao)
            {
                PrecisaPermissao = true;
                Resumo = Idioma.T("Sem permissão para ler o quadro.");
                return false;
            }
            if (d.Valor is { } detalhado) _detalhados[quadro.Id] = quadro = detalhado;
        }
        MostrarVisoes(quadro);
        var (filtro, soMinhas) = FiltroDaVisao();
        Resumo = Idioma.F("Lendo {0}…", _visao?.Nome ?? quadro.Titulo);
        var r = await GitHub.ItensDoQuadroAsync(quadro, filtro);
        if (versao != _versao) return false;
        if (r.SemPermissao)
        {
            PrecisaPermissao = true;
            Resumo = Idioma.T("Sem permissão para ler o quadro.");
            return false;
        }
        if (r.Valor is not { } itens)
        {
            Resumo = Idioma.F("Não deu para ler o quadro: {0}", r.Erro);
            return false;
        }
        if (soMinhas)
        {
            var eu = await GitHub.ContaUsadaAsync(quadro.Pasta);
            if (versao != _versao) return false;
            itens = [.. itens.Where(i => i.MeuOuLivre(eu))];
        }
        MostrarItens(itens);
        return true;
    }

    void MostrarVisoes(QuadroGh quadro)
    {
        _quadro = quadro;
        if (_visoesDe != quadro.Id)
        {
            _visoesDe = quadro.Id;
            Visoes.Clear();
            foreach (var v in quadro.Visoes.Count > 0 ? quadro.Visoes : [new VisaoDoQuadro(Idioma.T("Tudo"), "", null)])
                Visoes.Add(new VisaoIssuesVM(v, EscolherVisao));
            var salva = App.Config.VisoesGitHub.GetValueOrDefault(quadro.Id);
            _visao = Visoes.FirstOrDefault(v => v.Nome == salva) ?? Visoes[0];
        }
        foreach (var v in Visoes) v.Escolhida = v == _visao;
    }

    (string Filtro, bool SoMinhas) FiltroDaVisao()
    {
        var filtro = (_visao?.Dados.Filtro ?? "").Trim();
        return (filtro, SoMinhas && !filtro.Contains("assignee:", StringComparison.OrdinalIgnoreCase));
    }

    void MostrarItens(IEnumerable<ItemDoQuadro> itens)
    {
        _linhas = [.. itens.Select(i => new IssueLinhaVM(i, ProjetoDe(i.Repositorio), Colunas, AbrirLinha, AbrirPr))];
        Agrupar();
        var (filtro, soMinhas) = FiltroDaVisao();
        var n = _linhas.Count;
        Resumo = $"{(n == 1 ? Idioma.T("1 item") : Idioma.F("{0} itens", n))}  ·  {(filtro.Length > 0 ? filtro : Idioma.T("sem filtro"))}{(soMinhas ? "  ·  " + Idioma.T("suas e sem responsável") : "")}";
    }

    void EscolherVisao(VisaoIssuesVM visao)
    {
        if (_quadro is null) return;
        _visao = visao;
        App.Config.VisoesGitHub[_quadro.Id] = visao.Nome;
        Salvar();
        _ = CarregarFonteAsync();
    }

    void Agrupar()
    {
        if (_quadro is not { } quadro) return;
        Colunas.Quadro = true;
        Colunas.Atualizada = false;
        Colunas.Prioridade = quadro.Campo("Priority", "Prioridade") is not null;
        Colunas.Tamanho = quadro.Campo("Size", "Tamanho") is not null;
        Colunas.Iteracao = quadro.CampoIteracao is not null;
        Colunas.Prs = true;
        var campo = (_visao?.Dados.AgruparPor is { } g ? quadro.Campo(g) : null) ?? quadro.Status;
        _campoGrupo = campo;
        AvisarModo();
        var abertos = Grupos.Where(x => !x.Aberto).Select(x => x.Nome).ToHashSet();
        var visiveis = Visiveis();
        Grupos.Clear();
        if (campo is null)
        {
            Grupos.Add(new GrupoIssuesVM(_visao?.Nome ?? quadro.Titulo, "GRAY", visiveis));
            return;
        }
        foreach (var opcao in campo.Opcoes)
        {
            var linhas = visiveis.Where(l => l.Item.Valor(campo.Nome)?.OpcaoId == opcao.Id).ToList();
            if (linhas.Count > 0 || campo == quadro.Status) Grupos.Add(new GrupoIssuesVM(opcao.Nome, opcao.Cor, linhas, opcao.Id) { Aberto = !abertos.Contains(opcao.Nome) });
        }
        var sem = visiveis.Where(l => l.Item.Valor(campo.Nome) is not { } v || campo.Opcoes.All(o => o.Id != v.OpcaoId)).ToList();
        if (sem.Count > 0) Grupos.Add(new GrupoIssuesVM(Idioma.F("Sem {0}", campo.Nome), "GRAY", sem) { Aberto = !abertos.Contains(Idioma.F("Sem {0}", campo.Nome)) });
    }

    void AgruparLista()
    {
        Grupos.Clear();
        foreach (var g in Visiveis().GroupBy(l => l.Item.Repositorio, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()))
            Grupos.Add(new GrupoIssuesVM(g.First().Projeto?.Nome ?? g.Key, "PURPLE", [.. g]));
    }

    List<IssueLinhaVM> Visiveis() => Busca is { Length: > 0 } b ? [.. _linhas.Where(l => l.Item.Combina(b))] : _linhas;

    [ObservableProperty]
    public partial string Busca { get; set; } = "";

    partial void OnBuscaChanged(string value)
    {
        if (_quadro is not null) Agrupar();
        else AgruparLista();
    }

    public bool PodeArrastar(IssueLinhaVM linha) => _quadro is not null && _campoGrupo is not null && linha.Item.ItemId.Length > 0;

    public async Task SoltarAsync(IssueLinhaVM linha, GrupoIssuesVM grupo)
    {
        if (_quadro is not { } quadro || _campoGrupo is not { } campo || grupo.OpcaoId is not { } opcaoId) return;
        var anterior = linha.Item.Valor(campo.Nome);
        if (anterior?.OpcaoId == opcaoId || campo.Opcoes.FirstOrDefault(o => o.Id == opcaoId) is not { } opcao) return;
        var antes = linha.Item;
        var campos = new Dictionary<string, ValorDeCampo>(linha.Item.Campos, StringComparer.OrdinalIgnoreCase) { [campo.Nome] = new ValorDeCampo(opcao.Nome, opcao.Cor, opcao.Id) };
        linha.Trocar(linha.Item with { Campos = campos });
        Agrupar();
        if (JanelaVM.Demo) return;
        var alvo = new StatusNoQuadro(quadro.Titulo, quadro.Id, linha.Item.ItemId, campo.Id, anterior?.OpcaoId, campo.Opcoes);
        var r = await GitHub.MudarStatusAsync(PastaDe(linha), alvo, opcaoId);
        if (r.Ok)
        {
            Resumo = Idioma.F("{0} foi para {1}.", linha.Numero, opcao.Nome);
            return;
        }
        linha.Trocar(antes);
        Agrupar();
        if (GitHub.FaltaPermissaoDeQuadro(r.Saida + r.Erro)) PrecisaPermissao = true;
        Resumo = Idioma.F("Não deu para mover {0}: {1}", linha.Numero, GitHub.FaltaPermissaoDeQuadro(r.Saida + r.Erro) ? Idioma.T("falta a permissão de projetos") : r.Mensagem);
    }

    void AbrirPr(PrLigado pr)
    {
        if (janela.AbrirPrDaSessao(pr))
        {
            FecharEspiada();
            return;
        }
        PainelPrVM.AbrirLink(pr.Url);
    }

    void AbrirLinha(IssueLinhaVM linha)
    {
        if (Espiada is { } anterior) anterior.Escolhida = false;
        Espiada = linha;
        linha.Escolhida = true;
        _ = CarregarEspiadaAsync(linha);
    }

    [RelayCommand]
    void FecharEspiada()
    {
        if (Espiada is { } e) e.Escolhida = false;
        Espiada = null;
    }

    public void EspiarVizinho(int passo)
    {
        var visiveis = Grupos.Where(g => g.Aberto).SelectMany(g => g.Linhas).ToList();
        if (visiveis.Count == 0) return;
        var i = Espiada is null ? -1 : visiveis.IndexOf(Espiada);
        var j = Math.Clamp(i + passo, 0, visiveis.Count - 1);
        if (j != i) AbrirLinha(visiveis[j]);
    }

    public async Task EscolherPrimeiraAsync()
    {
        var espera = DateTime.UtcNow.AddSeconds(60);
        while ((Carregando || _linhas.Count == 0) && DateTime.UtcNow < espera) await Task.Delay(200);
        if (Grupos.SelectMany(g => g.Linhas).FirstOrDefault() is { } primeira) AbrirLinha(primeira);
    }

    string PastaDe(IssueLinhaVM linha) =>
        linha.Projeto?.Principal ?? _quadro?.Pasta ?? _porRepositorio.Values.FirstOrDefault()?.Principal ?? Path.GetTempPath();

    async Task CarregarEspiadaAsync(IssueLinhaVM linha)
    {
        Detalhe = null;
        TextoDetalhe = Idioma.T("Abrindo…");
        Quadros.Clear();
        AvisoStatus = null;
        AvisoSessao = null;
        var item = linha.Item;
        var pasta = PastaDe(linha);
        var branches = linha.Projeto?.GarantirBranchesAsync() ?? Task.CompletedTask;
        Task status;
        if (_quadro is { Status: { } campo } quadro && item.ItemId.Length > 0)
        {
            Quadros.Add(new StatusQuadroVM(new StatusNoQuadro(quadro.Titulo, quadro.Id, item.ItemId, campo.Id, item.Valor(campo.Nome)?.OpcaoId, campo.Opcoes),
                (vm, opcao) => MudarStatusAsync(linha, vm, opcao)));
            status = Task.CompletedTask;
        }
        else status = item.Url.Length > 0 ? CarregarStatusAsync(linha, pasta) : Task.CompletedTask;
        if (item.Numero <= 0)
        {
            TextoDetalhe = Idioma.T("Rascunho do quadro: ainda não virou issue no GitHub.");
        }
        else if (JanelaVM.Demo)
        {
            Detalhe = new DetalheIssue(item.Numero, item.Titulo,
                Idioma.T("Quando o cliente aplica um cupom que já venceu, o desconto ainda entra no total.\n\n**Como reproduzir**\n\n1. Adicione um produto ao carrinho.\n2. Use o cupom `NATAL10`, que venceu ontem.\n3. O total cai 10%.\n\n**Esperado:** o cupom é recusado com a mensagem \"Este cupom venceu\"."),
                item.Url, "ana-dev", [.. item.Etiquetas.Select(e => e.Nome)], DateTimeOffset.Now.AddDays(-2), 3);
            TextoDetalhe = null;
        }
        else
        {
            var detalhe = await GitHub.IssueAsync(pasta, item.Numero, item.Repositorio, item.EhPr);
            if (Espiada == linha)
            {
                Detalhe = detalhe;
                TextoDetalhe = detalhe is null ? Idioma.T("Não deu para abrir pelo gh.") : detalhe.Corpo.Trim().Length == 0 ? Idioma.T("Sem descrição.") : null;
            }
        }
        await Task.WhenAll(status, branches);
    }

    async Task CarregarStatusAsync(IssueLinhaVM linha, string pasta)
    {
        var s = await GitHub.StatusAsync(pasta, linha.Item.Url);
        if (Espiada != linha) return;
        foreach (var q in s.Quadros) Quadros.Add(new StatusQuadroVM(q, (vm, opcao) => MudarStatusAsync(linha, vm, opcao)));
        if (s.SemPermissao)
        {
            PrecisaPermissao = true;
            if (await GitHub.ContaUsadaAsync(pasta) is { } conta) _contasSemPermissao.Add(conta);
        }
    }

    async Task MudarStatusAsync(IssueLinhaVM linha, StatusQuadroVM quadro, OpcaoStatus opcao)
    {
        quadro.Salvando = true;
        AvisoStatus = null;
        var r = await GitHub.MudarStatusAsync(PastaDe(linha), quadro.Dados, opcao.Id);
        quadro.Salvando = false;
        if (r.Ok)
        {
            quadro.Confirmar(opcao.Id);
            AvisoStatus = Idioma.F("Status trocado para {0}.", opcao.Nome);
            if (_quadro is { } atual && quadro.Dados.QuadroId == atual.Id && atual.Status is { } campo)
            {
                var campos = new Dictionary<string, ValorDeCampo>(linha.Item.Campos, StringComparer.OrdinalIgnoreCase) { [campo.Nome] = new ValorDeCampo(opcao.Nome, opcao.Cor, opcao.Id) };
                linha.Trocar(linha.Item with { Campos = campos });
                Agrupar();
            }
            return;
        }
        quadro.Voltar();
        if (GitHub.FaltaPermissaoDeQuadro(r.Saida + r.Erro))
        {
            PrecisaPermissao = true;
            AvisoStatus = Idioma.T("Para trocar o status, o gh precisa da permissão de projetos (botão no topo da página).");
        }
        else AvisoStatus = Idioma.F("Não deu para trocar o status: {0}", r.Mensagem);
    }

    async Task ExplicarFaltaAsync()
    {
        var contas = await GitHub.ContasGhAsync() ?? [];
        var faltam = contas.Where(c => !c.VeQuadros).Select(c => c.Conta).ToList();
        TextoFaltaPermissao = faltam.Count switch
        {
            0 => Idioma.T("Sem ela, o Nexora só mostra a lista simples, sem as colunas de status."),
            1 => Idioma.F("Falta na conta {0}. Sem ela, o Nexora só mostra a lista simples, sem as colunas de status.", faltam[0]),
            _ => Idioma.F("Falta nas contas {0}. Sem ela, o Nexora só mostra a lista simples, sem as colunas de status.", string.Join(Idioma.T(" e "), faltam)),
        };
    }

    [RelayCommand]
    async Task DarPermissao()
    {
        if (DandoPermissao) return;
        DandoPermissao = true;
        try
        {
            await PermissaoGh.PedirAsync(_contasSemPermissao);
        }
        finally
        {
            DandoPermissao = false;
        }
        PrecisaPermissao = false;
        _preferirQuadro = true;
        await CarregarAsync(true);
    }

    [RelayCommand]
    async Task ComecarEmWorktree()
    {
        if (Espiada is not { Projeto: { } projeto } linha || CriandoSessao) return;
        CriandoSessao = true;
        AvisoSessao = Idioma.F("Criando o worktree a partir de {0}…", projeto.BaseTarefa ?? Idioma.T("branch padrão"));
        try
        {
            var nome = linha.Item.Numero > 0 ? $"{linha.Item.Numero} {linha.Titulo}" : linha.Titulo;
            var (novo, mensagem) = await projeto.CriarWorktreeAsync(nome, projeto.BaseTarefa);
            if (novo is null)
            {
                AvisoSessao = mensagem;
                return;
            }
            FecharEspiada();
            Comecar(novo.Local, linha);
        }
        finally
        {
            CriandoSessao = false;
        }
    }

    [RelayCommand]
    void NovaSessao()
    {
        if (Espiada is not { Projeto: { } projeto } linha) return;
        FecharEspiada();
        Comecar(projeto.Local, linha);
    }

    void Comecar(Local local, IssueLinhaVM linha)
    {
        var url = Detalhe?.Url is { Length: > 0 } u ? u : linha.Item.Url;
        var corpo = Detalhe is { } d && d.Numero == linha.Item.Numero ? d.Corpo : null;
        var rascunho = Rascunho(url, linha.Item.EhPr, linha.Titulo, corpo, DateTime.Now, TemplatesDoApp.DaIssue?.Texto);
        if (linha.Item.Numero <= 0 || linha.Item.Url.Length == 0)
        {
            janela.AbrirSessaoDaIssue(local, rascunho, null, corpo);
            return;
        }
        janela.AbrirSessaoDaIssue(local, rascunho, new IssueDaSessao(linha.Item.Numero, linha.Item.Repositorio, linha.Item.Url, linha.Titulo), corpo);
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ComentarCommand))]
    public partial string NovoComentario { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ComentarCommand))]
    public partial bool Comentando { get; set; }

    [ObservableProperty]
    public partial string? AvisoComentario { get; set; }

    public IReadOnlyList<ComentarioVM> Comentarios =>
        [.. (Detalhe?.Lista ?? []).Select(c => new ComentarioVM(c.Autor, c.Corpo, c.Quando is { } q ? Core.Texto.Tempo.Relativo(q.LocalDateTime) : ""))];

    public bool TemComentarios => Detalhe?.Lista.Count > 0;

    static readonly HttpClient Http = CriarHttp();
    readonly Dictionary<string, Task<Avalonia.Media.Imaging.Bitmap?>> _imagens = [];
    Task<IReadOnlyDictionary<string, string>>? _assinadas;
    string? _assinadasDe;
    DateTime _assinadasEm;

    static HttpClient CriarHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Nexora/{Atualizacoes.Atual.ToString(3)}");
        return http;
    }

    public Func<string, Task<Avalonia.Media.Imaging.Bitmap?>> CarregarImagem => ImagemAsync;

    Task<Avalonia.Media.Imaging.Bitmap?> ImagemAsync(string url)
    {
        lock (_imagens)
        {
            if (!_imagens.TryGetValue(url, out var tarefa)) _imagens[url] = tarefa = BaixarImagemAsync(url, Espiada);
            return tarefa;
        }
    }

    async Task<Avalonia.Media.Imaging.Bitmap?> BaixarImagemAsync(string url, IssueLinhaVM? linha)
    {
        try
        {
            var alvo = url;
            if (Uri.TryCreate(url, UriKind.Absolute, out var endereco) && endereco.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase)
                && Core.Texto.ImagensMd.Chave(url) is { } chave && linha is not null && !JanelaVM.Demo
                && (await Assinadas(linha)).TryGetValue(chave, out var assinada))
                alvo = assinada;
            var bytes = await Http.GetByteArrayAsync(alvo);
            return await Task.Run(() =>
            {
                using var fluxo = new MemoryStream(bytes);
                return new Avalonia.Media.Imaging.Bitmap(fluxo);
            });
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or ArgumentException or InvalidOperationException or NotSupportedException or IOException)
        {
            lock (_imagens) _imagens.Remove(url);
            return null;
        }
    }

    Task<IReadOnlyDictionary<string, string>> Assinadas(IssueLinhaVM linha)
    {
        var chave = $"{linha.Item.Repositorio}#{linha.Item.Numero}";
        if (_assinadas is null || _assinadasDe != chave || DateTime.UtcNow - _assinadasEm > TimeSpan.FromMinutes(4))
        {
            _assinadasDe = chave;
            _assinadasEm = DateTime.UtcNow;
            _assinadas = GitHub.ImagensAssinadasAsync(PastaDe(linha), linha.Item.Repositorio, linha.Item.Numero);
        }
        return _assinadas;
    }

    bool PodeComentar() => !Comentando && !string.IsNullOrWhiteSpace(NovoComentario) && Espiada is { Item.Numero: > 0 };

    [RelayCommand(CanExecute = nameof(PodeComentar))]
    async Task Comentar()
    {
        if (Espiada is not { } linha) return;
        var texto = NovoComentario.Trim();
        Comentando = true;
        AvisoComentario = null;
        try
        {
            if (JanelaVM.Demo)
            {
                if (Detalhe is { } d) Detalhe = d with { Comentarios = d.Comentarios + 1, Lista = [.. d.Lista, new ComentarioIssue("voce", texto, DateTimeOffset.Now)] };
                NovoComentario = "";
                return;
            }
            var pasta = PastaDe(linha);
            var r = await GitHub.ComentarAsync(pasta, linha.Item.Numero, linha.Item.Repositorio, texto, linha.Item.EhPr);
            if (!r.Ok)
            {
                AvisoComentario = Idioma.F("Não deu para comentar: {0}", r.Mensagem);
                return;
            }
            NovoComentario = "";
            AvisoComentario = Idioma.T("Comentário enviado.");
            var novo = await GitHub.IssueAsync(pasta, linha.Item.Numero, linha.Item.Repositorio, linha.Item.EhPr);
            if (Espiada == linha && novo is not null) Detalhe = novo;
        }
        finally
        {
            Comentando = false;
        }
    }

    [RelayCommand]
    void AbrirNoGitHub()
    {
        var url = Detalhe?.Url ?? Espiada?.Item.Url;
        if (url is not { Length: > 0 } || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            Sistema.Abrir(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    [RelayCommand]
    void AbrirQuadroNoGitHub()
    {
        if (_quadro?.Url is not { Length: > 0 } url || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            Sistema.Abrir(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    void PreencherDemo()
    {
        var status = new CampoDoQuadro("F1", "Status", [new("s1", "Ready", "RED"), new("s2", "In progress", "ORANGE"), new("s3", "In review", "PURPLE"), new("s4", "Done", "GREEN")]);
        var prioridade = new CampoDoQuadro("F2", "Priority", [new("p1", "High", "RED"), new("p2", "Medium", "YELLOW"), new("p3", "Low", "BLUE")]);
        var tamanho = new CampoDoQuadro("F3", "Size", [new("t1", "Small", "GREEN"), new("t2", "Medium", "YELLOW"), new("t3", "Large", "ORANGE")]);
        var quadro = new QuadroGh("Q1", "Loja · Sprint", 1, "https://github.com", "voce/loja", @"D:\Projetos\Loja")
        {
            Campos = [status, prioridade, tamanho],
            CampoIteracao = "Iteration",
            Visoes = [new(Idioma.T("Sprint atual"), "iteration:@current", "Status"), new(Idioma.T("Meus"), "assignee:@me", "Status"), new("Backlog", "status:Backlog", null), new(Idioma.T("Concluído"), "status:Done", null)],
            Detalhado = true,
        };
        ItemDoQuadro Item(int n, string titulo, string quem, string st, string? pri, string? tam, string[] etiquetas, int[] prs) =>
            new($"I{n}", "Issue", n, titulo, $"https://github.com/voce/loja/issues/{n}", "OPEN", "voce/loja", quem.Length > 0 ? [quem] : [],
                [.. etiquetas.Select(e => new Etiqueta(e, e switch { "bug" => "d73a4a", "checkout" => "0e8a16", "ux" => "5319e7", _ => "a2eeef" }))],
                new Dictionary<string, ValorDeCampo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Status"] = new(status.Opcoes.First(o => o.Id == st).Nome, status.Opcoes.First(o => o.Id == st).Cor, st),
                    ["Priority"] = pri is null ? null! : new(prioridade.Opcoes.First(o => o.Id == pri).Nome, prioridade.Opcoes.First(o => o.Id == pri).Cor, pri),
                    ["Size"] = tam is null ? null! : new(tamanho.Opcoes.First(o => o.Id == tam).Nome, tamanho.Opcoes.First(o => o.Id == tam).Cor, tam),
                }.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase),
                "Iteration 12", prs, DateTimeOffset.Now.AddHours(-n % 30))
            {
                PrsLigados = PrsDemo.Ligados(n),
            };
        var itens = new[]
        {
            Item(48, Idioma.T("Cupom vencido ainda aplica desconto"), "voce", "s2", "p1", "t1", ["bug", "checkout"], [52]),
            Item(51, Idioma.T("Frete grátis acima de R$ 199"), "ana-dev", "s1", "p2", null, ["enhancement"], []),
            Item(55, Idioma.T("Revisar textos da página de pedido"), "", "s1", "p3", "t1", [], []),
            Item(42, Idioma.T("Carrinho perde itens ao trocar de aba"), "voce", "s1", "p1", "t2", ["bug"], []),
            Item(44, Idioma.T("Novo layout do resumo do pedido"), "bruno", "s2", "p2", "t3", ["ux"], [54]),
            Item(39, Idioma.T("Pix com QR dinâmico"), "voce", "s3", "p2", "t2", ["checkout"], [46, 53]),
        };
        _porRepositorio = janela.Ambientes.Take(1).ToDictionary(a => "voce/loja", a => a, StringComparer.OrdinalIgnoreCase);
        _trocandoFonte = true;
        Fontes.Clear();
        Fontes.Add(new FonteIssuesVM(quadro.Titulo, quadro.Repositorio, quadro));
        Fontes.Add(new FonteIssuesVM(Idioma.T("Issues abertas"), Idioma.T("por projeto, sem quadro"), null));
        Fonte = Fontes[0];
        _trocandoFonte = false;
        _quadro = quadro;
        Visoes.Clear();
        foreach (var v in quadro.Visoes) Visoes.Add(new VisaoIssuesVM(v, EscolherVisaoDemo));
        _visao = Visoes[0];
        _visao.Escolhida = true;
        _itensDemo = itens;
        FiltrarDemo();
        _lidoEm = DateTime.UtcNow;
    }

    IReadOnlyList<ItemDoQuadro> _itensDemo = [];

    void EscolherVisaoDemo(VisaoIssuesVM visao)
    {
        _visao = visao;
        foreach (var v in Visoes) v.Escolhida = v == visao;
        FiltrarDemo();
    }

    void FiltrarDemo()
    {
        var filtro = (_visao?.Dados.Filtro ?? "").Trim();
        var soMinhas = SoMinhas && !filtro.Contains("assignee:", StringComparison.OrdinalIgnoreCase);
        IEnumerable<ItemDoQuadro> itens = _itensDemo;
        if (filtro.Contains("assignee:@me", StringComparison.OrdinalIgnoreCase))
            itens = itens.Where(i => i.Responsaveis.Contains("voce", StringComparer.OrdinalIgnoreCase));
        if (filtro.StartsWith("status:", StringComparison.OrdinalIgnoreCase))
        {
            var status = filtro["status:".Length..];
            itens = itens.Where(i => string.Equals(i.Valor("Status")?.Nome, status, StringComparison.OrdinalIgnoreCase));
        }
        if (soMinhas) itens = itens.Where(i => i.MeuOuLivre("voce"));
        _linhas = [.. itens.Select(i => new IssueLinhaVM(i, ProjetoDe(i.Repositorio), Colunas, AbrirLinha, AbrirPr))];
        Agrupar();
        var n = _linhas.Count;
        Resumo = $"{(n == 1 ? Idioma.T("1 item") : Idioma.F("{0} itens", n))}  ·  {(filtro.Length > 0 ? filtro : Idioma.T("sem filtro"))}{(soMinhas ? "  ·  " + Idioma.T("suas e sem responsável") : "")}";
    }
}
