using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Nexora.Core.Texto;
using Nexora.ViewModels;

namespace Nexora.Views;

public partial class BoasVindasView : UserControl
{
    public BoasVindasView()
    {
        InitializeComponent();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && IsVisible) Avalonia.Threading.Dispatcher.UIThread.Post(() => Focus());
        };
    }

    async void AdicionarPasta_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BoasVindasVM vm || TopLevel.GetTopLevel(this) is not { } topo) return;
        var pastas = await topo.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Idioma.T("Pasta onde ficam seus repositórios") });
        if (pastas.FirstOrDefault()?.TryGetLocalPath() is { } pasta) vm.AdicionarRaiz(pasta);
    }

    async void AdicionarProjeto_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BoasVindasVM vm || TopLevel.GetTopLevel(this) is not { } topo) return;
        var pastas = await topo.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Idioma.T("Pasta do projeto") });
        if (pastas.FirstOrDefault()?.TryGetLocalPath() is { } pasta) vm.AdicionarProjeto(pasta);
    }

    async void Importar_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BoasVindasVM vm || TopLevel.GetTopLevel(this) is not { } topo) return;
        var arquivos = await topo.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Idioma.T("Importar um .code-workspace"),
            FileTypeFilter = [new FilePickerFileType(Idioma.T("Workspace do VS Code")) { Patterns = ["*.code-workspace"] }],
        });
        if (arquivos.FirstOrDefault()?.TryGetLocalPath() is { } arquivo) vm.Importar(arquivo);
    }
}
