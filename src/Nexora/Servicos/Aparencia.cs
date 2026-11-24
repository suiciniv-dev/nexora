using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data;
using Avalonia.VisualTree;

namespace Nexora.Servicos;

public static partial class Aparencia
{
    public const double ZoomMinimo = 0.8;
    public const double ZoomMaximo = 1.5;
    public const double PassoDoZoom = 0.1;
    public const string FonteDoSistema = "sistema";
    public const string AnimacoesDoSistema = "sistema";
    public const string AnimacoesLigadas = "ligadas";
    public const string AnimacoesReduzidasId = "reduzidas";
    public static readonly string[] ModosDeAnimacao = [AnimacoesDoSistema, AnimacoesLigadas, AnimacoesReduzidasId];

    static readonly ConditionalWeakTable<Animatable, IDisposable> Desligadas = new();
    static readonly List<WeakReference<Window>> Abertas = [];
    static volatile bool _reduzidas;
    static bool _ligada;

    public static event Action? Mudou;

    public static double Zoom { get; private set; } = 1;
    public static bool AnimacoesReduzidas => _reduzidas;
    public static string ModoDeAnimacao { get; private set; } = AnimacoesDoSistema;

    public static void Ligar()
    {
        if (_ligada) return;
        _ligada = true;
        Animatable.TransitionsProperty.Changed.AddClassHandler<Animatable>((alvo, e) =>
        {
            if (_reduzidas && e.NewValue is Transitions { Count: > 0 }) Desligar(alvo);
        });
        Window.WindowOpenedEvent.AddClassHandler<Window>((janela, e) =>
        {
            Abertas.RemoveAll(r => !r.TryGetTarget(out _));
            Abertas.Add(new WeakReference<Window>(janela));
        });
        WindowBase.IsActiveProperty.Changed.AddClassHandler<Window>((janela, _) =>
        {
            if (janela.IsActive && ModoDeAnimacao == AnimacoesDoSistema) Reavaliar();
        });
        Zoom = Limitar(App.Config.Zoom);
        ModoDeAnimacao = Normalizar(App.Config.Animacoes);
        AplicarRecursos();
        Reavaliar();
    }

    static void AplicarRecursos()
    {
        if (Application.Current is not { } app) return;
        app.Resources["TamanhoDaDica"] = 12 * Zoom;
        app.Resources["LarguraDaDica"] = 380 * Zoom;
    }

    public static double Limitar(double zoom) =>
        double.IsFinite(zoom) && zoom > 0 ? Math.Round(Math.Clamp(zoom, ZoomMinimo, ZoomMaximo), 2) : 1;

    static string Normalizar(string? modo) => ModosDeAnimacao.Contains(modo) ? modo! : AnimacoesDoSistema;

    public static void DefinirZoom(double zoom)
    {
        var novo = Limitar(zoom);
        if (novo == Zoom) return;
        Zoom = novo;
        App.Config.Zoom = novo;
        AplicarRecursos();
        Mudou?.Invoke();
    }

    public static void DefinirAnimacoes(string modo)
    {
        ModoDeAnimacao = Normalizar(modo);
        App.Config.Animacoes = ModoDeAnimacao;
        Reavaliar();
    }

    public static void Reavaliar()
    {
        var reduzidas = ModoDeAnimacao switch
        {
            AnimacoesLigadas => false,
            AnimacoesReduzidasId => true,
            _ => SistemaReduzMovimento(),
        };
        if (reduzidas == _reduzidas) return;
        _reduzidas = reduzidas;
        if (reduzidas) DesligarTudo();
        else ReligarTudo();
        Pulso.Instancia.MovimentoMudou();
        Mudou?.Invoke();
    }

    static void Desligar(Animatable alvo)
    {
        if (Desligadas.TryGetValue(alvo, out _)) return;
        if (alvo.SetValue(Animatable.TransitionsProperty, null, BindingPriority.Animation) is { } volta) Desligadas.AddOrUpdate(alvo, volta);
    }

    static void DesligarTudo()
    {
        var janelas = Abertas.Select(r => r.TryGetTarget(out var j) ? j : null).OfType<Window>().ToList();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime vida) janelas.AddRange(vida.Windows);
        foreach (var janela in janelas.Distinct())
        {
            Desligar(janela);
            foreach (var visual in janela.GetVisualDescendants())
                if (visual.Transitions is { Count: > 0 }) Desligar(visual);
        }
    }

    static void ReligarTudo()
    {
        var todas = Desligadas.ToList();
        Desligadas.Clear();
        foreach (var (_, volta) in todas) volta.Dispose();
    }

    public static bool SistemaReduzMovimento()
    {
        try
        {
            if (OperatingSystem.IsWindows()) return SystemParametersInfo(SpiGetClientAreaAnimation, 0, out var ligada, 0) && ligada == 0;
            if (OperatingSystem.IsMacOS()) return MacReduzMovimento();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException)
        {
        }
        return false;
    }

    const uint SpiGetClientAreaAnimation = 0x1042;

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint acao, uint parametro, out int valor, uint flags);

    const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    const uint Utf8 = 0x08000100;

    [LibraryImport(CoreFoundation, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr CFStringCreateWithCString(IntPtr alocador, string texto, uint codificacao);

    [LibraryImport(CoreFoundation)]
    private static partial byte CFPreferencesGetAppBooleanValue(IntPtr chave, IntPtr app, out byte valida);

    [LibraryImport(CoreFoundation)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CFPreferencesAppSynchronize(IntPtr app);

    [LibraryImport(CoreFoundation)]
    private static partial void CFRelease(IntPtr objeto);

    static bool MacReduzMovimento()
    {
        var chave = CFStringCreateWithCString(IntPtr.Zero, "reduceMotion", Utf8);
        var app = CFStringCreateWithCString(IntPtr.Zero, "com.apple.universalaccess", Utf8);
        try
        {
            if (chave == IntPtr.Zero || app == IntPtr.Zero) return false;
            CFPreferencesAppSynchronize(app);
            return CFPreferencesGetAppBooleanValue(chave, app, out var valida) != 0 && valida != 0;
        }
        finally
        {
            if (chave != IntPtr.Zero) CFRelease(chave);
            if (app != IntPtr.Zero) CFRelease(app);
        }
    }
}
