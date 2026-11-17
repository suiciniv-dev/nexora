using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Navegador;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.Modelos;

public sealed record ServidorNoAr(string Nome, string Url, string Projeto, bool NoAr)
{
    public string Endereco => Enderecos.Curto(Url);
    public string Detalhe => NoAr ? Idioma.F("{0} · no ar", Projeto) : Idioma.F("{0} · parado", Projeto);
}
