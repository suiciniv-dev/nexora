using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Nexora.ViewModels;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.Views;

public partial class AmbienteView : UserControl
{
    public AmbienteView()
    {
        InitializeComponent();
        Composer.AddHandler(KeyDownEvent, AoTeclar, RoutingStrategies.Tunnel);
    }

    void AoTeclar(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift)) return;
        e.Handled = true;
        if (DataContext is AmbienteVM ambiente && ambiente.ComecarCommand.CanExecute(null)) ambiente.ComecarCommand.Execute(null);
    }

    async void AdicionarPasta_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } janela || DataContext is not AmbienteVM ambiente) return;
        var pastas = await janela.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Idioma.T("Pasta para vincular ao ambiente"),
            AllowMultiple = true,
        });
        foreach (var pasta in pastas)
            if (pasta.TryGetLocalPath() is { } caminho) ambiente.AdicionarVinculado(caminho);
    }
}
