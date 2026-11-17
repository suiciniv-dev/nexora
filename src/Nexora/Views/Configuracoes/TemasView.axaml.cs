using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Nexora.Core.Texto;
using Nexora.ViewModels;

namespace Nexora.Views;

public partial class TemasView : UserControl
{
    static readonly FilePickerFileType Json = new(Idioma.T("Tema do Nexora")) { Patterns = ["*.json"] };
    static readonly FilePickerFileType Imagens = new(Idioma.T("Imagens")) { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"] };

    public TemasView() => InitializeComponent();

    async void Importar_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } janela || DataContext is not TemasVM vm) return;
        var arquivos = await janela.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = Idioma.T("Importar tema"), FileTypeFilter = [Json] });
        foreach (var a in arquivos)
            if (a.TryGetLocalPath() is { } caminho) vm.Importar(caminho);
    }

    async void Exportar_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } janela || DataContext is not TemasVM vm || (sender as Control)?.DataContext is not TemaCartaoVM cartao) return;
        var destino = await janela.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Idioma.T("Exportar tema"),
            SuggestedFileName = cartao.Tema.Id + ".json",
            FileTypeChoices = [Json],
        });
        if (destino?.TryGetLocalPath() is { } caminho) vm.Exportar(cartao.Tema, caminho);
    }

    async void Imagem_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } janela || DataContext is not TemasVM { Editor: { } editor }) return;
        var arquivos = await janela.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = Idioma.T("Imagem de fundo"), FileTypeFilter = [Imagens] });
        if (arquivos.Count > 0 && arquivos[0].TryGetLocalPath() is { } caminho) editor.Imagem = caminho;
    }
}
