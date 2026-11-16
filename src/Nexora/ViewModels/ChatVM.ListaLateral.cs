using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Nexora.Core.Claude;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    static readonly TimeSpan ParadaDepoisDe = TimeSpan.FromHours(1);

    static readonly HashSet<string> MudamALinha =
    [
        nameof(Estado), nameof(TempoTexto), nameof(UltimaAtividade), nameof(AtividadeEm), nameof(TextoPr), nameof(Pr),
    ];

    [ObservableProperty]
    public partial bool RecolhidaNaLateral { get; set; }

    public bool ParadaHaTempo =>
        Estado is EstadoChat.Pronta or EstadoChat.Nova && !EmFoco && AtividadeEm is { } quando && DateTime.Now - quando > ParadaDepoisDe;

    public string LinhaLateral => Estado switch
    {
        EstadoChat.PrecisaDeVoce => TempoTexto,
        EstadoChat.Trabalhando when UltimaAtividade is { Length: > 0 } a => Idioma.F("{0} · {1}", Resumos.UmaLinha(a, 60), TempoCurto(TurnoDesde)),
        EstadoChat.Trabalhando => TempoTexto,
        EstadoChat.Erro => Idioma.F("erro no turno · {0}", TempoCurto(AtividadeEm)),
        EstadoChat.Pronta when TemPr => TextoPr,
        EstadoChat.Pronta => Idioma.F("pronta · {0}", TempoCurto(AtividadeEm)),
        _ => Idioma.T("nova"),
    };

    string? _pedidoInicial;

    public string? PedidoInicial => _pedidoInicial;
    public bool TemPedidoInicial => _pedidoInicial is not null && !string.Equals(_pedidoInicial, Titulo, StringComparison.OrdinalIgnoreCase);

    void AtualizarPedidoInicial()
    {
        if (_pedidoInicial is not null && Itens.Count > 0) return;
        var texto = Itens.OfType<ItemUsuario>().FirstOrDefault(u => u.TemTexto)?.Texto;
        var novo = texto is null ? null : Resumos.UmaLinha(texto, 200);
        if (novo == _pedidoInicial) return;
        _pedidoInicial = novo;
        OnPropertyChanged(nameof(PedidoInicial));
        OnPropertyChanged(nameof(TemPedidoInicial));
    }

    public bool LinhaPrecisa => Estado == EstadoChat.PrecisaDeVoce;
    public bool LinhaErro => Estado == EstadoChat.Erro || PrFalhou;
    public bool LinhaPronta => Estado == EstadoChat.Pronta && !PrFalhou;

    static string TempoCurto(DateTime? desde)
    {
        if (desde is not { } d) return Idioma.T("agora");
        var t = DateTime.Now - d;
        return t.TotalMinutes < 1 ? Idioma.T("agora")
            : t.TotalHours < 1 ? Idioma.F("{0} min", (int)t.TotalMinutes)
            : t.TotalDays < 1 ? Idioma.F("{0} h", (int)t.TotalHours)
            : Idioma.F("{0} d", (int)t.TotalDays);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(SessaoId) or nameof(Vazia)) AvisarTrocaDeAgente();
        if (e.PropertyName is nameof(Modelo) && !EhClaude) base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(ModeloRotulo)));
        if (e.PropertyName is not { } nome || !MudamALinha.Contains(nome)) return;
        base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(LinhaLateral)));
        base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(LinhaPrecisa)));
        base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(LinhaErro)));
        base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(LinhaPronta)));
    }
}
