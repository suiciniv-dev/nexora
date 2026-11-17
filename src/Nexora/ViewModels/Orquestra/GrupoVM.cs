using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Ambientes;
using Nexora.Core.Claude;
using Nexora.Core.Orquestra;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class GrupoVM(string nome) : ObservableObject
{
    const double Folga = 14, Cabeca = 34;

    [ObservableProperty]
    public partial string Nome { get; set; } = nome;

    [ObservableProperty]
    public partial double X { get; set; }

    [ObservableProperty]
    public partial double Y { get; set; }

    [ObservableProperty]
    public partial double Largura { get; set; }

    [ObservableProperty]
    public partial double Altura { get; set; }

    public void Medir(IEnumerable<NoVM> membros)
    {
        var lista = membros.ToList();
        if (lista.Count > 0) (X, Y, Largura, Altura) = Caixa(lista);
    }

    public static (double X, double Y, double Largura, double Altura) Caixa(IReadOnlyList<NoVM> membros)
    {
        var x = membros.Min(n => n.X) - Folga;
        var y = membros.Min(n => n.Y) - Cabeca;
        return (x, y, membros.Max(n => n.X) + OrquestraVM.LarguraNo + Folga - x, membros.Max(n => n.Y) + OrquestraVM.AlturaNo + Folga - y);
    }
}
