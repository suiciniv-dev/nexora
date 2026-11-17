using Avalonia;
using Avalonia.Controls;
using Nexora.ViewModels;
using Nexora.Views;

namespace Nexora.Aplicativo;

public sealed class JanelaSalva
{
    public int X { get; set; }
    public int Y { get; set; }
    public double Largura { get; set; }
    public double Altura { get; set; }
    public bool Maximizada { get; set; }
    public List<string> Guias { get; set; } = [];
}
