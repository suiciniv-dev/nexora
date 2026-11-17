using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Git;
using Nexora.Core.Revisao;
using Nexora.Core.Senna;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class PainelPrVM
{
    Passagem? _passagem;
    bool _destinoEscolhido;

    public static Func<string?, string?, Task<Passagem?>> BuscarPassagem { get; set; } = PassagemDoSennaAsync;

    static async Task<Passagem?> PassagemDoSennaAsync(string? pasta, string? branch)
    {
        if (JanelaVM.Demo) return PrsDemo.Passagem(branch);
        if (!PassagensDoSenna.Disponivel) return null;
        return PassagensDoSenna.DaBranch(await PassagensDoSenna.ListarAsync(), pasta, branch);
    }

    static bool EhMeu(DetalhePr pr, string autor) => pr.Eu is { Length: > 0 } eu && autor.Equals(eu, StringComparison.OrdinalIgnoreCase);

    public Passagem? Passagem => _passagem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ParaOriginal), nameof(ParaNova), nameof(DetalheDestino))]
    public partial DestinoDaRevisao Destino { get; set; } = DestinoDaRevisao.Nova;

    public bool ParaOriginal => Destino == DestinoDaRevisao.Original;
    public bool ParaNova => Destino == DestinoDaRevisao.Nova;

    [ObservableProperty]
    public partial string MotivoDestino { get; set; } = "";

    [ObservableProperty]
    public partial DestinoDaRevisao Recomendado { get; set; } = DestinoDaRevisao.Nova;

    public bool TemOriginal => !_chat.Vazia;
    public string TextoNova => _passagem is not null ? Idioma.T("Nova + passagem") : Idioma.T("Conversa nova");

    public string DetalheDestino => Destino == DestinoDaRevisao.Original
        ? Idioma.F("Entra em \"{0}\", última atividade {1}.", _chat.Titulo, _chat.AtividadeEm is { } a ? Tempo.Relativo(a) : Idioma.T("sem registro"))
        : _passagem is { } p
            ? Idioma.F("Começa com \"{0}\" ({1}) e depois os comentários.", p.Titulo, Tempo.Relativo(p.Quando.LocalDateTime))
            : Idioma.T("Não achei a passagem desta branch no Senna. A conversa nova começa lendo o PR e o diff.");

    IReadOnlyList<ConversaDoPr> ConversasParaMandar => [.. Grupos.SelectMany(g => g.Conversas).Where(c => c.Incluir).Select(c => c.Dados)];
    IReadOnlyList<ComentarioDoPr> GeraisParaMandar => [.. Comentarios.Where(c => c.Incluir).Select(c => c.Dados)];

    IReadOnlyList<RevisaoDoPr> RevisoesParaMandar => Pr is { } pr
        ? [.. pr.Revisoes.Where(r => r.Estado is "CHANGES_REQUESTED" or "COMMENTED" && r.Corpo.Trim().Length > 0 && !EhMeu(pr, r.Autor))]
        : [];

    public int Selecionados => ConversasParaMandar.Count + GeraisParaMandar.Count + (ConversasParaMandar.Count + GeraisParaMandar.Count > 0 ? RevisoesParaMandar.Count : 0);
    public bool PodeMandar => Pr is not null && ConversasParaMandar.Count + GeraisParaMandar.Count > 0;
    public bool TemEnvio => Pr is not null && (Grupos.Any(g => g.Conversas.Count > 0) || Comentarios.Any(c => !c.Meu));
    public string TextoSelecionados => Selecionados == 1 ? Idioma.T("1 comentário marcado") : Idioma.F("{0} comentários marcados", Selecionados);

    void AtualizarEnvio()
    {
        if (Pr is not null && !_destinoEscolhido)
        {
            var (destino, motivo) = ComentariosDeRevisao.Padrao(
                new SituacaoDaOriginal(TemOriginal, _chat.AtividadeEm, _chat.ContextoFracao, _passagem?.Quando), _passagem is not null, DateTime.Now);
            Recomendado = destino;
            MotivoDestino = motivo;
            Destino = TemOriginal ? destino : DestinoDaRevisao.Nova;
        }
        OnPropertyChanged(nameof(TemOriginal));
        OnPropertyChanged(nameof(TextoNova));
        OnPropertyChanged(nameof(DetalheDestino));
        OnPropertyChanged(nameof(Selecionados));
        OnPropertyChanged(nameof(PodeMandar));
        OnPropertyChanged(nameof(TemEnvio));
        OnPropertyChanged(nameof(TextoSelecionados));
        MandarComentariosCommand.NotifyCanExecuteChanged();
    }

    async Task BuscarPassagemAsync(DetalhePr pr)
    {
        var branch = pr.HeadBranch.Length > 0 ? pr.HeadBranch : _chat.Branch;
        var achada = await BuscarPassagem(_chat.Pasta, branch);
        if (Pr?.Numero != pr.Numero) return;
        _passagem = achada;
        OnPropertyChanged(nameof(Passagem));
        AtualizarEnvio();
    }

    [RelayCommand]
    void EscolherDestino(string destino)
    {
        _destinoEscolhido = true;
        Destino = destino == "original" && TemOriginal ? DestinoDaRevisao.Original : DestinoDaRevisao.Nova;
    }

    public string PedidoDosComentarios(DestinoDaRevisao destino) => Pr is { } pr
        ? ComentariosDeRevisao.PedidoDoPr(pr, ConversasParaMandar, GeraisParaMandar, RevisoesParaMandar, destino, _passagem)
        : "";

    [RelayCommand(CanExecute = nameof(PodeMandar))]
    void MandarComentarios()
    {
        if (!PodeMandar) return;
        var destino = TemOriginal ? Destino : DestinoDaRevisao.Nova;
        var texto = PedidoDosComentarios(destino);
        var quantos = Selecionados;
        foreach (var c in Grupos.SelectMany(g => g.Conversas)) c.Incluir = false;
        foreach (var c in Comentarios) c.Incluir = false;
        if (destino == DestinoDaRevisao.Original)
        {
            _chat.MandarRevisao(texto);
            Aviso = quantos == 1 ? Idioma.T("Mandei 1 comentário para a conversa original.") : Idioma.F("Mandei {0} comentários para a conversa original.", quantos);
            return;
        }
        _chat.AbrirConversaNova(texto);
        Aviso = _passagem is not null
            ? Idioma.T("Abri uma conversa nova que começa pela passagem e traz os comentários.")
            : Idioma.T("Abri uma conversa nova com os comentários.");
    }

    async Task VerDiffAsync(string arquivo)
    {
        if (Pr is not { } pr) return;
        var pasta = _chat.Pasta;
        var texto = JanelaVM.Demo ? PrsDemo.DiffDoArquivo(arquivo) : await Task.Run(() => GitHub.DiffDoArquivoNoPrAsync(pasta, pr, arquivo));
        if (texto is null)
        {
            Aviso = Idioma.F("Não deu para ler o diff de {0} no PR.", arquivo);
            return;
        }
        var caminho = Path.Combine(pasta, arquivo.Replace('/', Path.DirectorySeparatorChar));
        var conversas = pr.Conversas.Where(c => string.Equals(c.Arquivo, arquivo, StringComparison.OrdinalIgnoreCase)).ToList();
        _chat.Editor.AbrirDiff($"{Path.GetFileName(arquivo)} (PR #{pr.Numero})", $"{caminho}#pr{pr.Numero}", Diff.LerUnificado(texto), caminho, conversas);
    }

    public Task VerDiffDoArquivoAsync(string arquivo) => VerDiffAsync(arquivo);
}
