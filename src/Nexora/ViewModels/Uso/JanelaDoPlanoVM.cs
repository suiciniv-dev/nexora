using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Claude;
using Nexora.Core.Uso;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed record JanelaDoPlanoVM(string Nome, double Percentual, string Reinicia)
{
    public double Fracao => Math.Clamp(Percentual / 100, 0, 1);
    public string TextoPercentual => $"{Percentual.ToString("0.#", Idioma.Cultura)}%";
    public bool Alto => Percentual >= 80;
}
