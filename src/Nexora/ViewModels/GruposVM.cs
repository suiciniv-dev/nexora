using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class GrupoProjetoVM : ObservableObject
{
    readonly JanelaVM _janela;

    public GrupoProjetoVM(JanelaVM janela, AmbienteVM? ambiente, string nome)
    {
        _janela = janela;
        Ambiente = ambiente;
        Nome = nome;
        Expandido = !App.Config.ProjetosFechados.Contains(nome);
    }

    public AmbienteVM? Ambiente { get; }
    public string Nome { get; }
    public string? Branch => Ambiente?.Branch;
    public ObservableCollection<ChatVM> Sessoes { get; } = [];
    public bool TemSessoes => Sessoes.Count > 0;
    public string Contagem => Sessoes.Count == 0 ? "" : Sessoes.Count.ToString();

    [ObservableProperty]
    public partial bool Expandido { get; set; }

    partial void OnExpandidoChanged(bool value)
    {
        App.Config.ProjetosFechados.Remove(Nome);
        if (!value) App.Config.ProjetosFechados.Add(Nome);
        if (!JanelaVM.Isolada) App.Config.Salvar();
    }

    public ObservableCollection<RamoVM> Ramos { get; } = [];
    public ChatVM? Primeira => Sessoes.Count > 0 ? Sessoes[0] : null;
    public string Iniciais => Siglas.De(Nome);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemParadas), nameof(TextoParadas))]
    public partial int Paradas { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoParadas))]
    public partial bool MostrarParadas { get; set; }

    public bool TemParadas => Paradas > 0;

    public string TextoParadas => MostrarParadas ? Idioma.T("Esconder as paradas")
        : Paradas == 1 ? Idioma.T("1 parada há mais de 1 h") : Idioma.F("{0} paradas há mais de 1 h", Paradas);

    string _assinatura = "";

    partial void OnMostrarParadasChanged(bool value) => Arrumar();

    [RelayCommand]
    void AlternarParadas() => MostrarParadas = !MostrarParadas;

    public void Avisar()
    {
        OnPropertyChanged(nameof(TemSessoes));
        OnPropertyChanged(nameof(Contagem));
        OnPropertyChanged(nameof(Branch));
        OnPropertyChanged(nameof(Primeira));
        Arrumar();
    }

    public void Arrumar()
    {
        var chaves = Sessoes.Select(c => c.Branch ?? "").Distinct().ToList();
        var principal = Ambiente?.Principal.TrimEnd('\\', '/');
        var cabecalhos = chaves.Count > 1 || chaves.Count == 1 && chaves[0].Length > 0 && Ambiente is not null && chaves[0] != Ambiente.Branch;
        var assinatura = string.Join("|", Sessoes.Select(c => $"{c.Branch}:{c.Codigo}")) + cabecalhos;
        if (assinatura != _assinatura)
        {
            _assinatura = assinatura;
            Ramos.Clear();
            foreach (var chave in chaves)
            {
                var dele = Sessoes.Where(c => (c.Branch ?? "") == chave).ToList();
                var worktree = principal is not null && dele.Any(c => !Core.Ambientes.Workspaces.MesmaPasta(c.Pasta.TrimEnd('\\', '/'), principal));
                var ramo = new RamoVM(chave.Length > 0 ? chave : null, worktree, cabecalhos && chave.Length > 0);
                foreach (var c in dele) ramo.Sessoes.Add(c);
                Ramos.Add(ramo);
            }
        }
        var paradas = 0;
        foreach (var c in Sessoes)
        {
            var parada = c.ParadaHaTempo;
            if (parada) paradas++;
            c.RecolhidaNaLateral = parada && !MostrarParadas;
        }
        Paradas = paradas;
        if (paradas == 0) MostrarParadas = false;
    }

    [RelayCommand]
    void Alternar() => Expandido = !Expandido;

    [RelayCommand]
    void Abrir()
    {
        if (Ambiente is not null) _janela.Navegar(Ambiente);
    }

    [RelayCommand]
    void NovaSessao()
    {
        if (Ambiente is not null) _janela.AbrirSessao(Ambiente.Local);
    }
}

public sealed partial class GrupoHistoricoVM(string nome, IReadOnlyList<ConversaVM> todas) : ObservableObject
{
    const int Curto = 5;

    public string Nome { get; } = nome;
    public IReadOnlyList<ConversaVM> Todas { get; } = todas;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Visiveis), nameof(TemMais), nameof(TextoMais))]
    public partial bool Completo { get; set; } = App.Config.ConversasCompletas.Contains(nome);

    [ObservableProperty]
    public partial bool Expandido { get; set; } = !App.Config.ConversasFechadas.Contains(nome);

    partial void OnCompletoChanged(bool value) => Lembrar(App.Config.ConversasCompletas, value);

    partial void OnExpandidoChanged(bool value) => Lembrar(App.Config.ConversasFechadas, !value);

    void Lembrar(List<string> lista, bool presente)
    {
        var mudou = presente ? !lista.Contains(Nome) : lista.Remove(Nome);
        if (presente && mudou) lista.Add(Nome);
        if (mudou && !JanelaVM.Isolada) App.Config.Salvar();
    }

    public IReadOnlyList<ConversaVM> Visiveis => Completo ? Todas : [.. Todas.Take(Curto)];
    public bool TemMais => Todas.Count > Curto;
    public string TextoMais => Completo ? Idioma.T("Mostrar menos") : Idioma.F("Mostrar mais {0}", Todas.Count - Curto);

    [RelayCommand]
    void AlternarMais() => Completo = !Completo;

    [RelayCommand]
    void Alternar() => Expandido = !Expandido;
}

public sealed partial class LinhaRecursoVM(string nome, string detalhe, long memoria, bool ativo, Func<Task>? parar) : ObservableObject
{
    public string Nome { get; } = nome;
    public string Detalhe { get; } = detalhe;
    public long Memoria { get; } = memoria;
    public bool Ativo { get; } = ativo;
    public string TextoMemoria => Ativo ? RecursosVM.Formatar(Memoria) : "parado";
    public bool PodeParar => parar is not null && Ativo;

    [RelayCommand]
    async Task Parar()
    {
        if (parar is not null) await parar();
    }
}

public sealed partial class RecursosVM : ObservableObject
{
    readonly JanelaVM _janela;
    readonly DispatcherTimer _relogio;

    public RecursosVM(JanelaVM janela)
    {
        _janela = janela;
        _relogio = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Background, (_, _) => _ = AtualizarAsync());
    }

    public ObservableCollection<LinhaRecursoVM> Mcps { get; } = [];
    public ObservableCollection<LinhaRecursoVM> Sessoes { get; } = [];
    public DesempenhoVM Desempenho { get; } = new();

    [ObservableProperty]
    public partial string Resumo { get; set; } = "";

    [ObservableProperty]
    public partial string Economia { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FraseNexora))]
    public partial string TextoNexora { get; set; } = "";

    public string FraseNexora => Idioma.F("O próprio Nexora usa {0}.", TextoNexora);

    [ObservableProperty]
    public partial string TextoBuild { get; set; } = Idioma.T("Nenhum processo de build aberto.");

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LiberarCommand))]
    public partial bool TemBuild { get; set; }

    [ObservableProperty]
    public partial string? AvisoLiberar { get; set; }

    [RelayCommand(CanExecute = nameof(TemBuild))]
    async Task Liberar()
    {
        AvisoLiberar = Idioma.T("Liberando…");
        var liberado = await Core.Recursos.ServidoresDeBuild.DesligarAsync();
        AvisoLiberar = liberado > 0 ? Idioma.F("Liberados {0}.", Formatar(liberado)) : Idioma.T("Nada foi liberado: os processos podiam estar em uso.");
        await AtualizarAsync();
    }

    public static string DescreverBuild(IReadOnlyList<Core.Recursos.ServidorDeBuild> servidores)
    {
        if (servidores.Count == 0) return Idioma.T("Nenhum processo de build aberto.");
        var tipos = string.Join(", ", servidores.GroupBy(s => s.Tipo).Select(g => g.Count() == 1 ? g.Key : $"{g.Count()} × {g.Key}"));
        return Idioma.F("{0} ocupando {1}: {2}.", servidores.Count == 1 ? Idioma.T("1 processo") : Idioma.F("{0} processos", servidores.Count), Formatar(servidores.Sum(s => s.Memoria)), tipos);
    }

    public bool HubLigado => App.Hub is not null;
    public bool SemMcps => Mcps.Count == 0;

    public void Ativar(bool ligado)
    {
        Desempenho.Ativar(ligado);
        if (ligado)
        {
            _ = AtualizarAsync();
            _relogio.Start();
        }
        else _relogio.Stop();
    }

    [RelayCommand]
    public async Task AtualizarAsync()
    {
        var estado = App.Hub is { } hubMcp ? await Task.Run(hubMcp.Estado) : [];
        var sessoes = _janela.Abertas.Where(c => c.Pid is not null).Select(c => (c.Titulo, c.NomePasta, c.Pid!.Value)).ToList();
        var servidores = await Task.Run(Core.Recursos.ServidoresDeBuild.Listar);
        TextoBuild = DescreverBuild(servidores);
        TemBuild = servidores.Count > 0;
        var (linhasSessoes, nexora) = await Task.Run(() =>
        {
            var lista = new List<(string, string, long)>();
            foreach (var (titulo, pasta, pid) in sessoes)
                try
                {
                    using var p = Process.GetProcessById(pid);
                    lista.Add((titulo, pasta, p.PrivateMemorySize64));
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                }
            using var eu = Process.GetCurrentProcess();
            return (lista, eu.PrivateMemorySize64);
        });

        Mcps.Clear();
        foreach (var e in estado)
        {
            var modo = e.PorSessao ? Idioma.F("de {0}", e.Dono ?? Idioma.T("uma sessão")) : e.Sessoes == 1 ? Idioma.T("1 sessão usando") : Idioma.F("{0} sessões usando", e.Sessoes);
            var detalhe = $"{Path.GetFileName(e.Pasta.TrimEnd('\\', '/'))} · {modo}" + (e.Ativo && e.Processos > 1 ? Idioma.F(" · {0} processos", e.Processos) : "");
            var instancia = e.Instancia;
            Mcps.Add(new LinhaRecursoVM(e.Nome, detalhe, e.Memoria, e.Ativo, App.Hub is { } hub ? () => hub.PararAsync(instancia) : null));
        }
        Sessoes.Clear();
        foreach (var (titulo, pasta, memoria) in linhasSessoes) Sessoes.Add(new LinhaRecursoVM(titulo, pasta, memoria, true, null));

        var totalMcp = estado.Sum(e => e.Memoria);
        var totalSessoes = linhasSessoes.Sum(l => l.Item3);
        if (JanelaVM.Demo && DesempenhoVM.Demonstracao(DateTime.Now).Agora is { } demo)
        {
            nexora = demo.NexoraPrivada;
            totalSessoes = demo.Sessoes.Ram;
            totalMcp = demo.Mcps.Ram;
        }
        TextoNexora = Formatar(nexora);
        Resumo = Idioma.F("Total {0}  ·  sessões {1}  ·  MCPs {2}", Formatar(nexora + totalMcp + totalSessoes), Formatar(totalSessoes), Formatar(totalMcp));
        var poupadas = estado.Where(e => !e.PorSessao && e.Ativo).Sum(e => Math.Max(0, e.Sessoes - 1));
        var paradas = estado.Count(e => !e.Ativo);
        Economia = (poupadas, paradas) switch
        {
            (0, 0) => "",
            (_, 0) => Idioma.F("Sem o hub seriam mais {0} cópias de MCP rodando.", poupadas),
            (0, _) => Idioma.F("{0} MCP parado por ociosidade, sobe de novo quando alguém usar.", paradas),
            _ => Idioma.F("Sem o hub seriam mais {0} cópias de MCP rodando; {1} parado por ociosidade.", poupadas, paradas),
        };
        OnPropertyChanged(nameof(SemMcps));
    }

    public static string Formatar(long bytes) =>
        bytes >= 1L << 30 ? $"{(bytes / (double)(1L << 30)).ToString("0.0", Idioma.Cultura)} GB" : $"{bytes >> 20} MB";
}

public sealed partial class SaidaVM(string caminho, Action<string, bool> abrir) : ObservableObject
{
    public string Caminho { get; } = caminho;
    public string Nome => Path.GetFileName(Caminho);
    public string Diretorio => Path.GetFileName(Path.GetDirectoryName(Caminho) ?? "");
    public string Icone => Path.GetExtension(Caminho).ToLowerInvariant() is ".cs" or ".ts" or ".js" or ".razor" or ".py" or ".java" or ".sql" ? "codigo" : "arquivo";

    [ObservableProperty]
    public partial int Edicoes { get; set; } = 1;

    [RelayCommand]
    void VerDiff() => abrir(Caminho, true);

    [RelayCommand]
    void Abrir() => abrir(Caminho, false);
}

public sealed class CategoriaContextoVM(string nome, long tokens, double fracao, string cor)
{
    public string Nome { get; } = nome;
    public string Tokens { get; } = tokens >= 1000 ? $"{(tokens / 1000.0).ToString("0.#", Idioma.Cultura)}k" : tokens.ToString();
    public string Percentual { get; } = $"{(fracao * 100).ToString("0.#", Idioma.Cultura)}%";
    public Avalonia.Media.IBrush Cor { get; } = Avalonia.Media.Brush.Parse(cor);
}

public sealed class RamoVM(string? nome, bool worktree, bool cabecalho)
{
    public string? Nome { get; } = nome;
    public bool Worktree { get; } = worktree;
    public bool Branch => !Worktree;
    public bool Cabecalho { get; } = cabecalho;
    public ObservableCollection<ChatVM> Sessoes { get; } = [];
}
