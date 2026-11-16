using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Cli;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class ConfiguracoesVM
{
    static ComandoNoTerminal Comando => new(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        OperatingSystem.IsWindows() ? Trampolim.DotnetInstalado() : null,
        OperatingSystem.IsWindows() ? Path.Combine(AppContext.BaseDirectory, "Nexora.dll") : Environment.ProcessPath ?? "Nexora");

    public bool ComandoInstalado => Comando.Instalado;

    public string TextoComando => ComandoInstalado
        ? Idioma.F("Instalado em {0}. No terminal: nexora status, nexora iniciar . \"pedido\", nexora ajuda.", Comando.Arquivo)
        : Idioma.T("Controla o Nexora aberto pelo terminal: começar conversas, mandar mensagens, ver o que precisa de você. Útil em scripts e para outros agentes.");

    public string BotaoComando => ComandoInstalado ? Idioma.T("Remover") : Idioma.T("Instalar comando");

    [RelayCommand]
    void AlternarComando()
    {
        if (JanelaVM.Demo || JanelaVM.Isolada)
        {
            Aviso = Idioma.T("No modo demonstração o comando não é instalado.");
            return;
        }
        try
        {
            if (ComandoInstalado)
            {
                Comando.Remover();
                Aviso = Idioma.T("O comando nexora foi removido.");
            }
            else Aviso = Comando.Instalar();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Aviso = Idioma.F("Não deu para instalar o comando: {0}", ex.Message);
        }
        OnPropertyChanged(nameof(ComandoInstalado));
        OnPropertyChanged(nameof(TextoComando));
        OnPropertyChanged(nameof(BotaoComando));
    }
}
