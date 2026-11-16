using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Ambientes;
using Nexora.Core.Claude;
using Nexora.Core.Orquestra;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class OrquestraVM : ObservableObject, IDisposable
{
    public const double LarguraNo = 300;
    public const double AlturaNo = 204;
    const double Coluna = 340, Linha = 232, Margem = 40, InicioAgentes = 420;
    static readonly JsonSerializerOptions Saida = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    readonly JanelaVM _janela;
    ServidorMcp? _servidor;
    string? _chave;

    const int Lembrados = 10;
    readonly DispatcherTimer _salvar;

    public OrquestraVM(JanelaVM janela)
    {
        _janela = janela;
        _janela.Abertas.CollectionChanged += AoMudarAbertas;
        _salvar = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
        {
            _salvar!.Stop();
            Salvar();
        });
    }

    public ObservableCollection<OrquestraAnteriorVM> Anteriores { get; } = [];
    public bool TemAnteriores => Anteriores.Count > 0;

    public ObservableCollection<NoVM> Nos { get; } = [];
    public ObservableCollection<GrupoVM> Grupos { get; } = [];
    public event Action? Mexeu;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Chefe), nameof(TemChefe), nameof(SemChefe))]
    public partial NoVM? NoChefe { get; set; }

    [ObservableProperty]
    public partial Local? LocalEscolhido { get; set; }

    [ObservableProperty]
    public partial int ModeloIndice { get; set; } = ChatVM.IndiceDoModelo(App.Config.ModeloChefe);

    [ObservableProperty]
    public partial int EsforcoIndice { get; set; } = ChatVM.IndiceDoEsforco(App.Config.EsforcoChefe);

    public string[] Modelos => ChatVM.RotulosModelo;
    public string[] Esforcos => ChatVM.RotulosEsforco;

    [ObservableProperty]
    public partial bool ConversaVisivel { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomTexto))]
    public partial double Zoom { get; set; } = 1;

    public string ZoomTexto => $"{Zoom:P0}";

    [ObservableProperty]
    public partial IReadOnlyList<Local> Locais { get; set; } = [];

    public ChatVM? Chefe => NoChefe?.Chat;
    public bool TemChefe => NoChefe is not null;
    public bool SemChefe => NoChefe is null;
    public bool SemAgentes => Nos.Count == 0;
    public double LarguraCanvas => Math.Max(900, Todos().Select(n => n.X + LarguraNo).DefaultIfEmpty(0).Max() + Margem);
    public double AlturaCanvas => Math.Max(640, Todos().Select(n => n.Y + AlturaNo).DefaultIfEmpty(0).Max() + Margem);

    IEnumerable<NoVM> Todos() => NoChefe is { } chefe ? Nos.Prepend(chefe) : Nos;

    public void Preparar()
    {
        Locais = _janela.Locais();
        LocalEscolhido ??= Locais.FirstOrDefault(l => App.Config.UltimaPasta is { } u && Workspaces.MesmaPasta(l.Pasta, u)) ?? Locais.FirstOrDefault();
        Anteriores.Clear();
        foreach (var (id, salvo) in App.Config.Orquestradores.OrderByDescending(o => o.Value.Atualizado).Take(Lembrados))
            Anteriores.Add(new OrquestraAnteriorVM(id, salvo, a => Retomar(a.SessaoId)));
        OnPropertyChanged(nameof(TemAnteriores));
    }

    public static bool EhOrquestrador(string? sessaoId) => sessaoId is not null && App.Config.Orquestradores.ContainsKey(sessaoId);

    public bool Retomar(string sessaoId)
    {
        if (NoChefe is not null || !App.Config.Orquestradores.TryGetValue(sessaoId, out var salvo)) return false;
        if (_janela.ChatDaSessao(sessaoId, salvo.Pasta) is not { } chefe) return false;
        Ligar(chefe);
        chefe.ModeloValor = ChatVM.ModeloDoIndice(ModeloIndice);
        chefe.EsforcoValor = ChatVM.EsforcoDoIndice(EsforcoIndice);
        NoChefe = Observar(new NoVM(chefe, this) { X = salvo.ChefeX, Y = salvo.ChefeY });
        foreach (var agente in salvo.Agentes)
        {
            if (_janela.ChatDaSessao(agente.SessaoId, agente.Pasta) is not { } chat || Nos.Any(n => n.Chat == chat)) continue;
            chat.Chefe = chefe;
            var no = Observar(new NoVM(chat, this) { X = agente.X, Y = agente.Y });
            no.Grupo = agente.Grupo is null ? null : Grupos.FirstOrDefault(g => g.Nome == agente.Grupo) ?? CriarGrupo(agente.Grupo);
            Nos.Add(no);
        }
        Avisar();
        _janela.Navegar(this);
        return true;
    }

    void Ligar(ChatVM chefe)
    {
        _servidor ??= new ServidorMcp(Ferramentas.Todas, ExecutarAsync);
        _chave = _servidor.Registrar(chefe.Codigo);
        chefe.Titulo = Idioma.T("Orquestrador");
        chefe.Mcp = _servidor.Configuracao(_chave);
        chefe.PromptExtra = Ferramentas.Instrucoes;
        chefe.Permitidas = Ferramentas.Livres;
    }

    void AgendarSalvar()
    {
        if (Chefe is null) return;
        _salvar.Stop();
        _salvar.Start();
    }

    void Salvar()
    {
        if (Chefe is not { SessaoId: { } id } chefe || NoChefe is not { } noChefe) return;
        App.Config.Orquestradores[id] = new OrquestraSalva
        {
            Pasta = chefe.Pasta,
            Titulo = chefe.Itens.OfType<ItemUsuario>().FirstOrDefault(u => u.TemTexto)?.Texto is { } pedido ? Resumos.UmaLinha(pedido, 90) : Idioma.T("Orquestrador"),
            Atualizado = DateTime.Now,
            ChefeX = noChefe.X,
            ChefeY = noChefe.Y,
            Agentes = [.. Nos.Where(n => n.Chat.SessaoId is not null).Select(n => new AgenteSalvo { SessaoId = n.Chat.SessaoId!, Pasta = n.Chat.Pasta, Grupo = n.Grupo?.Nome, X = n.X, Y = n.Y })],
        };
        foreach (var velho in App.Config.Orquestradores.OrderByDescending(o => o.Value.Atualizado).Skip(Lembrados).Select(o => o.Key).ToList())
            App.Config.Orquestradores.Remove(velho);
        if (!JanelaVM.Isolada) App.Config.Salvar();
    }

    [RelayCommand]
    void Iniciar() => IniciarEm(LocalEscolhido);

    public ChatVM? IniciarEm(Local? local)
    {
        if (NoChefe is not null || local is null) return Chefe;
        _servidor ??= new ServidorMcp(Ferramentas.Todas, ExecutarAsync);
        var chefe = _janela.CriarSessao(local);
        Ligar(chefe);
        chefe.ModeloValor = ChatVM.ModeloDoIndice(ModeloIndice);
        chefe.EsforcoValor = ChatVM.EsforcoDoIndice(EsforcoIndice);
        App.Config.ModeloChefe = ChatVM.ModeloDoIndice(ModeloIndice);
        App.Config.EsforcoChefe = ChatVM.EsforcoDoIndice(EsforcoIndice);
        if (!JanelaVM.Isolada) App.Config.Salvar();
        NoChefe = Observar(new NoVM(chefe, this) { X = Margem, Y = Margem + Linha });
        Avisar();
        return chefe;
    }

    public void Demo(ChatVM chefe, IReadOnlyList<(ChatVM Agente, string? Grupo)> agentes)
    {
        chefe.Mcp = "{}";
        NoChefe = Observar(new NoVM(chefe, this) { X = Margem, Y = Margem + Linha });
        foreach (var (agente, grupo) in agentes)
        {
            agente.Chefe = chefe;
            Adicionar(agente, grupo);
        }
    }

    [RelayCommand]
    void Encerrar() => Chefe?.FecharCommand.Execute(null);

    [RelayCommand]
    void AlternarConversa() => ConversaVisivel = !ConversaVisivel;

    [RelayCommand]
    public void Aproximar() => Zoom = Math.Min(1.4, Math.Round(Zoom + 0.1, 1));

    [RelayCommand]
    public void Afastar() => Zoom = Math.Max(0.4, Math.Round(Zoom - 0.1, 1));

    public void Mostrar(ChatVM chat) => _janela.Mostrar(chat);

    public void Remover(NoVM no) => no.Chat.FecharCommand.Execute(null);

    public void Agrupar(NoVM no, GrupoVM? grupo)
    {
        no.Grupo = grupo;
        if (grupo is not null) Posicionar(no, grupo);
        Avisar();
    }

    public void NovoGrupo(NoVM no)
    {
        var numero = 1;
        while (Grupos.Any(g => g.Nome == Idioma.F("Grupo {0}", numero))) numero++;
        no.Grupo = null;
        Agrupar(no, CriarGrupo(Idioma.F("Grupo {0}", numero)));
    }

    public void Soltar(NoVM no)
    {
        var (cx, cy) = (no.X + LarguraNo / 2, no.Y + AlturaNo / 2);
        var destino = Grupos.FirstOrDefault(g =>
            Nos.Where(n => n != no && n.Grupo == g).ToList() is { Count: > 0 } outros
            && GrupoVM.Caixa(outros) is var (x, y, largura, altura)
            && cx >= x && cx <= x + largura && cy >= y && cy <= y + altura);
        if (destino is not null) no.Grupo = destino;
        else if (no.Grupo is { } atual && Nos.Any(n => n != no && n.Grupo == atual)) no.Grupo = null;
        Avisar();
    }

    public void MoverGrupo(GrupoVM grupo, double dx, double dy)
    {
        foreach (var no in Nos.Where(n => n.Grupo == grupo))
        {
            no.X = Math.Max(0, no.X + dx);
            no.Y = Math.Max(0, no.Y + dy);
        }
    }

    GrupoVM CriarGrupo(string nome)
    {
        var grupo = new GrupoVM(nome);
        Grupos.Add(grupo);
        return grupo;
    }

    NoVM Adicionar(ChatVM chat, string? grupo)
    {
        var no = Observar(new NoVM(chat, this));
        var g = grupo is null ? null : Grupos.FirstOrDefault(x => x.Nome.Equals(grupo, StringComparison.OrdinalIgnoreCase)) ?? CriarGrupo(grupo);
        Nos.Add(no);
        no.Grupo = g;
        Posicionar(no, g);
        Avisar();
        return no;
    }

    NoVM Observar(NoVM no)
    {
        no.PropertyChanged += AoMudarNo;
        no.Chat.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ChatVM.Estado)) Mexeu?.Invoke();
            if (e.PropertyName == nameof(ChatVM.SessaoId)) AgendarSalvar();
        };
        return no;
    }

    void Posicionar(NoVM no, GrupoVM? grupo)
    {
        var membros = grupo is null ? [] : Nos.Where(n => n != no && n.Grupo == grupo).ToList();
        if (membros.Count > 0)
        {
            no.X = membros[0].X;
            no.Y = membros.Max(m => m.Y) + Linha;
            return;
        }
        for (var coluna = 0; ; coluna++)
        {
            var x = InicioAgentes + coluna * Coluna;
            if (grupo is not null)
            {
                if (Nos.Any(n => n != no && Math.Abs(n.X - x) < LarguraNo)) continue;
                (no.X, no.Y) = (x, Margem);
                return;
            }
            if (Nos.Any(n => n != no && n.Grupo is not null && Math.Abs(n.X - x) < LarguraNo)) continue;
            for (var linha = 0; linha < 3; linha++)
            {
                var y = Margem + linha * Linha;
                if (Nos.Any(n => n != no && Math.Abs(n.X - x) < LarguraNo && Math.Abs(n.Y - y) < AlturaNo)) continue;
                (no.X, no.Y) = (x, y);
                return;
            }
        }
    }

    void AoMudarNo(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NoVM.X) or nameof(NoVM.Y)) Avisar();
    }

    void Avisar()
    {
        foreach (var vazio in Grupos.Where(g => !Nos.Any(n => n.Grupo == g)).ToList()) Grupos.Remove(vazio);
        foreach (var grupo in Grupos) grupo.Medir(Nos.Where(n => n.Grupo == grupo));
        OnPropertyChanged(nameof(LarguraCanvas));
        OnPropertyChanged(nameof(AlturaCanvas));
        OnPropertyChanged(nameof(SemAgentes));
        Mexeu?.Invoke();
        AgendarSalvar();
    }

    void AoMudarAbertas(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var chat in e.OldItems?.OfType<ChatVM>() ?? [])
        {
            if (chat == Chefe)
            {
                Salvar();
                if (_chave is not null) _servidor?.Esquecer(_chave);
                _chave = null;
                NoChefe = null;
                Nos.Clear();
                Grupos.Clear();
            }
            else if (Nos.FirstOrDefault(n => n.Chat == chat) is { } no)
                Nos.Remove(no);
        }
        Avisar();
    }

    Task<ResultadoFerramenta> ExecutarAsync(string dono, string ferramenta, JsonElement argumentos, CancellationToken cancelar)
    {
        var copia = argumentos.ValueKind == JsonValueKind.Object ? argumentos.Clone() : default;
        return Dispatcher.UIThread.InvokeAsync(() => ExecutarNaTelaAsync(dono, ferramenta, copia, cancelar));
    }

    async Task<ResultadoFerramenta> ExecutarNaTelaAsync(string dono, string ferramenta, JsonElement a, CancellationToken cancelar)
    {
        if (Chefe is not { } chefe || chefe.Codigo != dono) return Falha("Este orquestrador não está mais aberto no Nexora.");
        string? T(string nome) => Protocolo.Texto(a, nome)?.Trim() is { Length: > 0 } t ? t : null;
        int? N(string nome) => a.ValueKind == JsonValueKind.Object && a.TryGetProperty(nome, out var v) && v.TryGetInt32(out var n) ? n : null;
        return ferramenta switch
        {
            Ferramentas.ListarAmbientes => Ok(new JsonArray([.. _janela.Ambientes.Select(DescreverAmbiente)])),
            Ferramentas.ListarSessoes => Ok(new JsonArray([.. _janela.Abertas.Where(c => c != chefe).Select(c => Descrever(c, chefe))])),
            Ferramentas.CriarAgente => await CriarAgenteAsync(chefe, T("ambiente"), T("mensagem"), T("tarefa"), T("titulo"), T("grupo"), T("modelo"), T("esforco")),
            Ferramentas.EnviarMensagem => Enviar(T("id"), T("mensagem")),
            Ferramentas.LerSessao => Ler(T("id"), N("ultimos") ?? 12),
            Ferramentas.Esperar => await EsperarAsync(chefe, Ids(a), N("segundos") ?? 120, cancelar),
            Ferramentas.ResponderPermissao => Responder(T("id"), T("decisao"), T("motivo")),
            Ferramentas.PararSessao => Parar(T("id")),
            _ => Falha($"Ferramenta desconhecida: {ferramenta}"),
        };
    }

    static JsonObject DescreverAmbiente(AmbienteVM a) => new()
    {
        ["nome"] = a.Nome,
        ["pasta"] = a.Principal,
        ["branch"] = a.Branch,
        ["vinculados"] = new JsonArray([.. a.Local.Vinculados.Select(v => (JsonNode)v)]),
    };

    JsonObject Descrever(ChatVM c, ChatVM chefe)
    {
        var o = new JsonObject
        {
            ["id"] = c.Codigo,
            ["titulo"] = c.Titulo,
            ["pasta"] = c.Pasta,
            ["branch"] = c.Branch,
            ["estado"] = Estado(c),
            ["agente"] = c.Chefe == chefe,
        };
        if (Nos.FirstOrDefault(n => n.Chat == c)?.Grupo is { } grupo) o["grupo"] = grupo.Nome;
        if (c.UltimaAtividade is { } atividade) o["ultima_atividade"] = atividade;
        if (c.PermissaoPendente is { } p) o["permissao_pendente"] = Pendencia(p);
        return o;
    }

    static string Estado(ChatVM c) => c.Estado switch
    {
        EstadoChat.Trabalhando => "trabalhando",
        EstadoChat.PrecisaDeVoce => "precisa de resposta",
        EstadoChat.Pronta => "pronta",
        EstadoChat.Erro => "erro",
        _ => "nova",
    };

    static JsonObject Pendencia(ItemPermissao p)
    {
        var o = new JsonObject { ["ferramenta"] = p.Pedido.Ferramenta, ["titulo"] = p.Titulo };
        if (p.Detalhe is { } detalhe) o["detalhe"] = Resumos.Limitar(detalhe, 1500);
        if (p.EhPergunta)
            o["perguntas"] = new JsonArray([.. p.Perguntas.Select(q => new JsonObject
            {
                ["pergunta"] = q.Texto,
                ["opcoes"] = new JsonArray([.. q.Opcoes.Select(op => (JsonNode)op.Rotulo)]),
            })]);
        return o;
    }

    async Task<ResultadoFerramenta> CriarAgenteAsync(ChatVM chefe, string? ambiente, string? mensagem, string? tarefa, string? titulo, string? grupo, string? modelo, string? esforco)
    {
        if (ambiente is null || mensagem is null) return Falha("Informe ambiente e mensagem.");
        var achado = _janela.Ambientes.FirstOrDefault(a => a.Nome.Equals(ambiente, StringComparison.OrdinalIgnoreCase)) ?? _janela.AmbienteDe(ambiente);
        if (achado is null && !Directory.Exists(ambiente)) return Falha($"Ambiente não encontrado: {ambiente}. Veja listar_ambientes.");
        var local = achado?.Local ?? _janela.LocalDe(ambiente);
        string? worktree = null;
        if (tarefa is not null)
        {
            achado ??= _janela.Registrar(new Ambiente(local.Nome, local.Pasta, [], null));
            var (novo, aviso) = await achado.CriarWorktreeAsync(tarefa);
            if (novo is null) return Falha(aviso);
            local = novo.Local;
            worktree = aviso;
        }
        var agente = _janela.CriarSessao(local);
        agente.Chefe = chefe;
        agente.ModeloValor = ChatVM.ModeloDoIndice(ChatVM.IndiceDoModelo(modelo));
        agente.EsforcoValor = ChatVM.EsforcoDoIndice(ChatVM.IndiceDoEsforco(esforco));
        if (titulo is not null) agente.Titulo = titulo;
        var no = Adicionar(agente, grupo);
        agente.EnviarTexto(mensagem);
        return Ok(new JsonObject
        {
            ["id"] = agente.Codigo,
            ["pasta"] = agente.Pasta,
            ["branch"] = agente.Branch,
            ["grupo"] = no.Grupo?.Nome,
            ["modelo"] = modelo ?? "padrão",
            ["esforco"] = esforco ?? "padrão",
            ["worktree"] = worktree,
            ["estado"] = Estado(agente),
        });
    }

    ChatVM? Sessao(string? id) => id is null ? null : _janela.Abertas.FirstOrDefault(c => c.Codigo == id);

    static ResultadoFerramenta SemSessao(string? id) => Falha($"Sessão não encontrada: {id}. Veja listar_sessoes.");

    ResultadoFerramenta Enviar(string? id, string? mensagem)
    {
        if (Sessao(id) is not { } chat) return SemSessao(id);
        if (chat.EhChefe) return Falha("Essa é a sua própria sessão.");
        if (mensagem is null) return Falha("A mensagem está vazia.");
        chat.EnviarTexto(mensagem);
        return chat.Trabalhando || chat.Precisa
            ? new ResultadoFerramenta($"Mensagem enviada para {chat.Codigo}.")
            : Falha($"{chat.Codigo} não aceitou a mensagem: {chat.Itens.OfType<ItemAviso>().LastOrDefault()?.Texto}");
    }

    ResultadoFerramenta Ler(string? id, int ultimos)
    {
        if (Sessao(id) is not { } chat) return SemSessao(id);
        var sb = new StringBuilder();
        sb.AppendLine($"{chat.Codigo} · {chat.Titulo} · {Estado(chat)} · {chat.Onde}");
        if (chat.TemContexto) sb.AppendLine($"Contexto: {chat.ContextoTexto}");
        foreach (var item in chat.Itens.TakeLast(Math.Clamp(ultimos, 1, 60)))
            if (LinhaDoItem(item) is { } linha) sb.AppendLine(linha);
        return new ResultadoFerramenta(sb.ToString().TrimEnd());
    }

    internal static string? LinhaDoItem(ItemChat item) => item switch
    {
        ItemUsuario u => "Pedido: " + Resumos.Limitar(u.Texto, 1500),
        ItemTexto t => "Claude: " + Resumos.Limitar(t.Texto, 2500),
        ItemFerramenta { Rodando: true } f => $"[{f.Nome}] {f.Resumo} (rodando)",
        ItemFerramenta { Erro: true } f => $"[{f.Nome}] {f.Resumo} (erro: {Resumos.UmaLinha(f.Resultado ?? "", 200)})",
        ItemFerramenta f => $"[{f.Nome}] {f.Resumo}",
        ItemExploracao e => string.Join("\n", e.Partes.Select(f => $"[{f.Nome}] {f.Resumo}")),
        ItemPermissao { Pendente: true } p => $"[Permissão pendente] {p.Titulo}" + (p.TemDetalhe ? $": {Resumos.Limitar(p.Detalhe!, 600)}" : ""),
        ItemPermissao p => $"[Permissão] {p.Titulo}: {p.Resposta}",
        ItemAviso { EhRodape: true } a => $"[Fim do turno] {a.Texto}",
        ItemAviso a => $"[Aviso] {a.Texto}",
        _ => null,
    };

    static List<string>? Ids(JsonElement a) =>
        a.ValueKind == JsonValueKind.Object && a.TryGetProperty("ids", out var ids) && ids.ValueKind == JsonValueKind.Array
            ? [.. ids.EnumerateArray().Where(i => i.ValueKind == JsonValueKind.String).Select(i => i.GetString()!)]
            : null;

    async Task<ResultadoFerramenta> EsperarAsync(ChatVM chefe, List<string>? ids, int segundos, CancellationToken cancelar)
    {
        var alvos = ids is { Count: > 0 } ? ids.Select(Sessao).OfType<ChatVM>().ToList() : _janela.Abertas.Where(c => c.Chefe == chefe).ToList();
        if (alvos.Count == 0) return Falha("Nenhuma sessão para esperar.");
        var limite = DateTime.Now.AddSeconds(Math.Clamp(segundos, 1, Ferramentas.EsperaMaxima));
        while (DateTime.Now < limite && Chefe == chefe && alvos.Any(c => c.Trabalhando) && !alvos.Any(c => c.Precisa))
        {
            try
            {
                await Task.Delay(500, cancelar);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        return Ok(new JsonArray([.. alvos.Select(c => Descrever(c, chefe))]));
    }

    ResultadoFerramenta Responder(string? id, string? decisao, string? motivo)
    {
        if (Sessao(id) is not { } chat) return SemSessao(id);
        if (chat.EhChefe) return Falha("Essa é a sua própria sessão.");
        if (decisao is not ("permitir" or "negar")) return Falha("decisao precisa ser permitir ou negar.");
        if (decisao == "permitir" && chat.PermissaoPendente is { EhPergunta: true })
            return Falha("É uma pergunta do agente: use negar e escreva a resposta em motivo.");
        return chat.DecidirPeloChefe(decisao == "permitir", motivo) is { } erro
            ? Falha(erro)
            : new ResultadoFerramenta(decisao == "permitir" ? $"Permitido em {chat.Codigo}." : $"Negado em {chat.Codigo}.");
    }

    ResultadoFerramenta Parar(string? id)
    {
        if (Sessao(id) is not { } chat) return SemSessao(id);
        if (chat.EhChefe) return Falha("Essa é a sua própria sessão.");
        chat.PararCommand.Execute(null);
        return new ResultadoFerramenta($"Pedido de parada enviado para {chat.Codigo}.");
    }

    static ResultadoFerramenta Ok(JsonNode dados) => new(dados.ToJsonString(Saida));

    static ResultadoFerramenta Falha(string texto) => new(texto, Erro: true);

    public void Dispose() => _servidor?.Dispose();
}

public sealed class OrquestraSalva
{
    public string Pasta { get; set; } = "";
    public string Titulo { get; set; } = "";
    public DateTime Atualizado { get; set; }
    public double ChefeX { get; set; }
    public double ChefeY { get; set; }
    public List<AgenteSalvo> Agentes { get; set; } = [];
}

public sealed class AgenteSalvo
{
    public string SessaoId { get; set; } = "";
    public string Pasta { get; set; } = "";
    public string? Grupo { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed partial class OrquestraAnteriorVM(string sessaoId, OrquestraSalva dados, Action<OrquestraAnteriorVM> retomar) : ObservableObject
{
    public string SessaoId { get; } = sessaoId;
    public string Titulo => dados.Titulo;
    public string Detalhe => $"{Path.GetFileName(dados.Pasta.TrimEnd('\\', '/'))}  ·  {(dados.Agentes.Count == 1 ? Idioma.T("1 agente") : Idioma.F("{0} agentes", dados.Agentes.Count))}  ·  {Core.Texto.Tempo.Relativo(dados.Atualizado)}";

    [RelayCommand]
    void Retomar() => retomar(this);
}

public sealed partial class NoVM(ChatVM chat, OrquestraVM dono) : ObservableObject
{
    public ChatVM Chat { get; } = chat;
    public OrquestraVM Dono { get; } = dono;
    public bool EhChefe => Chat.EhChefe;

    [ObservableProperty]
    public partial double X { get; set; }

    [ObservableProperty]
    public partial double Y { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemGrupo))]
    public partial GrupoVM? Grupo { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DelegarCommand))]
    public partial string Recado { get; set; } = "";

    public bool TemGrupo => Grupo is not null;

    [RelayCommand]
    void Abrir() => Dono.Mostrar(Chat);

    bool PodeDelegar() => !string.IsNullOrWhiteSpace(Recado);

    [RelayCommand(CanExecute = nameof(PodeDelegar))]
    void Delegar()
    {
        Chat.EnviarTexto(Recado.Trim());
        Recado = "";
    }

    [RelayCommand]
    void Remover() => Dono.Remover(this);

    [RelayCommand]
    void AgruparEm(GrupoVM grupo) => Dono.Agrupar(this, grupo);

    [RelayCommand]
    void NovoGrupo() => Dono.NovoGrupo(this);

    [RelayCommand]
    void TirarDoGrupo() => Dono.Agrupar(this, null);
}

public sealed partial class GrupoVM(string nome) : ObservableObject
{
    const double Folga = 14, Cabeca = 34;

    [ObservableProperty]
    public partial string Nome { get; set; } = nome;

    [ObservableProperty]
    public partial double X { get; set; }

    [ObservableProperty]
    public partial double Y { get; set; }

    [ObservableProperty]
    public partial double Largura { get; set; }

    [ObservableProperty]
    public partial double Altura { get; set; }

    public void Medir(IEnumerable<NoVM> membros)
    {
        var lista = membros.ToList();
        if (lista.Count > 0) (X, Y, Largura, Altura) = Caixa(lista);
    }

    public static (double X, double Y, double Largura, double Altura) Caixa(IReadOnlyList<NoVM> membros)
    {
        var x = membros.Min(n => n.X) - Folga;
        var y = membros.Min(n => n.Y) - Cabeca;
        return (x, y, membros.Max(n => n.X) + OrquestraVM.LarguraNo + Folga - x, membros.Max(n => n.Y) + OrquestraVM.AlturaNo + Folga - y);
    }
}
