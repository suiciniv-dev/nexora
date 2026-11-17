using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit.TextMate;
using Nexora.Temas;
using Nexora.ViewModels;
using TextMateSharp.Grammars;

namespace Nexora.Views;

public partial class ArquivoView : UserControl
{
    static readonly RegistryOptions Opcoes = new(ThemeName.Dracula);

    TextMate.Installation? _textMate;
    ArquivoVM? _vm;

    public ArquivoView()
    {
        InitializeComponent();
        Texto.Options.HighlightCurrentLine = true;
        Texto.Options.EnableHyperlinks = false;
        Texto.Options.EnableEmailHyperlinks = false;
        AddHandler(KeyDownEvent, AoTeclar, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) => Ligar(DataContext as ArquivoVM);
        AttachedToVisualTree += (_, _) => Catalogo.Mudou += AoMudarTema;
        DetachedFromVisualTree += (_, _) =>
        {
            Catalogo.Mudou -= AoMudarTema;
            if (_vm is not null) _vm.Recarregado -= AoRecarregar;
            _textMate?.Dispose();
            _textMate = null;
        };
    }

    void Ligar(ArquivoVM? vm)
    {
        if (_vm is not null) _vm.Recarregado -= AoRecarregar;
        _vm = vm;
        if (vm is null) return;
        Texto.Document = vm.Documento;
        _textMate ??= Texto.InstallTextMate(Opcoes);
        AplicarTema();
        var linguagem = Opcoes.GetLanguageByExtension(vm.Extensao);
        _textMate.SetGrammar(linguagem is null ? null : Opcoes.GetScopeByLanguageId(linguagem.Id));
        vm.Recarregado += AoRecarregar;
        if (vm.LinhaInicial is { } linha)
        {
            vm.LinhaInicial = null;
            Dispatcher.UIThread.Post(() => IrPara(linha), DispatcherPriority.Background);
        }
        Dispatcher.UIThread.Post(() => Texto.TextArea.Focus(), DispatcherPriority.Background);
    }

    void AplicarTema()
    {
        if (_textMate is null) return;
        var nome = Enum.TryParse<ThemeName>(Catalogo.EditorDe(Catalogo.Atual), out var escolhido) ? escolhido : ThemeName.Dracula;
        _textMate.SetTheme(Opcoes.LoadTheme(nome));
        Texto.Background = Brushes.Transparent;
    }

    void AoMudarTema() => AplicarTema();

    void AoRecarregar() => Texto.TextArea.TextView.Redraw();

    void IrPara(int linha)
    {
        if (linha < 1 || linha > Texto.Document.LineCount) return;
        Texto.ScrollToLine(linha);
        Texto.TextArea.Caret.Line = linha;
    }

    void AoTeclar(object? sender, KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(Sistema.Comando)) return;
        if (e.Key == Key.S)
        {
            _vm?.Salvar();
            e.Handled = true;
        }
        else if (e.Key == Key.L)
        {
            Citar();
            e.Handled = true;
        }
    }

    void Citar_Click(object? sender, RoutedEventArgs e) => Citar();

    void Citar()
    {
        if (_vm is null) return;
        if (_vm.EhImagem)
        {
            _vm.CitarImagem();
            return;
        }
        var documento = Texto.Document;
        if (Texto.SelectionLength == 0)
        {
            var linha = documento.GetLineByOffset(Texto.CaretOffset);
            _vm.Citar(linha.LineNumber, linha.LineNumber, documento.GetText(linha));
            return;
        }
        var primeira = documento.GetLineByOffset(Texto.SelectionStart).LineNumber;
        var ultima = documento.GetLineByOffset(Texto.SelectionStart + Texto.SelectionLength).LineNumber;
        _vm.Citar(primeira, ultima, Texto.SelectedText);
    }
}
