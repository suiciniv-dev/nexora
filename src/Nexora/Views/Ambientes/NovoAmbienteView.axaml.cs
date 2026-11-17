using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Nexora.ViewModels;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.Views;

public partial class NovoAmbienteView : UserControl
{
    public NovoAmbienteView() => InitializeComponent();

    async void Principal_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not NovoAmbienteVM vm) return;
        foreach (var pasta in await Escolher(Idioma.T("Pasta principal do ambiente"), multiplas: false)) vm.UsarPrincipal(pasta);
    }

    async void Vinculado_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not NovoAmbienteVM vm) return;
        foreach (var pasta in await Escolher(Idioma.T("Pastas para vincular"), multiplas: true)) vm.AdicionarVinculado(pasta);
    }

    async Task<IEnumerable<string>> Escolher(string titulo, bool multiplas)
    {
        if (TopLevel.GetTopLevel(this) is not { } janela) return [];
        var pastas = await janela.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = titulo, AllowMultiple = multiplas });
        return pastas.Select(p => p.TryGetLocalPath()).OfType<string>();
    }
}
