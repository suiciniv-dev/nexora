using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class JanelaVM
{
    public AtalhosVM Atalhos { get; } = new();

    [RelayCommand]
    void MostrarAtalhos()
    {
        if (BuscaAberta) FecharBusca();
        Atalhos.Abrir();
    }

    IEnumerable<ResultadoBuscaVM> ComandosDaBusca(string termo)
    {
        bool Combina(AtalhoDoApp a) => termo.Length == 0 || a.Descricao.Contains(termo, StringComparison.CurrentCultureIgnoreCase)
            || a.Tecla.Contains(termo, StringComparison.CurrentCultureIgnoreCase);
        var comandos = CatalogoDeAtalhos.Todos().Where(a => a.NaBusca && Combina(a));
        if (termo.Length == 0) comandos = comandos.Where(a => a.Tecla == CatalogoDeAtalhos.TeclaDosAtalhos);
        foreach (var atalho in comandos.Take(6))
        {
            var comando = atalho.Comando!;
            var parametro = atalho.Parametro;
            yield return new ResultadoBuscaVM(atalho.Icone, atalho.Descricao, Idioma.F("comando · {0}", atalho.TeclaExibida), () =>
            {
                if (comando(this) is { } alvo && alvo.CanExecute(parametro)) alvo.Execute(parametro);
            });
        }
    }
}
