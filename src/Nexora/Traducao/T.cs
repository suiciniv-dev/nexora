using Avalonia.Markup.Xaml;
using Nexora.Core.Texto;

namespace Nexora.Traducao;

public sealed class T : MarkupExtension
{
    public T()
    {
    }

    public T(string texto) => Texto = texto;

    public string Texto { get; set; } = "";

    public override object ProvideValue(IServiceProvider servicos) => Idioma.T(Texto);
}
