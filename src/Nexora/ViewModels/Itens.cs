using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public abstract class ItemChat : ObservableObject
{
    public bool Animar { get; set; }
}

public sealed class ItemUsuario(string texto, IReadOnlyList<Avalonia.Media.Imaging.Bitmap>? imagens = null, int imagensSalvas = 0) : ItemChat
{
    static readonly (string? Trecho, string Texto) Nada = (null, "");
    readonly (string? Trecho, string Texto) _partes = texto.StartsWith('>') ? Core.Texto.Citacoes.Separar(texto) : Nada;
    public string Enviado { get; } = texto;
    public string Texto => _partes.Trecho is null ? Enviado : _partes.Texto;
    public CitacaoVM? Citacao => _citacao ??= _partes.Trecho is { } trecho ? new CitacaoVM(trecho) : null;
    public bool TemCitacao => _partes.Trecho is not null;
    CitacaoVM? _citacao;
    public IReadOnlyList<CitadoVM> Citados { get; } = [.. Core.Texto.Mencoes.Arquivos(texto).Select(c => new CitadoVM(c))];
    public bool TemCitados => Citados.Count > 0;
    public IReadOnlyList<Avalonia.Media.Imaging.Bitmap> Imagens { get; } = imagens ?? [];
    public bool TemTexto => Texto.Length > 0;
    public bool TemImagens => Imagens.Count > 0;
    public bool TemImagensSalvas => Imagens.Count == 0 && imagensSalvas > 0;
    public string TextoImagens => imagensSalvas == 1 ? Idioma.T("1 imagem") : Idioma.F("{0} imagens", imagensSalvas);
}

public sealed partial class AnexoVM(Anexo dados, Avalonia.Media.Imaging.Bitmap miniatura, Action<AnexoVM> remover) : ObservableObject
{
    public Anexo Dados { get; } = dados;
    public Avalonia.Media.Imaging.Bitmap Miniatura { get; } = miniatura;
    public string Nome => Dados.Nome;

    [RelayCommand]
    void Remover() => remover(this);
}

public sealed partial class ItemTexto : ItemChat
{
    public ItemTexto(string texto) => Texto = texto;

    [ObservableProperty]
    public partial string Texto { get; set; }
}

public enum AcaoFerramenta { Diff, Abrir }

public sealed partial class ItemFerramenta : ItemChat
{
    const int MaiorResultado = 6000;
    static readonly HashSet<string> ComArquivo = ["Read", "Edit", "Write", "MultiEdit", "NotebookEdit"];
    static readonly HashSet<string> ComDiff = ["Edit", "Write", "MultiEdit"];

    readonly Action<ItemFerramenta, AcaoFerramenta>? _acao;

    public ItemFerramenta(string nome, string resumo, JsonElement entrada = default, Action<ItemFerramenta, AcaoFerramenta>? acao = null)
    {
        Nome = nome;
        Resumo = resumo;
        Entrada = entrada;
        _acao = acao;
        Caminho = Protocolo.Texto(entrada, "file_path") ?? Protocolo.Texto(entrada, "notebook_path");
    }

    public static bool EditaArquivo(string nome) => ComDiff.Contains(nome);

    public string Nome { get; }
    public string Rotulo => Resumos.NomeDaFerramenta(Nome);
    public string Resumo { get; }
    public JsonElement Entrada { get; }
    public string? Caminho { get; }
    public bool TemArquivo => _acao is not null && Caminho is not null && ComArquivo.Contains(Nome);
    public bool TemDiff => _acao is not null && Caminho is not null && ComDiff.Contains(Nome);

    [RelayCommand]
    void VerDiff() => _acao?.Invoke(this, AcaoFerramenta.Diff);

    [RelayCommand]
    void AbrirArquivo() => _acao?.Invoke(this, AcaoFerramenta.Abrir);

    [ObservableProperty]
    public partial string? Resultado { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Ok))]
    public partial bool Erro { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Ok), nameof(Rodando))]
    public partial bool Concluida { get; set; }

    [ObservableProperty]
    public partial bool Aberta { get; set; }

    public bool Ok => Concluida && !Erro;
    public bool Rodando => !Concluida;

    public void Concluir(string resultado, bool erro)
    {
        Resultado = string.IsNullOrWhiteSpace(resultado) ? Idioma.T("(sem saída)") : Resumos.Limitar(resultado.Trim(), MaiorResultado);
        Erro = erro;
        Concluida = true;
    }

    [RelayCommand]
    void Alternar() => Aberta = !Aberta;
}

public sealed partial class ItemTurno(int numero, ItemUsuario mensagem, Action<ItemTurno> ver, Func<ItemTurno, Task> desfazer) : ItemChat
{
    public int Numero { get; } = numero;
    public ItemUsuario Mensagem { get; } = mensagem;
    public string? Antes { get; set; }
    public string? Depois { get; set; }
    public string? Transcricao { get; set; }
    public long Bytes { get; set; }
    public string? TurnoCodex { get; set; }
    public Task Marcacao { get; set; } = Task.CompletedTask;
    public int Seguintes { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Visivel), nameof(TemArquivos), nameof(TextoArquivos), nameof(TextoMais), nameof(TextoMenos))]
    public partial Core.Git.ResumoDoTurno? Resumo { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostraAcoes))]
    public partial bool Confirmando { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostraAcoes))]
    public partial bool Desfazendo { get; set; }

    public bool Visivel => Resumo is not null;
    public bool MostraAcoes => !Confirmando && !Desfazendo;
    public bool TemArquivos => Resumo is { Arquivos.Count: > 0 };
    public string TextoArquivos => Resumo switch
    {
        null => "",
        { Arquivos.Count: 0 } => Idioma.T("Nenhum arquivo mudou neste turno"),
        { Arquivos.Count: 1 } => Idioma.T("1 arquivo"),
        var r => Idioma.F("{0} arquivos", r.Arquivos.Count),
    };
    public string TextoMais => Resumo is { } r ? $"+{r.Mais}" : "";
    public string TextoMenos => Resumo is { } r ? $"−{r.Menos}" : "";
    public string TextoConfirmar => Seguintes == 0
        ? Idioma.T("Desfazer este turno? Os arquivos voltam a como estavam antes desta mensagem e a conversa volta para antes dela.")
        : Idioma.F("Desfazer este turno e os {0} seguintes? Os arquivos voltam a como estavam antes desta mensagem e a conversa volta para antes dela.", Seguintes);

    [RelayCommand]
    void Ver() => ver(this);

    [RelayCommand]
    void Desfazer()
    {
        OnPropertyChanged(nameof(TextoConfirmar));
        Confirmando = true;
    }

    [RelayCommand]
    void Cancelar() => Confirmando = false;

    [RelayCommand]
    Task Confirmar() => desfazer(this);
}

public enum TipoAviso { Info, Erro, Rodape, Troca, NovaSessao }

public sealed class ItemAviso(string texto, TipoAviso tipo, string? chave = null) : ItemChat
{
    public string Texto { get; } = texto;
    public string? Chave { get; } = chave;
    public bool EhNovaSessao => tipo == TipoAviso.NovaSessao;
    public bool EhErro => tipo == TipoAviso.Erro;
    public bool EhInfo => tipo == TipoAviso.Info;
    public bool EhRodape => tipo == TipoAviso.Rodape;
    public bool EhTroca => tipo == TipoAviso.Troca;

    public static ItemAviso Erro(string texto) => new(texto, TipoAviso.Erro);
    public static ItemAviso Info(string texto) => new(texto, TipoAviso.Info);

    public static ItemAviso Troca(string texto, string? chave = null) => new(texto, TipoAviso.Troca, chave);

    public static ItemAviso NovaSessao(string texto) => new(texto, TipoAviso.NovaSessao);

    public static ItemAviso Rodape(TurnoEncerrado fim, string? modelo = null) =>
        new(modelo is { Length: > 0 } ? $"{Tempo.Duracao(fim.DuracaoMs)}  ·  {modelo}" : Tempo.Duracao(fim.DuracaoMs), TipoAviso.Rodape);
}

public sealed partial class ItemPensamento : ItemChat
{
    readonly DateTime _inicio;

    public ItemPensamento(DateTime? inicio = null) => _inicio = inicio ?? DateTime.Now;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Rotulo), nameof(TemTokens))]
    public partial int Tokens { get; set; }

    public bool TemTokens => Tokens > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Rotulo), nameof(Fase), nameof(Rodando), nameof(Concluido))]
    public partial TimeSpan? Duracao { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemTexto))]
    public partial string Texto { get; set; } = "";

    [ObservableProperty]
    public partial bool Aberto { get; set; }

    public bool Rodando => Duracao is null;
    public bool Concluido => Duracao is not null;
    public bool TemTexto => Texto.Length > 0;

    public string Rotulo
    {
        get
        {
            var tokens = Tokens > 0 ? "  ·  " + TextoTokens(Tokens) : "";
            return Fase + tokens;
        }
    }

    public string Fase => Duracao is { } d ? Idioma.F("Pensou por {0}", Tempo.Duracao(Math.Max(1000, (long)d.TotalMilliseconds))) : Idioma.T("Pensando…");

    public static string TextoTokens(int tokens) =>
        tokens < 1000 ? Idioma.F("{0} tokens", tokens) : Idioma.F("{0:0.#} mil tokens", tokens / 1000.0);

    public void Encerrar(DateTime? agora = null)
    {
        if (Duracao is null) Duracao = (agora ?? DateTime.Now) - _inicio;
    }

    [RelayCommand]
    void Alternar()
    {
        if (TemTexto) Aberto = !Aberto;
    }
}

public sealed partial class OpcaoMenuVM(string nome, string? descricao, bool selecionada, Action escolher) : ObservableObject
{
    public string Nome { get; } = nome;
    public string? Descricao { get; } = descricao;
    public bool TemDescricao => !string.IsNullOrEmpty(Descricao);
    public bool Selecionada { get; } = selecionada;

    [RelayCommand]
    void Escolher() => escolher();
}

public sealed partial class OpcaoEsforcoVM(string rotulo, bool ativa, bool padrao, Action escolher) : ObservableObject
{
    public string Rotulo { get; } = rotulo;
    public bool Ativa { get; } = ativa;
    public bool Padrao { get; } = padrao;
    public string Dica => Padrao ? Idioma.F("{0} (padrão do Claude)", Rotulo) : Rotulo;

    [RelayCommand]
    void Escolher() => escolher();
}

public enum Decisao { Permitir, PermitirSempre, PermitirEmTodas, Negar, Responder }

public sealed partial class ItemPermissao : ItemChat
{
    readonly Action<ItemPermissao, Decisao> _decidir;

    public ItemPermissao(PermissaoPedida pedido, string? pasta, Action<ItemPermissao, Decisao> decidir)
    {
        Pedido = pedido;
        _decidir = decidir;
        Detalhe = Resumos.DetalheDaPermissao(pedido.Ferramenta, pedido.Entrada, pasta);
        Motivo = pedido.Motivo;
        switch (pedido.Ferramenta)
        {
            case "ExitPlanMode":
                Titulo = Idioma.T("O plano está pronto. Posso seguir?");
                Plano = Protocolo.Texto(pedido.Entrada, "plan");
                RotuloPermitir = Idioma.T("Aprovar o plano");
                RotuloNegar = Idioma.T("Continuar planejando");
                Motivo = null;
                break;
            case "AskUserQuestion":
                Titulo = Idioma.T("O agente tem uma pergunta");
                Perguntas = Protocolo.Itens(Protocolo.Objeto(pedido.Entrada, "questions")).Select(p => new PerguntaVM(p)).ToList();
                Motivo = null;
                foreach (var p in Perguntas)
                {
                    p.Mudou += () => OnPropertyChanged(nameof(PodeResponder));
                    if (RespostaRapida)
                        foreach (var o in p.Opcoes) o.Rapida = escolhida =>
                        {
                            escolhida.Selecionada = true;
                            Responder();
                        };
                }
                break;
            default:
                Titulo = Idioma.F("Permitir {0}", pedido.Ferramenta) + (pedido.Descricao is { Length: > 0 } d && d != Detalhe ? $": {Resumos.UmaLinha(d, 80)}" : "?");
                break;
        }
    }

    public PermissaoPedida Pedido { get; }
    public string? Subagente { get; init; }
    public bool TemSubagente => Subagente is { Length: > 0 };
    public string Titulo { get; }
    public string? Detalhe { get; }
    public string? Motivo { get; }
    public string? Plano { get; }
    public List<PerguntaVM> Perguntas { get; } = [];
    public string RotuloPermitir { get; } = Idioma.T("Permitir");
    public string RotuloNegar { get; } = Idioma.T("Negar");

    public bool TemDetalhe => !string.IsNullOrWhiteSpace(Detalhe);
    public bool TemMotivo => !string.IsNullOrWhiteSpace(Motivo);
    public bool TemPlano => !string.IsNullOrWhiteSpace(Plano);
    public bool EhPergunta => Perguntas.Count > 0;
    public bool MostraBotoes => Pendente && !EhPergunta;
    public bool MostraResponder => Pendente && EhPergunta;
    public bool RespostaRapida => Perguntas.Count == 1 && !Perguntas[0].Multipla && Perguntas[0].Opcoes.Count > 0;
    public bool PrecisaBotaoResponder => !RespostaRapida;
    public bool PodeResponder => Pendente && Perguntas.All(p => p.Opcoes.Any(o => o.Selecionada) || !string.IsNullOrWhiteSpace(p.Outra));
    public bool PodeSempre => Pedido.Sugestoes is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Pendente), nameof(MostraBotoes), nameof(MostraResponder), nameof(PodeResponder))]
    public partial bool Respondida { get; set; }

    [ObservableProperty]
    public partial string? Resposta { get; set; }

    public bool Pendente => !Respondida;

    public JsonElement? Respostas()
    {
        var respostas = new Dictionary<string, string>();
        foreach (var p in Perguntas)
        {
            var escolhidas = p.Opcoes.Where(o => o.Selecionada).Select(o => o.Rotulo).ToList();
            if (!string.IsNullOrWhiteSpace(p.Outra)) escolhidas.Add(p.Outra.Trim());
            if (escolhidas.Count == 0) return null;
            respostas[p.Texto] = string.Join(", ", escolhidas);
        }
        return JsonSerializer.SerializeToElement(respostas);
    }

    public void Encerrar(string resposta)
    {
        Respondida = true;
        Resposta = resposta;
    }

    [RelayCommand]
    void Permitir() => _decidir(this, Decisao.Permitir);

    [RelayCommand]
    void PermitirSempre() => _decidir(this, Decisao.PermitirSempre);

    [RelayCommand]
    void PermitirEmTodas() => _decidir(this, Decisao.PermitirEmTodas);

    [RelayCommand]
    void Negar() => _decidir(this, Decisao.Negar);

    [RelayCommand]
    void Responder() => _decidir(this, Decisao.Responder);
}

public sealed partial class PerguntaVM : ObservableObject
{
    public PerguntaVM(JsonElement pergunta)
    {
        Texto = Protocolo.Texto(pergunta, "question") ?? "";
        Cabecalho = Protocolo.Texto(pergunta, "header");
        Multipla = Protocolo.Objeto(pergunta, "multiSelect").ValueKind == JsonValueKind.True;
        Opcoes = Protocolo.Itens(Protocolo.Objeto(pergunta, "options"))
            .Select(o => new OpcaoVM(Protocolo.Texto(o, "label") ?? "", Protocolo.Texto(o, "description"), Escolher))
            .ToList();
    }

    public string Texto { get; }
    public string? Cabecalho { get; }
    public bool Multipla { get; }
    public List<OpcaoVM> Opcoes { get; }

    public event Action? Mudou;

    [ObservableProperty]
    public partial string? Outra { get; set; }

    partial void OnOutraChanged(string? value) => Mudou?.Invoke();

    void Escolher(OpcaoVM escolhida)
    {
        if (!Multipla && escolhida.Selecionada)
            foreach (var o in Opcoes)
                if (o != escolhida) o.Selecionada = false;
        Mudou?.Invoke();
    }
}

public sealed partial class OpcaoVM(string rotulo, string? descricao, Action<OpcaoVM> aoEscolher) : ObservableObject
{
    public string Rotulo { get; } = rotulo;
    public string? Descricao { get; } = descricao;
    public bool TemDescricao => !string.IsNullOrWhiteSpace(Descricao);

    public Action<OpcaoVM>? Rapida { get; set; }

    [ObservableProperty]
    public partial bool Selecionada { get; set; }

    partial void OnSelecionadaChanged(bool value) => aoEscolher(this);

    [RelayCommand]
    void Escolher()
    {
        if (Rapida is { } rapida) rapida(this);
        else Selecionada = !Selecionada;
    }
}
