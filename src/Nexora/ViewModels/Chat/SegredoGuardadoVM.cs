using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Segredos;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class SegredoGuardadoVM(SegredoGuardado dados, Action<SegredoGuardadoVM> apagar) : ObservableObject
{
    public SegredoGuardado Dados { get; } = dados;
    public string Nome => Dados.Nome;
    public string Projeto => Path.GetFileName(Dados.Projeto.TrimEnd('\\', '/')) is { Length: > 0 } nome ? nome : Dados.Projeto;
    public string CaminhoDoProjeto => Dados.Projeto;

    public string Onde => Dados.Destino == DestinoDoSegredo.ArquivoEnv
        ? Idioma.F("Arquivo {0}", Dados.Arquivo ?? ".env")
        : Idioma.T("Variável de ambiente (cofre do sistema)");

    public string DicaApagar => Dados.Destino == DestinoDoSegredo.ArquivoEnv
        ? Idioma.F("Tira a linha {0} do {1}", Dados.Nome, Dados.Arquivo ?? ".env")
        : Idioma.T("Apaga o valor do cofre do sistema");

    [ObservableProperty]
    public partial bool Confirmando { get; set; }

    [RelayCommand]
    void PedirApagar() => Confirmando = true;

    [RelayCommand]
    void Cancelar() => Confirmando = false;

    [RelayCommand]
    void Apagar() => apagar(this);
}
