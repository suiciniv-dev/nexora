using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Nexora.ViewModels;

namespace Nexora.Views;

public partial class PainelArquivosView : UserControl
{
    PainelVM? _vm;

    static PainelArquivosView() => IsVisibleProperty.Changed.AddClassHandler<TextBox>((campo, _) =>
    {
        if (!campo.IsVisible || !campo.Classes.Contains("renomear")) return;
        Dispatcher.UIThread.Post(() =>
        {
            campo.Focus();
            var texto = campo.Text ?? "";
            var ponto = campo.DataContext is NoArquivoVM { EhPasta: false } ? texto.LastIndexOf('.') : -1;
            campo.SelectionStart = 0;
            campo.SelectionEnd = ponto > 0 ? ponto : texto.Length;
        }, DispatcherPriority.Background);
    });

    public PainelArquivosView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.PedirFocoBusca -= FocarBusca;
            _vm = DataContext as PainelVM;
            if (_vm is not null) _vm.PedirFocoBusca += FocarBusca;
        };
    }

    void FocarBusca() => Dispatcher.UIThread.Post(() =>
    {
        CampoBusca.Focus();
        CampoBusca.SelectAll();
    }, DispatcherPriority.Background);

    async void CopiarCaminho_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is NoArquivoVM { Real: true } no && TopLevel.GetTopLevel(this)?.Clipboard is { } area)
            await area.SetTextAsync(no.Caminho);
    }
}
