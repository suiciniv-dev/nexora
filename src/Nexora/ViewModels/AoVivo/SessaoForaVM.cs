using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Historico;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class SessaoForaVM(Action<string, string?> abrir, Func<string, string?, string, Task<ChatVM?>> continuar) : CartaoVM
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EnviarCommand))]
    public partial string Rascunho { get; set; } = "";

    bool PodeEnviar() => !string.IsNullOrWhiteSpace(Rascunho);

    [RelayCommand(CanExecute = nameof(PodeEnviar))]
    async Task Enviar()
    {
        var texto = Rascunho.Trim();
        Rascunho = "";
        if (await continuar(SessaoId, Pasta, texto) is not { } chat)
        {
            Rascunho = texto;
            return;
        }
        Expandido = false;
        chat.Expandido = true;
        Continuou?.Invoke(chat);
    }

    public event Action<ChatVM>? Continuou;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Origem), nameof(Pasta), nameof(Onde), nameof(Trabalhando), nameof(Precisa), nameof(Parada), nameof(TempoTexto), nameof(TextoResponder), nameof(TextoContinuar), nameof(PodeTirar), nameof(DicaTirar))]
    public partial SessaoAtiva? Dados { get; set; }

    [ObservableProperty]
    public partial string Titulo { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Onde))]
    public partial string? Branch { get; set; }

    public string SessaoId => Dados?.SessaoId ?? "";
    public string? Pasta => Dados?.Pasta;
    public string Origem => Dados is { Encerrada: true } d ? Idioma.F("{0} · fechada", d.OrigemLegivel) : Dados?.OrigemLegivel ?? "";
    public bool CacheValendo => CacheRestante is { } r && r > TimeSpan.Zero;
    public string TextoResponder => Idioma.F("Responda no {0}.", Origem);
    public string TextoContinuar => Dados is { Encerrada: true } d
        ? Idioma.F("A conversa foi fechada no {0}. O que você mandar continua ela aqui no Nexora, com o cache ainda valendo.", d.OrigemLegivel)
        : Idioma.F("O que você mandar segue no Nexora, numa cópia com todo o histórico; a aba do {0} fica como estava.", Origem);
    string NomePasta => Pasta is { } p ? Path.GetFileName(p.TrimEnd('\\', '/')) : Dados?.Nome ?? SessaoId;
    public override string NomeProjeto => NomePasta;
    public override string? PastaDoProjeto => Pasta;
    public string Onde => Branch is { Length: > 0 } b ? $"{NomePasta}  ·  {b}" : NomePasta;

    public override bool Trabalhando => Dados is { Ocupada: true };
    public override bool Precisa => Dados is { Esperando: true };
    public bool Parada => !Trabalhando && !Precisa;
    public override string DicaTirar => Idioma.F("Tirar do quadro. Volta sozinho se a conversa tiver atividade nova no {0}", Origem);

    public override DateTime? Recencia => Trabalhando || Precisa
        ? Dados?.EstadoDesde?.LocalDateTime ?? AtividadeEm
        : AtividadeEm ?? Dados?.EstadoDesde?.LocalDateTime;

    string? _arquivo;
    Andamento? _andamento;

    public override string TempoTexto => Dados switch
    {
        { Ocupada: true, EstadoDesde: { } desde } => Idioma.F("trabalhando há {0}", Duracao(desde)),
        { Esperando: true, EstadoDesde: { } desde } => Idioma.F("esperando há {0}", Duracao(desde)),
        _ when AtividadeEm is { } quando => Core.Texto.Tempo.Relativo(quando),
        _ => "",
    };

    static string Duracao(DateTimeOffset desde) => Core.Texto.Tempo.Duracao((long)Math.Max(0, (DateTimeOffset.Now - desde).TotalMilliseconds));

    public void Atualizar(SessaoAtiva dados, Andamento? andamento, string? titulo, string? arquivo)
    {
        var mudou = !ReferenceEquals(andamento, _andamento);
        (_andamento, _arquivo) = (andamento, arquivo);
        Dados = dados;
        Titulo = andamento?.Titulo ?? titulo ?? NomePasta;
        Branch = andamento?.Branch;
        UltimaAtividade = andamento?.Atividade;
        AtividadeEm = andamento?.AtividadeEm?.LocalDateTime;
        if (andamento?.Uso is { } uso)
        {
            ContextoTokens = uso.Tokens;
            CacheLonga = uso.CacheLonga ?? CacheLonga;
            UltimaResposta = uso.Quando;
            Modelo = NomeDoModelo(uso.Modelo) ?? Modelo;
        }
        AtualizarTempo();
        if (Expandido && (mudou || Trecho.Count == 0)) _ = CarregarTrechoAsync();
    }

    protected override void AoExpandir(bool aberto)
    {
        if (aberto) _ = CarregarTrechoAsync();
    }

    public const int TrechoDoCartao = TamanhoDoTrecho;
    public int TamanhoTrecho { get; set; } = TamanhoDoTrecho;

    async Task CarregarTrechoAsync()
    {
        if (_arquivo is not { } arquivo) return;
        var tamanho = TamanhoTrecho;
        var eventos = await Task.Run(() => Transcricao.LerFim(arquivo, tamanho * 3));
        var itens = new List<ItemChat>();
        var ferramentas = new Dictionary<string, ItemFerramenta>();
        foreach (var evento in eventos)
            switch (evento)
            {
                case MensagemDoUsuario m:
                    itens.Add(new ItemUsuario(m.Texto, imagensSalvas: m.Imagens));
                    break;
                case TextoCompleto t:
                    itens.Add(new ItemTexto(t.Texto));
                    break;
                case FerramentaChamada f:
                    var item = new ItemFerramenta(f.Nome, Resumos.DaFerramenta(f.Nome, f.Entrada, Pasta), f.Entrada);
                    ferramentas[f.Id] = item;
                    itens.Add(item);
                    break;
                case FerramentaRespondeu r when ferramentas.Remove(r.Id, out var feita):
                    feita.Concluir(r.Conteudo, r.Erro);
                    break;
            }
        if (!Trabalhando)
            foreach (var solta in ferramentas.Values) solta.Concluir("", false);
        Trecho.Clear();
        foreach (var item in itens.TakeLast(tamanho)) Trecho.Add(item);
    }

    [RelayCommand]
    void Ver() => abrir(SessaoId, Pasta);
}
