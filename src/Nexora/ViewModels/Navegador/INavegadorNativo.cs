using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Navegador;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public interface INavegadorNativo
{
    void Navegar(string url);
    void Voltar();
    void Avancar();
    void Recarregar();
    Task DesignAsync(bool ligado);
    Task RestaurarAsync(IReadOnlyList<Anotacao> anotacoes);
    Task RemoverAsync(int id);
    Task LimparAsync();
    Task<byte[]?> CapturarAsync();
}
