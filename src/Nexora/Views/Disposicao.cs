using Avalonia;
using Avalonia.Controls;

namespace Nexora.Views;

public static class Disposicao
{
    public static readonly AttachedProperty<bool> VerticalProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("Vertical", typeof(Disposicao), false, inherits: true);

    public static bool GetVertical(AvaloniaObject objeto) => objeto.GetValue(VerticalProperty);

    public static void SetVertical(AvaloniaObject objeto, bool valor) => objeto.SetValue(VerticalProperty, valor);

    public const double LarguraDeitada = 900;

    public static bool EhVertical(Size tamanho) => tamanho.Height > tamanho.Width || tamanho.Width < LarguraDeitada;

    public static bool Aplicar(Window janela, Size tamanho)
    {
        var vertical = EhVertical(tamanho);
        SetVertical(janela, vertical);
        janela.Classes.Set("vertical", vertical);
        return vertical;
    }

    public static void Acompanhar(Window janela) => janela.SizeChanged += (_, e) => Aplicar(janela, e.NewSize);
}
