using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Nuvem;
using Nexora.Core.Texto;

namespace Nexora.ViewModels;

public sealed partial class MaquinasNaNuvemVM
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PodeSalvarChaveFreestyle))]
    public partial string ChaveFreestyle { get; set; } = "";

    [ObservableProperty]
    public partial bool MostrandoChaveFreestyle { get; set; }

    [ObservableProperty]
    public partial bool FormularioFreestyle { get; set; }

    [ObservableProperty]
    public partial string NomeFreestyle { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CriarFreestyleCommand))]
    public partial bool CriandoFreestyle { get; set; }

    public bool PodeSalvarChaveFreestyle => ChaveFreestyle.Trim().Length > 0;
    public bool TemChaveFreestyle => _nuvem.TemChaveFreestyle;

    [RelayCommand]
    async Task SalvarChaveFreestyleAsync()
    {
        try
        {
            _nuvem.GuardarChaveFreestyle(ChaveFreestyle);
            ChaveFreestyle = "";
            MostrandoChaveFreestyle = false;
            OnPropertyChanged(nameof(TemChaveFreestyle));
            Aviso = Idioma.T("Chave da Freestyle guardada no cofre. Conferindo…");
            var total = await Task.Run(() => _nuvem.Freestyle.Cliente.ContarAsync());
            Aviso = Idioma.F("A Freestyle aceitou a chave ({0} máquinas na conta). Já dá para criar máquinas.", total);
        }
        catch (Exception ex) when (ex is ArgumentException or PlatformNotSupportedException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception
            or ErroDaFreestyle or HttpRequestException or TaskCanceledException)
        {
            Aviso = ex.Message;
        }
    }

    [RelayCommand]
    void ApagarChaveFreestyle()
    {
        _nuvem.ApagarChaveFreestyle();
        FormularioFreestyle = false;
        OnPropertyChanged(nameof(TemChaveFreestyle));
        Aviso = Idioma.T("Chave da Freestyle apagada deste PC. As máquinas continuam lá; revogue a chave no painel da Freestyle se não for mais usar.");
    }

    [RelayCommand]
    void AlternarChaveFreestyle() => MostrandoChaveFreestyle = !MostrandoChaveFreestyle;

    [RelayCommand]
    void NovaFreestyle()
    {
        FormularioFreestyle = true;
        Aviso = null;
        if (NomeFreestyle.Length == 0) NomeFreestyle = Idioma.T("Máquina Freestyle");
    }

    [RelayCommand]
    void CancelarFreestyle() => FormularioFreestyle = false;

    [RelayCommand(CanExecute = nameof(PodeCriarFreestyle))]
    async Task CriarFreestyleAsync()
    {
        var nome = NomeFreestyle.Trim().Length > 0 ? NomeFreestyle.Trim() : Idioma.T("Máquina Freestyle");
        CriandoFreestyle = true;
        Aviso = Idioma.T("Criando a máquina na Freestyle…");
        try
        {
            var (_, relatorio) = await Task.Run(() => _nuvem.CriarNaFreestyleAsync(nome, p => Dispatcher.UIThread.Post(() => Aviso = p)));
            FormularioFreestyle = false;
            NomeFreestyle = "";
            Recarregar();
            Aviso = relatorio.Conectou ? Descrever(relatorio) : relatorio.Erro;
        }
        catch (Exception ex) when (ex is ErroDaFreestyle or HttpRequestException or TaskCanceledException or TimeoutException or IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Aviso = ex.Message;
        }
        finally
        {
            CriandoFreestyle = false;
        }
    }

    bool PodeCriarFreestyle() => !CriandoFreestyle;
}
