using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace Nexora.Controles;

public static class NomesAcessiveis
{
    static bool _ligado;

    public static void Ligar()
    {
        if (_ligado) return;
        _ligado = true;
        Control.LoadedEvent.AddClassHandler<TextBox>((c, _) => Nomear(c, c.PlaceholderText));
        Control.LoadedEvent.AddClassHandler<ComboBox>((c, _) => Nomear(c, c.PlaceholderText));
        Control.LoadedEvent.AddClassHandler<ToggleSwitch>((c, _) => Nomear(c, c.OnContent as string));
        Control.LoadedEvent.AddClassHandler<NumericUpDown>((c, _) => Nomear(c, null));
        Control.LoadedEvent.AddClassHandler<Slider>((c, _) => Nomear(c, null));
        Control.LoadedEvent.AddClassHandler<Button>((c, _) => NomearBotao(c));
    }

    static void Nomear(Control controle, string? reserva)
    {
        if (!string.IsNullOrWhiteSpace(AutomationProperties.GetName(controle))) return;
        var nome = RotuloAoLado(controle) ?? Dica(controle) ?? reserva;
        if (!string.IsNullOrWhiteSpace(nome)) AutomationProperties.SetName(controle, nome.Trim());
    }

    static void NomearBotao(Button botao)
    {
        if (botao.Content is string || !string.IsNullOrWhiteSpace(AutomationProperties.GetName(botao))) return;
        var nome = PrimeiroTexto(botao.Content as ILogical) ?? Dica(botao);
        if (!string.IsNullOrWhiteSpace(nome)) AutomationProperties.SetName(botao, nome.Trim());
    }

    static string? Dica(Control controle) => ToolTip.GetTip(controle) as string;

    static string? RotuloAoLado(Control controle)
    {
        if (controle.Parent is not Grid grade) return null;
        var coluna = Grid.GetColumn(controle);
        var linha = Grid.GetRow(controle);
        if (coluna == 0) return null;
        foreach (var irmao in grade.Children)
        {
            if (irmao == controle || Grid.GetRow(irmao) != linha || Grid.GetColumn(irmao) >= coluna) continue;
            if (PrimeiroTexto(irmao) is { } texto) return texto;
        }
        return null;
    }

    static string? PrimeiroTexto(ILogical? raiz)
    {
        if (raiz is null) return null;
        if (raiz is TextBlock { Text: { Length: > 0 } texto } bloco && bloco.IsVisible) return texto;
        foreach (var filho in raiz.LogicalChildren)
            if (PrimeiroTexto(filho) is { } achado) return achado;
        return null;
    }
}
