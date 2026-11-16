using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;

namespace Nexora.Views;

public sealed class Adiado : Decorator
{
    public static readonly StyledProperty<bool> MostrarProperty = AvaloniaProperty.Register<Adiado, bool>(nameof(Mostrar));

    public bool Mostrar { get => GetValue(MostrarProperty); set => SetValue(MostrarProperty, value); }

    [Content]
    public IDataTemplate? Conteudo
    {
        get;
        set
        {
            field = value;
            Atualizar();
        }
    }

    public bool Montado => Child is not null;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MostrarProperty) Atualizar();
    }

    void Atualizar()
    {
        if (Mostrar && Child is null && Conteudo is { } conteudo) Child = conteudo.Build(DataContext);
        if (Child is { } filho) filho.IsVisible = Mostrar;
    }
}
