using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

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
