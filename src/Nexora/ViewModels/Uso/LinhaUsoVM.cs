using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Uso;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed record LinhaUsoVM(string Nome, string Iniciais, double Fracao, string TextoParte, string? TextoPlano, string TextoDetalhe, string TextoModelos, IReadOnlyList<BarraVM> Historico)
{
    public bool TemPlano => TextoPlano is not null;
}
