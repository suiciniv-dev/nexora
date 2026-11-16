using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public abstract partial class CartaoVM : ObservableObject
{
    protected const int TamanhoDoTrecho = 14;

    protected CartaoVM() => Trecho.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TemTrecho));

    public ObservableCollection<ItemChat> Trecho { get; } = [];
    public bool TemTrecho => Trecho.Count > 0;
    public abstract DateTime? Recencia { get; }

    public virtual string AgenteDoCartao => Core.Claude.Agentes.Claude;
    public virtual bool NaNuvem => false;
    public virtual string NomeDaMaquina => "";
    public virtual bool NuvemCaiu => false;
    public virtual string DicaOndeRoda => "";

    public string NomeDoAgente => AgenteDoCartao == CatalogoDeAvatares.IdOrquestrador ? Idioma.T("Orquestrador") : Core.Provedores.CatalogoAgentes.De(AgenteDoCartao).Nome;

    [ObservableProperty]
    public partial bool Expandido { get; set; }

    partial void OnExpandidoChanged(bool value) => AoExpandir(value);

    protected virtual void AoExpandir(bool aberto)
    {
    }

    [RelayCommand]
    void AlternarExpandido() => Expandido = !Expandido;

    [ObservableProperty]
    public partial string? UltimaAtividade { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TempoTexto))]
    public partial DateTime? AtividadeEm { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextoTexto), nameof(ContextoFracao), nameof(TemContexto), nameof(ContextoCheio))]
    public partial long? ContextoTokens { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextoTexto), nameof(ContextoFracao), nameof(ContextoCheio), nameof(TemJanela))]
    public partial long? JanelaContexto { get; set; }

    [ObservableProperty]
    public partial bool? CacheLonga { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset? UltimaResposta { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemModelo))]
    public partial string? Modelo { get; set; }

    public abstract bool Trabalhando { get; }
    public abstract bool Precisa { get; }
    public abstract string TempoTexto { get; }
    public virtual bool Pronta => false;
    public bool PodeTirar => !Trabalhando && !Precisa;
    public abstract string DicaTirar { get; }
    public abstract string NomeProjeto { get; }
    public string Iniciais => Siglas.De(NomeProjeto);
    public virtual string? PastaDoProjeto => null;

    Avalonia.Media.Imaging.Bitmap? _iconeRepo;
    bool _iconePedido;

    public Avalonia.Media.Imaging.Bitmap? IconeRepo
    {
        get
        {
            PedirIcone();
            return _iconeRepo;
        }
    }

    public bool TemIconeRepo
    {
        get
        {
            PedirIcone();
            return _iconeRepo is not null;
        }
    }

    bool _pastaSolta;

    public bool PastaSolta => _pastaSolta;
    public bool MostrarIniciais => _iconeRepo is null && !_pastaSolta;

    void PedirIcone()
    {
        if (_iconePedido || JanelaVM.Demo || PastaDoProjeto is not { Length: > 0 } pasta) return;
        _iconePedido = true;
        _ = CarregarIconeAsync(pasta);
    }

    async Task CarregarIconeAsync(string pasta)
    {
        var icone = await IconesDeRepositorio.DeAsync(pasta);
        if (icone.Imagem is null && icone.Repositorio) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _iconeRepo = icone.Imagem;
            _pastaSolta = !icone.Repositorio;
            OnPropertyChanged(nameof(IconeRepo));
            OnPropertyChanged(nameof(TemIconeRepo));
            OnPropertyChanged(nameof(PastaSolta));
            OnPropertyChanged(nameof(MostrarIniciais));
        });
    }

    public bool TemModelo => Modelo is not null;
    public bool TemContexto => ContextoTokens is > 0;
    public bool TemJanela => JanelaContexto is > 0;
    public double ContextoFracao => ContextoTokens is { } t && JanelaContexto is > 0 ? Math.Min(1, (double)t / JanelaContexto.Value) : 0;
    public bool ContextoCheio => ContextoFracao >= 0.8;

    public string ContextoTexto => ContextoTokens is not { } t ? ""
        : JanelaContexto is > 0 ? Idioma.F("{0} de {1}  ({2:P0})", Mil(t), Mil(JanelaContexto.Value), ContextoFracao)
        : $"{Mil(t)} tokens";

    protected TimeSpan? CacheRestante =>UltimaResposta is { } ultima && CacheLonga is { } longa
        ? (longa ? TimeSpan.FromHours(1) : TimeSpan.FromMinutes(5)) - (DateTimeOffset.Now - ultima)
        : null;

    public bool TemCache => CacheRestante is not null;

    public bool AvisoCacheVisivel => CacheExpirado && !Trabalhando && ContextoTokens is > 0;

    public virtual string AvisoCache => UltimaResposta is { } ultima && ContextoTokens is { } tokens
        ? Idioma.F("Parada há {0}. O cache do prompt provavelmente expirou: a próxima mensagem relê cerca de {1} tokens.", Core.Texto.Tempo.Duracao((long)(DateTimeOffset.Now - ultima).TotalMilliseconds), TokensPorExtenso(tokens))
        : "";

    public static string TokensPorExtenso(long tokens) =>
        tokens >= 2_000_000 ? Idioma.F("{0:0.#} milhões", tokens / 1_000_000.0)
        : tokens >= 1_000_000 ? Idioma.F("{0:0.#} milhão", tokens / 1_000_000.0)
        : tokens >= 1000 ? Idioma.F("{0} mil", Math.Round(tokens / 1000.0)) : tokens.ToString();
    public bool CacheExpirado => CacheRestante is { } r && r <= TimeSpan.Zero;
    public bool CacheAcabando => CacheRestante is { } r && r > TimeSpan.Zero && r < TimeSpan.FromMinutes(10);

    public string CacheTexto => CacheRestante switch
    {
        null => "",
        { } r when r <= TimeSpan.Zero => Idioma.T("cache expirado: a próxima mensagem relê tudo"),
        { } r when r < TimeSpan.FromMinutes(1) => Idioma.T("cache expira em menos de 1 min"),
        { } r => Idioma.F("cache por mais {0} min", (int)Math.Ceiling(r.TotalMinutes)),
    };

    static string Mil(long tokens) => tokens >= 1_000_000 ? string.Format(Idioma.Cultura, "{0:0.#}M", tokens / 1_000_000.0) : tokens >= 1000 ? string.Format(Idioma.Cultura, "{0:0}k", tokens / 1000.0) : tokens.ToString();

    public static string? NomeDoModelo(string? id) =>
        string.IsNullOrWhiteSpace(id) || id.StartsWith('<') ? null : Core.Claude.CatalogoModelos.NomeDoId(id);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemPr), nameof(TextoPr), nameof(PrFalhou), nameof(PrAprovado), nameof(PrFechado), nameof(PrMergeado))]
    public partial Core.Git.EstadoPr? Pr { get; set; }

    partial void OnPrChanged(Core.Git.EstadoPr? value) => AoMudarPr(value);

    protected virtual void AoMudarPr(Core.Git.EstadoPr? pr)
    {
    }

    public bool TemPr => Pr is not null;
    public bool PrFalhou => Pr is { Aberto: true } p && (p.ChecksFalhos > 0 || p.Revisao == "CHANGES_REQUESTED");
    public bool PrAprovado => Pr is { Aberto: true, ChecksFalhos: 0, ChecksPendentes: 0, Revisao: "APPROVED" };
    public bool PrFechado => Pr is { Aberto: false, Mergeado: false };
    public bool PrMergeado => Pr is { Mergeado: true };

    public string TextoPr => Pr switch
    {
        null => "",
        { Mergeado: true } p => Idioma.F("PR #{0} · mergeado", p.Numero),
        { Aberto: false } p => Idioma.F("PR #{0} · fechado", p.Numero),
        { } p => string.Join(" · ", new[]
        {
            $"PR #{p.Numero}",
            p.Rascunho ? Idioma.T("rascunho") : null,
            p.ChecksFalhos > 0 ? (p.ChecksFalhos == 1 ? Idioma.T("1 check falhou") : Idioma.F("{0} checks falharam", p.ChecksFalhos))
                : p.ChecksPendentes > 0 ? Idioma.T("checks rodando")
                : p.ChecksOk > 0 ? Idioma.T("checks ok") : null,
            p.Revisao switch
            {
                "APPROVED" => Idioma.T("aprovado"),
                "CHANGES_REQUESTED" => Idioma.T("pediram mudanças"),
                "REVIEW_REQUIRED" => Idioma.T("esperando revisão"),
                _ => null,
            },
        }.OfType<string>()),
    };

    [RelayCommand]
    void AbrirPr() => AoAbrirPr();

    protected virtual void AoAbrirPr()
    {
        if (Pr?.Url is not { Length: > 0 } url || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            Sistema.Abrir(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    public void AtualizarTempo()
    {
        OnPropertyChanged(nameof(TempoTexto));
        OnPropertyChanged(nameof(CacheTexto));
        OnPropertyChanged(nameof(TemCache));
        OnPropertyChanged(nameof(CacheExpirado));
        OnPropertyChanged(nameof(CacheAcabando));
        OnPropertyChanged(nameof(AvisoCacheVisivel));
        OnPropertyChanged(nameof(AvisoCache));
    }
}
