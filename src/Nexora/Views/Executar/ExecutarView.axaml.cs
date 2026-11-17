using System.ComponentModel;
using Avalonia.Controls;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Nexora.ViewModels;

namespace Nexora.Views;

public partial class ExecutarView : UserControl
{
    ExecutarVM? _vm;
    TextDocument? _documento;

    public ExecutarView()
    {
        InitializeComponent();
        Saida.TextArea.TextView.Bind(TextView.LinkTextForegroundBrushProperty, Saida.GetResourceObservable("Acento"));
        DataContextChanged += (_, _) => Ligar(DataContext as ExecutarVM);
        DetachedFromVisualTree += (_, _) => Ligar(null);
    }

    void Ligar(ExecutarVM? vm)
    {
        if (_vm is not null) _vm.PropertyChanged -= AoMudar;
        _vm = vm;
        if (vm is not null) vm.PropertyChanged += AoMudar;
        Mostrar(vm?.Selecionado);
    }

    void AoMudar(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExecutarVM.Selecionado)) Mostrar(_vm?.Selecionado);
    }

    void Mostrar(AlvoVM? alvo)
    {
        if (_documento is not null) _documento.Changed -= AoEscrever;
        _documento = alvo?.Saida;
        Saida.Document = _documento ?? new TextDocument();
        if (_documento is null) return;
        _documento.Changed += AoEscrever;
        Saida.ScrollToEnd();
    }

    void AoEscrever(object? sender, DocumentChangeEventArgs e)
    {
        var rolagem = Saida.TextArea.TextView;
        if (rolagem.VerticalOffset + rolagem.Bounds.Height >= rolagem.DocumentHeight - 40) Saida.ScrollToEnd();
    }
}
