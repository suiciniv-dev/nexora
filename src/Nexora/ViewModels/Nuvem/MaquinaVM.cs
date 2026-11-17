using System.Collections.ObjectModel;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Core.Nuvem;
using Idioma = Nexora.Core.Texto.Idioma;

namespace Nexora.ViewModels;

public sealed partial class MaquinaVM(MaquinaNaNuvem dados, MaquinasNaNuvemVM pai) : ObservableObject
{
    public MaquinaNaNuvem Dados { get; } = dados;
    public string Nome => Dados.Nome;
    public string Endereco => Dados.Endereco;
    public bool Testada => Dados.Testada;
    public bool CriadaPeloNexora => Dados.Tipo is TiposDeMaquina.InstaBox or TiposDeMaquina.Freestyle;
    public string Provedor => Dados.Tipo == TiposDeMaquina.Freestyle ? "Freestyle" : "InstaBox";
    public string TextoTirar => CriadaPeloNexora ? Idioma.F("Apagar em {0}", Provedor) : Idioma.T("Tirar");
    public string DicaTirar => CriadaPeloNexora ? Idioma.F("Apagar esta máquina em {0}, com os arquivos dela", Provedor) : Idioma.T("Tirar esta máquina do Nexora");

    [ObservableProperty]
    public partial bool Ocupada { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Ligada), nameof(MostraLigar), nameof(MostraPausar), nameof(TextoEstado), nameof(TemEstado))]
    public partial string? EstadoRemoto { get; set; }

    public bool Ligada => EstadoRemoto is "running" or "starting" or "resuming" or "creating";
    public bool MostraEnergia => CriadaPeloNexora && !Confirmando;
    public bool MostraLigar => !Ligada && EstadoRemoto is not ("deleted" or "deleting" or "suspending" or "stopping");
    public bool MostraPausar => EstadoRemoto is null or "running";
    public string? TextoEstado => TextoDoEstado(EstadoRemoto);
    public bool TemEstado => TextoEstado is not null;

    public static string? TextoDoEstado(string? estado) => estado switch
    {
        null or "" => null,
        "running" => Idioma.T("ligada"),
        "starting" or "resuming" or "creating" => Idioma.T("ligando…"),
        "suspended" => Idioma.T("pausada"),
        "suspending" => Idioma.T("pausando…"),
        "stopped" => Idioma.T("desligada"),
        "stopping" => Idioma.T("desligando…"),
        "deleted" or "deleting" => Idioma.T("apagada no provedor"),
        "error" => Idioma.T("com erro"),
        _ => estado,
    };

    public string Situacao => Dados.Testada
        ? Idioma.F("Conectada em {0:g}  ·  {1}", Dados.TestadaEm, Dados.Resumo ?? "")
        : CriadaPeloNexora ? Idioma.T("Ainda não conectou. Teste a conexão.") : Idioma.T("Ainda não conectou. Cole a chave do Nexora na máquina e teste.");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostraEnergia))]
    public partial bool Confirmando { get; set; }

    [RelayCommand]
    async Task TestarAsync()
    {
        if (await pai.TestarAsync(Dados) is { } relatorio)
            pai.Aviso = relatorio.Conectou ? Nome + ": " + MaquinasNaNuvemVM.Descrever(relatorio) : relatorio.Erro;
    }

    [RelayCommand]
    void PedirApagar() => Confirmando = true;

    [RelayCommand]
    void Cancelar() => Confirmando = false;

    [RelayCommand]
    Task Apagar() => pai.ApagarAsync(this);

    [RelayCommand]
    Task Pausar() => pai.LigarOuPausarAsync(this, true);

    [RelayCommand]
    Task Ligar() => pai.LigarOuPausarAsync(this, false);
}
