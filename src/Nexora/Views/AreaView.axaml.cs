using System.Collections.Specialized;
using Avalonia.Controls;
using Nexora.ViewModels;

namespace Nexora.Views;

public partial class AreaView : UserControl
{
    AreaVM? _area;

    public AreaView()
    {
        InitializeComponent();
        Abas.FocoPedido += conteudo => _area?.FocarConteudo(conteudo);
        DataContextChanged += (_, _) => Ligar(DataContext as AreaVM);
    }

    public AbasVivas Vivas => Abas;

    void Ligar(AreaVM? area)
    {
        if (_area is not null) _area.Guias.CollectionChanged -= AoMudarGuias;
        _area = area;
        if (area is not null) area.Guias.CollectionChanged += AoMudarGuias;
    }

    void AoMudarGuias(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_area is not null) Abas.Podar(_area.Guias.Select(g => g.Conteudo));
    }
}
