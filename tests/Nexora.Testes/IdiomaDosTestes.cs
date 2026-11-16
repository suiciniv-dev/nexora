using System.Globalization;
using System.Runtime.CompilerServices;

namespace Nexora.Testes;

static class IdiomaDosTestes
{
    [ModuleInitializer]
    internal static void Portugues()
    {
        var cultura = CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        CultureInfo.CurrentCulture = cultura;
        CultureInfo.CurrentUICulture = cultura;
        Core.Texto.Idioma.Definir("pt");
    }
}
