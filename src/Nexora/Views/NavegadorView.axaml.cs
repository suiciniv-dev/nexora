using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.Core.Navegador;
using Nexora.ViewModels;
#if !MAC
using Microsoft.Web.WebView2.Core;
#endif
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.Views;

public partial class NavegadorView : UserControl, INavegadorNativo
{
    static readonly List<WeakReference<NavegadorView>> Vivas = [];
    static string? _roteiro;

    NavegadorVM? _vm;
#if !MAC
    WebViewNativo? _web;
    CoreWebView2? _core;
    string? _aIr;
#endif

    public NavegadorView()
    {
        InitializeComponent();
        CampoEndereco.AddHandler(KeyDownEvent, AoTeclarNoEndereco, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) => Ligar(DataContext as NavegadorVM);
#if MAC
        Grade(SemNavegador);
#endif
    }

#if MAC
    void Grade(Control aviso)
    {
        if (aviso.Parent is Panel pai) pai.Children.Remove(aviso);
        aviso.IsVisible = true;
        Hospedeiro.Children.Add(aviso);
    }
#endif

    public static IEnumerable<NavegadorView> Abertas()
    {
        lock (Vivas)
        {
            Vivas.RemoveAll(r => !r.TryGetTarget(out _));
            return [.. Vivas.Select(r => r.TryGetTarget(out var v) ? v : null).OfType<NavegadorView>()];
        }
    }

    void AoTeclarNoEndereco(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter && e.Key != Key.Return) return;
        if (CampoEndereco.FindDescendantOfType<TextPresenter>() is { PreeditText.Length: > 0 }) return;
        e.Handled = true;
        _vm?.IrCommand.Execute(null);
#if !MAC
        _web?.Focar();
#endif
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        lock (Vivas) Vivas.Add(new WeakReference<NavegadorView>(this));
#if !MAC
        if (_web is not null || !OperatingSystem.IsWindows()) return;
        _web = new WebViewNativo { Fundo = CorDoFundo() };
        _web.Pronto += Preparar;
        _web.Falhou += m => Dispatcher.UIThread.Post(() => _vm?.AoFalharWebView(m));
        Hospedeiro.Children.Add(_web);
#endif
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        lock (Vivas) Vivas.RemoveAll(r => !r.TryGetTarget(out var v) || v == this);
#if !MAC
        if (_web is not null)
        {
            Hospedeiro.Children.Remove(_web);
            _web = null;
            _core = null;
        }
#endif
        _vm?.Soltar(this);
        base.OnDetachedFromVisualTree(e);
    }

    void Ligar(NavegadorVM? vm)
    {
        if (_vm == vm) return;
        _vm?.Soltar(this);
        _vm = vm;
#if !MAC
        if (vm is not null && _core is not null) vm.Nativo = this;
#else
        if (vm is not null) vm.Nativo = this;
#endif
    }

#if !MAC
    void Preparar(CoreWebView2 web)
    {
        _core = web;
        web.Settings.AreDefaultContextMenusEnabled = true;
        web.Settings.IsZoomControlEnabled = true;
        web.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") web.Navigate(e.Uri);
        };
        web.NavigationStarting += (_, e) => _vm?.AoComecar(e.Uri);
        web.SourceChanged += (_, _) => Avisar();
        web.HistoryChanged += (_, _) => Avisar();
        web.DocumentTitleChanged += (_, _) => Avisar();
        web.NavigationCompleted += (_, e) =>
        {
            Avisar();
            _vm?.AoTerminar(web.Source, e.IsSuccess, e.WebErrorStatus.ToString(), e.HttpStatusCode);
        };
        web.WebMessageReceived += (_, e) =>
        {
            string? texto;
            try
            {
                texto = e.TryGetWebMessageAsString();
            }
            catch (ArgumentException)
            {
                texto = null;
            }
            _vm?.AoReceber(texto);
        };
        _ = PrepararRoteiroAsync(web);
    }

    async Task PrepararRoteiroAsync(CoreWebView2 web)
    {
        try
        {
            _roteiro ??= await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Assets", "Navegador", "design.js"));
            await web.AddScriptToExecuteOnDocumentCreatedAsync(_roteiro);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
        }
        if (_core != web) return;
        if (_aIr is { } url)
        {
            _aIr = null;
            Navegar(url);
        }
        if (_vm is { } vm) vm.Nativo = this;
    }

    void Avisar()
    {
        if (_core is not { } web) return;
        _vm?.AoMudar(web.Source, web.DocumentTitle, web.CanGoBack, web.CanGoForward);
    }

    async Task<string?> RodarAsync(string script)
    {
        if (_core is not { } web) return null;
        try
        {
            return await web.ExecuteScriptAsync(script);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            return null;
        }
    }

    async Task<bool> GarantirRoteiroAsync()
    {
        if (await RodarAsync("typeof window.__nexoraDesign") == "\"object\"") return true;
        if (_roteiro is null) return false;
        await RodarAsync(_roteiro);
        return await RodarAsync("typeof window.__nexoraDesign") == "\"object\"";
    }

    string OpcoesDoDesign() => JsonSerializer.Serialize(new
    {
        cor = Hex("Marca", "#b9a6f2"),
        textos = new
        {
            comentar = Idioma.T("O que mudar aqui?"),
            salvar = Idioma.T("Salvar"),
            cancelar = Idioma.T("Cancelar"),
            remover = Idioma.T("Remover"),
            dica = Idioma.T("Clique num elemento para comentar"),
            sair = Idioma.T("Esc sai"),
        },
    });
#endif

    public void Navegar(string url)
    {
#if !MAC
        if (_core is not { } web)
        {
            _aIr = url;
            return;
        }
        try
        {
            web.Navigate(url);
        }
        catch (ArgumentException)
        {
            _vm?.AoTerminar(url, false, "Unknown", 0);
        }
#else
        NavegadorVM.AbrirFora?.Invoke(url);
        _vm?.AoTerminar(url, true, "", 200);
#endif
    }

    public void Voltar()
    {
#if !MAC
        if (_core is { CanGoBack: true } web) web.GoBack();
#endif
    }

    public void Avancar()
    {
#if !MAC
        if (_core is { CanGoForward: true } web) web.GoForward();
#endif
    }

    public void Recarregar()
    {
#if !MAC
        _core?.Reload();
#endif
    }

    public async Task DesignAsync(bool ligado)
    {
#if !MAC
        if (!await GarantirRoteiroAsync()) return;
        await RodarAsync(ligado ? $"window.__nexoraDesign.ligar({OpcoesDoDesign()})" : "window.__nexoraDesign.desligar()");
        if (ligado) _web?.Focar();
#else
        await Task.CompletedTask;
#endif
    }

    public async Task RestaurarAsync(IReadOnlyList<Anotacao> anotacoes)
    {
#if !MAC
        if (anotacoes.Count == 0 || !await GarantirRoteiroAsync()) return;
        var lista = JsonSerializer.Serialize(anotacoes.Select(a => new { id = a.Id, seletor = a.Seletor, comentario = a.Comentario }));
        await RodarAsync($"window.__nexoraDesign.restaurar({lista})");
#else
        await Task.CompletedTask;
#endif
    }

    public async Task RemoverAsync(int id)
    {
#if !MAC
        await RodarAsync($"window.__nexoraDesign && window.__nexoraDesign.remover({id})");
#else
        await Task.CompletedTask;
#endif
    }

    public async Task LimparAsync()
    {
#if !MAC
        await RodarAsync("window.__nexoraDesign && window.__nexoraDesign.limpar()");
#else
        await Task.CompletedTask;
#endif
    }

    public async Task<byte[]?> CapturarAsync()
    {
#if !MAC
        if (_core is not { } web) return null;
        await RodarAsync("window.__nexoraDesign && window.__nexoraDesign.foto(true)");
        try
        {
            using var memoria = new MemoryStream();
            await web.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, memoria);
            return memoria.Length > 0 ? memoria.ToArray() : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            return null;
        }
        finally
        {
            await RodarAsync("window.__nexoraDesign && window.__nexoraDesign.foto(false)");
        }
#else
        await Task.CompletedTask;
        return null;
#endif
    }

    public async Task<(byte[] Png, Rect Area)?> FotografarAsync(Visual raiz)
    {
#if !MAC
        if (_web is not { IsEffectivelyVisible: true } web || _core is not { } core || web.TransformToVisual(raiz) is not { } m) return null;
        using var memoria = new MemoryStream();
        try
        {
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, memoria);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
        return (memoria.ToArray(), new Rect(web.Bounds.Size).TransformToAABB(m));
#else
        await Task.CompletedTask;
        return null;
#endif
    }

    public async Task<string?> RodarParaTesteAsync(string script)
    {
#if !MAC
        if (!await GarantirRoteiroAsync()) return null;
        return await RodarAsync(script);
#else
        await Task.CompletedTask;
        return null;
#endif
    }

    string Hex(string chave, string padrao) =>
        Application.Current?.TryGetResource(chave, Application.Current.ActualThemeVariant, out var valor) == true && valor is ISolidColorBrush pincel
            ? $"#{pincel.Color.R:x2}{pincel.Color.G:x2}{pincel.Color.B:x2}"
            : padrao;

    static System.Drawing.Color CorDoFundo() =>
        Application.Current?.TryGetResource("FundoSolido", Application.Current.ActualThemeVariant, out var valor) == true && valor is ISolidColorBrush pincel
            ? System.Drawing.Color.FromArgb(255, pincel.Color.R, pincel.Color.G, pincel.Color.B)
            : System.Drawing.Color.FromArgb(255, 18, 15, 23);
}
