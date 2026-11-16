using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Git;
using Nexora.Core.Nuvem;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    LancadorRemoto? _lancador;
    AvisoDeTrabalho? _acordada;
    readonly string _tarefaNaNuvem = Guid.NewGuid().ToString("N")[..12];
    ExecucaoSalva? _execucao;
    bool _sincronizado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NaNuvem), nameof(OndeRodaRotulo), nameof(DicaOndeRoda), nameof(MostraOndeRoda), nameof(NomeDaMaquina), nameof(MostraFaixaNuvem), nameof(TextoNuvem), nameof(SugestaoNuvem), nameof(MostraSugestaoNuvem))]
    public partial string? MaquinaId { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoNuvem), nameof(MostraFaixaNuvem), nameof(NuvemCaiu), nameof(NuvemOcupada), nameof(PodeTrazerDaNuvem))]
    public partial EstadoDaNuvem? EstadoNuvem { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoNuvem))]
    public partial string? DetalheNuvem { get; set; }

    [ObservableProperty]
    public partial bool MenuOndeRodaAberto { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeTrazerDaNuvem), nameof(TextoNuvem))]
    public partial bool TrazendoDaNuvem { get; set; }

    public ObservableCollection<OpcaoMenuVM> OpcoesOndeRoda { get; } = [];

    public MaquinaNaNuvem? Maquina => App.Nuvem.Maquina(MaquinaId);
    public override bool NaNuvem => MaquinaId is not null && Maquina is not null;
    public override string NomeDaMaquina => Maquina?.Nome ?? "";
    public string? ExecucaoNaNuvem => _execucao?.Execucao;

    public string ProjetoDaNuvem => Repositorio.PrincipalDoWorktree(Pasta) ?? Pasta;

    public bool ProjetoPodeIrParaNuvem => PodeIrParaNuvem(ProjetoDaNuvem);

    public static bool PodeIrParaNuvem(string projeto) =>
        App.Config.ProjetosNaNuvem.Contains(Config.ChaveDePasta(projeto), StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<MaquinaNaNuvem> MaquinasProntas => [.. App.Nuvem.Maquinas.Where(m => m.Testada)];

    public bool MostraOndeRoda => NaNuvem || (ProjetoPodeIrParaNuvem && MaquinasProntas.Count > 0 && !EhLateral);

    public string OndeRodaRotulo => NaNuvem ? Idioma.F("Nuvem · {0}", NomeDaMaquina) : Idioma.T("Neste PC");

    public bool PodeTrocarOndeRoda => PodeTrocarAgente;

    public override string DicaOndeRoda => PodeTrocarOndeRoda
        ? Idioma.T("Onde o agente desta conversa roda: neste PC ou numa máquina na nuvem")
        : NaNuvem ? Idioma.F("Esta conversa roda na nuvem, em {0}", NomeDaMaquina) : Idioma.T("Esta conversa roda neste PC");

    public bool MostraFaixaNuvem => NaNuvem && (EstadoNuvem is not null || TrazendoDaNuvem);
    public override bool NuvemCaiu => EstadoNuvem is EstadoDaNuvem.Desconectado;
    public bool NuvemOcupada => EstadoNuvem is EstadoDaNuvem.Conectando or EstadoDaNuvem.Sincronizando or EstadoDaNuvem.Iniciando;
    public bool PodeTrazerDaNuvem => NaNuvem && _execucao is not null && !TrazendoDaNuvem;

    public string TextoNuvem => TrazendoDaNuvem
        ? DetalheNuvem ?? Idioma.T("Trazendo o código da nuvem…")
        : EstadoNuvem switch
        {
            EstadoDaNuvem.Conectando => Idioma.F("Conectando a {0}…", NomeDaMaquina),
            EstadoDaNuvem.Sincronizando => DetalheNuvem ?? Idioma.T("Sincronizando o código…"),
            EstadoDaNuvem.Iniciando => Idioma.F("Abrindo o {0} na máquina…", NomeAgente),
            EstadoDaNuvem.Rodando => Idioma.F("Rodando na nuvem, em {0}. Pode desligar este PC: o agente continua.", NomeDaMaquina),
            EstadoDaNuvem.Desconectado => Idioma.F("Desconectado de {0}. O agente continua lá; tentando de novo…", NomeDaMaquina),
            EstadoDaNuvem.Terminou => Idioma.F("O agente terminou em {0}.", NomeDaMaquina),
            EstadoDaNuvem.Falhou => DetalheNuvem ?? Idioma.T("A conexão com a nuvem falhou."),
            _ => "",
        };

    [RelayCommand]
    void AbrirOndeRoda()
    {
        if (!PodeTrocarOndeRoda) return;
        OpcoesOndeRoda.Clear();
        OpcoesOndeRoda.Add(new OpcaoMenuVM(Idioma.T("Neste PC"), Idioma.T("O agente usa o processador e a memória deste computador."), MaquinaId is null, () => EscolherOndeRoda(null)));
        foreach (var maquina in MaquinasProntas)
        {
            var id = maquina.Id;
            OpcoesOndeRoda.Add(new OpcaoMenuVM(Idioma.F("Nuvem · {0}", maquina.Nome), maquina.Resumo is { Length: > 0 } r ? maquina.Endereco + "  ·  " + r : maquina.Endereco, MaquinaId == id, () => EscolherOndeRoda(id)));
        }
        MenuOndeRodaAberto = true;
    }

    public void EscolherOndeRoda(string? maquina)
    {
        MenuOndeRodaAberto = false;
        if (!PodeTrocarOndeRoda) return;
        MaquinaId = maquina is not null && App.Nuvem.Maquina(maquina) is not null ? maquina : null;
    }

    OpcoesSessao OpcoesNaNuvem(OpcoesSessao locais)
    {
        var maquina = Maquina ?? throw new InvalidOperationException(Idioma.T("A máquina desta conversa não está mais nas Configurações."));
        if (maquina.Home is not { Length: > 0 } home)
            throw new InvalidOperationException(Idioma.F("Teste a conexão com {0} em Configurações › Máquinas na nuvem antes de usar.", maquina.Nome));
        var remota = _execucao?.PastaRemota ?? CodigoNaNuvem.PastaRemota(home, ProjetoDaNuvem, Branch ?? "main");
        _execucao ??= new ExecucaoSalva(ServicoDaNuvem.NovaExecucao(), maquina.Id, remota, Pasta, Branch ?? "", Agente, SessaoId, DateTime.Now);
        var naMaquina = App.Nuvem.Abrir(maquina);
        var pastaLocal = Pasta;
        Func<Action<MudancaDaNuvem>, CancellationToken, Task>? preparar = _sincronizado || SessaoId is not null
            ? null
            : async (avisar, token) =>
            {
                await CodigoNaNuvem.EnviarAsync(naMaquina, pastaLocal, remota, t => avisar(new MudancaDaNuvem(EstadoDaNuvem.Sincronizando, t)), token);
                _sincronizado = true;
            };
        var plano = new PlanoDaExecucao(_execucao.Execucao, remota, App.Nuvem.AmbienteDoAgente(Agente, AmbienteDoProjeto()), preparar, ContinuarDoTurno: EhClaude);
        var lancador = new LancadorRemoto(naMaquina, plano);
        lancador.Mudou += m => Dispatcher.UIThread.Post(() => AoMudarNuvem(lancador, m));
        _lancador = lancador;
        OnPropertyChanged(nameof(PodeTrazerDaNuvem));
        return locais with
        {
            Pasta = remota,
            Diretorios = [],
            Mcp = null,
            Permitidas = [.. (locais.Permitidas ?? []).Where(p => !p.StartsWith("mcp__", StringComparison.Ordinal))],
            Ambiente = plano.Ambiente,
            Lancador = lancador,
        };
    }

    void AoMudarNuvem(LancadorRemoto lancador, MudancaDaNuvem mudanca)
    {
        if (_lancador != lancador) return;
        DetalheNuvem = mudanca.Detalhe;
        EstadoNuvem = mudanca.Estado;
    }

    void LembrarNuvem()
    {
        if (!NaNuvem || _execucao is null || !EhClaude || SessaoId is null || JanelaVM.Demo) return;
        _execucao = _execucao with { SessaoId = SessaoId, Branch = Branch ?? _execucao.Branch };
        var salva = _execucao;
        _ = Task.Run(() => App.Nuvem.Lembrar(salva));
    }

    static DispatcherTimer? _relogioDaMemoria;
    static double? _memoriaEmUso;
    static event Action? MemoriaMediu;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SugestaoNuvem), nameof(MostraSugestaoNuvem))]
    public partial bool SugestaoNuvemDispensada { get; set; }

    MaquinaNaNuvem? MaquinaSugerida => MaquinasProntas.FirstOrDefault();

    public string? SugestaoNuvem => SugestaoNuvemDispensada || NaNuvem || !PodeTrocarOndeRoda || EhLateral || Estado != EstadoChat.Nova
        ? null
        : SugestaoDaNuvem.Texto(_memoriaEmUso, ProjetoPodeIrParaNuvem, MaquinaSugerida?.Nome);

    public bool MostraSugestaoNuvem => SugestaoNuvem is not null;

    public string TextoAceitarSugestao => Idioma.F("Rodar em {0}", MaquinaSugerida?.Nome ?? "");

    void LigarSugestaoDaNuvem()
    {
        MemoriaMediu += AtualizarSugestaoDaNuvem;
        if (_relogioDaMemoria is not null) return;
        _relogioDaMemoria = new DispatcherTimer(TimeSpan.FromSeconds(20), DispatcherPriority.Background, (_, _) => MedirMemoria());
        _relogioDaMemoria.Start();
        MedirMemoria();
    }

    public static void MedirMemoria() => _ = Task.Run(() => SugestaoDaNuvem.Medir()).ContinueWith(t =>
    {
        if (t.IsCompletedSuccessfully) Dispatcher.UIThread.Post(() =>
        {
            _memoriaEmUso = t.Result;
            MemoriaMediu?.Invoke();
        });
    }, TaskScheduler.Default);

    void AtualizarSugestaoDaNuvem()
    {
        OnPropertyChanged(nameof(SugestaoNuvem));
        OnPropertyChanged(nameof(MostraSugestaoNuvem));
        OnPropertyChanged(nameof(TextoAceitarSugestao));
    }

    [RelayCommand]
    void AceitarSugestaoNuvem()
    {
        if (MaquinaSugerida is { } maquina) EscolherOndeRoda(maquina.Id);
        SugestaoNuvemDispensada = true;
    }

    [RelayCommand]
    void DispensarSugestaoNuvem() => SugestaoNuvemDispensada = true;

    void ManterMaquinaAcordada(bool trabalhando)
    {
        if (trabalhando && _acordada is null && NaNuvem && Maquina is { } maquina && !JanelaVM.Demo)
            _acordada = App.Nuvem.ManterAcordada(maquina, _tarefaNaNuvem);
        else if (!trabalhando && _acordada is { } aviso)
        {
            _acordada = null;
            _ = aviso.DisposeAsync().AsTask();
        }
    }

    void EncerrarNuvem(bool fechandoOApp)
    {
        MemoriaMediu -= AtualizarSugestaoDaNuvem;
        ManterMaquinaAcordada(false);
        if (fechandoOApp || _execucao is not { } execucao || Maquina is not { } maquina) return;
        var naMaquina = App.Nuvem.Abrir(maquina);
        _ = Task.Run(async () =>
        {
            try
            {
                await naMaquina.RodarAsync(ScriptRemoto.Chamar("parar", execucao.Execucao));
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
            App.Nuvem.Esquecer(execucao.Execucao);
        });
    }

    [RelayCommand]
    void ReconectarNuvem() => _lancador?.Ultimo?.TentarAgora();

    [RelayCommand]
    async Task TrazerDaNuvemAsync()
    {
        if (!PodeTrazerDaNuvem || _execucao is not { } execucao || Maquina is not { } maquina) return;
        TrazendoDaNuvem = true;
        DetalheNuvem = null;
        try
        {
            var repositorio = ProjetoDaNuvem;
            var desejada = (Repositorio.Slug(Branch ?? "nuvem") is { Length: > 0 } s ? s : "nuvem") + "-nuvem";
            var naMaquina = App.Nuvem.Abrir(maquina);
            var trazido = await Task.Run(async () =>
            {
                var branch = await CodigoNaNuvem.BranchLivreAsync(repositorio, desejada);
                var pasta = Repositorio.PastaDoWorktree(repositorio, branch);
                return await CodigoNaNuvem.TrazerAsync(naMaquina, execucao.PastaRemota, repositorio, pasta, branch,
                    p => Dispatcher.UIThread.Post(() => DetalheNuvem = p));
            });
            Adicionar(ItemAviso.Info(trazido.TinhaAlteracoes
                ? Idioma.F("Trouxe o código da nuvem para {0}, na branch {1}, com as mudanças ainda sem commit. Abri uma conversa lá para o commit e o PR.", trazido.Pasta, trazido.Branch)
                : Idioma.F("Trouxe o código da nuvem para {0}, na branch {1}. Abri uma conversa lá para o commit e o PR.", trazido.Pasta, trazido.Branch)));
            _anfitriao.AbrirSessao(_anfitriao.LocalDe(trazido.Pasta));
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Adicionar(ItemAviso.Erro(Idioma.F("Não deu para trazer da nuvem: {0}", ex.Message)));
        }
        finally
        {
            TrazendoDaNuvem = false;
            DetalheNuvem = null;
        }
    }

    public static ChatVM DaNuvem(ExecucaoSalva execucao, Local local, IAnfitriao anfitriao, Action<UsoDoPlano> aoUsar, Action<ChatVM> aoFechar)
    {
        var chat = new ChatVM(local, Idioma.T("Conversa na nuvem"), execucao.SessaoId, anfitriao, aoUsar, aoFechar)
        {
            Agente = Agentes.Normalizar(execucao.Agente),
            Estado = EstadoChat.Pronta,
            Carregando = true,
            MaquinaId = execucao.Maquina,
        };
        chat._execucao = execucao;
        chat._sincronizado = true;
        chat.Branch ??= execucao.Branch;
        chat.Carregamento = chat.CarregarDaNuvemAsync();
        return chat;
    }

    async Task CarregarDaNuvemAsync()
    {
        if (_execucao is not { } execucao || Maquina is not { } maquina || SessaoId is not { } sessao)
        {
            Carregando = false;
            return;
        }
        EstadoNuvem = EstadoDaNuvem.Conectando;
        try
        {
            var naMaquina = App.Nuvem.Abrir(maquina);
            var (eventos, rodando, noMeio, titulo) = await Task.Run(async () =>
            {
                await naMaquina.GarantirAjudanteAsync();
                var estado = EstadoDaExecucao.Ler((await naMaquina.RodarAsync(ScriptRemoto.Chamar("estado", execucao.Execucao))).Saida);
                var turno = await naMaquina.RodarAsync(ScriptRemoto.Chamar("inicio-do-turno", execucao.Execucao));
                var meio = estado.Rodando && long.TryParse(turno.Saida.Trim(), out var inicio) && inicio < estado.Tamanho;
                var arquivo = Path.Combine(Path.GetTempPath(), "nexora-nuvem-" + sessao + ".jsonl");
                var recebido = await naMaquina.ReceberAsync(
                    $"f=$(ls \"$HOME\"/.claude/projects/*/{Shell.Aspas(sessao + ".jsonl")} 2>/dev/null | head -n 1); [ -n \"$f\" ] && cat \"$f\"",
                    async fluxo =>
                    {
                        await using var destino = File.Create(arquivo);
                        await fluxo.CopyToAsync(destino);
                    });
                IReadOnlyList<EventoClaude> lidos = [];
                string? nome = null;
                try
                {
                    if (recebido.Ok && new FileInfo(arquivo).Length > 0)
                    {
                        lidos = Core.Historico.Transcricao.Ler(arquivo, 400);
                        nome = Core.Historico.ConversasSalvas.Resumir(new FileInfo(arquivo))?.Titulo;
                    }
                }
                finally
                {
                    try { File.Delete(arquivo); } catch (IOException) { }
                }
                return (lidos, estado.Rodando, meio, nome);
            });
            if (noMeio && eventos.Select((e, i) => (e, i)).LastOrDefault(x => x.e is MensagemDoUsuario) is { e: not null } ultima)
                eventos = [.. eventos.Take(ultima.i + 1)];
            if (titulo is { Length: > 0 }) Titulo = titulo;
            MontarHistorico(eventos, new Dictionary<string, IReadOnlyList<EventoClaude>>(), null);
            EstadoNuvem = null;
            if (rodando)
            {
                GarantirSessao();
                if (noMeio)
                {
                    Estado = EstadoChat.Trabalhando;
                    TurnoDesde ??= DateTime.Now;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            EstadoNuvem = EstadoDaNuvem.Falhou;
            DetalheNuvem = Idioma.F("Não deu para falar com {0}: {1}", maquina.Nome, ex.Message);
            Carregando = false;
        }
    }
}
