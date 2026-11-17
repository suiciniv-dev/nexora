using System.Windows.Input;
using Nexora.Core.Texto;
using Nexora.ViewModels;

namespace Nexora.Aplicativo;

public sealed record AtalhoDoApp(string Grupo, string Tecla, string Descricao, string[] Gestos, Func<JanelaVM, ICommand?>? Comando = null, object? Parametro = null, string Icone = "")
{
    public string TeclaExibida => CatalogoDeAtalhos.Exibir(Tecla);
    public bool NaBusca => Comando is not null && Icone.Length > 0;
}
