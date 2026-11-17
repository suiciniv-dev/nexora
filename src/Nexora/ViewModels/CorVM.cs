using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;
using Nexora.Fundos;
using Nexora.Temas;
using TextMateSharp.Grammars;

namespace Nexora.ViewModels;

public sealed partial class CorVM : ObservableObject
{
    readonly Action _mudou;
    bool _sincronizando;

    public CorVM(string chave, string rotulo, Color cor, Action mudou)
    {
        Chave = chave;
        Rotulo = rotulo;
        _mudou = mudou;
        Cor = cor;
        Hex = Formatar(cor);
    }

    public string Chave { get; }
    public string Rotulo { get; }
    public IBrush Pincel => new SolidColorBrush(Cor);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Pincel))]
    public partial Color Cor { get; set; }

    [ObservableProperty]
    public partial string Hex { get; set; }

    partial void OnCorChanged(Color value)
    {
        if (_sincronizando) return;
        _sincronizando = true;
        Hex = Formatar(value);
        _sincronizando = false;
        _mudou();
    }

    partial void OnHexChanged(string value)
    {
        if (_sincronizando || !Color.TryParse(value.Trim().StartsWith('#') ? value.Trim() : "#" + value.Trim(), out var cor)) return;
        _sincronizando = true;
        Cor = cor;
        _sincronizando = false;
        _mudou();
    }

    static string Formatar(Color cor) =>
        cor.A == 255 ? $"#{cor.R:X2}{cor.G:X2}{cor.B:X2}" : $"#{cor.A:X2}{cor.R:X2}{cor.G:X2}{cor.B:X2}";
}
