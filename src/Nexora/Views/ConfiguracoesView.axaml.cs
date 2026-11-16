using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexora.Core.Texto;
using Nexora.ViewModels;

namespace Nexora.Views;

public partial class ConfiguracoesView : UserControl
{
    ConfiguracoesVM? _vm;

    public ConfiguracoesView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= AoMudarVm;
            _vm = DataContext as ConfiguracoesVM;
            if (_vm is null) return;
            _vm.PropertyChanged += AoMudarVm;
            if (_vm.SecaoPedida is { } secao) Rolar(secao);
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= AoMudarVm;
            _vm = null;
        };
    }

    void AoMudarVm(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConfiguracoesVM.SecaoPedida) && _vm?.SecaoPedida is { } secao) Rolar(secao);
    }

    void Rolar(string secao)
    {
        var nome = secao == ConfiguracoesVM.SecaoAparencia ? nameof(SecaoAparencia) : secao;
        Dispatcher.UIThread.Post(() =>
        {
            if (_vm is not null) _vm.SecaoPedida = null;
            IrPara(nome);
        }, DispatcherPriority.Loaded);
    }

    void IrPara(string nome)
    {
        if (this.FindControl<Control>(nome) is not { } alvo || Rolagem.Content is not Visual conteudo) return;
        if (alvo.TranslatePoint(default, conteudo) is not { } ponto) return;
        Rolagem.Offset = new Vector(Rolagem.Offset.X, Math.Max(0, ponto.Y - 12));
        if (alvo.GetVisualDescendants().OfType<Avalonia.Input.InputElement>().FirstOrDefault(i => i.Focusable && i.IsEffectivelyVisible && i.IsEffectivelyEnabled) is { } primeiro)
            primeiro.Focus(Avalonia.Input.NavigationMethod.Tab);
    }

    void IrPara_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is string nome) IrPara(nome);
    }

    async void AdicionarPasta_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ConfiguracoesVM vm || TopLevel.GetTopLevel(this) is not { } topo) return;
        var pastas = await topo.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Idioma.T("Pasta onde ficam seus repositórios") });
        if (pastas.FirstOrDefault()?.TryGetLocalPath() is { } pasta) vm.AdicionarPasta(pasta);
    }
}
