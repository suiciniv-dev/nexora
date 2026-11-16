using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Nexora.Core.Texto;
using Nexora.ViewModels;

namespace Nexora.Views;

public partial class ProjetosView : UserControl
{
    public ProjetosView()
    {
        InitializeComponent();
        SolturaDePastas.Ligar(AreaDeSoltar, RealceSoltar);
    }

    async Task<string?> EscolherPasta(string titulo)
    {
        if (TopLevel.GetTopLevel(this) is not { } topo) return null;
        var pastas = await topo.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = titulo });
        return pastas.FirstOrDefault()?.TryGetLocalPath();
    }

    async void AdicionarProjeto_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProjetosVM vm && await EscolherPasta(Idioma.T("Pasta do projeto")) is { } pasta) vm.AdicionarPasta(pasta);
    }

    async void AdicionarRaiz_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProjetosVM vm && await EscolherPasta(Idioma.T("Pasta onde ficam seus repositórios")) is { } pasta) await vm.AdicionarRaiz(pasta);
    }

    async void Importar_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProjetosVM vm || TopLevel.GetTopLevel(this) is not { } topo) return;
        var arquivos = await topo.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Idioma.T("Importar um .code-workspace"),
            FileTypeFilter = [new FilePickerFileType(Idioma.T("Workspace do VS Code")) { Patterns = ["*.code-workspace"] }],
        });
        if (arquivos.FirstOrDefault()?.TryGetLocalPath() is { } arquivo) vm.Importar(arquivo);
    }
}
