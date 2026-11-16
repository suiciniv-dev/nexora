using CommunityToolkit.Mvvm.ComponentModel;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class ChatVM
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvisoCache))]
    public partial bool Hibernada { get; set; }

    public bool PodeHibernar =>
        _sessao is { Ativa: true }
        && !_reiniciar
        && Estado is EstadoChat.Pronta or EstadoChat.Nova
        && TurnoDesde is null
        && PermissaoPendente is null
        && !TemFila
        && SubagentesRodando == 0
        && !RemotoLigado
        && !NaNuvem
        && CacheExpirado;

    public bool HibernarSePuder()
    {
        if (!App.Config.HibernarSessoes || !PodeHibernar || _sessao is not { } sessao) return false;
        _sessao = null;
        Hibernada = true;
        _ = sessao.DisposeAsync().AsTask();
        OnPropertyChanged(nameof(Pid));
        OnPropertyChanged(nameof(PodeTrocarLocal));
        return true;
    }

    public override string AvisoCache => base.AvisoCache + TextoHibernada;

    string TextoHibernada => Hibernada
        ? " " + Idioma.T("O agente desta conversa foi fechado para liberar memória e volta sozinho quando você mandar a próxima mensagem.")
        : "";
}
