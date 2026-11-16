using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Nexora.ViewModels;
#if !MAC
using Microsoft.Web.WebView2.Core;
#endif

namespace Nexora.Views;

public partial class TerminalView : UserControl
{
    const string Anfitriao = "terminal.nexora";
    readonly StringBuilder _pendente = new();
    readonly DispatcherTimer _envio;
    TerminalVM? _vm;
#if !MAC
    WebViewNativo? _web;
    CoreWebView2? _core;
#endif
    bool _pronto;

    public TerminalView()
    {
        InitializeComponent();
        _envio = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Background, (_, _) => Descarregar());
        DataContextChanged += (_, _) => Ligar(DataContext as TerminalVM);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Temas.Catalogo.Mudou += EnviarTema;
#if !MAC
        if (_web is not null || !OperatingSystem.IsWindows()) return;
        _web = new WebViewNativo { Fundo = CorDoFundo() };
        _web.Pronto += Preparar;
        Raiz.Children.Add(_web);
#endif
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Temas.Catalogo.Mudou -= EnviarTema;
        base.OnDetachedFromVisualTree(e);
    }

    void Focar()
    {
#if !MAC
        _web?.Focar();
#endif
        Enviar(new { t = "foco" });
    }

#if !MAC
    void Preparar(CoreWebView2 web)
    {
        _core = web;
        web.SetVirtualHostNameToFolderMapping(Anfitriao, Path.Combine(AppContext.BaseDirectory, "Assets", "Terminal"), CoreWebView2HostResourceAccessKind.Allow);
        web.WebMessageReceived += (_, e) => Receber(e.TryGetWebMessageAsString());
        web.Navigate($"https://{Anfitriao}/index.html");
    }
#endif

    void Receber(string? json)
    {
        if (json is null) return;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            switch (raiz.GetProperty("t").GetString())
            {
                case "pronto":
                    _pronto = true;
                    EnviarTema();
                    Reenviar();
                    Focar();
                    break;
                case "entrada":
                    _vm?.Escrever(raiz.GetProperty("d").GetString() ?? "");
                    break;
                case "tamanho":
                    _vm?.Garantir((short)raiz.GetProperty("c").GetInt32(), (short)raiz.GetProperty("l").GetInt32());
                    break;
                case "fechar":
                    _vm?.Fechar();
                    break;
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
        }
    }

    void Ligar(TerminalVM? vm)
    {
        if (_vm is not null)
        {
            _vm.Recebeu -= Acumular;
            _vm.Limpou -= Reenviar;
            _vm.PedirFoco -= Focar;
        }
        _vm = vm;
        if (vm is not null)
        {
            vm.Recebeu += Acumular;
            vm.Limpou += Reenviar;
            vm.PedirFoco += Focar;
        }
        if (_pronto) Reenviar();
    }

    void Reenviar()
    {
        _pendente.Clear();
        Enviar(new { t = "limpar" });
        if (_vm is null) return;
        _pendente.Append(_vm.Historico);
        Descarregar();
    }

    void Acumular(string texto)
    {
        _pendente.Append(texto);
        if (!_envio.IsEnabled) _envio.Start();
    }

    void Descarregar()
    {
        _envio.Stop();
        if (!_pronto || _pendente.Length == 0) return;
        Enviar(new { t = "saida", d = _pendente.ToString() });
        _pendente.Clear();
    }

    void Enviar(object mensagem)
    {
#if !MAC
        if (_pronto) _core?.PostWebMessageAsString(JsonSerializer.Serialize(mensagem));
#endif
    }

    void EnviarTema() => Enviar(new
    {
        t = "tema",
        tema = new
        {
            background = Hex("FundoSolido", "#120f17"),
            foreground = Hex("Texto", "#f1e9dc"),
            cursor = Hex("Marca", "#b9a6f2"),
            cursorAccent = Hex("FundoSolido", "#120f17"),
            selectionBackground = Hex("SelecaoTexto", "#b9a6f266"),
        },
    });

    string Hex(string chave, string padrao) =>
        Application.Current?.TryGetResource(chave, Application.Current.ActualThemeVariant, out var valor) == true && valor is ISolidColorBrush pincel
            ? $"#{pincel.Color.R:x2}{pincel.Color.G:x2}{pincel.Color.B:x2}{(pincel.Color.A < 255 ? pincel.Color.A.ToString("x2") : "")}"
            : padrao;

    System.Drawing.Color CorDoFundo() =>
        Application.Current?.TryGetResource("FundoSolido", Application.Current.ActualThemeVariant, out var valor) == true && valor is ISolidColorBrush pincel
            ? System.Drawing.Color.FromArgb(255, pincel.Color.R, pincel.Color.G, pincel.Color.B)
            : System.Drawing.Color.FromArgb(255, 18, 15, 23);
}
