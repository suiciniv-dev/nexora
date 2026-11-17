using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Uso;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class UsoVM(JanelaVM janela) : ObservableObject
{
    const int Semanas = 8;
    static CultureInfo Br => Idioma.Cultura;
    IReadOnlyList<Parcela> _parcelas = [];
    Plano? _plano;
    bool _lendo;

    static string Cache => Path.Combine(JanelaVM.Isolada ? Path.GetTempPath() : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nexora", "uso.json");

    public ObservableCollection<JanelaDoPlanoVM> Plano { get; } = [];
    public ObservableCollection<LinhaUsoVM> Linhas { get; } = [];
    public ObservableCollection<FiltroUsoVM> Agentes { get; } = [];
    public ObservableCollection<FiltroUsoVM> Modelos { get; } = [];

    [ObservableProperty]
    public partial string? Agente { get; set; }

    [ObservableProperty]
    public partial string? Modelo { get; set; }

    public bool MostraAgentes => Agentes.Count > 2;
    public bool MostraModelos => Modelos.Count > 2;
    public bool MostraFiltros => MostraAgentes || MostraModelos;

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial string TextoPlano { get; set; } = Idioma.T("Lendo o /usage do Claude Code…");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeVoltar), nameof(PodeAvancar))]
    [NotifyCanExecuteChangedFor(nameof(SemanaAnteriorCommand), nameof(ProximaSemanaCommand))]
    public partial int Deslocamento { get; set; }

    [ObservableProperty]
    public partial string TituloSemana { get; set; } = "";

    [ObservableProperty]
    public partial string ResumoSemana { get; set; } = "";

    public bool PodeVoltar => Deslocamento > -(Semanas - 1);
    public bool PodeAvancar => Deslocamento < 0;
    public bool SemDados => Linhas.Count == 0 && !Carregando;

    public void Ativar(bool ligada)
    {
        if (ligada) _ = CarregarAsync(false);
    }

    [RelayCommand]
    Task Atualizar() => CarregarAsync(true);

    [RelayCommand(CanExecute = nameof(PodeVoltar))]
    void SemanaAnterior() => Deslocamento--;

    [RelayCommand(CanExecute = nameof(PodeAvancar))]
    void ProximaSemana() => Deslocamento++;

    partial void OnDeslocamentoChanged(int value) => Montar();

    partial void OnAgenteChanged(string? value) => Montar();

    partial void OnModeloChanged(string? value) => Montar();

    public static IReadOnlyList<Parcela> Filtrar(IEnumerable<Parcela> parcelas, string? agente, string? modelo) =>
        [.. parcelas.Where(p => (agente is null || p.Agente == agente) && (modelo is null || p.Modelo == modelo))];

    public static IReadOnlyList<string> OpcoesDeAgente(IEnumerable<Parcela> parcelas) =>
        [.. parcelas.GroupBy(p => p.Agente).OrderByDescending(g => g.Key == UsoLocal.Claude).ThenByDescending(g => g.Sum(p => p.Peso)).Select(g => g.Key)];

    public static IReadOnlyList<string> OpcoesDeModelo(IEnumerable<Parcela> parcelas, string? agente) =>
        [.. parcelas.Where(p => agente is null || p.Agente == agente).GroupBy(p => p.Modelo).OrderByDescending(g => g.Sum(p => p.Peso)).Select(g => g.Key)];

    void MontarFiltros()
    {
        var agentes = OpcoesDeAgente(_parcelas);
        if (Agente is { } a && !agentes.Contains(a))
        {
            Agente = null;
            return;
        }
        var modelos = OpcoesDeModelo(_parcelas, Agente);
        if (Modelo is { } m && !modelos.Contains(m))
        {
            Modelo = null;
            return;
        }
        Encher(Agentes, Idioma.T("Todos os agentes"), agentes, Agente, v => Agente = v);
        Encher(Modelos, Idioma.T("Todos os modelos"), modelos, Modelo, v => Modelo = v);
        OnPropertyChanged(nameof(MostraAgentes));
        OnPropertyChanged(nameof(MostraModelos));
        OnPropertyChanged(nameof(MostraFiltros));
    }

    static void Encher(ObservableCollection<FiltroUsoVM> lista, string todos, IReadOnlyList<string> opcoes, string? atual, Action<string?> escolher)
    {
        lista.Clear();
        lista.Add(new FiltroUsoVM(todos, null, atual is null, escolher));
        foreach (var o in opcoes) lista.Add(new FiltroUsoVM(o == "outro" ? Idioma.T("Outros") : o, o, o == atual, escolher));
    }

    async Task CarregarAsync(bool forcar)
    {
        if (JanelaVM.Demo)
        {
            _parcelas = Demonstracao();
            var reinicioDaSemana = DateTime.Today.AddDays(3).AddHours(9);
            _plano = new Plano(new("Sessão", 4, DateTime.Now.AddHours(3)), new("Semana", 35, reinicioDaSemana), [new("Sonnet", 12, reinicioDaSemana)], DateTime.Now);
            MontarPlano();
            Montar();
            return;
        }
        if (_lendo) return;
        _lendo = true;
        Carregando = true;
        OnPropertyChanged(nameof(SemDados));
        try
        {
            var desde = DateTime.Now.Date.AddDays(-7 * Semanas);
            _parcelas = await App.Nucleo.LerUsoAsync(Cache, desde, porDia: false);
            Montar();
            if (forcar || _plano is null || DateTime.Now - _plano.LidoEm > TimeSpan.FromMinutes(2))
            {
                if (LocalizadorClaude.Procurar() is { } claude && await PlanoDoClaude.LerAsync(claude) is { } plano) _plano = plano;
                MontarPlano();
                Montar();
            }
        }
        finally
        {
            _lendo = false;
            Carregando = false;
            OnPropertyChanged(nameof(SemDados));
        }
    }

    void MontarPlano()
    {
        Plano.Clear();
        if (_plano is null)
        {
            TextoPlano = Idioma.T("Não deu para ler o /usage agora. Os números por projeto continuam valendo.");
            return;
        }
        foreach (var j in new[] { _plano.Sessao, _plano.Semana }.Concat(_plano.PorModelo).OfType<JanelaDoPlano>())
            Plano.Add(new JanelaDoPlanoVM(j.Nome == "Semana" ? Idioma.T("Semana, todos os modelos") : j.Nome == "Sessão" ? Idioma.T("Sessão atual") : Idioma.F("Semana, {0}", j.Nome), j.Percentual, Reinicio(j.Reinicia)));
        TextoPlano = Idioma.F("Lido do /usage às {0:HH:mm}. Não gasta tokens.", _plano.LidoEm);
    }

    static string Reinicio(DateTime? quando) => quando switch
    {
        null => "",
        { } q when q.Date == DateTime.Today => Idioma.F("reinicia hoje às {0:HH:mm}", q),
        { } q when q.Date == DateTime.Today.AddDays(1) => Idioma.F("reinicia amanhã às {0:HH:mm}", q),
        { } q => Idioma.F("reinicia {0} às {1:HH:mm}", q.ToString(Idioma.T("ddd, d 'de' MMM"), Br), q),
    };

    static readonly string ForaDeProjetos = Idioma.T("Fora de projetos");
    static readonly string OutrasPastas = Idioma.T("Outras pastas");

    string ProjetoDe(string pasta)
    {
        if (janela.DonoDaPasta(pasta)?.Nome is { } nome) return nome;
        var casa = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.Equals(pasta.TrimEnd('\\', '/'), casa, StringComparison.OrdinalIgnoreCase)) return ForaDeProjetos;
        return Path.GetFileName(pasta.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : pasta;
    }

    void Montar()
    {
        MontarFiltros();
        var parcelas = Filtrar(_parcelas, Agente, Modelo);
        var semanaAtual = UsoLocal.SemanaDe(DateTime.Now);
        var escolhida = semanaAtual.AddDays(7 * Deslocamento);
        var chave = UsoLocal.Chave(escolhida);
        TituloSemana = (Deslocamento switch { 0 => Idioma.T("Esta semana"), -1 => Idioma.T("Semana passada"), _ => Idioma.T("Semana") })
            + Idioma.F(" · {0} a {1}", escolhida.ToString(Idioma.T("d 'de' MMM"), Br), escolhida.AddDays(6).ToString(Idioma.T("d 'de' MMM"), Br));
        var chaves = Enumerable.Range(0, Semanas).Select(i => UsoLocal.Chave(semanaAtual.AddDays(-7 * (Semanas - 1 - i)))).ToList();
        var nomeDe = parcelas.Select(p => p.Pasta).Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(p => p, ProjetoDe, StringComparer.OrdinalIgnoreCase);
        var pesoNaSemana = parcelas.Where(p => p.Semana == chave).GroupBy(p => nomeDe[p.Pasta], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Peso), StringComparer.OrdinalIgnoreCase);
        var totalNaSemana = pesoNaSemana.Values.Sum();
        string Agrupado(Parcela p)
        {
            var nome = nomeDe[p.Pasta];
            return nome != ForaDeProjetos && totalNaSemana > 0 && pesoNaSemana.GetValueOrDefault(nome) / totalNaSemana < 0.005 ? OutrasPastas : nome;
        }
        var porProjeto = parcelas.GroupBy(Agrupado, StringComparer.OrdinalIgnoreCase).ToList();
        var daSemana = porProjeto
            .Select(g => (Nome: g.Key, Soma: Somar(g.Where(p => p.Semana == chave)), Todas: g.ToList()))
            .Where(x => x.Soma.Respostas > 0)
            .OrderBy(x => x.Nome == OutrasPastas)
            .ThenByDescending(x => x.Soma.Peso)
            .ToList();
        var total = daSemana.Sum(x => x.Soma.Peso);
        var soClaude = Modelo is null && parcelas.All(p => p.Agente == UsoLocal.Claude);
        var semanaPlano = Deslocamento == 0 && soClaude ? _plano?.Semana?.Percentual : null;
        Linhas.Clear();
        foreach (var (nome, soma, todas) in daSemana)
        {
            var fracao = total > 0 ? soma.Peso / total : 0;
            var historico = chaves.Select(c =>
            {
                var peso = todas.Where(p => p.Semana == c).Sum(p => p.Peso);
                var totalDaSemana = parcelas.Where(p => p.Semana == c).Sum(p => p.Peso);
                var parte = totalDaSemana > 0 ? peso / totalDaSemana : 0;
                var dia = DateOnly.ParseExact(c, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                return new BarraVM(Math.Max(2, parte * 28), Idioma.F("{0}: {1:P0} da semana", dia.ToString(Idioma.T("d 'de' MMM"), Br), parte), c == chave);
            }).ToList();
            var modelos = soma.Modelos.Where(m => soma.Peso > 0 && m.Value / soma.Peso >= 0.01).OrderByDescending(m => m.Value)
                .Select(m => $"{m.Key} {(m.Value / soma.Peso).ToString("P0", Br)}");
            var detalhe = Idioma.F("{0}  ·  {1}  ·  {2} de saída", Sessoes(soma.Sessoes.Count), Respostas(soma.Respostas), Milhoes(soma.Saida));
            Linhas.Add(new LinhaUsoVM(nome, Siglas.De(nome), fracao, Idioma.F("{0:P0} da semana", fracao),
                semanaPlano is { } p ? Idioma.F("≈ {0:0.#}% do plano", fracao * p) : null, detalhe, string.Join("  ·  ", modelos), historico));
        }
        var respostas = daSemana.Sum(x => x.Soma.Respostas);
        var sessoes = daSemana.SelectMany(x => x.Soma.Sessoes).Distinct().Count();
        var projetos = daSemana.Count(x => x.Nome != OutrasPastas && x.Nome != ForaDeProjetos);
        var vazio = Agente is null && Modelo is null ? Idioma.T("Nenhuma resposta do Claude nesta semana.") : Idioma.T("Nenhuma resposta com este filtro nesta semana.");
        ResumoSemana = respostas == 0 ? vazio : Idioma.F("{0}, {1}, {2}", Sessoes(sessoes), Respostas(respostas), projetos == 1 ? Idioma.T("1 projeto") : Idioma.F("{0} projetos", projetos));
        OnPropertyChanged(nameof(SemDados));
    }

    static List<Parcela> Demonstracao()
    {
        const string raiz = @"D:\Projetos";
        (string Pasta, double Peso, double Opus)[] projetos =
        [
            ($@"{raiz}\Loja", 42, 0.8),
            ($@"{raiz}\Loja-pedidos", 23, 0.6),
            ($@"{raiz}\App.Notas", 14, 0.4),
            ($@"{raiz}\Loja.Api", 9, 0.7),
            (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 6, 0.3),
        ];
        var semanaAtual = UsoLocal.SemanaDe(DateTime.Now);
        var parcelas = new List<Parcela>();
        for (var s = 0; s < Semanas; s++)
            for (var i = 0; i < projetos.Length; i++)
            {
                var (pasta, peso, opus) = projetos[i];
                var p = peso * (0.55 + (s * 7 + i * 3) % 9 / 10.0);
                parcelas.Add(new Parcela
                {
                    Pasta = pasta,
                    Semana = UsoLocal.Chave(semanaAtual.AddDays(-7 * s)),
                    Peso = p,
                    Respostas = (int)(p * 21),
                    Entrada = (long)(p * 9_000),
                    Saida = (long)(p * 31_000),
                    Sessoes = [.. Enumerable.Range(0, 1 + (int)(p / 6)).Select(n => $"demo-{i}-{s}-{n}")],
                    Modelo = "Opus",
                    Modelos = new() { ["Opus"] = p * opus },
                });
                parcelas.Add(new Parcela
                {
                    Pasta = pasta,
                    Semana = UsoLocal.Chave(semanaAtual.AddDays(-7 * s)),
                    Peso = p * (1 - opus),
                    Respostas = (int)(p * 14),
                    Saida = (long)(p * 12_000),
                    Sessoes = [$"demo-{i}-{s}-0"],
                    Modelo = "Sonnet",
                    Modelos = new() { ["Sonnet"] = p * (1 - opus) },
                });
                if (i < 3)
                    parcelas.Add(new Parcela
                    {
                        Agente = UsoLocal.Codex,
                        Pasta = pasta,
                        Semana = UsoLocal.Chave(semanaAtual.AddDays(-7 * s)),
                        Peso = p * 0.3,
                        Respostas = (int)(p * 6),
                        Saida = (long)(p * 8_000),
                        Sessoes = [$"codex-{i}-{s}"],
                        Modelo = "GPT-5.1 Codex",
                        Modelos = new() { ["GPT-5.1 Codex"] = p * 0.3 },
                    });
            }
        return parcelas;
    }

    static Parcela Somar(IEnumerable<Parcela> parcelas)
    {
        var soma = new Parcela();
        foreach (var p in parcelas) soma.Somar(p);
        return soma;
    }

    static string Sessoes(int n) => n == 1 ? Idioma.T("1 sessão") : Idioma.F("{0} sessões", n);
    static string Respostas(int n) => n == 1 ? Idioma.T("1 resposta") : Idioma.F("{0:N0} respostas", n);
    static string Milhoes(long tokens) => tokens >= 1_000_000 ? Idioma.F("{0:0.#} mi tokens", tokens / 1_000_000.0) : Idioma.F("{0:0} mil tokens", tokens / 1000.0);
}
