using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Git;
using Nexora.Core.Arquivos;
using Nexora.Core.Historico;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public enum EstadoChat { Nova, Pronta, Trabalhando, PrecisaDeVoce, Erro }

public sealed partial class ChatVM : CartaoVM
{
    public static string TituloNovo => Idioma.T("Nova conversa");
    static readonly string?[] ValoresModo = [null, "manual", "acceptEdits", "auto", "plan", "bypassPermissions"];
    static readonly string?[] ValoresModelo = [null, "opus", "sonnet", "haiku", "fable"];
    static readonly string?[] ValoresEsforco = [null, "low", "medium", "high", "xhigh", "max"];

    readonly Dictionary<string, ItemFerramenta> _ferramentas = [];
    readonly StringBuilder _parcial = new();
    readonly DispatcherTimer _descarga;
    readonly IAnfitriao _anfitriao;
    readonly Action<UsoDoPlano> _aoUsar;
    readonly Action<ChatVM> _aoFechar;
    ItemTexto? _textoAtual;
    ISessaoAgente? _sessao;

    ChatVM(Local local, string titulo, string? sessaoId, IAnfitriao anfitriao, Action<UsoDoPlano> aoUsar, Action<ChatVM> aoFechar)
    {
        Locais = MontarLocais(anfitriao, local);
        local = Locais.First(l => l.Pasta == local.Pasta);
        LocalAtual = local;
        Titulo = titulo;
        SessaoId = sessaoId;
        Branch = local.Branch;
        Rascunho = "";
        _anfitriao = anfitriao;
        _aoUsar = aoUsar;
        _aoFechar = aoFechar;
        LocalEscolhido = local;
        _descarga = new DispatcherTimer(TimeSpan.FromMilliseconds(60), DispatcherPriority.Background, (_, _) => DescarregarParcial());
        LigarSugestaoDaNuvem();
        Anexos.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(TemAnexos));
            EnviarCommand.NotifyCanExecuteChanged();
        };
        Itens.CollectionChanged += (_, e) =>
        {
            OnPropertyChanged(nameof(Vazia));
            OnPropertyChanged(nameof(PodeTrocarLocal));
            SincronizarTela(e);
            AtualizarTrecho(e);
            AtualizarPedidoInicial();
        };
        Editor = CriarEditor();
        Painel = new PainelVM(local, Editor);
        AtualizarRecentes();
        IniciarDetalhes();
        IniciarModelo();
        IniciarFila();
        IniciarCompactar();
        IniciarPassagens();
    }

    public ObservableCollection<ConversaVM> Recentes { get; } = [];
    public bool TemRecentes => Recentes.Count > 0;

    public void AtualizarRecentes()
    {
        Recentes.Clear();
        foreach (var c in _anfitriao.ConversasDe(Pasta)) Recentes.Add(c);
        OnPropertyChanged(nameof(TemRecentes));
    }

    public void ProximoModo()
    {
        int[] ciclo = [0, 2, 4];
        var atual = Array.IndexOf(ciclo, ModoIndice);
        ModoIndice = ciclo[(atual + 1) % ciclo.Length];
        Lembrar();
    }

    public EditorVM Editor { get; }
    public PainelVM Painel { get; }
    public ObservableCollection<ArquivoIndexado> Sugestoes { get; } = [];
    public ObservableCollection<ComandoBarra> ComandosSugeridos { get; } = [];
    public ObservableCollection<AnexoVM> Anexos { get; } = [];
    public bool TemAnexos => Anexos.Count > 0;

    public bool Anexar(byte[] bytes, string nome) => Guardar(Imagens.Preparar(bytes, nome));

    public bool Anexar(Avalonia.Media.Imaging.Bitmap bitmap) => Guardar(Imagens.DeBitmap(bitmap, $"print-{DateTime.Now:HHmmss}.png"));

    public void AnexarArquivo(string caminho)
    {
        if (Imagens.Extensoes.Contains(Path.GetExtension(caminho)) && File.Exists(caminho))
        {
            try
            {
                if (Anexar(File.ReadAllBytes(caminho), Path.GetFileName(caminho))) return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
        var mencao = caminho.StartsWith(Pasta, StringComparison.OrdinalIgnoreCase) ? Path.GetRelativePath(Pasta, caminho) : caminho;
        InserirNoRascunho("@" + mencao.Replace('\\', '/'));
    }

    bool Guardar(Anexo? anexo)
    {
        if (anexo is null) return false;
        Anexos.Add(new AnexoVM(anexo, Imagens.Miniatura(anexo, 160), a => Anexos.Remove(a)));
        return true;
    }
    IReadOnlyList<ComandoBarra>? _comandos;
    bool _respondeuNoTurno;

    public async Task BuscarComandosAsync(string termo)
    {
        var comandos = _comandos ?? (await CatalogoComandos.ObterAsync(Pasta, JanelaVM.Isolada)).Comandos;
        comandos = [.. comandos.Where(c => c.Nome is not ("btw" or "clear")), ComandoDeLimpar, new ComandoBarra("btw", Idioma.T("Pergunta lateral: abre um painel ao lado, com o contexto desta conversa, sem alterá-la"), Idioma.T("pergunta"))];
        ComandosSugeridos.Clear();
        foreach (var comando in CatalogoComandos.Filtrar(comandos, termo)) ComandosSugeridos.Add(comando);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RemotoLigado), nameof(TextoRemoto))]
    public partial string? EnderecoRemoto { get; set; }

    [ObservableProperty]
    public partial bool LigandoRemoto { get; set; }

    [ObservableProperty]
    public partial bool RemotoDisponivel { get; set; } = true;

    [ObservableProperty]
    public partial bool EmFoco { get; set; }

    public bool RemotoLigado => EnderecoRemoto is not null;

    public string TextoRemoto => RemotoLigado ? Idioma.T("Desligar o Remote Control") : Idioma.T("Remote Control: continuar pelo celular");

    [RelayCommand]
    async Task AlternarRemoto()
    {
        if (LigandoRemoto) return;
        if (RemotoLigado)
        {
            if (_sessao is { Ativa: true } ativa) await ativa.LigarRemotoAsync(false);
            EnderecoRemoto = null;
            Adicionar(ItemAviso.Info(Idioma.T("Remote Control desligado.")));
            return;
        }
        ISessaoAgente sessao;
        try
        {
            PrepararContinuacao();
            sessao = GarantirSessao();
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Adicionar(ItemAviso.Erro(ex.Message));
            return;
        }
        LigandoRemoto = true;
        var resposta = await sessao.LigarRemotoAsync(true);
        LigandoRemoto = false;
        if (resposta is { Sucesso: true, Dados: { } dados } && Protocolo.Texto(dados, "session_url") is { } url)
        {
            EnderecoRemoto = url;
            Adicionar(ItemAviso.Info(Idioma.F("Remote Control ligado: continue esta conversa no celular (app do Claude) ou em {0}. Fica ligado enquanto a sessão estiver aberta no Nexora.", url)));
        }
        else
            Adicionar(ItemAviso.Erro(Idioma.F("Não deu para ligar o Remote Control: {0}.", resposta.Erro ?? Idioma.T("o claude não devolveu o endereço"))));
    }

    [RelayCommand]
    void AbrirRemoto()
    {
        if (EnderecoRemoto is not { } url) return;
        try
        {
            Sistema.Abrir(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Adicionar(ItemAviso.Erro(ex.Message));
        }
    }

    public string? SessaoOrigem { get; private set; }
    string? _modeloQueRespondeu;

    void LembrarOrigem(string id)
    {
        if (SessaoOrigem is null)
        {
            if (App.Config.OrigensDasSessoes.TryGetValue(id, out var salva)) SessaoOrigem = salva;
            return;
        }
        if (id == SessaoOrigem || JanelaVM.Isolada) return;
        App.Config.OrigensDasSessoes[id] = SessaoOrigem;
        App.Config.Salvar();
    }

    void PrepararContinuacao()
    {
        if (_sessao is not null || Bifurcar || SessaoId is not { } id || _anfitriao.EmOutroLugar(id) is not { } origem) return;
        Bifurcar = true;
        SessaoOrigem = id;
        Adicionar(ItemAviso.Info(Idioma.F("Esta conversa continua aberta no {0}. Daqui em diante ela segue no Nexora, numa cópia com todo o histórico; a do {0} fica como estava.", origem)));
    }
    public event Action? PedirFoco;

    IndiceArquivos? _indice;
    Task<IndiceArquivos>? _montandoIndice;

    public void InserirNoRascunho(string texto)
    {
        Rascunho = string.IsNullOrWhiteSpace(Rascunho) ? texto : Rascunho.TrimEnd() + "\n\n" + texto;
        PedirFoco?.Invoke();
    }

    [ObservableProperty]
    public partial bool Indexando { get; set; }

    public async Task BuscarMencoesAsync(string termo)
    {
        if (_indice is null)
        {
            var raizes = new[] { Pasta }.Concat(LocalAtual.Vinculados).ToList();
            Indexando = true;
            _montandoIndice ??= IndiceArquivos.MontarAsync(raizes);
            _indice = await _montandoIndice;
            Indexando = false;
        }
        Sugestoes.Clear();
        foreach (var a in _indice.Buscar(termo, 12)) Sugestoes.Add(a);
    }

    public string TextoDaMencao(ArquivoIndexado arquivo) =>
        arquivo.Caminho.StartsWith(Pasta, StringComparison.OrdinalIgnoreCase) ? arquivo.Relativo : arquivo.Caminho.Replace('\\', '/');

    ItemFerramenta CriarFerramenta(FerramentaChamada chamada) =>
        new(chamada.Nome, Resumos.DaFerramenta(chamada.Nome, chamada.Entrada, Pasta), chamada.Entrada, AcaoDaFerramenta);

    void AcaoDaFerramenta(ItemFerramenta item, AcaoFerramenta acao)
    {
        if (item.Caminho is not { } caminho) return;
        if (acao == AcaoFerramenta.Abrir)
        {
            Editor.Abrir(caminho);
            return;
        }
        string? T(System.Text.Json.JsonElement e, string c) => Protocolo.Texto(e, c);
        var linhas = item.Nome switch
        {
            "Write" => Core.Texto.Diff.Comparar("", T(item.Entrada, "content") ?? ""),
            "MultiEdit" => Protocolo.Itens(Protocolo.Objeto(item.Entrada, "edits"))
                .SelectMany((e, i) => new[] { new LinhaDiff(TipoLinha.Cabecalho, Idioma.F("edição {0}", i + 1)) }
                    .Concat(Core.Texto.Diff.Comparar(T(e, "old_string") ?? "", T(e, "new_string") ?? "")))
                .ToList(),
            _ => Core.Texto.Diff.Comparar(T(item.Entrada, "old_string") ?? "", T(item.Entrada, "new_string") ?? ""),
        };
        Editor.AbrirDiff($"{Path.GetFileName(caminho)} ({item.Nome})", $"{caminho}#{item.GetHashCode()}", linhas, caminho);
    }

    DateTime? _conversaEm = DateTime.Now;
    public override DateTime? Recencia => _conversaEm;

    void Conversou() => _conversaEm = DateTime.Now;

    void AtualizarTrecho(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e is { Action: System.Collections.Specialized.NotifyCollectionChangedAction.Add, NewItems: { } novos } && e.NewStartingIndex == Itens.Count - novos.Count)
            foreach (var item in novos.OfType<ItemChat>().Where(i => i is not (ItemPensamento or ItemTurno))) Trecho.Add(item);
        else if (e is { Action: System.Collections.Specialized.NotifyCollectionChangedAction.Add, NewItems: { } antigos } && ForaDoTrecho(e.NewStartingIndex + antigos.Count))
            return;
        else
        {
            Trecho.Clear();
            foreach (var item in Itens.Where(i => i is not (ItemPensamento or ItemTurno)).TakeLast(TamanhoDoTrecho)) Trecho.Add(item);
        }
        while (Trecho.Count > TamanhoDoTrecho) Trecho.RemoveAt(0);
    }

    bool ForaDoTrecho(int desde)
    {
        var contados = 0;
        for (var i = desde; i < Itens.Count && contados < TamanhoDoTrecho; i++)
            if (Itens[i] is not (ItemPensamento or ItemTurno)) contados++;
        return contados >= TamanhoDoTrecho;
    }

    public static int IndiceDoModelo(string? modelo) => Math.Max(0, Array.IndexOf(ValoresModelo, modelo));
    public static int IndiceDoEsforco(string? esforco) => Math.Max(0, Array.IndexOf(ValoresEsforco, esforco));
    public static string? ModeloDoIndice(int indice) => ValoresModelo.ElementAtOrDefault(indice);
    public static string? EsforcoDoIndice(int indice) => ValoresEsforco.ElementAtOrDefault(indice);
    public static string[] RotulosModelo { get; } = [Idioma.T("Modelo padrão"), "Opus", "Sonnet", "Haiku", "Fable"];
    public static string[] RotulosEsforco { get; } = [Idioma.T("Esforço padrão"), Idioma.T("Esforço baixo"), Idioma.T("Esforço médio"), Idioma.T("Esforço alto"), Idioma.T("Esforço extra alto"), Idioma.T("Esforço máximo")];

    public static ChatVM Nova(Local local, IAnfitriao anfitriao, Action<UsoDoPlano> aoUsar, Action<ChatVM> aoFechar) =>
        new(local, TituloNovo, null, anfitriao, aoUsar, aoFechar);

    public static ChatVM DeConversa(ConversaSalva conversa, Local local, IAnfitriao anfitriao, Action<UsoDoPlano> aoUsar, Action<ChatVM> aoFechar)
    {
        var chat = new ChatVM(local, conversa.Titulo, conversa.SessaoId, anfitriao, aoUsar, aoFechar)
        {
            Agente = Agentes.Normalizar(conversa.Agente),
            Estado = EstadoChat.Pronta,
            Carregando = true,
        };
        chat.Branch ??= conversa.Branch;
        chat._conversaEm = conversa.Atualizada;
        if (conversa.PrimeiraMensagem is { Length: > 0 } primeira) chat._pedidoInicial = Resumos.UmaLinha(primeira, 200);
        if (!chat.EhClaude && !JanelaVM.Isolada) chat.ModeloDoAgente = App.Config.ModeloPorAgente.GetValueOrDefault(chat.Agente);
        chat.Carregamento = chat.EhClaude ? chat.CarregarHistoricoAsync(conversa.Arquivo) : chat.CarregarDoAgenteAsync();
        return chat;
    }

    public ObservableCollection<ItemChat> Itens { get; } = [];
    [ObservableProperty]
    public partial IReadOnlyList<Local> Locais { get; set; } = [];
    public string[] Modos { get; } = [Idioma.T("Modo padrão"), Idioma.T("Perguntar sempre"), Idioma.T("Aceitar edições"), Idioma.T("Automático"), Idioma.T("Planejar"), Idioma.T("Sem perguntar")];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Pasta), nameof(NomePasta), nameof(Onde), nameof(Cabecalho), nameof(TemVinculados), nameof(TextoVinculados))]
    public partial Local LocalAtual { get; set; }

    [ObservableProperty]
    public partial Local? LocalEscolhido { get; set; }

    [ObservableProperty]
    public partial string Titulo { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Onde), nameof(Cabecalho))]
    public partial string? Branch { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeTrocarLocal))]
    public partial string? SessaoId { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Vazia))]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstadoTexto), nameof(Trabalhando), nameof(Precisa), nameof(EstaPronta), nameof(TemErro), nameof(PodeTirar))]
    public partial EstadoChat Estado { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EnviarCommand))]
    public partial string Rascunho { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModoRotulo))]
    public partial int ModoIndice { get; set; }

    bool _reiniciar;

    public string Pasta => LocalAtual.Pasta;
    public string NomePasta => LocalAtual.Nome;
    public string Onde => Branch is { Length: > 0 } b ? $"{NomePasta}  ·  {b}" : NomePasta;
    public string Cabecalho => Branch is { Length: > 0 } b ? $"{Pasta}  ·  {b}" : Pasta;
    public bool TemVinculados => LocalAtual.Vinculados.Count > 0;
    public string TextoVinculados => Idioma.F("+{0} vinculados", LocalAtual.Vinculados.Count);
    public bool Vazia => Itens.Count == 0 && !Carregando;
    public bool PodeTrocarLocal => _sessao is null && SessaoId is null && Itens.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TempoTexto))]
    public partial DateTime? TurnoDesde { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemPedidoNoCartao), nameof(TemPerguntaNoCartao))]
    public partial ItemPermissao? PermissaoPendente { get; set; }

    public bool TemPedidoNoCartao => PermissaoPendente is { MostraBotoes: true };
    public bool TemPerguntaNoCartao => PermissaoPendente is { MostraBotoes: false };

    void AtualizarPendente() => PermissaoPendente = Itens.OfType<ItemPermissao>().FirstOrDefault(p => p.Pendente);

    public EstadoChat EstadoAnterior { get; private set; }
    public int? Pid => _sessao?.Pid;

    static int _contador;
    public string Codigo { get; } = $"s{Interlocked.Increment(ref _contador)}";
    public ChatVM? Chefe { get; set; }
    public bool Bifurcar { get; set; }
    public Task Carregamento { get; private set; } = Task.CompletedTask;
    public bool EhChefe => Mcp is not null;
    public string? Mcp
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged(nameof(EhChefe));
            OnPropertyChanged(nameof(AgenteDoCartao));
            OnPropertyChanged(nameof(NomeDoAgente));
        }
    }
    public string? PromptExtra { get; set; }
    public IReadOnlyList<string>? Permitidas { get; set; }

    public override string TempoTexto => Estado switch
    {
        EstadoChat.Trabalhando when TurnoDesde is { } desde => Idioma.F("trabalhando há {0}", Core.Texto.Tempo.Duracao((long)(DateTime.Now - desde).TotalMilliseconds)),
        EstadoChat.PrecisaDeVoce when AtividadeEm is { } quando => Idioma.F("esperando há {0}", Core.Texto.Tempo.Duracao((long)(DateTime.Now - quando).TotalMilliseconds)),
        _ when AtividadeEm is { } quando => Core.Texto.Tempo.Relativo(quando),
        _ => "",
    };

    partial void OnEstadoChanged(EstadoChat oldValue, EstadoChat newValue)
    {
        EstadoAnterior = oldValue;
        OnPropertyChanged(nameof(TempoTexto));
        OnPropertyChanged(nameof(TemRevisao));
        AcompanharTurno(newValue == EstadoChat.Trabalhando);
        ManterMaquinaAcordada(newValue == EstadoChat.Trabalhando);
        AtualizarSugestaoDaNuvem();
    }

    void Atividade(string texto)
    {
        UltimaAtividade = Resumos.UmaLinha(texto, 160);
        AtividadeEm = DateTime.Now;
    }

    [RelayCommand]
    void Mostrar() => _anfitriao.Mostrar(this);

    [RelayCommand]
    void Executar() => _anfitriao.MostrarExecutar(LocalAtual);

    public override bool Trabalhando => Estado == EstadoChat.Trabalhando;
    public override bool Precisa => Estado == EstadoChat.PrecisaDeVoce;
    public override string DicaTirar => Idioma.T("Fechar a conversa. Ela continua na lista de conversas");
    public bool EstaPronta => Estado == EstadoChat.Pronta;
    public bool TemErro => Estado == EstadoChat.Erro;

    public string EstadoTexto => Estado switch
    {
        EstadoChat.Trabalhando => Idioma.T("Trabalhando"),
        EstadoChat.PrecisaDeVoce => Idioma.T("Precisa de você"),
        EstadoChat.Pronta => Idioma.T("Pronta"),
        EstadoChat.Erro => Idioma.T("Erro"),
        _ => Idioma.T("Nova"),
    };

    partial void OnLocalEscolhidoChanged(Local? value)
    {
        if (_remontandoLocais || value is null || value == LocalAtual || !PodeTrocarLocal) return;
        LocalAtual = value;
        LerPadroes();
        if (SennaDisponivel && !JanelaVM.Isolada) _ = CarregarPassagensAsync(false);
        Branch = value.Branch;
        Painel.Trocar(value);
        _indice = null;
        _montandoIndice = null;
        AtualizarRecentes();
    }

    partial void OnModoIndiceChanged(int value)
    {
        if (_sessao is { Ativa: true } s && ValoresModo[value] is { } modo) s.MudarModo(modo);
    }

    void Reiniciar()
    {
        _reiniciar = false;
        var porSegredo = _reinicioPorSegredo;
        _reinicioPorSegredo = false;
        if (_sessao is not { } sessao) return;
        _sessao = null;
        if (RemotoLigado)
        {
            EnderecoRemoto = null;
            Adicionar(ItemAviso.Info(porSegredo
                ? Idioma.T("O Remote Control desligou porque a sessão reiniciou para receber o segredo. Ligue de novo se precisar.")
                : Idioma.T("O Remote Control desligou porque o claude reiniciou para trocar o esforço. Ligue de novo se precisar.")));
        }
        _ = sessao.DisposeAsync().AsTask();
    }

    public void UsarPasta(string pasta)
    {
        if (!PodeTrocarLocal) return;
        LocalEscolhido = _anfitriao.LocalDe(pasta);
    }

    public void AtualizarBranch()
    {
        var atual = Repositorio.BranchAtual(Pasta);
        if (atual == Branch) return;
        Branch = atual;
        RemontarLocais();
        _anfitriao.BranchMudou(Pasta);
    }

    bool _remontandoLocais;

    static IReadOnlyList<Local> MontarLocais(IAnfitriao anfitriao, Local local)
    {
        var locais = anfitriao.Locais().ToList();
        if (!locais.Any(l => l.Pasta == local.Pasta)) locais.Insert(0, local);
        return locais;
    }

    void RemontarLocais()
    {
        var atual = LocalAtual;
        _remontandoLocais = true;
        try
        {
            Locais = MontarLocais(_anfitriao, atual);
            LocalEscolhido = null;
            LocalEscolhido = Locais.First(l => l.Pasta == atual.Pasta);
        }
        finally
        {
            _remontandoLocais = false;
        }
    }

    bool PodeEnviar() => !string.IsNullOrWhiteSpace(Rascunho) || Anexos.Count > 0;

    [RelayCommand(CanExecute = nameof(PodeEnviar))]
    void Enviar()
    {
        var texto = Rascunho.Trim();
        var anexos = Anexos.ToList();
        if (texto.Length == 0 && anexos.Count == 0) return;
        if (anexos.Count == 0 && EhComandoLateral(texto, out var pergunta))
        {
            Rascunho = "";
            _anfitriao.AbrirLateral(this, pergunta);
            return;
        }
        var passos = Core.Texto.Templates.Passos(texto);
        if (passos.Count > 1)
        {
            texto = passos[0];
            foreach (var passo in passos.Skip(1)) Fila.Add(new MensagemNaFilaVM(passo, [], this));
        }
        if (!EhComandoDeLimpar(texto)) texto = Citar(texto);
        if (DeveEnfileirar)
        {
            if (passos.Count > 1)
            {
                Fila.Insert(Fila.Count - (passos.Count - 1), new MensagemNaFilaVM(texto, anexos, this));
                Rascunho = "";
                Anexos.Clear();
                return;
            }
            Enfileirar(texto, anexos);
            return;
        }
        Rascunho = "";
        Anexos.Clear();
        EnviarConteudo(texto, anexos);
    }

    internal void EnviarConteudo(string texto, List<AnexoVM> anexos)
    {
        if (anexos.Count == 0 && EhComandoDeLimpar(texto))
        {
            ComecarSessaoNova();
            return;
        }
        PrepararContinuacao();
        _respondeuNoTurno = false;
        Conversou();
        TurnoDesde = DateTime.Now;
        _modeloQueRespondeu = null;
        MudarFase(FaseTurno.Pensando);
        var proprio = Citacoes.Separar(texto).Texto;
        Atividade(Idioma.F("Você: {0}", proprio.Length > 0 ? proprio : anexos.Count == 1 ? Idioma.T("1 imagem") : Idioma.F("{0} imagens", anexos.Count)));
        if ((Titulo == TituloNovo || Titulo == "Nova conversa") && proprio.Length > 0) Titulo = Resumos.UmaLinha(proprio, 60);
        var mensagem = new ItemUsuario(texto, [.. anexos.Select(a => Imagens.Miniatura(a.Dados))]);
        Adicionar(mensagem);
        AoEnviarMensagem();
        IReadOnlyList<Anexo> imagens = [.. anexos.Select(a => a.Dados)];
        if (GuardaTurnos)
        {
            _ = EnviarComPontoAsync(texto, imagens, mensagem);
            return;
        }
        Mandar(texto, imagens);
    }

    void Mandar(string texto, IReadOnlyList<Anexo> imagens)
    {
        if (JanelaVM.Demo)
        {
            Adicionar(ItemAviso.Info(Idioma.T("No modo demonstração a mensagem não vai para nenhum agente.")));
            Estado = EstadoChat.Pronta;
            return;
        }
        try
        {
            GarantirSessao().Enviar(texto, imagens);
            if (Estado != EstadoChat.PrecisaDeVoce) Estado = EstadoChat.Trabalhando;
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Adicionar(ItemAviso.Erro(ex.Message));
            Estado = EstadoChat.Erro;
        }
    }

    [RelayCommand]
    void Parar()
    {
        if (!SoNoFundo)
        {
            _sessao?.Interromper();
            return;
        }
        ZerarSegundoPlano();
        LiberarDoFundo();
    }

    [RelayCommand]
    void Fechar() => _aoFechar(this);

    public void Encerrar(bool esperar = false)
    {
        _encerrada = true;
        _descarga.Stop();
        EncerrarTerminal();
        EncerrarNavegador();
        _relogioTurno?.Stop();
        _relogioSubagentes?.Stop();
        VagasDeSubagentes.SoltarDe(this);
        CancelarEsperaDoFundo();
        EsquecerCatalogo();
        Editor.Descartar();
        if (_sessao is { } s)
        {
            if (esperar) s.Encerrar();
            else _ = s.DisposeAsync().AsTask();
        }
        _sessao = null;
        EncerrarNuvem(esperar);
        if (_hub is { } hub && App.Hub is { } central) _ = central.EsquecerAsync(hub.Chave);
        _hub = null;
        EsquecerSegredos();
    }

    public void Aplicar(EventoClaude evento)
    {
        ComecarTurnoDoFundo(evento);
        if (AplicarAndamento(evento) || AplicarSubagente(evento)) return;
        switch (evento)
        {
            case SessaoIniciada iniciada:
                SessaoId = iniciada.SessaoId;
                LembrarNuvem();
                if (EhLateral) RegistrarLateral(iniciada.SessaoId);
                VisaoGeralVM.LembrarDoNexora(iniciada.SessaoId);
                Bifurcar = false;
                GravarRefs();
                Modelo = NomeDoModelo(iniciada.Modelo) ?? Modelo;
                LembrarConversaDoAgente();
                break;
            case ContextoUsado uso:
                if (NomeDoModelo(uso.Modelo) is not null) _modeloQueRespondeu = uso.Modelo;
                ContextoTokens = uso.Tokens;
                CacheLonga = uso.CacheLonga ?? CacheLonga;
                UltimaResposta = uso.Quando ?? DateTimeOffset.Now;
                Modelo = NomeDoModelo(uso.Modelo) ?? Modelo;
                AtualizarTempo();
                break;
            case ComandosDisponiveis comandos:
                _comandos = comandos.Comandos;
                RemotoDisponivel = comandos.RemotoDisponivel;
                if (EhClaude)
                {
                    CatalogoComandos.Guardar(Pasta, comandos);
                    CatalogoModelos.Guardar(comandos.Modelos);
                }
                else
                {
                    GuardarModelosDoAgente(Agente, comandos.Modelos);
                    OnPropertyChanged(nameof(DicaDoCampo));
                }
                break;
            case TextoParcial parcial:
                _respondeuNoTurno = true;
                if (_pensamentoAtual is not null) FecharPensamento();
                if (Fase != FaseTurno.Escrevendo) MudarFase(FaseTurno.Escrevendo);
                _parcial.Append(parcial.Texto);
                if (!_descarga.IsEnabled) _descarga.Start();
                break;
            case TextoCompleto completo:
                _respondeuNoTurno = true;
                _parcial.Clear();
                if (_textoAtual is null) Adicionar(new ItemTexto(completo.Texto));
                else _textoAtual.Texto = completo.Texto;
                _textoAtual = null;
                Atividade(completo.Texto);
                break;
            case FerramentaChamada chamada:
                DescarregarParcial();
                FecharPensamento();
                _textoAtual = null;
                var item = CriarFerramenta(chamada);
                _ferramentas[chamada.Id] = item;
                AdicionarFerramenta(item, animar: true);
                MudarFase(FaseTurno.Ferramenta, item.Rotulo);
                Atividade($"{item.Rotulo}  {item.Resumo}");
                break;
            case FerramentaRespondeu resposta:
                if (_ferramentas.Remove(resposta.Id, out var ferramenta)) ferramenta.Concluir(resposta.Conteudo, resposta.Erro);
                VoltarDaFerramenta();
                AtualizarBranch();
                break;
            case PermissaoPedida pedido:
                DescarregarParcial();
                FecharPensamento();
                _textoAtual = null;
                var permissao = new ItemPermissao(pedido, Pasta, Decidir) { Subagente = SubagenteDoPedido(pedido) };
                Adicionar(permissao);
                if (PermissaoLiberavel(permissao) && _anfitriao.LiberadaEmTodas(pedido) && _sessao is { Ativa: true } liberada)
                {
                    liberada.Permitir(pedido.PedidoId, Protocolo.ParaNo(pedido.Entrada));
                    permissao.Encerrar(Idioma.T("Permitido em todas as sessões"));
                    break;
                }
                Atividade(permissao.Titulo);
                PermissaoPendente ??= permissao;
                Conversou();
                Estado = EstadoChat.PrecisaDeVoce;
                break;
            case TurnoEncerrado fim:
                DescarregarParcial();
                FecharPensamento();
                _textoAtual = null;
                foreach (var pendente in Itens.OfType<ItemPermissao>().Where(p => p.Pendente)) pendente.Encerrar(Idioma.T("O turno acabou antes da resposta."));
                EncerrarSegredosPendentes(Idioma.T("O turno acabou antes da resposta."));
                PararSubagentes(false);
                if (!fim.Erro && !_respondeuNoTurno && fim.Texto is { Length: > 0 } saida) Adicionar(new ItemTexto(saida));
                Adicionar(ItemAviso.Rodape(fim, NomeDoModelo(_modeloQueRespondeu)));
                FecharTurno();
                Conversou();
                TurnoDesde = null;
                AtividadeEm = DateTime.Now;
                JanelaContexto = fim.JanelaDe(_modeloQueRespondeu) ?? JanelaContexto;
                _modeloQueRespondeu = null;
                PermissaoPendente = null;
                if (fim.Erro) Adicionar(ItemAviso.Erro(fim.Texto is { Length: > 0 } t ? t : Idioma.T("O turno foi interrompido.")));
                Estado = fim.Erro ? EstadoChat.Erro : TemTrabalhoNoFundo ? EstadoChat.Trabalhando : EstadoChat.Pronta;
                if (Estado == EstadoChat.Trabalhando) SegurarNoFundo();
                AtualizarBranch();
                AoEncerrarTurno(fim.Erro);
                LembrarConversaDoAgente();
                if (_reiniciar) Reiniciar();
                SoltarFilaDepoisDoTurno(fim.Erro);
                break;
            case UsoDoPlano uso:
                _aoUsar(uso);
                break;
            case Core.Codex.UsoDoCodex codex:
                AvisarUsoDoCodex(codex);
                break;
            case AvisoDoAgente aviso:
                Adicionar(ItemAviso.Info(aviso.Texto));
                break;
            case ConversaCompactada compactada:
                AoCompactar(compactada);
                break;
            case ControleRespondido { Sucesso: false } recusa:
                Adicionar(ItemAviso.Erro(recusa.Erro ?? Idioma.T("O Claude recusou o comando.")));
                break;
        }
    }

    public string? DecidirPeloChefe(bool permitir, string? motivo)
    {
        if (PermissaoPendente is not { } item) return Idioma.T("Esta sessão não tem permissão pendente.");
        if (_sessao is not { Ativa: true } sessao) return Idioma.T("A sessão já fechou.");
        var pedido = item.Pedido;
        if (permitir)
        {
            sessao.Permitir(pedido.PedidoId, Protocolo.ParaNo(pedido.Entrada));
            item.Encerrar(Idioma.T("Permitido pelo orquestrador"));
        }
        else
        {
            sessao.Negar(pedido.PedidoId, string.IsNullOrWhiteSpace(motivo) ? "O orquestrador negou." : motivo);
            item.Encerrar(string.IsNullOrWhiteSpace(motivo) ? Idioma.T("Negado pelo orquestrador") : Idioma.F("Orquestrador: {0}", Resumos.UmaLinha(motivo, 120)));
        }
        AtualizarPendente();
        if (PermissaoPendente is null) Estado = EstadoChat.Trabalhando;
        return null;
    }

    public void EnviarTexto(string texto)
    {
        Rascunho = texto;
        EnviarCommand.Execute(null);
    }

    void Decidir(ItemPermissao item, Decisao decisao) => Decidir(item, decisao, null);

    void Decidir(ItemPermissao item, Decisao decisao, string? texto)
    {
        if (_sessao is not { Ativa: true } sessao)
        {
            item.Encerrar(Idioma.T("A sessão já tinha fechado."));
            return;
        }
        var pedido = item.Pedido;
        switch (decisao)
        {
            case Decisao.Permitir:
                sessao.Permitir(pedido.PedidoId, Protocolo.ParaNo(pedido.Entrada));
                item.Encerrar(texto ?? (item.TemPlano ? Idioma.T("Plano aprovado") : Idioma.T("Permitido")));
                break;
            case Decisao.PermitirEmTodas:
                sessao.Permitir(pedido.PedidoId, Protocolo.ParaNo(pedido.Entrada));
                item.Encerrar(Idioma.T("Permitido em todas as sessões"));
                _anfitriao.LiberarEmTodas(ChaveDePermissao.De(pedido.Ferramenta, pedido.Entrada));
                break;
            case Decisao.PermitirSempre:
                sessao.Permitir(pedido.PedidoId, Protocolo.ParaNo(pedido.Entrada), Protocolo.ParaNo(pedido.Sugestoes));
                item.Encerrar(Idioma.T("Permitido nesta sessão"));
                break;
            case Decisao.Negar:
                sessao.Negar(pedido.PedidoId, item.TemPlano
                    ? "O usuário ainda não aprovou o plano. Continue planejando e pergunte o que precisar."
                    : "O usuário negou pelo Nexora.");
                item.Encerrar(item.TemPlano ? Idioma.T("Continuar planejando") : Idioma.T("Negado"));
                break;
            case Decisao.Responder:
                if (item.Respostas() is not { } respostas) return;
                var entrada = Protocolo.ParaNo(pedido.Entrada) as JsonObject ?? [];
                entrada["answers"] = Protocolo.ParaNo(respostas);
                sessao.Permitir(pedido.PedidoId, entrada);
                item.Encerrar(Idioma.F("Respondido: {0}", string.Join("  ·  ", respostas.EnumerateObject().Select(r => r.Value.GetString()))));
                break;
        }
        AtualizarPendente();
        if (PermissaoPendente is null) Estado = EstadoChat.Trabalhando;
    }

    Core.Mcp.RegistroHub? _hub;
    string? _chaveDoNavegador;

    public IReadOnlyList<string> McpCompartilhados => _hub?.Nomes ?? [];

    partial void OnTituloChanged(string value)
    {
        if (_hub is { } hub) App.Hub?.Renomear(hub.Chave, value);
    }

    ISessaoAgente GarantirSessao()
    {
        if (_sessao is { Ativa: true } ativa) return ativa;
        if (!Directory.Exists(Pasta)) throw new InvalidOperationException(Idioma.F("A pasta {0} não existe mais.", Pasta));
        if (_hub is null && App.Hub is { } central)
        {
            _hub = central.Registrar(Pasta, Titulo);
            OnPropertyChanged(nameof(TemMcpDoHub));
            OnPropertyChanged(nameof(TextoMcpDoHub));
        }
        var mcp = _hub?.Configuracao is { } doHub ? Core.Mcp.HubMcp.Juntar(Mcp, doHub) : Mcp;
        var permitidas = Permitidas;
        if (NavegadorDoApp.Servidor is { } servidorDoNavegador)
        {
            _chaveDoNavegador ??= servidorDoNavegador.Registrar(Codigo);
            mcp = Core.Mcp.HubMcp.Juntar(mcp, servidorDoNavegador.Configuracao(_chaveDoNavegador));
            permitidas = [.. permitidas ?? [], .. Core.Navegador.FerramentasDoNavegador.Livres];
        }
        var opcoes = new OpcoesSessao(
            Pasta,
            SessaoId,
            ModeloDaSessao,
            ValoresModo[ModoIndice],
            LocalAtual.Vinculados.Where(Directory.Exists).ToList(),
            JanelaVM.Isolada,
            ConfiguracaoComSegredos(mcp),
            PromptExtra,
            PermitidasComSegredos(permitidas),
            Bifurcar,
            EsforcoValor,
            ConfiguracoesDaSessao,
            AmbienteDoProjeto());
        if (NaNuvem) opcoes = OpcoesNaNuvem(opcoes);
        var sessao = CriarSessao(opcoes);
        sessao.Evento += e => Dispatcher.UIThread.Post(() => Aplicar(e));
        sessao.Encerrou += (codigo, erros) => Dispatcher.UIThread.Post(() => AoEncerrar(sessao, codigo, erros));
        sessao.Iniciar();
        _sessao = sessao;
        Hibernada = false;
        OnPropertyChanged(nameof(PodeTrocarLocal));
        return sessao;
    }

    void AoEncerrar(ISessaoAgente sessao, int codigo, string erros)
    {
        if (_sessao != sessao) return;
        _sessao = null;
        EnderecoRemoto = null;
        DescarregarParcial();
        FecharPensamento();
        ZerarSegundoPlano();
        FecharTurno();
        if (codigo == 0)
        {
            if (Estado == EstadoChat.Trabalhando)
            {
                Estado = EstadoChat.Pronta;
                SoltarFilaDepoisDoTurno(false);
            }
            return;
        }
        Adicionar(ItemAviso.Erro(string.IsNullOrWhiteSpace(erros) ? Idioma.F("O {0} fechou com o código {1}.", NomeAgente, codigo) : Resumos.Limitar(erros, 1500)));
        Estado = EstadoChat.Erro;
    }

    void DescarregarParcial()
    {
        _descarga.Stop();
        if (_parcial.Length == 0) return;
        if (_textoAtual is null)
        {
            _textoAtual = new ItemTexto("");
            Adicionar(_textoAtual);
        }
        _textoAtual.Texto += _parcial.ToString();
        _parcial.Clear();
    }

    async Task CarregarHistoricoAsync(string arquivo)
    {
        var (trecho, modoDaConversa, internos) = await Task.Run(() =>
        {
            var lido = Transcricao.LerTurnos(arquivo, TurnosPorVez);
            return (lido, ConversasSalvas.UltimoModo(arquivo), InternosDoHistorico(arquivo, lido.Eventos));
        });
        ComecarHistorico(arquivo, trecho);
        MontarHistorico(trecho.Eventos, internos, modoDaConversa);
    }

    async Task CarregarDoAgenteAsync()
    {
        IReadOnlyList<EventoClaude> eventos = [];
        try
        {
            if (GarantirSessao() is IHistoricoDoAgente historico) eventos = await historico.LerHistoricoAsync();
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Itens.Add(ItemAviso.Erro(ex.Message));
        }
        MontarHistorico(eventos, new Dictionary<string, IReadOnlyList<EventoClaude>>(), null);
    }

    void LembrarConversaDoAgente()
    {
        if (EhClaude || EhLateral || JanelaVM.Demo || JanelaVM.Isolada || SessaoId is not { } id) return;
        var primeira = Itens.OfType<ItemUsuario>().FirstOrDefault()?.Texto;
        var ultima = Itens.OfType<ItemTexto>().LastOrDefault()?.Texto;
        var titulo = Titulo;
        var (agente, pasta, branch) = (Agente, Pasta, Branch);
        _ = Task.Run(() => ConversasDosAgentes.Guardar(new ConversaDeAgente(agente, id, pasta, titulo, branch, DateTime.Now,
            primeira is null ? null : Resumos.UmaLinha(primeira, 200), ultima is null ? null : Resumos.UmaLinha(ultima, 200))));
    }

    void MontarHistorico(IReadOnlyList<EventoClaude> eventos, Dictionary<string, IReadOnlyList<EventoClaude>> internos, string? modoDaConversa)
    {
        var itens = MontarItens(eventos, internos, antigos: false);
        itens.Add(ItemAviso.Info(Idioma.T("Conversa trazida do histórico. A próxima mensagem continua esta sessão.")));
        _telaSuspensa = true;
        try
        {
            foreach (var item in itens) Itens.Add(item);
        }
        finally
        {
            _telaSuspensa = false;
        }
        RefazerTela();
        UsarDoHistorico(_modeloDoHistorico, modoDaConversa);
        Carregando = false;
        _ = AtualizarRevisaoAsync();
        _ = CarregarTurnosAsync();
    }

    List<ItemChat>? _montagem;
    string? _modeloDoHistorico;

    IList<ItemChat> Destino => (IList<ItemChat>?)_montagem ?? Itens;

    List<ItemChat> MontarItens(IReadOnlyList<EventoClaude> eventos, Dictionary<string, IReadOnlyList<EventoClaude>> internos, bool antigos)
    {
        var lista = new List<ItemChat>();
        _montagem = lista;
        _antigosNoPainel = antigos ? 0 : -1;
        var agentesAntes = antigos ? _subagentes.Keys.ToHashSet() : null;
        try
        {
            var ferramentas = new Dictionary<string, ItemFerramenta>();
            foreach (var evento in eventos)
                switch (evento)
                {
                    case MensagemDoUsuario m:
                        lista.Add(new ItemUsuario(m.Texto, imagensSalvas: m.Imagens));
                        break;
                    case TextoCompleto t:
                        lista.Add(new ItemTexto(t.Texto));
                        break;
                    case FerramentaChamada f when EhSubagente(f.Nome):
                        AbrirSubagente(f, animar: false);
                        break;
                    case FerramentaChamada f:
                        var item = CriarFerramenta(f);
                        ferramentas[f.Id] = item;
                        AdicionarFerramenta(item, animar: false);
                        break;
                    case FerramentaRespondeu r when ferramentas.Remove(r.Id, out var feita):
                        feita.Concluir(r.Conteudo, r.Erro);
                        break;
                    case FerramentaRespondeu r when _subagentes.TryGetValue(r.Id, out var agente):
                        ConcluirSubagenteDoHistorico(agente, r, internos);
                        break;
                    case TarefaAtualizada tarefa:
                        AplicarTarefa(tarefa);
                        break;
                    case ContextoUsado uso when !antigos:
                        ContextoTokens = uso.Tokens;
                        CacheLonga = uso.CacheLonga ?? CacheLonga;
                        UltimaResposta = uso.Quando;
                        Modelo = NomeDoModelo(uso.Modelo) ?? Modelo;
                        if (NomeDoModelo(uso.Modelo) is not null) _modeloDoHistorico = uso.Modelo;
                        break;
                }
            foreach (var solta in ferramentas.Values) solta.Concluir("", false);
        }
        finally
        {
            _montagem = null;
            _antigosNoPainel = -1;
        }
        if (agentesAntes is null) PararSubagentes(true);
        else
            foreach (var agente in _subagentes.Where(a => !agentesAntes.Contains(a.Key) && a.Value.Rodando).Select(a => a.Value).ToList())
                agente.Concluir(EstadoSubagente.Parado, null);
        return lista;
    }

    void Adicionar(ItemChat item)
    {
        item.Animar = true;
        Itens.Add(item);
    }

    void AdicionarFerramenta(ItemFerramenta item, bool animar)
    {
        if (ItemExploracao.Explora(item.Nome))
        {
            var ultimo = Destino.LastOrDefault(i => i is not (ItemPensamento or ItemTurno));
            if (ultimo is ItemExploracao grupo)
            {
                grupo.Juntar(item);
                return;
            }
            var novo = new ItemExploracao(item);
            if (animar) Adicionar(novo);
            else Destino.Add(novo);
            return;
        }
        if (animar) Adicionar(item);
        else Destino.Add(item);
    }
}
