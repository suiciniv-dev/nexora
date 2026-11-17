using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Nexora.Controles;

public class Botao : Button
{
    public static void RegistrarTema(Application app)
    {
        if (app.TryFindResource(typeof(Button), out var tema) && tema is ControlTheme doButton)
            app.Resources[typeof(Botao)] = new ControlTheme(typeof(Botao)) { BasedOn = doButton };
    }
}
