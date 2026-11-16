using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Nexora;

public sealed class Pulso : INotifyPropertyChanged
{
    public const double ValorParado = 1;
    public const double EsqueletoParado = 0.45;
    static readonly PropertyChangedEventArgs MudouValor = new(nameof(Valor));
    static readonly PropertyChangedEventArgs MudouEsqueleto = new(nameof(Esqueleto));

    public static Pulso Instancia { get; } = new();

    readonly DispatcherTimer _relogio;
    readonly Stopwatch _tempo = Stopwatch.StartNew();

    Pulso() => _relogio = new DispatcherTimer(TimeSpan.FromMilliseconds(125), DispatcherPriority.Background, (_, _) => Passo());

    public double Valor { get; private set; } = ValorParado;
    public double Esqueleto { get; private set; } = EsqueletoParado;
    public bool Rodando => _relogio.IsEnabled;
    public bool Animando => _relogio.IsEnabled && !Aparencia.AnimacoesReduzidas;

    public void MovimentoMudou()
    {
        if (Aparencia.AnimacoesReduzidas) Definir(ValorParado, EsqueletoParado);
        AtividadeMudou?.Invoke();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? AtividadeMudou;

    public static void Acompanhar()
    {
        WindowBase.IsActiveProperty.Changed.AddClassHandler<Window>((_, _) => Instancia.Reavaliar());
        Window.WindowStateProperty.Changed.AddClassHandler<Window>((_, _) => Instancia.Reavaliar());
    }

    public void Reavaliar()
    {
        var algumaAtiva = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime vida
            && vida.Windows.Any(w => w.IsActive && w.WindowState != WindowState.Minimized);
        if (algumaAtiva)
        {
            if (_relogio.IsEnabled) return;
            _relogio.Start();
            AtividadeMudou?.Invoke();
            return;
        }
        if (!_relogio.IsEnabled) return;
        _relogio.Stop();
        Definir(ValorParado, EsqueletoParado);
        AtividadeMudou?.Invoke();
    }

    void Passo()
    {
        if (Aparencia.AnimacoesReduzidas) return;
        var fase = Math.Cos(_tempo.Elapsed.TotalSeconds * Math.PI / 1.2);
        Definir(Math.Round(0.625 + 0.375 * fase, 2), Math.Round(0.4 + 0.2 * fase, 2));
    }

    void Definir(double valor, double esqueleto)
    {
        if (valor != Valor)
        {
            Valor = valor;
            PropertyChanged?.Invoke(this, MudouValor);
        }
        if (esqueleto != Esqueleto)
        {
            Esqueleto = esqueleto;
            PropertyChanged?.Invoke(this, MudouEsqueleto);
        }
    }
}
